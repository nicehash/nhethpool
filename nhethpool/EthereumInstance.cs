using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Globalization;

namespace nhethpool
{
    class EthereumInstance
    {
        #region PRIVATE PROPERTIES
        private Uri walletUri;
        private Thread worker;
        private bool isRunning;
        private DateTime lastWalletTime;
        private Job currentJob;
        private string currentSeed;
        private FixedJobQueue jobQueue;
        private List<Miner> miners;
        private Queue<int> extraNonces;
        private int maxMiners;
        private Stack<WalletShare> pendingShares;
        private TcpListener stratumServer;
        private SpeedMeasure speedMeasure;
        private object locker;
        #endregion

        /// <summary>
        /// Initialize Ethereum instance.
        /// </summary>
        /// <param name="host">Wallet hostname.</param>
        /// <param name="port">Wallet RPC port.</param>
        /// <param name="user">Wallet username.</param>
        /// <param name="pass">Wallet password.</param>
        /// <param name="maxminers">Max count of miners.</param>
        /// <param name="stratumS">Stratum listening interface.</param>
        public EthereumInstance(string host, int port, string user, string pass, int maxminers, ref TcpListener stratums)
        {
            UriBuilder walletUriB = new UriBuilder("http", host, port);
            walletUriB.UserName = user;
            walletUriB.Password = pass;
            walletUri = walletUriB.Uri;
            jobQueue = new FixedJobQueue(8); // keep last 8 jobs
            miners = new List<Miner>(1024);
            int maxxn = 1 << (Config.ConfigData.StratumExtranonceSize * 8);
            extraNonces = new Queue<int>(maxxn);
            //extraNonces.Enqueue(0xa2ee); // for testing only
            for (int i = 0; i != maxxn; ++i)
                extraNonces.Enqueue(i);
            maxMiners = maxxn / 2;
            if (maxminers < maxMiners)
                maxMiners = maxminers;
            lastWalletTime = new DateTime(0);
            pendingShares = new Stack<WalletShare>();
            stratumServer = stratums;
            speedMeasure = new SpeedMeasure();
            locker = new object();
        }

        /// <summary>
        /// Start Ethereum instance; launch thread, which periodically query eth_getWork and handles all connected miners.
        /// </summary>
        public void Start()
        {
            if (worker != null) return;
            isRunning = true;
            worker = new Thread(WorkerThread);
            worker.Start();
        }

        /// <summary>
        /// Stop Ethereum instance.
        /// </summary>
        public void Stop()
        {
            if (worker == null) return;
            isRunning = false;
            worker.Join();
            worker = null;
        }

        /// <summary>
        /// Get some information about instance.
        /// </summary>
        /// <param name="minerCount">Number of connected (working) miners.</param>
        /// <param name="speed">Speed of all miners combined.</param>
        public void QueryInfo(out int minerCount, out double speed)
        {
            minerCount = 0;

            lock (locker)
            {
                foreach (Miner m in miners)
                    if (m.initialWorkSent)
                        minerCount++;
            }

            speed = speedMeasure.GetSpeed();
        }

        #region PRIVATE METHODS
        private void ProcessWallet()
        {
            DateTime before_q = DateTime.Now;
            string[] workdata = Wallet.GetWork(walletUri);
            DateTime after_q = DateTime.Now;
            if (workdata != null && workdata.Length == 3)
            {
                // work data received successfully
                Logging.Log(5, "Received Eth workdata: seedhash=" + workdata[1] +
                    ", headerhash=" + workdata[0] + ", target=" + workdata[2]);

                Logging.Log(5, "RPC time: " + (after_q - before_q).TotalMilliseconds.ToString("F0") + " ms");

                double walletDiff = ShareCheckWrapper.getHashDiff(workdata[2]);
                Job j = new Job(workdata[1], workdata[0], walletDiff);
                if (!(j ^ currentJob))
                {
                    j.AdjustID();

                    Logging.Log(1, "New eth work, job id: " + j.uniqueID + ", wallet diff: " + walletDiff.ToString("F4"));

                    // new job provided
                    jobQueue.FixedEnqueue(j);

                    currentJob = j;

                    // determine miner diff
                    double minerDiff = Config.ConfigData.StratumDifficulty;
                    if (currentJob.difficulty < minerDiff)
                        minerDiff = currentJob.difficulty;

                    //Logging.Log(2, "Setting miner diff to: " + minerDiff.ToString("F4"));

                    // dispatch job to miners
                    lock (locker)
                    {
                        foreach (Miner m in miners)
                        {
                            if (m.isAuthorized && m.isSubscribed)
                                m.SetDifficultyAndJob(minerDiff, currentJob);
                        }
                    }
                }

                // verify current seed
                if (currentSeed != currentJob.seedHash)
                {
                    int res = ShareCheckWrapper.loadDAGFile(currentJob.seedHash, Config.ConfigData.DAGFolder + "\\");
                    switch (res)
                    {
                        case 0:
                            currentSeed = currentJob.seedHash;
                            Logging.Log(1, "Loaded DAG file: " + currentJob.seedHash.Substring(0, 16));
                            break;
                        case 1:
                            Logging.Log(1, "DAG load failed: invalid seedhash: " + currentJob.seedHash.Substring(0, 16));
                            break;
                        case 2:
                            Logging.Log(1, "DAG load failed: DAG file does not exist: " + currentJob.seedHash.Substring(0, 16));
                            break;
                        case 3:
                            Logging.Log(1, "DAG load failed: DAG file size is zero: " + currentJob.seedHash.Substring(0, 16));
                            break;
                        case 4:
                            Logging.Log(1, "DAG load failed: not enough memory?");
                            break;
                        case 5:
                            Logging.Log(1, "DAG load failed: cannot read DAG file: " + currentJob.seedHash.Substring(0, 16));
                            break;
                        default:
                            break;
                    }
                }

                // now that wallet is 100% on, dispatch all queued wallet shares
                while (pendingShares.Count > 0)
                {
                    WalletShare ws = pendingShares.Pop();
                    bool result;
                    if (!Wallet.SubmitWork(walletUri, ws, out result))
                    {
                        pendingShares.Push(ws); // wallet off again, save again and try later
                        break;
                    }
                }
            }
        }


        private void WorkerThread()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            while (isRunning)
            {
                int actionCounter = 0;

                DateTime now = DateTime.Now;
                if ((lastWalletTime + TimeSpan.FromMilliseconds(Config.ConfigData.WalletRPCIntervalMS)) < now)
                {
                    lastWalletTime = now;
                    actionCounter++;
                    ProcessWallet();
                }

                // check for incoming stratum connections
                // do this only if not having more than half connections
                bool canHaveNew = false;
                lock (locker)
                {
                    if (miners.Count < maxMiners)
                        canHaveNew = true;
                }

                if (canHaveNew && currentJob != null)
                {
                    lock (stratumServer)
                    {
                        if (stratumServer.Pending())
                        {
                            actionCounter++;
                            try
                            {
                                TcpClient cl = stratumServer.AcceptTcpClient();
                                Logging.Log(2, "New miner: " + ((IPEndPoint)cl.Client.RemoteEndPoint).Address.ToString());
                                Miner m = new Miner(cl);
                                lock (locker)
                                {
                                    miners.Add(m);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logging.Log(4, "Error accepting new miner: " + ex.Message);
                            }
                        }
                    }
                }

                // process all miners
                List<Miner> terminateList = new List<Miner>();
                lock (locker)
                {
                    foreach (Miner m in miners)
                    {
                        if (!m.Process(ProcessClientMessage, ref actionCounter))
                            terminateList.Add(m);
                    }
                }

                foreach (Miner m in terminateList)
                {
                    actionCounter++;
                    lock (locker)
                    {
                        miners.Remove(m);
                    }
                    if (m.extraNonce != null)
                        ReleaseExtraNonce(m.extraNonce);
                    Logging.Log(2, "Miner gone: " + ((IPEndPoint)m.connection.Client.RemoteEndPoint).Address.ToString());
                    try { m.connection.Close(); }
                    catch { }
                }

                if (actionCounter == 0) Thread.Sleep(5);
            }

            lock (locker)
            {
                foreach (Miner m in miners)
                {
                    if (m.extraNonce != null)
                        ReleaseExtraNonce(m.extraNonce);
                    try { m.connection.Close(); }
                    catch { }
                }
                miners.Clear();
            }
        }


        private bool ProcessClientMessage(Miner m, StratumMessageClient smc)
        {
            StratumMessageServerResult smsr;

            switch (smc.type)
            {
                case StratumMessageClientType.Subscribe:
                    if (!m.isSubscribed &&
                        smc.parameters != null &&
                        smc.parameters.Length > 1 &&
                        smc.parameters[1] == Config.ETHEREUM_STRATUM_VERSION)
                    {
                        m.isSubscribed = true;

                        m.extraNonce = GetExtraNonce();

                        smsr = new StratumMessageServerResult();
                        smsr.id = smc.id;
                        smsr.error = null;
                        smsr.result = StratumMessageServerResult.CreateMiningNotifyResult(m.extraNonce);

                        m.Send(smsr);

                        break;
                    }
                    else
                        return false;
                case StratumMessageClientType.SubscribeExtranonce:
                    smsr = new StratumMessageServerResult();
                    smsr.id = smc.id;
                    smsr.error = null;
                    smsr.result = true;
                    m.Send(smsr);
                    break;
                case StratumMessageClientType.Authorize:
                    if (!m.isAuthorized &&
                        smc.parameters != null &&
                        smc.parameters.Length == 2)
                    {
                        // accept all miners with all usernames for now
                        m.userName = smc.parameters[0];
                        m.isAuthorized = true;

                        smsr = new StratumMessageServerResult();
                        smsr.id = smc.id;
                        smsr.error = null;
                        smsr.result = true;

                        m.Send(smsr);
                        break;
                    }
                    else
                        return false;
                case StratumMessageClientType.Submit:
                    if (m.initialWorkSent &&
                        smc.parameters != null &&
                        smc.parameters.Length == 3)
                    {
                        if (smc.parameters[0] == m.userName)
                        {
                            ProcessShare(m, smc.id, smc.parameters[1], smc.parameters[2]);
                        }
                    }
                    break;
            }

            if (!m.initialWorkSent && m.isSubscribed && m.isAuthorized)
            {
                double minerDiff = Config.ConfigData.StratumDifficulty;
                if (currentJob.difficulty < minerDiff)
                    minerDiff = currentJob.difficulty;

                if (m.isAuthorized && m.isSubscribed)
                    m.SetDifficultyAndJob(minerDiff, currentJob);

                m.initialWorkSent = true;
            }

            return true;
        }


        private void ProcessShare(Miner m, object id, string jobid, string minernonce)
        {
            string mixhash;
            double d;

            jobid = jobid.ToLower();
            minernonce = minernonce.ToLower();

            if (minernonce.Length != 16 - (Config.ConfigData.StratumExtranonceSize * 2))
                return;

            Logging.Log(3, "Received share for job: " + jobid + " xn: " + m.extraNonce + " nonce: " + minernonce);

            if (jobid != currentJob.uniqueID)
            {
                // report stale share
                Logging.Log(3, "Received share is stale!");
                m.SendShareResultFailed(id, "Stale share.");

                // maybe "uncle"?
                Job j = jobQueue.Find(jobid);
                if (j != null && j.seedHash == currentSeed)
                {
                    d = ShareCheckWrapper.getShareDiff(j.headerHash, m.extraNonce + minernonce, out mixhash);
                    if (d >= j.difficulty)
                    {
                        Logging.Log(1, "Submitting possible uncle, nonce: " + m.extraNonce + minernonce);
                        WalletShare ws = new WalletShare(m.extraNonce + minernonce, j.headerHash, mixhash);
                        bool result;
                        if (!Wallet.SubmitWork(walletUri, ws, out result))
                            pendingShares.Push(ws); // failed to send, save this share and send once wallet is online again
                    }
                }

                return;
            }

            // verify for duplicate share
            foreach (string n in m.receivedNonces)
            {
                if (n == minernonce)
                {
                    // report duplicate share
                    Logging.Log(3, "Received share is duplicate!");
                    m.SendShareResultFailed(id, "Duplicate share.");
                    
                    return;
                }
            }

            // verify share diff
            d = ShareCheckWrapper.getShareDiff(currentJob.headerHash, m.extraNonce + minernonce, out mixhash);
            Logging.Log(3, "Share diff: " + d.ToString("F4") + "/" + m.difficulty.ToString("F4"));
            if (d < m.difficulty)
            {
                // report low diff share
                Logging.Log(3, "Received share is above target!");
                m.SendShareResultFailed(id, "Share above target.");
                
                return;
            }
            else if (d >= currentJob.difficulty)
            {
                // submit to wallet
                Logging.Log(1, "Submitting share to the wallet, nonce: " + m.extraNonce + minernonce + " diff: " + d.ToString("F4") + "/" + currentJob.difficulty.ToString("F4"));
                WalletShare ws = new WalletShare(m.extraNonce + minernonce, currentJob.headerHash, mixhash);
                bool result;
                if (!Wallet.SubmitWork(walletUri, ws, out result))
                    pendingShares.Push(ws); // failed to send, save this share and send once wallet is online again
            }

            speedMeasure.AddShare(m.difficulty);

            // report OK share
            Logging.Log(3, "Received share is OK!");
            m.SendShareResultOK(id);
            
            // save share
            m.receivedNonces.Add(minernonce);
        }


        private string GetExtraNonce()
        {
            int xn = extraNonces.Dequeue();
            return xn.ToString("x" + (Config.ConfigData.StratumExtranonceSize * 2).ToString());
        }


        private void ReleaseExtraNonce(string xn)
        {
            int num = int.Parse(xn, NumberStyles.HexNumber);
            extraNonces.Enqueue(num);
        }
        #endregion
    }
}

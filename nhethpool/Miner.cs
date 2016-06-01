using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace nhethpool
{
    class Miner
    {
        public delegate bool ParseMessage(Miner m, StratumMessageClient minerm);

        public TcpClient connection;
        public DateTime connectTime;
        public bool isSubscribed;
        public bool isAuthorized;
        public bool initialWorkSent;
        public string userName;
        public string extraNonce;
        public double difficulty;
        public List<string> receivedNonces;

        private byte[] recvBuffer;
        private Queue<StratumMessageClient> receivedMessages = new Queue<StratumMessageClient>();


        public Miner(TcpClient c)
        {
            connection = c;
            isSubscribed = false;
            isAuthorized = false;
            initialWorkSent = false;
            connectTime = DateTime.Now;
            c.Client.Blocking = false;
            c.Client.NoDelay = true;
            c.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            receivedNonces = new List<string>();
        }


        public bool Process(ParseMessage pm, ref int actionCounter)
        {
            // read stratum lines from network
            List<string> receivedLines = ReadNetworkData();

            // parse stratum lines into stratum messages
            foreach (string line in receivedLines)
            {
                actionCounter++;
                Logging.Log(5, "Received from miner: " + line);
                StratumMessageClient smc = StratumMessage.ParseClientMessage(line);
                if (smc != null) receivedMessages.Enqueue(smc);
                else
                {
                    Logging.Log(5, "Invalid stratum message: " + line);

                    // todo: handle non supported messages?
                }
            }

            // process stratum messages
            while (receivedMessages.Count > 0)
            {
                actionCounter++;
                StratumMessageClient smc = receivedMessages.Dequeue();
                if (!pm(this, smc)) return false;
            }

            // check if connection is dead
            if (connection.Client.Poll(0, SelectMode.SelectRead) && connection.Client.Available == 0)
                return false;

            return true;
        }


        public void Send(StratumMessageServer sms)
        {
            try
            {
                string data = sms.ToString();
                Logging.Log(5, "Sending to miner: " + data);
                byte[] bdata = ASCIIEncoding.ASCII.GetBytes(data + "\n");
                connection.Client.Send(bdata);
            } 
            catch (Exception ex)
            {
                Logging.Log(4, "Error sending data to miner: " + ex.Message);
            }
        }


        public void SetDifficultyAndJob(double diff, Job j)
        {
            SetDifficulty(diff);
            SetJob(j);
        }


        public void SetDifficulty(double diff)
        {
            if (difficulty == diff) return;

            difficulty = diff;

            StratumMessageServerNotify smsn = new StratumMessageServerNotify();
            smsn.id = null;
            smsn.method = "mining.set_difficulty";
            smsn.parameters = new object[1];
            smsn.parameters[0] = difficulty;

            Send(smsn);
        }


        public void SetJob(Job j)
        {
            // clear nonces
            receivedNonces.Clear();

            StratumMessageServerNotify smsn = new StratumMessageServerNotify();
            smsn.id = null;
            smsn.method = "mining.notify";
            smsn.parameters = new object[4] { j.uniqueID, j.seedHash, j.headerHash, true };

            Send(smsn);
        }


        public void SendShareResultOK(object id)
        {
            StratumMessageServerResult smsr = new StratumMessageServerResult();
            smsr.id = id;
            smsr.result = true;
            smsr.error = false;

            Send(smsr);
        }


        public void SendShareResultFailed(object id, string msg)
        {
            StratumMessageServerResult smsr = new StratumMessageServerResult();
            smsr.id = id;
            smsr.result = false;
            smsr.error = StratumMessageServerResult.CreateError(0, msg);

            Send(smsr);
        }


        private List<string> ReadNetworkData()
        {
            List<string> receivedLines = new List<string>();

            if (connection.Client.Available == 0) return receivedLines;

            byte[] inbuff = new byte[connection.Client.Available];
            connection.Client.Receive(inbuff);

            if (recvBuffer != null && recvBuffer.Length > 0)
            {
                byte[] newbuff = new byte[inbuff.Length + recvBuffer.Length];
                Buffer.BlockCopy(recvBuffer, 0, newbuff, 0, recvBuffer.Length);
                Buffer.BlockCopy(inbuff, 0, newbuff, recvBuffer.Length, inbuff.Length);

                inbuff = newbuff;
                recvBuffer = null;
            }

            for (int i = 0; i < inbuff.Length; i++)
            {
                if (inbuff[i] == '\n')
                {
                    inbuff[i] = 0;
                    string line = ASCIIEncoding.ASCII.GetString(inbuff, 0, i);
                    receivedLines.Add(line);

                    byte[] newbuff = new byte[inbuff.Length - i - 1];
                    Buffer.BlockCopy(inbuff, i + 1, newbuff, 0, inbuff.Length - i - 1);
                    inbuff = newbuff;
                    i = 0;
                }
            }

            if (inbuff.Length > 0)
                recvBuffer = inbuff;

            return receivedLines;
        }
    }
}

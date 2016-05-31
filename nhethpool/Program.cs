using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading;


// TODO:
// configurable timeouts for wallet RPCs
// various wallet mining addresses?


namespace nhethpool
{
    class Program
    {
        private static bool isRunning;
        private static List<EthereumInstance> eiList;

        private static void QueryThread()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            double minerCount;
            double speed;

            while (isRunning)
            {
                minerCount = 0;
                speed = 0;

                foreach (EthereumInstance ei in eiList)
                {
                    int mc;
                    double s;
                    ei.QueryInfo(out mc, out s);
                    minerCount += mc;
                    speed += s;
                }

                Console.Title = "Miners: " + minerCount.ToString() + "   Speed: " + speed.ToString("F4") + " MH/s";

                for (int i = 0; i < 100 && isRunning; i++)
                    Thread.Sleep(100);
            }
        }

        static void Main(string[] args)
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Logging.StartFileLogging();
            isRunning = true;

            eiList = new List<EthereumInstance>();

            Console.WriteLine("Press Q to terminate the pool. Now starting up...");
            System.Threading.Thread.Sleep(500);

            // init stratum server
            TcpListener stratumServer;
            try
            {
                stratumServer = new TcpListener(IPAddress.Any, Config.ConfigData.StratumPort);
                stratumServer.Start();
                Logging.Log(1, "Stratum listening port: " + Config.ConfigData.StratumPort.ToString());
            }
            catch (Exception ex)
            {
                Logging.Log(1, "Failed to start stratum server on port " + Config.ConfigData.StratumPort.ToString() + ": " + ex.Message);
                Logging.EndFileLogging();
                return;
            }

            foreach (WalletStratum instance in Config.ConfigData.Instances)
            {
                EthereumInstance ei = new EthereumInstance(
                    instance.WalletHost, 
                    instance.WalletPort,
                    instance.WalletUsername, 
                    instance.WalletPassword,
                    instance.MaxMiners,
                    ref stratumServer);
                ei.Start();
                eiList.Add(ei);
            }

            Thread qt = new Thread(QueryThread);
            qt.Start();

            while (true)
            {
                // Get pressed console key
                ConsoleKeyInfo cki = Console.ReadKey(true);

                // If pressed key is Q, then quit this app
                if (cki.Key == ConsoleKey.Q)
                    break;
            }

            Console.WriteLine("Qutting, please wait...");

            isRunning = false;
            qt.Join();

            foreach (EthereumInstance ei in eiList)
            {
                ei.Stop();
            }

            Logging.EndFileLogging();
        }
    }
}

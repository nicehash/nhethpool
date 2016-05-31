using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Newtonsoft.Json;

namespace nhethpool
{
    class WalletStratum
    {
#pragma warning disable 649
        public string WalletHost;
        public int WalletPort;
        public string WalletUsername;
        public string WalletPassword;
        public int MaxMiners;
#pragma warning restore 649
    }

    class Config
    {
        public static string ETHEREUM_STRATUM_VERSION = "EthereumStratum/1.0.0";

#pragma warning disable 649
        public int LogConsoleLevel;
        public int LogFileLevel;
        public string LogFileFolder;

        public WalletStratum[] Instances;

        public string DAGFolder;
        public int WalletRPCIntervalMS;

        public int StratumPort;
        public int StratumExtranonceSize;
        public double StratumDifficulty;
#pragma warning restore 649


        public static Config ConfigData;


        static Config()
        {
            // Set defaults
            ConfigData = new Config();
            ConfigData.LogConsoleLevel = 4;
            ConfigData.LogFileLevel = 5;
            ConfigData.LogFileFolder = "logs";
            ConfigData.DAGFolder = "D:\\DAGs";
            ConfigData.WalletRPCIntervalMS = 500;
            ConfigData.StratumPort = 3333;
            ConfigData.StratumExtranonceSize = 2;
            ConfigData.StratumDifficulty = 0.1;

            ConfigData.Instances = new WalletStratum[1];
            ConfigData.Instances[0] = new WalletStratum();

            ConfigData.Instances[0].WalletHost = "localhost";
            ConfigData.Instances[0].WalletPort = 8545;
            ConfigData.Instances[0].WalletUsername = "user";
            ConfigData.Instances[0].WalletPassword = "pass";
            ConfigData.Instances[0].MaxMiners = 1024;

            try { ConfigData = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json")); }
            catch { }

            if (ConfigData.StratumExtranonceSize > 3)
                ConfigData.StratumExtranonceSize = 3;
            else if (ConfigData.StratumExtranonceSize < 1)
                ConfigData.StratumExtranonceSize = 1;
        }


        public static void Commit()
        {
            try { File.WriteAllText("config.json", JsonConvert.SerializeObject(ConfigData, Formatting.Indented)); }
            catch { }
        }
    }
}

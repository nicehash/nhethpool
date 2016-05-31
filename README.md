# NiceHash Ethereum Pool

- [Introduction](#introduction)
- [Features](#features)
- [Requirements](#requirements)
- [How to get&run it?](#build)
- [Config options](#options)
- [Troubleshooting](#troubleshooting)

# <a name="introduction"></a> Introduction

NiceHash Ethereum Pool is standalone simple, easy to compile, build&run pool for mining all Dagger-Hashimoto based coins such as Ethereum. This pool is made in C# .NET with share checker written in C/C++ and will easily compile and run on Windows X64. All what you need besides this pool is Ethereum wallet.

This pool only works with EthereumStratum/1.0.0 which is supported by NiceHash. Specifications are in file <a href="https://github.com/nicehash/nhethpool/blob/master/EthereumStratum_NiceHash_v1.0.0.txt">EthereumStratum_NiceHash_v1.0.0.txt</a>.

Compatible miners:
- https://github.com/nicehash/cpp-ethereum (WIN builds: https://github.com/nicehash/cpp-ethereum/releases)

# <a name="benefits"></a> Features

- Very small server side load.
- Support for single or multiple Ethereum wallets.
- Single RPC getWork call can support up to 32k miners.
- Compatibility with EthereumStratum/1.0.0, which is supported by NiceHash. Buy hashing power from NiceHash.
- Easy to setup solo mining pool.

# <a name="requirements"></a> Requirements for running

- X64 based Windows operating system
- .NET Framework 4.5: https://www.microsoft.com/en-us/download/details.aspx?id=30653
- Visual Studio 2013 redistributable: https://www.microsoft.com/en-us/download/details.aspx?id=40784
- Latest release from here: https://github.com/nicehash/nhethpool/releases
- This pool is unable to build DAGs on it's own (yet). You need to prebuild DAG files (using ethminer for example) and put them in some folder.
- After you have established Ethereum wallet and configured it's RPC, configure config.json and run nhethpool.exe.

# <a name="build"></a> Requirements for building

- Get Visual Studio 2013: https://www.visualstudio.com/en-us/news/vs2013-community-vs.aspx
- Unpack lib.7z in folder 3rdparty\cryptopp\lib
- Open solution with Visual Studio 2013
- Build

# <a name="options"></a> Config options

Example config file:
```
{
  "LogConsoleLevel": 4,
  "LogFileLevel": 5,
  "LogFileFolder": "logs",
  "Instances": [
    {
      "WalletHost": "localhost",
      "WalletPort": 8545,
      "WalletUsername": "user",
      "WalletPassword": "pass",
      "MaxMiners": 1024
    }
  ],
  "DAGFolder": "D:\\DAGs",
  "WalletRPCIntervalMS": 500,
  "StratumPort": 3333,
  "StratumExtranonceSize": 2,
  "StratumDifficulty": 0.1
}
```

# <a name="troubleshooting"></a> Troubleshooting

TODO
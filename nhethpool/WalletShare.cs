using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nhethpool
{
    class WalletShare
    {
        public string Nonce;
        public string HeaderHash;
        public string MixHash;

        public WalletShare(string nonce, string headerhash, string mixhash)
        {
            Nonce = nonce;
            HeaderHash = headerhash;
            MixHash = mixhash;
        }
    }
}

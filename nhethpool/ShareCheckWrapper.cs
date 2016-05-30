using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;


namespace nhethpool
{
    class ShareCheckWrapper
    {
        [DllImport("sharecheck.dll", CallingConvention = CallingConvention.Cdecl)]
        public extern static void init();

        [DllImport("sharecheck.dll", CallingConvention = CallingConvention.Cdecl)]
        public extern static void uninit();

        [DllImport("sharecheck.dll", CallingConvention = CallingConvention.Cdecl)]
        public extern static int loadDAGFile([MarshalAs(UnmanagedType.AnsiBStr)]string seedhash, 
            [MarshalAs(UnmanagedType.AnsiBStr)]string folder);

        [DllImport("sharecheck.dll", CallingConvention = CallingConvention.Cdecl)]
        public extern static void unloadDAGFile();

        [DllImport("sharecheck.dll", CallingConvention = CallingConvention.Cdecl)]
        public extern static double getHashDiff([MarshalAs(UnmanagedType.AnsiBStr)]string target);

        [DllImport("sharecheck.dll", EntryPoint="getShareDiff", CallingConvention = CallingConvention.Cdecl)]
        private extern static double _getShareDiff([MarshalAs(UnmanagedType.AnsiBStr)]string headerhash,
            [MarshalAs(UnmanagedType.AnsiBStr)]string nonce, [MarshalAs(UnmanagedType.LPStr)]StringBuilder mixhash);

        public static double getShareDiff(string headerhash, string nonce, out string mixhash)
        {
            StringBuilder sb = new StringBuilder(128);
            double sdiff = _getShareDiff(headerhash, nonce, sb);
            mixhash = sb.ToString();
            return sdiff;
        }

        [DllImport("sharecheck.dll", EntryPoint = "diffToTarget", CallingConvention = CallingConvention.Cdecl)]
        private extern static void _diffToTarget(double diff, [MarshalAs(UnmanagedType.LPStr)]StringBuilder mixhash);

        public static void diffToTarget(double diff, out string target)
        {
            StringBuilder sb = new StringBuilder(128);
            _diffToTarget(diff, sb);
            target = sb.ToString();
        }
    }
}

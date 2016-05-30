using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net;
using System.IO;

namespace nhethpool
{
    class Wallet
    {
        class RPCrequest
        {
#pragma warning disable 649
            public int id;
            public string jsonrpc;
            public string method;
            [JsonProperty("params")]
            public object parameters;
#pragma warning restore 649

            public RPCrequest() { }

            public RPCrequest(string mtd)
            {
                id = 0;
                jsonrpc = "2.0";
                method = mtd;
            }
        }

        class RPCresponse<T>
        {
#pragma warning disable 649
            public int id;
            public string jsonrpc;
            public T result;
#pragma warning restore 649
        }


        public static string[] GetWork(Uri uri)
        {
            RPCrequest getwork = new RPCrequest("eth_getWork");
            getwork.parameters = new object[0];

            try
            {
                string resp = GetResponse(uri, getwork);
                if (resp == null) return null;
                RPCresponse<string[]> response = JsonConvert.DeserializeObject<RPCresponse<string[]>>(resp);
                if (response.result != null && response.result.Length == 3)
                {
                    response.result[0] = response.result[0].Substring(2);
                    response.result[1] = response.result[1].Substring(2);
                    response.result[2] = response.result[2].Substring(2);
                }
                return response.result;
            }
            catch (Exception ex)
            {
                Logging.Log(2, "GetWork error: " + ex.Message);
                return null;
            }
        }


        public static bool SubmitWork(Uri uri, string nonce, string headerhash, string mixhash, out bool result)
        {
            RPCrequest submitwork = new RPCrequest("eth_submitWork");
            submitwork.parameters = new string[3];
            ((string[])submitwork.parameters)[0] = "0x" + nonce;
            ((string[])submitwork.parameters)[1] = "0x" + headerhash;
            ((string[])submitwork.parameters)[2] = "0x" + mixhash;

            result = false;

            try
            {
                string resp = GetResponse(uri, submitwork);
                if (resp == null) return false;
                RPCresponse<bool> response = JsonConvert.DeserializeObject<RPCresponse<bool>>(resp);
                result = response.result;
                return true;
            }
            catch (Exception ex)
            {
                Logging.Log(2, "SubmitWork error: " + ex.Message);
                return false;
            }
        }


        public static bool SubmitWork(Uri uri, WalletShare share, out bool result)
        {
            return SubmitWork(uri, share.Nonce, share.HeaderHash, share.MixHash, out result);
        }


        private static string GetResponse(Uri uri, RPCrequest req)
        {
            string data = JsonConvert.SerializeObject(req, Formatting.None);

            try
            {
                HttpWebRequest wrequest = (HttpWebRequest)WebRequest.Create(uri);
                wrequest.KeepAlive = true;
                wrequest.UserAgent = "NiceHash Ethereum Pool/1.0.0";
                wrequest.Method = "POST";
                wrequest.ContentType = "application/json";

                Stream ssw = wrequest.GetRequestStream();
                StreamWriter writeS = new StreamWriter(ssw);
                writeS.AutoFlush = true;

                writeS.Write(data);

                WebResponse wresponse = wrequest.GetResponse();
                Stream ssr = wresponse.GetResponseStream();
                StreamReader sr = new StreamReader(ssr);
                string responseStr = sr.ReadLine();
                sr.Close();
                wresponse.Close();

                return responseStr;
            }
            catch (Exception ex)
            {
                Logging.Log(2, "GetWork error: " + ex.Message);
                return null;
            }
        }
    }
}

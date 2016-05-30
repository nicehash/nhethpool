using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace nhethpool
{
    public enum StratumMessageClientType
    {
        Subscribe = 1,
        SubscribeExtranonce = 2,
        Authorize = 3,
        Submit = 4
    }

    class StratumMessage
    {
        public object id;

        /// <summary>
        /// Gets JSON text string of this stratum message.
        /// </summary>
        /// <returns>JSON text of stratum message.</returns>
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        /// <summary>
        /// Gets JSON text string of this stratum message (including new line character).
        /// </summary>
        /// <returns>JSON text of stratum message with new line character.</returns>
        public string ToMessage()
        {
            return ToString() + "\n";
        }

        /// <summary>
        /// Parse incoming JSON message in string format.
        /// </summary>
        /// <param name="line">JSON message.</param>
        /// <returns>StratumMessageClient object or null if unable to determine type of message.</returns>
        public static StratumMessageClient ParseClientMessage(string line)
        {
            try
            {
                StratumMessageClient smsg = JsonConvert.DeserializeObject<StratumMessageClient>(line);

                if (smsg.id == null)
                    throw new Exception("Incorrect id.");

                switch (smsg.method)
                {
                    case "mining.subscribe":
                        smsg.type = StratumMessageClientType.Subscribe;
                        break;
                    case "mining.extranonce.subscribe":
                        smsg.type = StratumMessageClientType.SubscribeExtranonce;
                        break;
                    case "mining.authorize":
                        smsg.type = StratumMessageClientType.Authorize;
                        break;
                    case "mining.submit":
                        smsg.type = StratumMessageClientType.Submit;
                        break;
                    default:
                        throw new Exception("Unknown stratum message.");
                }

                return smsg;
            }
            catch (Exception ex)
            {
                Logging.Log(4, "JSON parse error: " + ex.Message);
                return null;
            }
        }
    }


    class StratumMessageClient : StratumMessage
    {
#pragma warning disable 649
        public string method;
        [JsonProperty("params")]
        public string[] parameters;
#pragma warning restore 649

        [JsonIgnore]
        public StratumMessageClientType type;
    }


    class StratumMessageServer : StratumMessage
    {
    }


    class StratumMessageServerResult : StratumMessageServer
    {
        public object result;
        public object error;

        public static object[] CreateMiningNotifyResult(string extranonce)
        {
            object[] first = new object[2];
            first[0] = new string[3] { "mining.notify", "ae6812eb4cd7735a302a8a9dd95cf71f", Config.ETHEREUM_STRATUM_VERSION };
            first[1] = extranonce;
            return first;
        }

        public static object[] CreateError(int id, string msg)
        {
            object[] first = new object[3];
            first[0] = id;
            first[1] = msg;
            return first;
        }
    }


    class StratumMessageServerNotify : StratumMessageServer
    {
        public string method;

        [JsonProperty("params")]
        public object[] parameters;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nhethpool
{
    class Job
    {
        public string seedHash;
        public string headerHash;
        public double difficulty;
        public string uniqueID;

        private static uint IDcounter = 1;
        private static object clock = new object();

        public Job(string seedhash, string headerhash, double diff)
        {
            seedHash = seedhash;
            headerHash = headerhash;
            difficulty = diff;
        }

        public void AdjustID()
        {
            uniqueID = getUniqueID();
        }

        public static bool operator ^(Job j1, Job j2)
        {
            if (!(j1 is Job) || !(j2 is Job)) return false;

            if ((j1.seedHash == j2.seedHash) &&
                (j1.headerHash == j2.headerHash))
                return true;
            else
                return false;
        }

        private static string getUniqueID()
        {
            uint c;
            lock (clock)
            {
                c = IDcounter++;
            }

            return c.ToString("x8");
        }
    }
}

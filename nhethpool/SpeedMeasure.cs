using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nhethpool
{
    class SpeedMeasure
    {
        private static int MAX_SHARES = 1024 * 1024;
        private static long MEASURE_TIME_SECONDS = 300;

        private class ShareEntry
        {
            public long time;
            public double diff;

            public ShareEntry(double d, long t)
            {
                diff = d;
                time = t;
            }
        }

        private object locker;
        private Queue<ShareEntry> past;

        public SpeedMeasure()
        {
            locker = new object();
            past = new Queue<ShareEntry>();
        }

        public void AddShare(double diff)
        {
            long time = DateTime.Now.ToFileTimeUtc();

            lock (locker)
            {
                if (past.Count >= MAX_SHARES)
                {
                    Logging.Log(2, "Max shares reached, speed measure may be incorrect.");
                    return;
                }

                past.Enqueue(new ShareEntry(diff, time));
            }
        }

        public double GetSpeed()
        {
            long time5m = DateTime.Now.ToFileTimeUtc() - (10L * 1000L * 1000L * MEASURE_TIME_SECONDS);
            double total = 0;

            lock (locker)
            {
                while (past.Count > 0)
                {
                    ShareEntry se = past.Peek();
                    if (se.time < time5m) past.Dequeue();
                    else break;
                }

                foreach (ShareEntry se in past)
                    total += se.diff;
            }

            total *= Math.Pow(2, 32); // get total hashes
            total /= 1000000.0; // get MH
            total /= (double)MEASURE_TIME_SECONDS; // in past X minutes, get MH/s

            return total;
        }
    }
}

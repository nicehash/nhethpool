using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nhethpool
{
    class FixedJobQueue : Queue<Job>
    {
        protected int FixedCapacity;

        public FixedJobQueue(int capacity) 
            : base(capacity)
        {
            FixedCapacity = capacity;
        }

        public void FixedEnqueue(Job item)
        {
            if (FixedCapacity == 0) return;

            if (Count == FixedCapacity)
                Dequeue();
            Enqueue(item);
        }

        public Job Find(string jobid)
        {
            Job[] jobs = ToArray();
            foreach (Job j in jobs)
                if (j.uniqueID == jobid) return j;
            return null;
        }
    }
}

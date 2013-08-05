using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Threading;

namespace GW2Miner.Engine
{
    class TimeSlots
    {
        private static double _timeLimitInMS = 0;
        private static int _range;
        private static List<DateTime> _slots;

        public TimeSlots(int range, double timeLimitInMS)
        {
            if (range <= 0)
            {
                throw new ArgumentOutOfRangeException("range", range, "argument must be greater than zero!");
            }

            _timeLimitInMS = timeLimitInMS;
            _range = range;

            _slots = new List<DateTime>(range);
            for (int i = 0; i < range; i++)
            {
                _slots.Add(DateTime.MinValue);
            }
        }

        public int GetSlot()
        {
            int minIndex = this.minLastCallIndex;

            TimeSpan span = DateTime.Now - _slots[minIndex];
            if (span.TotalMilliseconds < _timeLimitInMS)
            {
                Thread.Sleep((int)(_timeLimitInMS - span.TotalMilliseconds));
            }
            _slots[minIndex] = DateTime.Now;

            return minIndex;
        }

        private int minLastCallIndex
        {
            get
            {
                DateTime min = _slots[0];
                int minIndex = 0;

                for (int i = 0; i < _range; i++)
                {
                    if (_slots[i] < min)
                    {
                        min = _slots[i];
                        minIndex = i;
                    }
                }

                return minIndex;
            }
        }
    }
}

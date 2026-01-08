using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tftp.Net.Transfer
{
    /// <summary>
    /// Simple implementation of a timer.
    /// </summary>
    class SimpleTimer
    {
        private DateTime nextTimeout;
        private readonly TimeSpan timeout;

        public SimpleTimer(TimeSpan timeout)
        {
            this.timeout = timeout;
            Restart();
        }

        public void Restart()
        {
            this.nextTimeout = DateTime.Now.Add(timeout);
        }

        public bool IsTimeout()
        {
            return DateTime.Now >= nextTimeout;
        }
    }
}

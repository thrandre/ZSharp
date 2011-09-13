using System;
using System.Timers;

namespace iAutomationAtHome.ZSharp.Nodes.DataReader
{
    /// <summary>
    /// Defines a DataReader
    /// </summary>
    public abstract class ZWaveDataReader
    {
        /// <summary>
        /// Fired when DataReader has new data
        /// </summary>
        public event EventHandler NewDataEvent;
        
        /// <summary>
        /// Fire NewDataEvent
        /// </summary>
        public void FireNewDataEvent()
        {
            if (this.NewDataEvent != null)
                this.NewDataEvent(this, null);
        }

        /// <summary>
        /// 
        /// </summary>
        protected ZWaveNode _node;
        private Timer _intervalTimer;
        private long _interval = 10000;
        
        /// <summary>
        /// Set poll interval
        /// </summary>
        public long Interval
        {
            set
            {
                this._interval = value;
            }
        }

        /// <summary>
        /// Construc
        /// </summary>
        /// <param name="node"></param>
        public ZWaveDataReader(ZWaveNode node)
        {
            this._node = node;
        }

        internal void Start()
        {
            this._intervalTimer = new Timer(this._interval);
            this._intervalTimer.Elapsed += IntervalElapsed;
            this._intervalTimer.AutoReset = true;
            
            // Fire once immidiately
            this.IntervalElapsed(this, null);

            // Start the timer
            this._intervalTimer.Start();
        }

        internal void Stop()
        {
            this._intervalTimer.Stop();
            this._intervalTimer.Dispose();
        }

        internal abstract void IntervalElapsed(object sender, EventArgs e);
    }
}

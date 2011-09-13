/*
 * ZSharp - C# Z-Wave Implementation
 * HiØ 2011
 * H11D13 / iAutomation@Home
 * Author: thomrand
 */

using System;
using iAutomationAtHome.ZSharp.Nodes;
using System.Collections.Generic;
using System.Timers;

namespace iAutomationAtHome.ZSharp
{
	/// <summary>
	/// Description of ZWaveJob.
	/// </summary>
	internal class ZWaveJob
	{
        // Define our events
        public event EventHandler ResponseReceived;
        private void FireResponseReceivedEvent()
        {
            if (this.ResponseReceived != null)
                this.ResponseReceived(this, null);
        }

        public event EventHandler JobCanceled;
        private void FireJobCanceledEvent()
        {
            if (this.JobCanceled != null)
                this.JobCanceled(this, null);
        }

        private ZWaveMessage _request;
        public ZWaveMessage Request
        {
            get { return this._request; }
            set { this._request = value; }
        }

        private Queue<ZWaveMessage> _response = new Queue<ZWaveMessage>();
        public ZWaveMessage GetResponse()
        {
            lock (this._responseLock)
            {
                if (this._response.Count > 0) return this._response.Dequeue();
                else return null;
            }
        }

        public void AddResponse(ZWaveMessage message)
        {
            this.RemoveTimeout();
            lock (this._responseLock) { this._response.Enqueue(message); }
            this.FireResponseReceivedEvent();
        }

        public void CancelJob()
        {
            System.Diagnostics.Debug.WriteLine("*** Canceled");
            
            this.Done();
            this._awaitACK = false;
            this._awaitResponse = false;
            this.FireJobCanceledEvent();
        }

        public void TriggerResend()
        {
            System.Diagnostics.Debug.WriteLine("*** Trigger resend");
            
            this._awaitACK = false;
            this._awaitResponse = false;
            this.Resend = true;
        }

        private Timer _timeout;
        public void SetTimeout(int interval)
        {
            this._timeout = new Timer(interval);
            this._timeout.Elapsed += Timeout;
            this._timeout.Start();
        }

        public void RemoveTimeout()
        {
            if (this._timeout != null)
            {
                this._timeout.Elapsed -= Timeout;
                this._timeout.Dispose();
                this._timeout = null;
            }
        }

        private void Timeout(object sender, EventArgs e)
        {
            this.TriggerResend();
        }

        // Messaging control-switch
        private bool _awaitACK = false;
        public bool AwaitACK
        {
            get { return this._awaitACK; }
            set { this._awaitACK = value; }
        }

        private bool _awaitResponse = false;
        public bool AwaitResponse
        {
            get { return this._awaitResponse; }
            set { this._awaitResponse = value; }
        }

        private Object _responseLock = new Object();
        public int SendCount = 0;
        
        public bool Resend = false;

        public bool JobDone = false;
        public void Done()
        {
            this.JobDone = true;
            this.RemoveTimeout();
        }

        public bool JobStarted = false;
        public void Start()
        {
            this.JobStarted = true;
        }

        public ZWaveJob() { }
    }
}

using System;
using iAutomationAtHome.Debugging;
using iAutomationAtHome.ZSharp.Nodes.DataReader;
using System.Timers;

namespace iAutomationAtHome.ZSharp.Nodes
{
    /// <summary>
    /// Represents a node of generic type SwitchMultilevel
    /// </summary>
    public class SwitchMultilevel : ZWaveNode, Switch
    {
        /// <summary>
        /// Fired when node changes state
        /// </summary>
        public event EventHandler NodeChangedStateEvent;
        private void FireNodeChangedStateEvent()
        {
            if (this.NodeChangedStateEvent != null)
                this.NodeChangedStateEvent(this, null);
        }

        private Timer _stateTimer;
        private byte _state = 0x00;
        
        /// <summary>
        /// Get state
        /// </summary>
        /// <returns></returns>
        public byte State
        {
            get { return this._state; }
        }

        internal SwitchMultilevel(ZWavePort port, byte nodeId) : base(port, nodeId)
        {
            this._basicType = ZWaveProtocol.Type.Basic.ROUTING_SLAVE;
            this._genericType = ZWaveProtocol.Type.Generic.SWITCH_MULTILEVEL;
        }

        /// <summary>
        /// Initialize node
        /// </summary>
        public override void Initialize()
        {
            base.BaseNodeInitializedEvent += BaseInitialized;
            base.InitializeBase();
        }

        private void BaseInitialized(object sender, EventArgs e)
        {
            base.BaseNodeInitializedEvent -= BaseInitialized;
            this.InitializeNode();
        }

        private void InitializeNode()
        {
            this._stateTimer = new Timer(ZWaveProtocol.ValueConstants.STATE_POLL_INTERVAL);
            this._stateTimer.AutoReset = true;
            this._stateTimer.Elapsed += GetState;
            this._stateTimer.Start();

            this.GetState(this, null);
            this.FireNodeInitializedEvent();
        }

        private void StateUpdated(byte state)
        {
            if (state != this._state)
            {
                this._state = state;
                this.FireNodeChangedStateEvent();
                DebugLogger.GetLogger.LogMessage(this, "Node " + this._nodeId + " changed state: " + this._state);
            }
        }

        private void GetState(object sender, EventArgs e)
        {
            ZWaveJob gis = new ZWaveJob();
            gis.Request = new ZWaveMessage(ZWaveProtocol.MessageType.REQUEST,
                                           ZWaveProtocol.Function.SEND_DATA,
                                           this._nodeId,
                                           ZWaveProtocol.CommandClass.SWITCH_MULTILEVEL,
                                           ZWaveProtocol.Command.SWITCH_MULTILEVEL_GET);
            gis.ResponseReceived += ResponseReceived;
            this.EnqueueJob(gis);
        }

        /// <summary>
        /// Set level
        /// </summary>
        /// <param name="level"></param>
        public void SetLevel(byte level)
        {
            ZWaveJob job = new ZWaveJob();
            job.Request = new ZWaveMessage(ZWaveProtocol.MessageType.REQUEST,
                                           ZWaveProtocol.Function.SEND_DATA,
                                           this._nodeId,
                                           ZWaveProtocol.CommandClass.SWITCH_MULTILEVEL,
                                           ZWaveProtocol.Command.SWITCH_MULTILEVEL_SET);
            job.Request.AddParameter(level);
            job.ResponseReceived += ResponseReceived;
            this.EnqueueJob(job);
        }

        /// <summary>
        /// Turn on
        /// </summary>
        public void On()
        {
            this.SetLevel(0x64);
        }

        /// <summary>
        /// Turn off
        /// </summary>
        public void Off()
        {
            this.SetLevel(0x00);
        }

        private void ResponseReceived(object sender, EventArgs e)
        {
            ZWaveJob job = (ZWaveJob)sender;
            ZWaveMessage request = job.Request;
            ZWaveMessage response = job.GetResponse();

            bool done = false;

            switch (request.Function)
            {
                case ZWaveProtocol.Function.SEND_DATA:
                    switch (response.Function)
                    {
                        case ZWaveProtocol.Function.APPLICATION_COMMAND_HANDLER:
                            switch (response.CommandClass)
                            {
                                case ZWaveProtocol.CommandClass.SWITCH_MULTILEVEL:
                                    switch (response.Command)
                                    {
                                        case ZWaveProtocol.Command.SWITCH_MULTILEVEL_REPORT:
                                            this.StateUpdated(response.Message[9]);
                                            done = true;
                                            break;
                                        default:
                                            job.SetTimeout(3000);
                                            break;
                                    }
                                    break;
                                default:
                                    job.SetTimeout(3000);
                                    break;
                            }
                            break;
                        case ZWaveProtocol.Function.SEND_DATA:
                            switch (request.Command)
                            {
                                case ZWaveProtocol.Command.SWITCH_MULTILEVEL_SET:
                                    //this.StateUpdated(request.Message[8]);
                                    done = true;
                                    break;
                                default:
                                    job.SetTimeout(3000);
                                    break;
                            }
                            break;
                        default:
                            job.SetTimeout(3000);
                            break;
                    }
                    break;
                default:
                    job.SetTimeout(3000);
                    break;
            }

            if (done)
            {
                job.Done();
                job.ResponseReceived -= ResponseReceived;
            }
        }
    }
}

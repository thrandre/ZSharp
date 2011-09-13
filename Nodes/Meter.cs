using System;

namespace iAutomationAtHome.ZSharp.Nodes
{
    /// <summary>
    /// Represents a node of generic type Meter
    /// </summary>
    public class Meter : ZWaveNode
    {
        internal Meter(ZWavePort port, byte nodeId) : base(port, nodeId)
        {
            this._basicType = ZWaveProtocol.Type.Basic.ROUTING_SLAVE;
            this._genericType = ZWaveProtocol.Type.Generic.METER;
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
            this.FireNodeInitializedEvent();
            this.NodeInitialized();
            this.GetMeterReport();
        }

        private void NodeInitialized()
        {
            this._initialized = true;
        }

        private void GetMeterReport()
        {
            ZWaveJob mr = new ZWaveJob();
            mr.Request = new ZWaveMessage(ZWaveProtocol.MessageType.REQUEST,
                                          ZWaveProtocol.Function.SEND_DATA,
                                          this._nodeId,
                                          0x3D,
                                          0x04);
            mr.ResponseReceived += ResponseReceived;
            //this.EnqueueJob(mr);
        }

        private void ResponseReceived(object sender, EventArgs e)
        {
            
        }
    }
}

using System;

namespace iAutomationAtHome.ZSharp.Nodes
{
    /// <summary>
    /// Represents a node of generic type SensorBinary
    /// </summary>
    public class SensorBinary : ZWaveNode
    {
        internal SensorBinary(ZWavePort port, byte nodeId) : base(port, nodeId)
        {
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
        }
    }
}

/*
 * ZSharp - C# Z-Wave Implementation
 * HiØ 2011
 * H11D13 / iAutomation@Home
 * Author: thomrand
 */

using System;
using System.Collections.Generic;
using iAutomationAtHome.Debugging;

namespace iAutomationAtHome.ZSharp.Nodes
{
    /// <summary>
    /// Defines a controller
    /// </summary>
    public class Controller : ZWaveNode
    {
        /// <summary>
        /// Fired when controller is ready
        /// </summary>
        public event EventHandler ReadyEvent;
        private void FireReadyEvent()
        {
            if (this.ReadyEvent != null)
            {
                this.ReadyEvent(this, null);
            }
        }

        /// <summary>
        /// NodeEventArgs
        /// </summary>
        public class NodeEventArgs : EventArgs
        {
            /// <summary>
            /// 
            /// </summary>
            public byte nodeId;
            /// <summary>
            /// 
            /// </summary>
            /// <param name="nodeId"></param>
            public NodeEventArgs(byte nodeId)
                : base()
            {
                this.nodeId = nodeId;
            }
        }

        /// <summary>
        /// Fired when node is added
        /// </summary>
        public event EventHandler NodeAddedEvent;
        private void FireNodeAddedEvent(byte nodeId)
        {
            if (this.NodeAddedEvent != null)
            {
                this.NodeAddedEvent(this, new NodeEventArgs(nodeId));
            }
        }

        private int _nodesInitialized = 0;
        private Dictionary<byte, ZWaveNode> _nodes;
        
        /// <summary>
        /// Get dictionary of nodes
        /// </summary>
        public Dictionary<byte, ZWaveNode> Nodes
        {
            get
            {
                if(this._nodes == null) this._nodes = new Dictionary<byte, ZWaveNode>();
                return this._nodes;
            }
        }

        internal Controller(ZWavePort port, byte nodeId) :
            base(port, nodeId)
        {
        }

        /// <summary>
        /// Initialize controller
        /// </summary>
        public override void Initialize()
        {
            DebugLogger.GetLogger.LogMessage(this, "Created controller node. NODE_ID: " + this._nodeId.ToString("X2"));
            this._port.UnsubscribedMessageEvent += UnsubscribedMessageReceived;
            this.GetSuccessorNode();
        }

        private void GetSuccessorNode()
        {
            ZWaveJob job = new ZWaveJob();
            job.Request = new ZWaveMessage(ZWaveProtocol.MessageType.REQUEST,
                                                         ZWaveProtocol.Function.GET_SUC_NODE_ID);
            job.ResponseReceived += this.ResponseReceived;
            this._port.EnqueueJob(job);
        }

        /// <summary>
        /// Become successor node
        /// </summary>
        public void BecomeSuccessorNode()
        {
            ZWaveJob enable = new ZWaveJob();
            enable.Request = new ZWaveMessage(ZWaveProtocol.MessageType.REQUEST,
                                                        ZWaveProtocol.Function.ENABLE_SUC);
            enable.ResponseReceived += ResponseReceived;
            this._port.EnqueueJob(enable);

            ZWaveJob set = new ZWaveJob();
            set.Request = new ZWaveMessage(ZWaveProtocol.MessageType.REQUEST,
                                                        ZWaveProtocol.Function.SET_SUC_NODE_ID);
            set.Request.AddParameter(this._nodeId);
            
            // SUC = 0, SIS = 1
            set.Request.AddParameter(0x01);
            
            // No low-power shit
            set.Request.AddParameter(0x00);
            
            // What's this for? Oh well, we need to follow the specification anyway
            set.Request.AddParameter(ZWaveProtocol.Function.NODEID_SERVER);
            set.ResponseReceived += ResponseReceived;
            this._port.EnqueueJob(set);
        }

        /// <summary>
        /// Perform discovery
        /// </summary>
        public void Discovery()
        {
            DebugLogger.GetLogger.LogMessage(this, "Performing node discovery");

            ZWaveJob job = new ZWaveJob();
            job.Request = new ZWaveMessage(ZWaveProtocol.MessageType.REQUEST,
                                                        ZWaveProtocol.Function.SERIAL_API_INIT_DATA);
            job.ResponseReceived += ResponseReceived;
            this._port.EnqueueJob(job);
        }

        private void ExtractNodeList(byte[] response)
        {
            int found = 0;
            for (int i = 7; i < 35; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    if((response[i] & (0x01 << j)) != 0)
                    {
                        byte nodeId = (byte)((i - 7)*8 + (j+1));
                        if (nodeId != this._nodeId)
                        {
                            found++;
                            System.Diagnostics.Debug.WriteLine("NodeID:" + nodeId);
                            this.GetNodeProtocolInfo(nodeId);
                        }
                    }
                }
            }

            if (found == 0)
            {
                DebugLogger.GetLogger.LogMessage(this, "No nodes were found");
                this.FireReadyEvent();
            }
        }

        private void GetNodeProtocolInfo(byte nodeId)
        {
            ZWaveJob pInfo = new ZWaveJob();
            pInfo.Request = new ZWaveMessage(ZWaveProtocol.MessageType.REQUEST,
                                             ZWaveProtocol.Function.GET_NODE_PROTOCOL_INFO,
                                             nodeId);
            pInfo.ResponseReceived += ResponseReceived;
            this._port.EnqueueJob(pInfo);
        }

        private void CreateNode(byte nodeId, bool sleeping, byte basicType, byte genericType, byte specificType)
        {
            ZWaveNode node = null;
            switch (genericType)
            {
                case ZWaveProtocol.Type.Generic.SWITCH_BINARY:
                    node = new SwitchBinary(this._port, nodeId);
                    break;
                case ZWaveProtocol.Type.Generic.SENSOR_BINARY:
                    node = new SensorBinary(this._port, nodeId);
                    break;
                case ZWaveProtocol.Type.Generic.SENSOR_MULTILEVEL:
                    node = new SensorBinary(this._port, nodeId);
                    break;
                case ZWaveProtocol.Type.Generic.METER:
                    node = new Meter(this._port, nodeId);
                    break;
                case ZWaveProtocol.Type.Generic.SWITCH_MULTILEVEL:
                    node = new SwitchMultilevel(this._port, nodeId);
                    break;
                default:
                    DebugLogger.GetLogger.LogMessage(this, "Unknown node found: NODE_ID = " + nodeId.ToString("X2") + " GENERIC_TYPE: " + genericType.ToString("X2"));
                    break;
            }

            if (node != null)
            {
                DebugLogger.GetLogger.LogMessage(this, "Node found: NODE_ID = " + nodeId.ToString("X2") + ", GENERIC_TYPE: " + Utils.GenericTypeToString(genericType) + ", SPECIFIC_TYPE: " + specificType.ToString("X2"));
                DebugLogger.GetLogger.LogMessage(this, "Sleeping: " + sleeping);
                
                if(!this.Nodes.ContainsKey(nodeId))
                    this.Nodes.Add(nodeId, node);
                
                node.IsSleepingNode = sleeping;

                if (sleeping)
                {
                    this.SetWakeupInterval(nodeId);
                }

                node.NodeInitializedEvent += NodeInitialized;
                node.Initialize();
            }
        }

        private void SetWakeupInterval(byte nodeId)
        {
            ZWaveJob wi = new ZWaveJob();
            wi.Request = new ZWaveMessage(ZWaveProtocol.MessageType.REQUEST,
                                          ZWaveProtocol.Function.SEND_DATA,
                                          nodeId,
                                          ZWaveProtocol.CommandClass.WAKE_UP,
                                          ZWaveProtocol.Command.WAKE_UP_INTERVAL_SET);
            
            long seconds = (long)(ZWaveProtocol.ValueConstants.WAKE_UP_INTERVAL / 1000);
            System.Diagnostics.Debug.WriteLine(seconds);
            // MSB
            wi.Request.AddParameter((byte)((seconds >> 16) & 0xFF));
            wi.Request.AddParameter((byte)((seconds >> 8) & 0xFF));
            // LSB
            wi.Request.AddParameter((byte)(seconds & 0xFF));
            wi.Request.AddParameter(this._nodeId);

            wi.ResponseReceived += ResponseReceived;
            this._port.EnqueueJob(wi);
        }

        private void ResponseReceived(object sender, EventArgs e)
        {
            ZWaveJob job = (ZWaveJob)sender;
            ZWaveMessage response = job.GetResponse();
            ZWaveMessage request = job.Request;

            bool done = false;

            switch (request.Function)
            {
                case ZWaveProtocol.Function.GET_SUC_NODE_ID:
                    switch (response.Function)
                    {
                        case ZWaveProtocol.Function.GET_SUC_NODE_ID:
                            if ((byte)response.Message[4] != this._nodeId)
                            {
                                // We are not SUC/SIS. Send request.
                                DebugLogger.GetLogger.LogMessage(this, "We are not SUC/SIS. Sending request to become one");
                                this.BecomeSuccessorNode();
                                done = true;
                            }
                            else
                            {
                                // We are allready SUC/SIS. Advance.
                                DebugLogger.GetLogger.LogMessage(this, "We are SUC/SIS");
                                this.FireNodeInitializedEvent();
                                this.Discovery();
                                done = true;
                            }
                            break;
                        default:
                            job.TriggerResend();
                            done = false;
                            break;
                    }
                    break;
                case ZWaveProtocol.Function.ENABLE_SUC:
                    switch (response.Function)
                    {
                        case ZWaveProtocol.Function.ENABLE_SUC:
                            // SUC Enabled
                            DebugLogger.GetLogger.LogMessage(this, "SUC Enabled");
                            done = true;
                            break;
                        default:
                            job.TriggerResend();
                            done = false;
                            break;
                    }
                    break;
                case ZWaveProtocol.Function.SET_SUC_NODE_ID:
                    switch (response.Function)
                    {
                        case ZWaveProtocol.Function.SET_SUC_NODE_ID:
                            // We are now SUC/SIS. Advance
                            DebugLogger.GetLogger.LogMessage(this, "We are now SUC/SIS");
                            this.FireNodeInitializedEvent();
                            this.Discovery();
                            done = true;
                            break;
                        default:
                            job.TriggerResend();
                            done = false;
                            break;
                    }
                    break;
                case ZWaveProtocol.Function.SERIAL_API_INIT_DATA:
                    switch (response.Function)
                    {
                        case ZWaveProtocol.Function.SERIAL_API_INIT_DATA:
                            // Extract the nodes from the received bitmask
                            DebugLogger.GetLogger.LogMessage(this, "Extracting nodes from bitmask");
                            this.ExtractNodeList(response.Message);
                            done = true;
                            break;
                        default:
                            job.TriggerResend();
                            done = false;
                            break;
                    }
                    break;
                case ZWaveProtocol.Function.GET_NODE_PROTOCOL_INFO:
                    switch (response.Function)
                    {
                        case ZWaveProtocol.Function.GET_NODE_PROTOCOL_INFO:
                            // Got protocol info from node
                            DebugLogger.GetLogger.LogMessage(this, "Got node protocol info");
                            byte[] msg = response.Message;
                            bool sleeping = !((msg[4] & (0x01 << 7)) > 0x00);
                            this.CreateNode(request.NodeId, sleeping, msg[7], msg[8], msg[9]);
                            done = true;
                            break;
                        default:
                            job.TriggerResend();
                            done = false;
                            break;
                    }
                    break;
                case ZWaveProtocol.Function.ADD_NODE_TO_NETWORK:
                    switch (response.Function)
                    {
                        case ZWaveProtocol.Function.ADD_NODE_TO_NETWORK:
                            switch(response.CommandClass)
                            {
                                case ZWaveProtocol.CommandClass.ADD_NODE_LEARN_READY:
                                    DebugLogger.GetLogger.LogMessage(this, "LEARN_READY");
                                    done = true;
                                    break;
                                default:
                                    job.SetTimeout(3000);
                                    done = false;
                                    break;
                            }
                            break;
                        default:
                            job.SetTimeout(3000);
                            done = false;
                            break;
                    }
                    break;
                case ZWaveProtocol.Function.SET_DEFAULT:
                    switch (response.Function)
                    {
                        case ZWaveProtocol.Function.SET_DEFAULT:
                            done = true;
                            this.Initialize();
                            break;
                    }
                    break;
                case ZWaveProtocol.Function.SEND_DATA:
                    switch (response.Function)
                    {
                        case ZWaveProtocol.Function.SEND_DATA:
                            if ((response.Message.Length - 2) == 5)
                            {
                                done = true;
                            }
                            break;
                    }
                    break;
            }

            if (done)
            {
                job.Done();
                job.ResponseReceived -= ResponseReceived;
            }
        }

        private void UnsubscribedMessageReceived(object sender, EventArgs e)
        {
            ZWaveMessage message = ((iAutomationAtHome.ZSharp.ZWavePort.UnsubscribedMessageEventArgs)e).Message;

            switch (message.CommandClass)
            {
                case ZWaveProtocol.CommandClass.ADD_NODE_STATUS_ADDING_SLAVE:
                    byte nodeId = message.Message[6];
                    DebugLogger.GetLogger.LogMessage(this, "New node found: " + nodeId.ToString("X2"));
                    this.GetNodeProtocolInfo(nodeId);
                    break;
            }
        }

        private void NodeInitialized(object sender, EventArgs e)
        {
            ((ZWaveNode)sender).NodeInitializedEvent -= NodeInitialized;
            this._nodesInitialized++;
            if (this._nodesInitialized == this.Nodes.Count)
            {
                this.FireReadyEvent();
            }
            this.FireNodeAddedEvent(((ZWaveNode)sender)._nodeId);
        }

        /// <summary>
        /// Start inclusion process
        /// </summary>
        public void AddNewNodeStart()
        {
            ZWaveJob job = new ZWaveJob();
            job.Request = new ZWaveMessage(ZWaveProtocol.MessageType.REQUEST, 
                                           ZWaveProtocol.Function.ADD_NODE_TO_NETWORK);
            job.Request.AddParameter(ZWaveProtocol.CommandClass.NODE_ANY);
            job.ResponseReceived += ResponseReceived;
            this._port.EnqueueJob(job);
        }

        /// <summary>
        /// Stop inclusion process
        /// </summary>
        public void AddNewNodeStop()
        {
            ZWaveJob job = new ZWaveJob();
            job.Request = new ZWaveMessage(ZWaveProtocol.MessageType.REQUEST,
                                           ZWaveProtocol.Function.ADD_NODE_TO_NETWORK);
            job.Request.AddParameter(ZWaveProtocol.CommandClass.ADD_NODE_STOP);
            job.ResponseReceived += ResponseReceived;
            this._port.InjectJob(job);
        }

        /// <summary>
        /// Reset Z-Wave controller
        /// </summary>
        public void Reset()
        {
            ZWaveJob job = new ZWaveJob();
            job.Request = new ZWaveMessage(ZWaveProtocol.MessageType.REQUEST,
                                           ZWaveProtocol.Function.SET_DEFAULT);
            job.ResponseReceived += ResponseReceived;
            this._port.EnqueueJob(job);
        }
    }
}

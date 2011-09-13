/*
 * ZSharp - C# Z-Wave Implementation
 * HiØ 2011
 * H11D13 / iAutomation@Home
 * Author: thomrand
 */

using System;
using System.Collections.Generic;
using iAutomationAtHome.ZSharp.Nodes.DataReader;
using System.Timers;
using System.ServiceModel;

namespace iAutomationAtHome.ZSharp.Nodes
{
	/// <summary>
	/// Description of ZWaveNode.
	/// </summary>
    public abstract class ZWaveNode
	{
        /// <summary>
        /// Fired when base node is initialized
        /// </summary>
        protected event EventHandler BaseNodeInitializedEvent;      
        
        /// <summary>
        /// Fire BaseNodeInitializedEvent
        /// </summary>
        protected void FireBaseNodeInitializedEvent()
        {
            if (this.BaseNodeInitializedEvent != null)
                this.BaseNodeInitializedEvent(this, null);
        }

        internal event EventHandler NodeInitializedEvent;
        internal void FireNodeInitializedEvent()
        {
            if(this.NodeInitializedEvent != null)
                this.NodeInitializedEvent(this, null);
        }

        /// <summary>
        /// 
        /// </summary>
        protected bool _initialized = false;
        
        /// <summary>
        /// Is the node initialized?
        /// </summary>
        public bool Initialized
        {
            get
            {
                return this._initialized;
            }
        }

        private Timer _delayedInitializationTimer;

        private bool _isSleepingNode = false;
        
        /// <summary>
        /// Is the node a sleeping node?
        /// </summary>
        public bool IsSleepingNode
        {
            get
            {
                return this._isSleepingNode;
            }

            set
            {
                this._isSleepingNode = value;
            }
        }

        internal ZWavePort _port;

        internal byte _nodeId;
        
        /// <summary>
        /// Get NodeID
        /// </summary>
        public byte NodeId
        {
            get
            {
                return this._nodeId;
            }
        }

        internal Queue<ZWaveJob> _jobQueue;
        internal Queue<ZWaveJob> JobQueue
        {
            get
            {
                if (this._jobQueue == null) this._jobQueue = new Queue<ZWaveJob>();
                return this._jobQueue;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        protected byte _basicType;
        
        /// <summary>
        /// Get basic type
        /// </summary>
        public byte BasicType
        {
            get
            {
                return this._basicType;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        protected byte _genericType;
        
        /// <summary>
        /// Get generic type
        /// </summary>
        public byte GenericType
        {
            get
            {
                return this._genericType;
            }
        }

        private int _manufacturer;
        /// <summary>
        /// Return manufacturer
        /// </summary>
        public int Manufacturer
        {
            get
            {
                return this._manufacturer;
            }
        }

        private List<byte> _capabilities = new List<byte>();
        
        /// <summary>
        /// Get node capabilities
        /// </summary>
        public List<byte> Capabilities
        {
            get
            {
                return this._capabilities;
            }
        }

        /// <summary>
        /// Get dictionary of DataReaders
        /// </summary>
        protected Dictionary<String, ZWaveDataReader> _dataReaders;
        
        /// <summary>
        /// Get Dictionary of DataReaders
        /// </summary>
        public Dictionary<String, ZWaveDataReader> DataReaders
        {
            get
            {
                if (this._dataReaders == null) this._dataReaders = new Dictionary<string, ZWaveDataReader>();
                return this._dataReaders;
            }
        }

        internal ZWaveNode(ZWavePort port, byte nodeId)
		{
            this._port = port;
            this._nodeId = nodeId;
        }

        internal void EnqueueJob(ZWaveJob job)
        {
            if (this._isSleepingNode)
            {
                this.JobQueue.Enqueue(job);
                this.FireBaseNodeInitializedEvent();
            }
            else
            {
                this._port.EnqueueJob(job);
            }
        }

        /// <summary>
        /// Trigger initialization of base class
        /// </summary>
        public void InitializeBase()
        {
            this.RequestNeighborUpdate();
        }

        private void RetryInitialization(object sender, EventArgs e)
        {
            if (this._delayedInitializationTimer != null)
            {
                this._delayedInitializationTimer.Elapsed -= RetryInitialization;
                this._delayedInitializationTimer.Dispose();
            }

            this.InitializeBase();
        }

        private void BaseNodeInitialized()
        {
            this.FireBaseNodeInitializedEvent();
        }

        /// <summary>
        /// Initialize node
        /// </summary>
        public abstract void Initialize();
        
        /// <summary>
        /// Triggered when node is awake
        /// </summary>
        public void WakeUp() { }

        private void RequestNeighborUpdate()
        {
            ZWaveJob nu = new ZWaveJob();
            nu.Request = new ZWaveMessage(ZWaveProtocol.MessageType.REQUEST,
                                          ZWaveProtocol.Function.REQUEST_NODE_NEIGHBOR_UPDATE,
                                          this._nodeId);
            nu.ResponseReceived += ResponseReceived;
            this.EnqueueJob(nu);
        }

        private void GetManufacturerSpecificReport()
        {
            ZWaveJob msr = new ZWaveJob();
            msr.Request = new ZWaveMessage(ZWaveProtocol.MessageType.REQUEST,
                                           ZWaveProtocol.Function.SEND_DATA,
                                           this._nodeId,
                                           ZWaveProtocol.CommandClass.MANUFACTURER_SPECIFIC,
                                           ZWaveProtocol.Command.MANUFACTURER_SPECIFIC_GET);
            msr.ResponseReceived += this.ResponseReceived;
            msr.JobCanceled += this.RequestFailed;
            this.EnqueueJob(msr);
        }

        private void GetNodeCapabilities()
        {
            ZWaveJob cc = new ZWaveJob();
            cc.Request = new ZWaveMessage(ZWaveProtocol.MessageType.REQUEST,
                                          ZWaveProtocol.Function.GET_NODE_CAPABILITIES,
                                          this._nodeId);
            cc.ResponseReceived += this.ResponseReceived;
            cc.JobCanceled += this.RequestFailed;
            this.EnqueueJob(cc);
        }

        private void RequestFailed(object sender, EventArgs e)
        {
            ((ZWaveJob)sender).JobCanceled -= RequestFailed;
            this._delayedInitializationTimer = new Timer(10000);
            this._delayedInitializationTimer.Elapsed += RetryInitialization;
            this._delayedInitializationTimer.Start();
        }

        private void ResponseReceived(object sender, EventArgs e)
        {
            ZWaveJob job = (ZWaveJob)sender;
            ZWaveMessage request = job.Request;
            ZWaveMessage response = job.GetResponse();

            bool done = false;

            switch(request.Function)
            {
                // Manufacturer specific report
                case ZWaveProtocol.Function.SEND_DATA:
                    switch (response.Function)
                    {
                        case ZWaveProtocol.Function.APPLICATION_COMMAND_HANDLER:
                            this._manufacturer = (((int)response.Message[9]) | ((int)response.Message[10]));
                            
                            this.GetNodeCapabilities();
                            done = true;
                            break;
                        default:
                            job.SetTimeout(3000);
                            done = false;
                            break;
                    }
                    break;
                case ZWaveProtocol.Function.GET_NODE_CAPABILITIES:
                    switch (response.Function)
                    {
                        case ZWaveProtocol.Function.GET_NODE_CAPABILITIES_RESPONSE:
                            for (int i = 10; i < ((int)(6 + response.Message[6])); i++)
                            {
                                this._capabilities.Add(response.Message[i]);
                                System.Diagnostics.Debug.WriteLine(response.Message[i].ToString("X2"));
                            }
                            this.BaseNodeInitialized();
                            done = true;
                            break;
                        default:
                            job.SetTimeout(3000);
                            break;

                    }
                    break;
                case ZWaveProtocol.Function.REQUEST_NODE_NEIGHBOR_UPDATE:
                    switch (response.Function)
                    {
                        case ZWaveProtocol.Function.REQUEST_NODE_NEIGHBOR_UPDATE:
                            this.GetManufacturerSpecificReport();
                            job.Done();
                            break;
                        default:
                            job.SetTimeout(3000);
                            break;
                    }
                    break;
                default:
                    job.SetTimeout(3000);
                    done = false;
                    break;
            }

            if (done)
            {
                job.ResponseReceived -= ResponseReceived;
                job.Done();
            }
        }
	}
}

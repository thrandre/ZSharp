/*
 * ZSharp - C# Z-Wave Implementation
 * HiØ 2011
 * H11D13 / iAutomation@Home
 * Author: thomrand
 */

using System;
using iAutomationAtHome.Debugging;
using iAutomationAtHome.ZSharp.Nodes;

namespace iAutomationAtHome.ZSharp
{
	/// <summary>
	/// Description of ZWave.
	/// </summary>
	public class ZWave
	{
        /// <summary>
        /// Event is fired when Z-Wave is done initializing
        /// </summary>
        public event EventHandler ZWaveInitializedEvent;
        private void FireZWaveInitializedEvent()
        {
            if (this.ZWaveInitializedEvent != null)
                this.ZWaveInitializedEvent(this, null);
        }

        /// <summary>
        /// Event is fired when Z-Wave initialization fails
        /// </summary>
        public event EventHandler ZWaveFailedEvent;
        private void FireZWaveFailedEvent()
        {
            if (this.ZWaveFailedEvent != null)
                this.ZWaveFailedEvent(this, null);
        }

        /// <summary>
        /// Event is fired when Z-Wave and all nodes are initialized 
        /// </summary>
        public event EventHandler ZWaveReadyEvent;
        private void FireZWaveReadyEvent()
        {
            if (this.ZWaveReadyEvent != null)
                this.ZWaveReadyEvent(this, null);
        }

        private Controller _controller = null;
        
        /// <summary>
        /// Get a reference to the Z-Wave controller
        /// </summary>
        public Controller Controller
        {
            get
            {
                return this._controller;
            }
        }

        private ZWavePort _port;

		/// <summary>
		/// Constructor
		/// </summary>
        public ZWave()
		{
            this._port = new ZWavePort();
		}

        /// <summary>
        /// Initialize Z-Wave
        /// </summary>
        public void Initialize()
        {
            if (!this._port.Open())
            {
                this.FireZWaveFailedEvent();
                return;
            }
            
            DebugLogger.GetLogger.LogMessage(this, "Initializing");
            this.GetVersion();
        }

        private void GetVersion()
        {
            ZWaveJob v = new ZWaveJob();
            v.Request = new ZWaveMessage(ZWaveProtocol.MessageType.REQUEST,
                                         ZWaveProtocol.Function.GET_VERSION);
            v.ResponseReceived += ResponseReceived;
            this._port.EnqueueJob(v);
        }

        private void GetHomeID()
        {
            ZWaveJob h = new ZWaveJob();
            h.Request = new ZWaveMessage(ZWaveProtocol.MessageType.REQUEST,
                                         ZWaveProtocol.Function.MEMORY_GET_ID);
            h.ResponseReceived += ResponseReceived;
            this._port.EnqueueJob(h);
        }

        /// <summary>
        /// Make sure Z-Wave is shut down gracefully
        /// </summary>
        public void ShutdownGracefully()
        {
            this._port.Close();
        }

        private void ResponseReceived(object sender, EventArgs e)
        {
            ZWaveJob job = (ZWaveJob)sender;
            ZWaveMessage request = job.Request;
            ZWaveMessage response = job.GetResponse();

            bool done = false;

            switch (request.Function)
            {
                case ZWaveProtocol.Function.GET_VERSION:
                    switch (response.Function)
                    {
                        case ZWaveProtocol.Function.GET_VERSION:
                            // Got version information
                            this.ParseVersionInformation(response.Message);
                            this.GetHomeID();
                            done = true;
                            break;
                        default:
                            job.TriggerResend();
                            done = false;
                            break;
                    }
                    break;
                case ZWaveProtocol.Function.MEMORY_GET_ID:
                    switch (response.Function)
                    {
                        case ZWaveProtocol.Function.MEMORY_GET_ID:
                            // Got home id and controller id
                            this.ParseHomeIDInformation(response.Message);
                            done = true;
                            break;
                        default:
                            job.TriggerResend();
                            done = false;
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

        private void ParseVersionInformation(byte[] message)
        {
            DebugLogger.GetLogger.LogMessage(this, "Received version information: API Version: " + Utils.VersionToString(Utils.ByteSubstring(message, 11, 4), Utils.VersionType.API) +
                              ", SDK Version: " + Utils.VersionToString(Utils.ByteSubstring(message, 11, 4), Utils.VersionType.SDK));
        }

        private void ParseHomeIDInformation(byte[] message)
        {
            String homeId = Utils.ByteArrayToString(Utils.ByteSubstring(message, 4, 4));
            byte controllerId = message[8];

            DebugLogger.GetLogger.LogMessage(this, "Received network configuration: HOME_ID: " + homeId +
                  ", PRIMARY_CONTROLLER_ID: " + controllerId.ToString("X2"));

            this.CreateController(controllerId);
        }

        // Create the controller node
        private void CreateController(byte controllerId)
        {
            this._controller = new Controller(this._port, controllerId);
            this._controller.NodeInitializedEvent += ControllerInitialized;
            this._controller.ReadyEvent += NodesInitialized;
            this._controller.Initialize();
        }

        // All nodes are initialized
        private void NodesInitialized(object sender, EventArgs e)
        {
            ((ZWaveNode)sender).NodeInitializedEvent -= NodesInitialized;
            this.FireZWaveReadyEvent();
            DebugLogger.GetLogger.LogMessage(this, "All nodes initialized. We are good to go.");
        }

        // The controller is initialized
        private void ControllerInitialized(object sender, EventArgs e)
        {
            this.FireZWaveInitializedEvent();
        }
	}
}

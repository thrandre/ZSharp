/*
 * ZSharp - C# Z-Wave Implementation
 * HiØ 2011
 * H11D13 / iAutomation@Home
 * Author: thomrand
 */

using System;
using System.IO.Ports;
using System.Threading;
using System.Collections.Generic;
using iAutomationAtHome.Debugging;

namespace iAutomationAtHome.ZSharp
{
	/// <summary>
	/// A class that deals with low level serial communications with a Z-Wave USB Controller.
	/// </summary>
	internal class ZWavePort
	{
        public class UnsubscribedMessageEventArgs : EventArgs
        {
            public ZWaveMessage Message;
            public UnsubscribedMessageEventArgs(ZWaveMessage message) : base()
            {
                this.Message = message;
            }
        }

        public event EventHandler UnsubscribedMessageEvent;
        public void FireUnsubscribedMessageEvent(ZWaveMessage message)
        {
            if (this.UnsubscribedMessageEvent != null)
                this.UnsubscribedMessageEvent(this, new UnsubscribedMessageEventArgs(message));
        }
        
        private SerialPort _sp;
		private Thread _runner;
        private Object _queueLock = new Object();

        private LinkedList<ZWaveJob> _jobQueue;
        private LinkedList<ZWaveJob> JobQueue
		{
			get
			{
				if(this._jobQueue == null) this._jobQueue = new LinkedList<ZWaveJob>();
				return this._jobQueue;
			}
		}

		/// <summary>
		/// Create and initialize a new communication port.
		/// </summary>
		public ZWavePort()
		{
			this._sp = new SerialPort();
			this._sp.Parity = Parity.None;
			this._sp.BaudRate = 115200;
			this._sp.Handshake = Handshake.None;
			this._sp.StopBits = StopBits.One;
			this._sp.DtrEnable = true;
			this._sp.RtsEnable = true;
			this._sp.NewLine = Environment.NewLine;
			
			this._runner = new Thread(new ThreadStart(Run));
		}
		
		/// <summary>
		/// Open the port.
		/// </summary>
		public bool Open()
		{
            Console.WriteLine("Opening port: " + this._sp.PortName);
            if (!this._sp.IsOpen)
            {
                for (int i = 1; i < 5; i++)
                {
                    String port = "COM" + i;
                    this._sp.PortName = port;

                    try
                    {
                        this._sp.Open();
                    }
                    catch (Exception e)
                    {
                        DebugLogger.GetLogger.LogMessage(this, "ZWave controller not found at port: " + this._sp.PortName);
                    }

                    if (this._sp.IsOpen)
                        break;
                }

                if (this._sp.IsOpen)
                {
                    DebugLogger.GetLogger.LogMessage(this, "Found ZWave controller at port: " + this._sp.PortName);
                    this._runner.Start();
                    return true;
                }
                else
                {
                    DebugLogger.GetLogger.LogMessage(this, "ZWave controller not found");
                    return false;
                }
            }
            else
            {
                return true;
            }
		}

        public void Close()
        {
            this._sp.Close();
            this._sp.Dispose();
        }
		
		/// <summary>
		///
		/// </summary>
		private void Run()
		{
            byte[] buf = new byte[1024];
            while(this._sp.IsOpen)
			{
                ZWaveJob _currentJob = null;
                lock (this._queueLock)
                {
                    if (this.JobQueue.Count > 0)
                    {
                        _currentJob = this.JobQueue.First.Value;
                        if (_currentJob.JobDone)
                        {
                            this.JobQueue.RemoveFirst();
                            _currentJob = null;
                            if (this.JobQueue.Count > 0)
                            {
                                _currentJob = this.JobQueue.First.Value;
                            }
                        }
                    }
                }
                
                // Check for incoming messages
                int btr = this._sp.BytesToRead;
                if (btr > 0)
                {
                    // Read first byte
                    this._sp.Read(buf, 0, 1);
                    switch (buf[0])
                    {
                        case ZWaveProtocol.SOF:
                            
                            // Read the length byte
                            this._sp.Read(buf, 1, 1);
                            byte len = buf[1];
                            
                            // Read rest of the frame
                            this._sp.Read(buf, 2, len);
                            byte[] message = Utils.ByteSubstring(buf, 0, (len + 2));
                            Console.WriteLine("Received: " + Utils.ByteArrayToString(message));

                            // Verify checksum
                            if (message[(message.Length - 1)] == CalculateChecksum(Utils.ByteSubstring(message, 0, (message.Length - 1))))
                            {
                                ZWaveMessage zMessage = new ZWaveMessage(message);

                                if (_currentJob == null)
                                {
                                    // Incoming response?
                                    this.FireUnsubscribedMessageEvent(zMessage);
                                    System.Diagnostics.Debug.WriteLine("*** Incoming response");
                                }
                                else
                                {
                                    if (_currentJob.AwaitACK)
                                    {
                                        // We wanted an ACK instead. Resend...
                                        _currentJob.AwaitACK = false;
                                        _currentJob.AwaitResponse = false;
                                        _currentJob.Resend = true;
                                    }
                                    else
                                    {
                                        _currentJob.AddResponse(zMessage);
                                        this.FireUnsubscribedMessageEvent(zMessage);
                                    }
                                }

                                // Send ACK - Checksum is correct
                                this._sp.Write(new byte[] { ZWaveProtocol.ACK }, 0, 1);
                                Console.WriteLine("Sent: ACK");
                            }
                            else
                            {
                                // Send NAK
                                this._sp.Write(new byte[] { ZWaveProtocol.NAK }, 0, 1);
                                Console.WriteLine("Sent: NAK");
                            }

                            break;
                        case ZWaveProtocol.CAN:
                            Console.WriteLine("Received: CAN");
                            break;
                        case ZWaveProtocol.NAK:
                            Console.WriteLine("Received: NAK");
                            _currentJob.AwaitACK = false;
                            _currentJob.JobStarted = false;
                            break;
                        case ZWaveProtocol.ACK:
                            Console.WriteLine("Received: ACK");
                            if (_currentJob != null)
                            {
                                if (_currentJob.AwaitACK && !_currentJob.AwaitResponse)
                                {
                                    _currentJob.AwaitResponse = true;
                                    _currentJob.AwaitACK = false;
                                }
                            }
                            break;
                        default:
                            Console.WriteLine("Critical error. Out of frame flow.");
                            break;
                    }
                }
                else
                {
                    if (_currentJob == null)
                    {
                        lock (this._queueLock)
                        {
                            if (this.JobQueue.Count > 0)
                            {
                                _currentJob = this.JobQueue.First.Value;
                            }
                        }
                    }

                    if (_currentJob != null)
                    {
                        if (_currentJob.SendCount >= 3)
                        {
                            _currentJob.CancelJob();
                        }

                        if ((!_currentJob.JobStarted && !_currentJob.JobDone) || _currentJob.Resend)
                        {
                            ZWaveMessage msg = _currentJob.Request;
                            if (msg != null)
                            {
                                this._sp.Write(msg.Message, 0, msg.Message.Length);
                                _currentJob.Start();
                                _currentJob.Resend = false;
                                _currentJob.AwaitACK = true;
                                _currentJob.SendCount++;
                                Console.WriteLine("Sent: " + Utils.ByteArrayToString(msg.Message));
                            }
                        }
                    }
                }
                Thread.Sleep(100);
			}
		}
		
		/// <summary>
		/// 
		/// </summary>
        public void EnqueueJob(ZWaveJob job)
        {
            lock (this._queueLock)
            {
                this.JobQueue.AddLast(job);
            }
        }

        public void InjectJob(ZWaveJob job)
        {
            lock (this._queueLock)
            {
                this.JobQueue.AddFirst(job);
            }
        }

		public static byte CalculateChecksum(byte[] message)
		{
			byte chksum = 0xff;
			for(int i = 1; i < message.Length; i++)
			{
				chksum ^= (byte)message[i];
			}
			return chksum;
		}
	}
}

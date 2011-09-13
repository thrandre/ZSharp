/*
 * ZSharp - C# Z-Wave Implementation
 * HiØ 2011
 * H11D13 / iAutomation@Home
 * Author: thomrand
 */

using System.Collections.Generic;

namespace iAutomationAtHome.ZSharp
{
	/// <summary>
	/// Description of ZWaveRequest.
	/// </summary>
	internal class ZWaveMessage
	{
        private bool _raw = false;
        
        private byte _messageType;
        public byte MessageType
        {
            get
            {
                return this._messageType;
            }
        }

        private byte _function;
        public byte Function
        {
            get
            {
                return this._function;
            }
        }

        private byte _nodeId;
        public byte NodeId
        {
            get
            {
                return this._nodeId;
            }
        }
        
        private byte _commandClass;
        public byte CommandClass
        {
            get
            {
                return this._commandClass;
            }
        }

        private byte _command;
        public byte Command
        {
            get
            {
                return this._command;
            }
        }

        private byte _transmissionOptions = ZWaveProtocol.TransmissonOption.ACK | ZWaveProtocol.TransmissonOption.AUTO_ROUTE;

        private List<byte> _params = new List<byte>();

        private byte _getMagicByte
        {
            get
            {
                switch(this._commandClass)
                {
                    case ZWaveProtocol.CommandClass.METER:
                        return 0x02;
                    case ZWaveProtocol.CommandClass.METER_PULSE:
                        return 0x02;
                    default:
                        return 0x03;
                }
            }
        }

        private byte[] _message;
		public byte[] Message
		{
			get
			{
                if (!this._raw)
                {
                    int length = 6;
                    if (this._nodeId != 0x00) length++;
                    if (this._commandClass != 0x00) length += 3;
                    length += this._params.Count;

                    byte[] message = new byte[length];
                    int index = 0;
                    
                    message[index++] = ZWaveProtocol.SOF;
                    
                    // Insert message length
                    message[index++] = (byte)(length - 2);
                    
                    message[index++] = this._messageType;
                    message[index++] = this._function;
                    
                    if (this._nodeId != 0x00)
                    {
                        message[index++] = this._nodeId;
                    }

                    if (this._commandClass != 0x00)
                    {
                        message[index++] = this._getMagicByte;
                        message[index++] = this._commandClass;
                        message[index++] = this._command;
                    }

                    for (int i = 0; i < this._params.Count; i++)
                    {
                        message[index++] = this._params[i];
                    }

                    message[index++] = this._transmissionOptions;               

                    // Calculate and insert the checksum
                    message[index++] = ZWavePort.CalculateChecksum(message);

                    return message;
                }
                else
                {
                    return this._message;
                }
			}
		}

        public void AddParameter(byte param)
        {
            this._params.Add(param);
        }

        public void Parse(byte[] message)
        {
            this._messageType = message[2];
            this._function = message[3];
            
            switch (this._function)
            {
                case ZWaveProtocol.Function.SEND_DATA:
                    this._commandClass = message[4];
                    break;
                case ZWaveProtocol.Function.APPLICATION_COMMAND_HANDLER:
                    this._commandClass = message[7];
                    this._command = message[8];
                    break;
                case ZWaveProtocol.Function.ADD_NODE_TO_NETWORK:
                    this._commandClass = message[5];
                    break;
                case ZWaveProtocol.Function.REMOVE_NODE_FROM_NETWORK:
                    this._commandClass = message[5];
                    break;
                default:
                    this._commandClass = 0x00;
                    break;
            }
        }

        
        /// <summary>
        /// 
        /// </summary>
        public ZWaveMessage() { }
        
        public ZWaveMessage(byte messageType, byte function, byte nodeId = 0x00, byte commandClass = 0x00, byte command = 0x00)
        {
            this._messageType = messageType;
            this._function = function;
            if (nodeId != 0x00)
            {
                this._nodeId = nodeId;
            }
            
            if(commandClass != 0x00)
            {
                this._commandClass = commandClass;
            }
            
            if(command != 0x00)
            {
                this._command = command;
            }
        }

        public ZWaveMessage(byte[] message)
        {
            this._message = message;
            this._raw = true;
            this.Parse(message);
        }
	}
}

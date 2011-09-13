using System;

namespace iAutomationAtHome.ZSharp.Nodes.DataReader
{
    /// <summary>
    /// Defines a MeterReader
    /// </summary>
    public class MeterReader : ZWaveDataReader
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="node"></param>
        public MeterReader(ZWaveNode node) : base(node) { }

        internal override void IntervalElapsed(object sender, EventArgs e)
        {
            this.GetMeterReport();
        }

        private void GetMeterReport()
        {
            ZWaveJob mr = new ZWaveJob();
            mr.Request = new ZWaveMessage(ZWaveProtocol.MessageType.REQUEST,
                                          ZWaveProtocol.Function.SEND_DATA,
                                          this._node._nodeId,
                                          ZWaveProtocol.CommandClass.METER,
                                          ZWaveProtocol.Command.METER_REPORT_GET);
            mr.ResponseReceived += MeterReportReceived;
            this._node.EnqueueJob(mr);
        }

        private void MeterReportReceived(object sender, EventArgs e)
        {
            ZWaveJob job = (ZWaveJob)sender;
            ZWaveMessage request = job.Request;
            ZWaveMessage response = job.GetResponse();

            bool done = false;

            switch (response.Function)
            {
                case ZWaveProtocol.Function.APPLICATION_COMMAND_HANDLER:
                    done = true;
                    break;
            }

            if (done)
            {
                job.Done();
                job.ResponseReceived -= MeterReportReceived;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using iAutomationAtHome.Debugging;

namespace iAutomationAtHome.ZSharp.Nodes
{
    /// <summary>
    /// Defines the basic methods of a switch
    /// </summary>
    public interface Switch
    {
        /// <summary>
        /// Fired when node changes state
        /// </summary>
        event EventHandler NodeChangedStateEvent;
        
        /// <summary>
        /// Get state
        /// </summary>
        /// <returns></returns>
        byte State { get; }
        
        /// <summary>
        /// Turn on
        /// </summary>
        void On();
        
        /// <summary>
        /// Turn off
        /// </summary>
        void Off();
    } 
}

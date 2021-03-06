﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NodeStartupWaitMessage.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Message to new node, indicating that cluster is full and request of configuration should be repeated
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.Launcher.Messages
{
    using System;

    /// <summary>
    /// Message to new node, indicating that cluster is full and request of configuration should be repeated
    /// </summary>
    public class NodeStartupWaitMessage
    {
        /// <summary>
        /// Gets or sets time interval value to wait for repeated request
        /// </summary>
        public TimeSpan WaitTime { get; set; }
    }
}
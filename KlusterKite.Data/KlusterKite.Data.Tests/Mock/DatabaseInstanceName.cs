﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DatabaseInstanceName.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The database instance name
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Data.Tests.Mock
{
    using System;

    /// <summary>
    /// The database instance name
    /// </summary>
    public class DatabaseInstanceName
    {
        /// <summary>
        /// The instance uid
        /// </summary>
        private Guid uid = Guid.NewGuid();

        /// <summary>
        /// Gets the instance name
        /// </summary>
        public string Name => this.uid.ToString("N");
    }
}

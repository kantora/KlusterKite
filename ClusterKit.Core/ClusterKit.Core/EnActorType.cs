﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EnActorType.cs" company="ClusterKit">
//   All rights reserved
// </copyright>
// <summary>
//   Types of actors to generate from <seealso cref="NameSpaceActor" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ClusterKit.Core
{
    /// <summary>
    /// Types of actors to generate from <seealso cref="NameSpaceActor"/>
    /// </summary>
    public enum EnActorType
    {
        /// <summary>
        /// Just simple actor
        /// </summary>
        Simple,

        /// <summary>
        /// Cluster singleton actor
        /// </summary>
        Singleton,

        /// <summary>
        /// Cluster singleton proxy actor
        /// </summary>
        SingletonProxy
    }
}
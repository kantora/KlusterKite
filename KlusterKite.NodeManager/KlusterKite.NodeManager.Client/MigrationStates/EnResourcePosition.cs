﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EnResourcePosition.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The possible resource migration position according to current migration
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.Client.MigrationStates
{
    using KlusterKite.API.Attributes;
    
    /// <summary>
    /// The possible resource migration position according to current migration
    /// </summary>
    [ApiDescription("The possible resource migration position according to current migration", Name = "EnResourcePosition")]
    public enum EnResourcePosition
    {
        /// <summary>
        /// The resource is not created yet
        /// </summary>
        [ApiDescription("The resource is not created yet")]
        NotCreated,

        /// <summary>
        /// The resource is in the source point migration
        /// </summary>
        [ApiDescription("The resource is in the source point migration")]
        Source,

        /// <summary>
        /// The migration position of the resource is neither source nor destination, but can be updated to source or destination
        /// </summary>
        [ApiDescription("The migration position of the resource is neither source nor destination")]
        InScope,

        /// <summary>
        /// The migration position of the resource is neither source nor destination and cannot be updated to source or destination
        /// </summary>
        [ApiDescription("The migration position of the resource is neither source nor destination")]
        OutOfScope,

        /// <summary>
        /// The resource is in the destination point migration
        /// </summary>
        [ApiDescription("The resource is in the destination point migration")]
        Destination,

        /// <summary>
        /// The resource is not modified during current migration
        /// </summary>
        [ApiDescription("The resource is not modified during current migration")]
        SourceAndDestination,

        /// <summary>
        /// The resource is no more supported in the target configuration
        /// </summary>
        [ApiDescription("The resource is no more supported in the target configuration")]
        Obsolete
    }
}
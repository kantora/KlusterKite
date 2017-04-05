﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NugetFeed.cs" company="ClusterKit">
//   All rights reserved
// </copyright>
// <summary>
//   Link to the nuget feed to download updates
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ClusterKit.NodeManager.Client.ORM
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    using ClusterKit.API.Attributes;
    using ClusterKit.Data.CRUD;

    /// <summary>
    /// Link to the nuget feed to download updates
    /// </summary>
    [ApiDescription("The link to the nuget feed to download updates", Name = "NugetFeed")]
    public class NugetFeed : IObjectWithId<int>
    {
        /// <summary>
        /// Type of NuGet feed
        /// </summary>
        [ApiDescription("The type of nuget feed", Name = "EnFeedType")]
        public enum EnFeedType
        {
            /// <summary>
            /// Public feed used to download third party packages
            /// </summary>
            Public = 0,

            /// <summary>
            /// Private feed is checked against updates and can be source of cluster update events
            /// </summary>
            Private = 1
        }

        /// <summary>
        /// Gets or sets seed url address
        /// </summary>
        [DeclareField("The seed url address")]
        public string Address { get; set; }

        /// <summary>
        /// Gets or sets unique address identification number
        /// </summary>
        [DeclareField("The unique address identification number", IsKey = true)]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity), Key]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets user password for basic authentication
        /// </summary>
        [DeclareField("The user password for basic authentication")]
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets type of feed
        /// </summary>
        [DeclareField("The  type of feed")]
        public EnFeedType Type { get; set; }

        /// <summary>
        /// Gets or sets username for basic authentication
        /// </summary>
        [DeclareField("The username for basic authentication")]
        public string UserName { get; set; }

        /// <inheritdoc />
        int IObjectWithId<int>.GetId()
        {
            return this.Id;
        }
    }
}
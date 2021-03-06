﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MigratorTemplate.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   A cluster migrator template definition
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.Client.ORM
{
    using System.Collections.Generic;

    using JetBrains.Annotations;

    using KlusterKite.API.Attributes;
    using KlusterKite.API.Client.Converters;
    using KlusterKite.NodeManager.Launcher.Messages;

    /// <summary>
    /// A cluster migrator template definition
    /// </summary>
    /// <remarks>
    /// Used to define code-base to handle non-code cluster upgrade such as database updates and so on.
    /// </remarks>
    [UsedImplicitly]
    [ApiDescription("A cluster migrator template definition", Name = "MigratorTemplate")]
    public class MigratorTemplate : ITemplate
    {
        /// <summary>
        /// Gets or sets the program readable migrator template name
        /// </summary>
        [UsedImplicitly]
        [DeclareField("The program readable migrator template name", IsKey = true)]
        public string Code { get; set; }

        /// <summary>
        /// Gets or sets akka configuration template for migrator. This should contain all needed connection strings and so on
        /// </summary>
        [UsedImplicitly]
        [DeclareField("The akka configuration for migrator")]
        public string Configuration { get; set; }

        /// <summary>
        /// Gets or sets the human readable node migrator name
        /// </summary>
        [UsedImplicitly]
        [DeclareField("The human readable migrator template name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the migrator description for other users
        /// </summary>
        [UsedImplicitly]
        [DeclareField("The migrator description for other users")]
        public string Notes { get; set; }

        /// <summary>
        /// Gets or sets the priority weight for migrator, when deciding witch migrator should be called first.
        /// </summary>
        /// <remarks>
        /// During cluster upgrade process migrators a called from most prioritised to less. During downgrade in backwards order.
        /// </remarks>
        [UsedImplicitly]
        [DeclareField("The priority weight for migrator, when deciding witch migrator should be called first. During cluster upgrade process migrators a called from most prioritised to less. During downgrade in backwards order.")]
        public double Priority { get; set; }

        /// <summary>
        /// Gets or sets the list of package requirements
        /// </summary>
        [UsedImplicitly]
        [DeclareField("The list of package requirements")]
        public List<NodeTemplate.PackageRequirement> PackageRequirements { get; set; } = new List<NodeTemplate.PackageRequirement>();  

        /// <summary>
        /// Gets or sets the list of packages to install for current template
        /// </summary>
        [UsedImplicitly]
        [DeclareField("The list of packages to install for current template", Converter = typeof(DictionaryConverter<string, List<PackageDescription>>))]
        public Dictionary<string, List<PackageDescription>> PackagesToInstall { get; set; }
    }
}

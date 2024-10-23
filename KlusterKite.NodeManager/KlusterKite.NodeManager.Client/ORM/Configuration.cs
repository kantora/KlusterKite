// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Configuration.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The cluster configuration with all program modules versions, node templates and configurations
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.Client.ORM
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    using JetBrains.Annotations;

    using KlusterKite.API.Attributes;
    using KlusterKite.Data.CRUD;

    using Newtonsoft.Json;

    /// <summary>
    /// The cluster configuration with all program modules versions, node templates and configurations
    /// </summary>
    [ApiDescription("The cluster configuration with all program modules versions, node templates and configurations", Name = "Configuration")]
    public class Configuration : IObjectWithId<int>
    {
        /// <summary>
        /// Gets or sets the configuration id
        /// </summary>
        [DeclareField("The configuration id", IsKey = true)]
        [Key]
        [UsedImplicitly]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column(TypeName = "serial")] // TODO: check and remove that Npgsql.EntityFrameworkCore.PostgreSQL can generate serial columns on migration without this kludge
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the configuration major version number
        /// </summary>
        [UsedImplicitly]
        [DeclareField("the configuration major version number")]
        public int MajorVersion { get; set; }

        /// <summary>
        /// Gets or sets the configuration minor version number
        /// </summary>
        [UsedImplicitly]
        [DeclareField("the configuration minor version number")]
        public int MinorVersion { get; set; }

        /// <summary>
        /// Gets or sets the configuration name
        /// </summary>
        [UsedImplicitly]
        [DeclareField("the configuration name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the configuration name
        /// </summary>
        [UsedImplicitly]
        [DeclareField("the configuration notes")]
        public string Notes { get; set; }

        /// <summary>
        /// Gets or sets the configuration creation date
        /// </summary>
        [UsedImplicitly]
        [DeclareField("configuration creation date", Access = EnAccessFlag.Queryable)]
        public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;

        /// <summary>
        /// Gets or sets the configuration start date (the date when cluster was switched on this configuration first time)
        /// </summary>
        [UsedImplicitly]
        [DeclareField("configuration start date (the date when cluster was switched on this configuration first time)", Access = EnAccessFlag.Queryable)]
        public DateTimeOffset? Started { get; set; }

        /// <summary>
        /// Gets or sets the configuration  finish date (the date when cluster was switched from this configuration last time)
        /// </summary>
        [UsedImplicitly]
        [DeclareField("configuration finish date (the date when cluster was switched from this configuration last time)", Access = EnAccessFlag.Queryable)]
        public DateTimeOffset? Finished { get; set; }

        /// <summary>
        /// Gets or sets the configuration state
        /// </summary>
        [UsedImplicitly]
        [DeclareField("The configuration state", Access = EnAccessFlag.Queryable)]
        public EnConfigurationState State { get; set; } = EnConfigurationState.Draft;

        /// <summary>
        /// Gets or sets a value indicating whether this configuration was considered stable
        /// </summary>
        [UsedImplicitly]
        [DeclareField("A value indicating whether this configuration was considered stable", Access = EnAccessFlag.Queryable)]
        public bool IsStable { get; set; }

        /// <summary>
        /// Gets or sets the configuration settings
        /// </summary>
        [DeclareField("the configuration settings")]
        [NotMapped]
        public ConfigurationSettings Settings { get; set; }

        /// <summary>
        /// Gets or sets the list of backward-compatible node templates
        /// </summary>
        [UsedImplicitly]
        [DeclareField("the list of compatible node templates", Access = EnAccessFlag.Queryable)]
        [InverseProperty(nameof(CompatibleTemplate.Configuration))]
        public List<CompatibleTemplate> CompatibleTemplatesBackward { get; set; }

        /// <summary>
        /// Gets or sets the list of node templates compatible with future configurations
        /// </summary>
        [UsedImplicitly]
        [DeclareField("the list of compatible node templates", Access = EnAccessFlag.Queryable)]
        [InverseProperty(nameof(CompatibleTemplate.CompatibleConfiguration))]
        public List<CompatibleTemplate> CompatibleTemplatesForward { get; set; }

        /// <summary>
        /// Gets or sets the list of migration operations
        /// </summary>
        [UsedImplicitly]
        [DeclareField("the list of migration logs")]
        [InverseProperty(nameof(MigrationLogRecord.Configuration))]
        public List<MigrationLogRecord> MigrationLogs { get; set; }

        /// <summary>
        ///  Gets or sets the configuration settings as json to store in database
        /// </summary>
        [UsedImplicitly]
        public string SettingsJson
        {
            get => this.Settings != null ? JsonConvert.SerializeObject(this.Settings, Formatting.None) : null;

            set => this.Settings = value == null
                                            ? null
                                            : JsonConvert.DeserializeObject<ConfigurationSettings>(value);
        }

        /// <inheritdoc />
        public int GetId()
        {
            return this.Id;
        }
    }
}

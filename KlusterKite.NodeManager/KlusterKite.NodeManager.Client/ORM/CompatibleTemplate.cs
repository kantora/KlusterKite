﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CompatibleTemplate.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Defines the CompatibleTemplate type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.Client.ORM
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    using JetBrains.Annotations;

    using KlusterKite.API.Attributes;

    /// <summary>
    /// The compatible node template
    /// </summary>
    [ApiDescription("The compatible node template", Name = "CompatibleTemplate")]
    public class CompatibleTemplate
    {
        /// <summary>
        /// Gets or sets the relation id
        /// </summary>
        [Key]
        [UsedImplicitly]
        [DeclareField("the relation id", IsKey = true)]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column(TypeName = "serial")] // TODO: check and remove that Npgsql.EntityFrameworkCore.PostgreSQL can generate serial columns on migration without this kludge
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the compatible configuration id
        /// </summary>
        [UsedImplicitly]
        [DeclareField("the compatible configuration id")]
        public int CompatibleConfigurationId { get; set; }

        /// <summary>
        /// Gets or sets the parent configuration id
        /// </summary>
        [UsedImplicitly]
        [DeclareField("the parent configuration id")]
        public int ConfigurationId { get; set; }

        /// <summary>
        /// Gets or sets the template code
        /// </summary>
        [UsedImplicitly]
        [DeclareField("the template code")]
        public string TemplateCode { get; set; }

        /// <summary>
        /// Gets or sets the compatible configuration
        /// </summary>
        [UsedImplicitly]
        [DeclareField("the compatible configuration")]
        [ForeignKey(nameof(CompatibleConfigurationId))]
        public Configuration CompatibleConfiguration { get; set; }

        /// <summary>
        /// Gets or sets the parent configuration
        /// </summary>
        [UsedImplicitly]
        [DeclareField("the parent configuration")]
        [ForeignKey(nameof(ConfigurationId))]
        public Configuration Configuration { get; set; }
    }
}

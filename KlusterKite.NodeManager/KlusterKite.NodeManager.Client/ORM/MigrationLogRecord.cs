// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MigrationLogRecord.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The base class for migration log records
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.Client.ORM
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    using JetBrains.Annotations;

    using KlusterKite.API.Attributes;

    /// <summary>
    /// The base class for migration log records
    /// </summary>
    [Table("MigrationLogRecords")]
    [ApiDescription(Name = "MigrationLogRecord")]
    public class MigrationLogRecord
    {
        /// <summary>
        /// Gets or sets the log record id
        /// </summary>
        [Key]
        [DeclareField("the log record id", IsKey = true)]
        [UsedImplicitly]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column(TypeName = "serial")]  // TODO: check and remove that Npgsql.EntityFrameworkCore.PostgreSQL can generate serial columns on migration without this kludge
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the log record type
        /// </summary>
        [DeclareField("the log record type")]
        [UsedImplicitly]
        public EnMigrationLogRecordType Type { get; set; }

        /// <summary>
        /// Gets or sets the migration id
        /// </summary>
        [DeclareField("the migration id")]
        [UsedImplicitly]
        public int? MigrationId { get; set; }

        /// <summary>
        /// Gets or sets the migration
        /// </summary>
        [UsedImplicitly]
        [ForeignKey(nameof(MigrationId))]
        public Migration Migration { get; set; }

        /// <summary>
        /// Gets or sets the configuration id
        /// </summary>        
        [DeclareField("the configuration id")]
        [UsedImplicitly]
        public int ConfigurationId { get; set; }

        /// <summary>
        /// Gets or sets the configuration
        /// </summary>
        [DeclareField("the configuration")]
        [UsedImplicitly]
        [ForeignKey(nameof(ConfigurationId))]
        public Configuration Configuration { get; set; }

        /// <summary>
        /// Gets or sets the migrator template code
        /// </summary>
        [DeclareField("the migrator template code")]
        [UsedImplicitly]
        public string MigratorTemplateCode { get; set; }

        /// <summary>
        /// Gets or sets the migrator template name
        /// </summary>
        [DeclareField("the migrator template name")]
        [UsedImplicitly]
        public string MigratorTemplateName { get; set; }

        /// <summary>
        /// Gets or sets the migrator type name
        /// </summary>
        [DeclareField("the migrator type name")]
        [UsedImplicitly]
        public string MigratorTypeName { get; set; }

        /// <summary>
        /// Gets or sets the migrator name
        /// </summary>
        [DeclareField("the migrator name")]
        [UsedImplicitly]
        public string MigratorName { get; set; }

        /// <summary>
        /// Gets or sets the resource code
        /// </summary>
        [DeclareField("the resource code")]
        [UsedImplicitly]
        public string ResourceCode { get; set; }

        /// <summary>
        /// Gets or sets the resource name
        /// </summary>
        [DeclareField("the resource name")]
        [UsedImplicitly]
        public string ResourceName { get; set; }

        /// <summary>
        /// Gets or sets the operation execution start time
        /// </summary>
        [UsedImplicitly]
        [DeclareField("the operation execution start time")]
        public DateTimeOffset Started { get; set; }

        /// <summary>
        /// Gets or sets the operation execution finish time
        /// </summary>
        [UsedImplicitly]
        [DeclareField("the operation execution finish time")]
        public DateTimeOffset? Finished { get; set; }

        /// <summary>
        /// Gets or sets the source migration point.
        /// </summary>
        [UsedImplicitly]
        [DeclareField("the source migration point")]
        public string SourcePoint { get; set; }

        /// <summary>
        /// Gets or sets the source migration point.
        /// </summary>
        [UsedImplicitly]
        [DeclareField("the destination migration point")]
        public string DestinationPoint { get; set; }

        /// <summary>
        /// Gets or sets the message
        /// </summary>
        [DeclareField("the message")]
        [UsedImplicitly]
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the error stack trace
        /// </summary>
        [DeclareField("the error stack trace")]
        [UsedImplicitly]
        public string ErrorStackTrace { get; set; }

        /// <summary>
        /// Sets the description from exception
        /// </summary>
        public Exception Exception
        {
            set
            {
                this.ErrorStackTrace = string.Empty;
                this.AddExceptionToStackTrace(value);
            }
        }

        /// <summary>
        /// Adds exception description to <see cref="ErrorStackTrace"/>
        /// </summary>
        /// <param name="value">The exception</param>
        private void AddExceptionToStackTrace(Exception value)
        {
#if APPDOMAIN
            this.ErrorStackTrace += $"{value.GetType().Name}: {value.Message}\n{value.StackTrace}\n{(value as System.IO.FileNotFoundException)?.FusionLog}";
#endif
#if CORECLR
            this.ErrorStackTrace += $"{value.GetType().Name}: {value.Message}\n{value.StackTrace}";
#endif

            if (value.InnerException != null)
            {
                this.ErrorStackTrace += "\n---------------------\n";
                this.AddExceptionToStackTrace(value.InnerException);
            }
        }
    }
}

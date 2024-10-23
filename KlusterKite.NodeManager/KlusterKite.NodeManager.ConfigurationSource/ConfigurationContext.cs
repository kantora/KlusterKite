// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConfigurationContext.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Configuration database context
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.ConfigurationSource
{
    using JetBrains.Annotations;

    using KlusterKite.NodeManager.Client.ORM;

    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Configuration database context
    /// </summary>
    public class ConfigurationContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationContext"/> class.
        /// </summary>
        /// <param name="options">
        /// The context options.
        /// </param>
        public ConfigurationContext(DbContextOptions<ConfigurationContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationContext"/> class.
        /// </summary>
        public ConfigurationContext()
        {
        }

        /// <summary>
        /// Gets or sets the list of migrations
        /// </summary>
        [UsedImplicitly]
        public DbSet<Migration> Migrations { get; set; }

        /// <summary>
        /// Gets or sets the list of web API users
        /// </summary>
        [UsedImplicitly]
        public DbSet<User> Users { get; set; }

        /// <summary>
        /// Gets or sets the list of web API user roles
        /// </summary>
        [UsedImplicitly]
        public DbSet<Role> Roles { get; set; }

        /// <summary>
        /// Gets or sets the list of role to user links
        /// </summary>
        [UsedImplicitly]
        public DbSet<RoleUser> RoleUsers { get; set; }

        /// <summary>
        /// Gets or sets the list of configurations
        /// </summary>
        [UsedImplicitly]
        public DbSet<Configuration> Configurations { get; set; }

        /// <summary>
        /// Gets or sets the global resource migration log
        /// </summary>
        [UsedImplicitly]
        public DbSet<MigrationLogRecord> MigrationLogs { get; set; }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Configuration>().Property(r => r.Id).ValueGeneratedOnAdd();

            modelBuilder.Entity<User>().HasIndex(u => u.Login);

            /*
            modelBuilder.Entity<MigrationLogRecord>().HasOne(r => r.Migration).WithMany(m => m.Logs)
                .HasForeignKey(r => r.MigrationId);

            modelBuilder.Entity<MigrationLogRecord>().HasOne(r => r.Configuration).WithMany(m => m.MigrationLogs)
                .HasForeignKey(r => r.ConfigurationId);

            
            modelBuilder.Entity<CompatibleTemplate>().HasOne(t => t.CompatibleConfiguration)
                .WithMany(r => r.CompatibleTemplatesForward).HasForeignKey(t => t.CompatibleConfigurationId);

            modelBuilder.Entity<CompatibleTemplate>().HasOne(t => t.Configuration)
                .WithMany(r => r.CompatibleTemplatesBackward).HasForeignKey(t => t.ConfigurationId);
            */

            modelBuilder.Entity<RoleUser>().HasKey(t => new { t.UserUid, t.RoleUid });
        }
    }
}
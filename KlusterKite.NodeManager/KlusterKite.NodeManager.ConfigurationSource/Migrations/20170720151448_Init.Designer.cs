﻿//<aut-generated/>
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using KlusterKite.NodeManager.ConfigurationSource;
using KlusterKite.NodeManager.Client.ORM;

namespace KlusterKite.NodeManager.ConfigurationSource.Migrations
{
    [DbContext(typeof(ConfigurationContext))]
    [Migration("20170720151448_Init")]
    partial class Init
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.1.2");

            modelBuilder.Entity("KlusterKite.NodeManager.Client.ORM.CompatibleTemplate", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("serial");

                    b.Property<int>("CompatibleConfigurationId");

                    b.Property<int>("ConfigurationId");

                    b.Property<string>("TemplateCode");

                    b.HasKey("Id");

                    b.HasIndex("CompatibleConfigurationId");

                    b.HasIndex("ConfigurationId");

                    b.ToTable("CompatibleTemplate");
                });

            modelBuilder.Entity("KlusterKite.NodeManager.Client.ORM.Configuration", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("serial");

                    b.Property<DateTimeOffset>("Created");

                    b.Property<DateTimeOffset?>("Finished");

                    b.Property<bool>("IsStable");

                    b.Property<int>("MajorVersion");

                    b.Property<int>("MinorVersion");

                    b.Property<string>("Name");

                    b.Property<string>("Notes");

                    b.Property<string>("SettingsJson");

                    b.Property<DateTimeOffset?>("Started");

                    b.Property<int>("State");

                    b.HasKey("Id");

                    b.ToTable("Configurations");
                });

            modelBuilder.Entity("KlusterKite.NodeManager.Client.ORM.Migration", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("serial");

                    b.Property<DateTimeOffset?>("Finished");

                    b.Property<int>("FromConfigurationId");

                    b.Property<bool>("IsActive");

                    b.Property<DateTimeOffset>("Started");

                    b.Property<int>("State");

                    b.Property<int>("ToConfigurationId");

                    b.HasKey("Id");

                    b.HasIndex("FromConfigurationId");

                    b.HasIndex("ToConfigurationId");

                    b.ToTable("Migrations");
                });

            modelBuilder.Entity("KlusterKite.NodeManager.Client.ORM.MigrationLogRecord", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("serial");

                    b.Property<int>("ConfigurationId");

                    b.Property<string>("DestinationPoint");

                    b.Property<string>("ErrorStackTrace");

                    b.Property<DateTimeOffset?>("Finished");

                    b.Property<string>("Message");

                    b.Property<int?>("MigrationId");

                    b.Property<string>("MigratorName");

                    b.Property<string>("MigratorTemplateCode");

                    b.Property<string>("MigratorTemplateName");

                    b.Property<string>("MigratorTypeName");

                    b.Property<string>("ResourceCode");

                    b.Property<string>("ResourceName");

                    b.Property<string>("SourcePoint");

                    b.Property<DateTimeOffset>("Started");

                    b.Property<int>("Type");

                    b.HasKey("Id");

                    b.HasIndex("ConfigurationId");

                    b.HasIndex("MigrationId");

                    b.ToTable("MigrationLogRecords");
                });

            modelBuilder.Entity("KlusterKite.NodeManager.Client.ORM.Role", b =>
                {
                    b.Property<Guid>("Uid")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("AllowedScopeJson");

                    b.Property<string>("DeniedScopeJson");

                    b.Property<string>("Name");

                    b.HasKey("Uid");

                    b.ToTable("Roles");
                });

            modelBuilder.Entity("KlusterKite.NodeManager.Client.ORM.RoleUser", b =>
                {
                    b.Property<Guid>("UserUid");

                    b.Property<Guid>("RoleUid");

                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("serial");

                    b.HasKey("UserUid", "RoleUid");

                    b.HasAlternateKey("Id");

                    b.HasIndex("RoleUid");

                    b.ToTable("RoleUsers");
                });

            modelBuilder.Entity("KlusterKite.NodeManager.Client.ORM.User", b =>
                {
                    b.Property<Guid>("Uid")
                        .ValueGeneratedOnAdd();

                    b.Property<DateTimeOffset?>("ActiveTill");

                    b.Property<DateTimeOffset?>("BlockedTill");

                    b.Property<bool>("IsBlocked");

                    b.Property<bool>("IsDeleted");

                    b.Property<string>("Login");

                    b.Property<string>("Password");

                    b.HasKey("Uid");

                    b.HasIndex("Login");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("KlusterKite.NodeManager.Client.ORM.CompatibleTemplate", b =>
                {
                    b.HasOne("KlusterKite.NodeManager.Client.ORM.Configuration", "CompatibleConfiguration")
                        .WithMany("CompatibleTemplatesForward")
                        .HasForeignKey("CompatibleConfigurationId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("KlusterKite.NodeManager.Client.ORM.Configuration", "Configuration")
                        .WithMany("CompatibleTemplatesBackward")
                        .HasForeignKey("ConfigurationId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("KlusterKite.NodeManager.Client.ORM.Migration", b =>
                {
                    b.HasOne("KlusterKite.NodeManager.Client.ORM.Configuration", "FromConfiguration")
                        .WithMany()
                        .HasForeignKey("FromConfigurationId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("KlusterKite.NodeManager.Client.ORM.Configuration", "ToConfiguration")
                        .WithMany()
                        .HasForeignKey("ToConfigurationId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("KlusterKite.NodeManager.Client.ORM.MigrationLogRecord", b =>
                {
                    b.HasOne("KlusterKite.NodeManager.Client.ORM.Configuration", "Configuration")
                        .WithMany("MigrationLogs")
                        .HasForeignKey("ConfigurationId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("KlusterKite.NodeManager.Client.ORM.Migration", "Migration")
                        .WithMany("Logs")
                        .HasForeignKey("MigrationId");
                });

            modelBuilder.Entity("KlusterKite.NodeManager.Client.ORM.RoleUser", b =>
                {
                    b.HasOne("KlusterKite.NodeManager.Client.ORM.Role", "Role")
                        .WithMany("Users")
                        .HasForeignKey("RoleUid")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("KlusterKite.NodeManager.Client.ORM.User", "User")
                        .WithMany("Roles")
                        .HasForeignKey("UserUid")
                        .OnDelete(DeleteBehavior.Cascade);
                });
        }
    }
}

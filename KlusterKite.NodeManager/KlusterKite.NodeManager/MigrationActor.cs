// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MigrationActor.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The actor responsible for non-code cluster migrations and updates
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;


    using Akka.Actor;
    using Akka.Event;

    using JetBrains.Annotations;

    using KlusterKite.Core.Utils;
    using KlusterKite.Data.EF;
    using KlusterKite.NodeManager.Client.Messages.Migration;
    using KlusterKite.NodeManager.Client.MigrationStates;
    using KlusterKite.NodeManager.Client.ORM;
    using KlusterKite.NodeManager.ConfigurationSource;
    using KlusterKite.NodeManager.Launcher.Messages;
    using KlusterKite.NodeManager.Launcher.Utils;
    using KlusterKite.NodeManager.RemoteDomain;

    using Microsoft.EntityFrameworkCore;

    using NuGet.Packaging.Core;
    using NuGet.Versioning;

    /// <summary>
    /// The actor responsible for non-code cluster migrations and updates
    /// </summary>
    public class MigrationActor : FSM<MigrationActor.EnState, MigrationActor.Data>
    {
        /// <summary>
        /// The configuration database connection string
        /// </summary>
        private readonly string connectionString;

        /// <summary>
        /// The context factory
        /// </summary>
        private readonly UniversalContextFactory contextFactory;

        /// <summary>
        /// The configuration database name
        /// </summary>
        private readonly string databaseName;

        /// <summary>
        /// The configuration database provider name
        /// </summary>
        private readonly string databaseProviderName;

        /// <summary>
        /// The nuget repository
        /// </summary>
        private readonly IPackageRepository nugetRepository;

        /// <summary>
        /// The current environment runtime name
        /// </summary>
        private readonly string runtime;

        /// <summary>
        /// Initializes a new instance of the <see cref="MigrationActor"/> class.
        /// </summary>
        /// <param name="contextFactory">
        /// The context Factory.
        /// </param>
        /// <param name="nugetRepository">
        /// The nuget Repository.
        /// </param>
        [UsedImplicitly]
        public MigrationActor(UniversalContextFactory contextFactory, IPackageRepository nugetRepository)
        {
            this.Parent = Context.Parent;
            this.contextFactory = contextFactory;
            this.nugetRepository = nugetRepository;
            this.connectionString =
                Context.System.Settings.Config.GetString(NodeManagerActor.ConfigConnectionStringPath);
            this.databaseName = Context.System.Settings.Config.GetString(NodeManagerActor.ConfigDatabaseNamePath);
            this.databaseProviderName =
                Context.System.Settings.Config.GetString(NodeManagerActor.ConfigDatabaseProviderNamePath);
            this.runtime = Context.System.Settings.Config.GetString("KlusterKite.NodeManager.Runtime");
        }

        /// <summary>
        /// The state of migration
        /// </summary>
        public enum EnState
        {
            /// <summary>
            /// There is no migration in process
            /// </summary>
            Idle,

            /// <summary>
            /// There is migration in process
            /// </summary>
            Migration,

            /// <summary>
            /// The state checked failed
            /// </summary>
            InitializationFailed
        }

        /// <summary>
        /// Gets the parent actor to notify of action complete
        /// </summary>
        protected virtual IActorRef Parent { get; }

        /// <summary>
        /// Creates migration state from configuration states
        /// </summary>
        /// <param name="sourceMigratorTemplateStates">The resource state according to source configuration</param>
        /// <param name="destinationMigratorTemplateStates">The resource state according to destination configuration</param>
        /// <returns>The overall resource migration state</returns>
        public static IEnumerable<MigratorTemplateMigrationState> CreateMigrationState(
            IReadOnlyCollection<MigratorTemplateConfigurationState> sourceMigratorTemplateStates,
            IReadOnlyCollection<MigratorTemplateConfigurationState> destinationMigratorTemplateStates)
        {
            foreach (var destinationMigratorTemplateState in destinationMigratorTemplateStates)
            {
                var sourceMigratorTemplateState =
                    sourceMigratorTemplateStates.FirstOrDefault(
                        t => t.Template.Code == destinationMigratorTemplateState.Template.Code);

                if (sourceMigratorTemplateState == null)
                {
                    yield return MigratorTemplateMigrationState.CreateFrom(
                        destinationMigratorTemplateState,
                        EnMigratorPosition.New);
                    continue;
                }

                var migratorStates = CreateMigrationState(
                    destinationMigratorTemplateState.Code,
                    sourceMigratorTemplateState.MigratorsStates,
                    destinationMigratorTemplateState.MigratorsStates);

                yield return new MigratorTemplateMigrationState
                                 {
                                     Code = destinationMigratorTemplateState.Template
                                         .Code,
                                     DestinationTemplate =
                                         destinationMigratorTemplateState.Template,
                                     SourceTemplate =
                                         sourceMigratorTemplateState.Template,
                                     Position = EnMigratorPosition.Present,
                                     Migrators = migratorStates.ToList()
                                 };
            }

            foreach (var sourceMigratorTemplateState in sourceMigratorTemplateStates.Where(
                s => destinationMigratorTemplateStates.All(d => d.Template.Code != s.Template.Code)))
            {
                yield return MigratorTemplateMigrationState.CreateFrom(
                    sourceMigratorTemplateState,
                    EnMigratorPosition.Obsolete);
            }
        }

        /// <inheritdoc />
        protected override void PreStart()
        {
            this.When(
                EnState.Migration,
                command => command.FsmEvent.Match<State<EnState, Data>>()
                    .With<List<ResourceUpgrade>>(this.StateMigrationHandleResourceUpgrade).ResultOrDefault(o => null));

            this.When(
                EnState.Idle,
                command => command.FsmEvent.Match<State<EnState, Data>>()
                    .With<List<ResourceUpgrade>>(this.StateIdleHandleResourceUpgrade).ResultOrDefault(o => null));

            this.When(EnState.InitializationFailed, command => null);

            this.WhenUnhandled(
                command => command.FsmEvent.Match<State<EnState, Data>>().With<RecheckState>(m => this.LoadState())
                    .ResultOrDefault(
                        o =>
                            {
                                Context.GetLogger().Warning(
                                    "{Type}: received unsupported message {MessageType} in state {StateName}",
                                    this.GetType().Name,
                                    o.GetType().Name,
                                    this.StateName.ToString());
                                return this.Stay();
                            }));

            this.Parent.Tell(new ProcessingTheRequest());
            var startState = this.LoadState(new Data(), true);
            this.StartWith(startState.StateName, startState.StateData, startState.Timeout);
            this.Initialize();
        }

        /// <summary>
        /// Creates migration state from configuration states
        /// </summary>
        /// <param name="templateCode">The migrator template code</param>
        /// <param name="sourceMigratorStates">The resource state according to source configuration</param>
        /// <param name="destinationMigratorStates">The resource state according to destination configuration</param>
        /// <returns>The overall resource migration state</returns>
        private static IEnumerable<MigratorMigrationState> CreateMigrationState(
            string templateCode,
            IReadOnlyCollection<MigratorConfigurationState> sourceMigratorStates,
            IReadOnlyCollection<MigratorConfigurationState> destinationMigratorStates)
        {
            foreach (var destinationMigratorState in destinationMigratorStates)
            {
                var sourceMigratorState =
                    sourceMigratorStates.FirstOrDefault(s => s.TypeName == destinationMigratorState.TypeName);

                if (sourceMigratorState == null)
                {
                    yield return MigratorMigrationState.CreateFrom(templateCode, destinationMigratorState, EnMigratorPosition.New);
                    continue;
                }

                var resourceStates = CreateMigrationState(templateCode, sourceMigratorState, destinationMigratorState).ToList();

                var direction = GetDirection(
                    sourceMigratorState.MigrationPoints,
                    destinationMigratorState.MigrationPoints);

                if (!resourceStates.Any() || resourceStates.All(
                        r => r.Position == EnResourcePosition.NotCreated || r.Position == EnResourcePosition.Obsolete))
                {
                    direction = EnMigrationDirection.Stay;
                }

                yield return new MigratorMigrationState
                                 {
                                     Name = destinationMigratorState.Name,
                                     TypeName = destinationMigratorState.TypeName,
                                     Direction = direction,
                                     Position = EnMigratorPosition.Present,
                                     Resources = resourceStates,
                                     Priority = destinationMigratorState.Priority,
                                     DependencyType = destinationMigratorState.DependencyType
                                 };
            }

            foreach (var sourceMigratorState in sourceMigratorStates.Where(
                s => destinationMigratorStates.All(d => d.TypeName != s.TypeName)))
            {
                yield return MigratorMigrationState.CreateFrom(templateCode, sourceMigratorState, EnMigratorPosition.Obsolete);
            }
        }

        /// <summary>
        /// Creates migration state from configuration states
        /// </summary>
        /// <param name="templateCode">The migrator template code</param>
        /// <param name="sourceMigratorConfigurationState">
        /// The migrator state according to source configuration
        /// </param>
        /// <param name="destinationMigratorConfigurationState">
        /// The migrator state according to destination configuration
        /// </param>
        /// <returns>
        /// The overall resource migration state
        /// </returns>
        private static IEnumerable<ResourceMigrationState> CreateMigrationState(
            string templateCode,
            MigratorConfigurationState sourceMigratorConfigurationState,
            MigratorConfigurationState destinationMigratorConfigurationState)
        {
            foreach (var destinationResourceState in destinationMigratorConfigurationState.Resources)
            {
                var sourceResourceState =
                    sourceMigratorConfigurationState.Resources.FirstOrDefault(
                        s => s.Code == destinationResourceState.Code);

                if (sourceResourceState == null)
                {
                    yield return ResourceMigrationState.CreateFrom(
                        templateCode,
                        destinationMigratorConfigurationState,
                        destinationResourceState,
                        EnMigratorPosition.New);
                    continue;
                }

                yield return ResourceMigrationState.CreateFrom(
                    templateCode,
                    sourceMigratorConfigurationState,
                    sourceResourceState,
                    destinationMigratorConfigurationState,
                    destinationResourceState);
            }

            foreach (var sourceResourceState in sourceMigratorConfigurationState.Resources.Where(
                s => destinationMigratorConfigurationState.Resources.All(d => d.Code != s.Code)))
            {
                yield return ResourceMigrationState.CreateFrom(
                    templateCode,
                    sourceMigratorConfigurationState,
                    sourceResourceState,
                    EnMigratorPosition.Obsolete);
            }
        }

        /// <summary>
        /// Calculates the migration direction according to the defined migration points
        /// </summary>
        /// <param name="sourcePoints">The list of migration points in the source configuration</param>
        /// <param name="destinationPoints">The list of migration points in the destination configuration</param>
        /// <returns>The migration direction</returns>
        private static EnMigrationDirection GetDirection(
            IEnumerable<string> sourcePoints,
            IEnumerable<string> destinationPoints)
        {
            var source = string.Join(", ", sourcePoints.Select(p => $"\"{p.Replace("\\", "\\\\")}\""));
            var destination = string.Join(", ", destinationPoints.Select(p => $"\"{p.Replace("\\", "\\\\")}\""));

            if (source == destination)
            {
                return EnMigrationDirection.Stay;
            }

            if (destination.IndexOf(source, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return EnMigrationDirection.Upgrade;
            }

            if (source.IndexOf(destination, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return EnMigrationDirection.Downgrade;
            }

            return EnMigrationDirection.Undefined;
        }

        /// <summary>
        /// Creates the resource migration plan
        /// </summary>
        /// <param name="request">The list of migrating resources</param>
        /// <param name="errors">The list of processing errors</param>
        /// <returns>The migration plan</returns>
        private MigrationPlan CreateConfigurationPlan(
            List<ResourceUpgrade> request,
            out List<MigrationLogRecord> errors)
        {
            var plan = new MigrationPlan();
            errors = new List<MigrationLogRecord>();

            MigratorTemplatePlan currentTemplatePlan = null;
            MigratorMigrationsCommand currentCommand = null;

            foreach (var resourceUpgrade in request)
            {
                var error = new MigrationLogRecord
                                {
                                    Started = DateTimeOffset.Now,
                                    Type = EnMigrationLogRecordType.Error,
                                    ConfigurationId = this.StateData.Configuration.Id,
                                    MigratorTemplateCode = resourceUpgrade.TemplateCode,
                                    MigratorTypeName = resourceUpgrade.MigratorTypeName,
                                    ResourceCode = resourceUpgrade.ResourceCode
                                };

                var template =
                    this.StateData.ConfigurationState.States.FirstOrDefault(
                        t => t.Template.Code == resourceUpgrade.TemplateCode);
                if (template == null)
                {
                    error.Message = "Migrator template was not found";
                    errors.Add(error);
                    continue;
                }

                error.MigratorTemplateName = template.Template.Name;

                var migrator =
                    template.MigratorsStates.FirstOrDefault(m => m.TypeName == resourceUpgrade.MigratorTypeName);
                if (migrator == null)
                {
                    error.Message = "Migrator was not found";
                    errors.Add(error);
                    continue;
                }

                error.MigratorName = migrator.Name;

                var resource = migrator.Resources.FirstOrDefault(r => r.Code == resourceUpgrade.ResourceCode);
                if (resource == null)
                {
                    error.Message = "Resource was not found";
                    errors.Add(error);
                    continue;
                }

                error.ResourceName = resource.Name;

                if (template.Code != currentTemplatePlan?.Template.Code || currentTemplatePlan == null)
                {
                    currentTemplatePlan =
                        new MigratorTemplatePlan
                            {
                                Template = template.Template
                            };
                    currentCommand = null;
                    plan.Executions.Add(currentTemplatePlan);
                }

                if (migrator.TypeName != currentCommand?.TypeName || currentCommand == null)
                {
                    currentCommand = new MigratorMigrationsCommand { TypeName = migrator.TypeName };
                    currentTemplatePlan.Commands.Add(currentCommand);
                }

                var migrationPoint = migrator.LastDefinedPoint;

                var resourceMigrationCommand =
                    new ResourceMigrationCommand { ResourceCode = resource.Code, MigrationPoint = migrationPoint };
                currentCommand.Resources.Add(resourceMigrationCommand);
            }

            return plan;
        }

        /// <summary>
        /// Creates the resource migration plan
        /// </summary>
        /// <param name="request">The list of migrating resources</param>
        /// <param name="errors">The list of processing errors</param>
        /// <returns>The migration plan</returns>
        private MigrationPlan CreateMigrationPlan(List<ResourceUpgrade> request, out List<MigrationLogRecord> errors)
        {
            var plan = new MigrationPlan();
            errors = new List<MigrationLogRecord>();
            var errorId = -1;

            MigratorTemplatePlan currentTemplatePlan = null;
            MigratorMigrationsCommand currentCommand = null;

            foreach (var resourceUpgrade in request)
            {
                var error = new MigrationLogRecord
                                {
                                    Id = errorId--,
                                    Type = EnMigrationLogRecordType.Error,
                                    Started = DateTimeOffset.Now,
                                    ConfigurationId = this.StateData.Migration.ToConfigurationId,
                                    MigratorTemplateCode = resourceUpgrade.TemplateCode,
                                    MigratorTypeName = resourceUpgrade.MigratorTypeName,
                                    ResourceCode = resourceUpgrade.ResourceCode
                                };

                var template =
                    this.StateData.MigrationState?.TemplateStates.FirstOrDefault(
                        t => t.Code == resourceUpgrade.TemplateCode);
                if (template == null)
                {
                    error.Message = "Migrator template was not found";
                    errors.Add(error);
                    continue;
                }

                error.MigratorTemplateName = template.DestinationTemplate?.Name ?? template.SourceTemplate?.Name;

                var migrator = template.Migrators.FirstOrDefault(m => m.TypeName == resourceUpgrade.MigratorTypeName);
                if (migrator == null)
                {
                    error.Message = "Migrator was not found";
                    errors.Add(error);
                    continue;
                }

                error.MigratorName = migrator.Name;

                var resource = migrator.Resources.FirstOrDefault(r => r.Code == resourceUpgrade.ResourceCode);
                if (resource == null)
                {
                    error.Message = "Resource was not found";
                    errors.Add(error);
                    continue;
                }

                error.ResourceName = resource.Name;

                var executionSide = resourceUpgrade.Target == EnMigrationSide.Destination
                                        ? resource.MigrationToDestinationExecutor
                                        : resource.MigrationToSourceExecutor;

                if (!executionSide.HasValue)
                {
                    error.Message = "Resource can not be migrated";
                    errors.Add(error);
                    continue;
                }

                if (template.Code != currentTemplatePlan?.Template.Code || executionSide != currentTemplatePlan?.Side)
                {
                    currentTemplatePlan =
                        new MigratorTemplatePlan
                            {
                                Side = executionSide.Value,
                                Template = executionSide == EnMigrationSide.Destination
                                               ? template.DestinationTemplate
                                               : template.SourceTemplate
                            };
                    currentCommand = null;
                    plan.Executions.Add(currentTemplatePlan);
                }

                if (migrator.TypeName != currentCommand?.TypeName || currentCommand == null)
                {
                    currentCommand = new MigratorMigrationsCommand { TypeName = migrator.TypeName };
                    currentTemplatePlan.Commands.Add(currentCommand);
                }

                var migrationPoint = resourceUpgrade.Target == EnMigrationSide.Source
                                         ? resource.SourcePoint
                                         : resource.DestinationPoint;

                var resourceMigrationCommand =
                    new ResourceMigrationCommand { ResourceCode = resource.Code, MigrationPoint = migrationPoint };
                currentCommand.Resources.Add(resourceMigrationCommand);
            }

            return plan;
        }

        /// <summary>
        /// Executes the migration for the template
        /// </summary>
        /// <param name="side">
        /// The migration execution side
        /// </param>
        /// <param name="plan">
        /// The migration plan
        /// </param>
        /// <returns>
        /// The generated migration log
        /// </returns>
        private List<MigrationLogRecord> ExecuteMigration(EnMigrationSide side, MigratorTemplatePlan plan)
        {
            var log = new List<MigrationLogRecord>();
            var configurationDir = side == EnMigrationSide.Source
                                       ? Path.Combine(this.StateData.FromConfigurationExecutionDir, plan.Template.Code)
                                       : Path.Combine(this.StateData.ToConfigurationExecutionDir, plan.Template.Code);

            var configurationId = side == EnMigrationSide.Source
                                      ? this.StateData.Migration.FromConfiguration.Id
                                      : this.StateData.Migration.ToConfigurationId;

            Context.GetLogger().Info(
                "{Type}: preparing for migration resources of {MigratorTemplateCode} of configuration {ConfigurationId}. {ResourceCount} resources will be migrated",
                this.GetType().Name,
                plan.Template.Code,
                configurationId,
                plan.Commands.SelectMany(m => m.Resources).Count());

            try
            {
                var collector = new MigrationExecutor { Commands = plan.Commands };

                Context.GetLogger().Info(
                    "{Type}: {MigratorTemplateCode} of configuration {ConfigurationId} migration executor was created",
                    this.GetType().Name,
                    plan.Template.Code,
                    configurationId);

                collector = this.ExecuteMigrator(configurationDir, collector);
                var operations = collector.Result;
                foreach (var logMessage in collector.Logs)
                {
                    Context.GetLogger().Info("{Type}: migration log - {LogMessage}", this.GetType().Name, logMessage);
                }

                Context.GetLogger().Info(
                    "{Type}: {MigratorTemplateCode} of configuration {ConfigurationId} migration executor was executed",
                    this.GetType().Name,
                    plan.Template.Code,
                    configurationId);

                if (operations != null)
                {
                    foreach (var operation in operations)
                    {
                        operation.ConfigurationId = configurationId;
                        operation.MigrationId = this.StateData.Migration.Id;
                        operation.MigratorTemplateCode = plan.Template.Code;
                        operation.MigratorTemplateName = plan.Template.Name;

                        if (operation.Type.HasFlag(EnMigrationLogRecordType.Error))
                        {
                            Context.GetLogger().Error(
                                "{Type}: Error while executing migration for template {MigratorTemplateCode} of configuration {ConfigurationId}: {ErrorMessage} \n {ErrorStackTrace}",
                                this.GetType().Name,
                                plan.Template.Code,
                                configurationId,
                                operation.Message,
                                operation.ErrorStackTrace);
                        }
                        else
                        {
                            Context.GetLogger().Info(
                                "{Type}: Migration for template {MigratorTemplateCode} of configuration {ConfigurationId} {ResourceCode} was successfully migrated from {SourcePoint} to {DestinationPoint}",
                                this.GetType().Name,
                                plan.Template.Code,
                                configurationId,
                                operation.ResourceCode,
                                operation.SourcePoint,
                                operation.DestinationPoint);
                        }
                    }

                    log.AddRange(operations);
                }

                if (collector.Errors.Any())
                {
                    foreach (var error in collector.Errors)
                    {
                        log.Add(
                            new MigrationLogRecord
                                {
                                    Type = EnMigrationLogRecordType.Error,
                                    ConfigurationId = configurationId,
                                    MigrationId = this.StateData.Migration.Id,
                                    MigratorTemplateCode = plan.Template.Code,
                                    MigratorTemplateName = plan.Template.Name,
                                    Message = $"Error while executing migration: {error}"
                                });

                        Context.GetLogger().Error(
                            "{Type}: Error while executing migration for template {MigratorTemplateCode} of configuration {ConfigurationId}: {ErrorMessage}",
                            this.GetType().Name,
                            plan.Template.Code,
                            configurationId,
                            error);
                    }
                }
            }
            catch (Exception exception)
            {
                log.Add(
                    new MigrationLogRecord
                        {
                            Type = EnMigrationLogRecordType.Error,
                            ConfigurationId = configurationId,
                            MigrationId = this.StateData.Migration.Id,
                            MigratorTemplateCode = plan.Template.Code,
                            MigratorTemplateName = plan.Template.Name,
                            Message = exception.Message,
                            Exception = exception
                        });
            }

            return log;
        }

        /// <summary>
        /// Executes the migration for the template
        /// </summary>
        /// <param name="plan">
        /// The migration plan
        /// </param>
        /// <returns>
        /// The migration execution log
        /// </returns>
        private List<MigrationLogRecord> ExecuteMigration(MigratorTemplatePlan plan)
        {
            var log = new List<MigrationLogRecord>();
            var configurationDir = Path.Combine(this.StateData.ConfigurationExecutionDir, plan.Template.Code);

            try
            {
                var collector = new MigrationExecutor { Commands = plan.Commands };
                collector = this.ExecuteMigrator(configurationDir, collector);

                var operations = collector.Result;
                if (operations != null)
                {
                    foreach (var operation in operations)
                    {
                        operation.ConfigurationId = this.StateData.Configuration.Id;
                        operation.MigratorTemplateCode = plan.Template.Code;
                        operation.MigratorTemplateName = plan.Template.Name;

                        if (operation.Type.HasFlag(EnMigrationLogRecordType.Error))
                        {
                            Context.GetLogger().Error(
                                "{Type}: Error while executing migration for template {MigratorTemplateCode} of configuration {ConfigurationId}: {ErrorMessage} \n {ErrorStackTrace}",
                                this.GetType().Name,
                                plan.Template.Code,
                                this.StateData.Configuration.Id,
                                operation.Message,
                                operation.ErrorStackTrace);
                        }
                    }

                    log.AddRange(operations);
                }

                if (collector.Errors.Any())
                {
                    foreach (var error in collector.Errors)
                    {
                        log.Add(
                            new MigrationLogRecord
                                {
                                    Type = EnMigrationLogRecordType.Error,
                                    ConfigurationId = this.StateData.Configuration.Id,
                                    MigratorTemplateCode = plan.Template.Code,
                                    MigratorTemplateName = plan.Template.Name,
                                    Message = $"Error while executing migration: {error}"
                                });
                        Context.GetLogger().Error(
                            "{Type}: Error while executing migration for template {MigratorTemplateCode} of configuration {ConfigurationId}: {ErrorMessage}",
                            this.GetType().Name,
                            plan.Template.Code,
                            this.StateData.Configuration.Id,
                            error);
                    }
                }
            }
            catch (Exception exception)
            {
                log.Add(
                    new MigrationLogRecord
                        {
                            Type = EnMigrationLogRecordType.Error,
                            ConfigurationId = this.StateData.Configuration.Id,
                            MigratorTemplateCode = plan.Template.Code,
                            MigratorTemplateName = plan.Template.Name,
                            Message = exception.Message,
                            Exception = exception
                        });
            }

            return log;
        }

        /// <summary>
        /// Executes the <see cref="MigrationCollector"/> from pre-installed service
        /// </summary>
        /// <typeparam name="T">The collector end-type</typeparam>
        /// <param name="installedPath">The service installation path</param>
        /// <param name="instance">The collector instance</param>
        /// <returns>The executed collector</returns>
        private T ExecuteMigrator<T>(string installedPath, T instance)
            where T : MigrationCollector
        {
#if APPDOMAIN
            var isMono = Type.GetType("Mono.Runtime") != null;
            

// ReSharper disable once InconsistentNaming
            var ExecutableFileName =
isMono ? "mono" : Path.Combine(installedPath, "KlusterKite.NodeManager.Migrator.Executor.exe");
            

// ReSharper disable once InconsistentNaming
            var ExecutableArguments = isMono ? "./KlusterKite.NodeManager.Migrator.Executor.exe" : string.Empty;
#elif CORECLR
            const string ExecutableFileName = "dotnet";
            const string ExecutableArguments = "./KlusterKite.NodeManager.Migrator.Executor.dll";
#endif

            var process = new Process
                              {
                                  StartInfo =
                                      {
                                          UseShellExecute = false,
                                          WorkingDirectory = installedPath,
                                          FileName = ExecutableFileName,
                                          Arguments = ExecutableArguments,
                                          RedirectStandardOutput = true,
                                          RedirectStandardInput = true,
                                          RedirectStandardError = true,
#if APPDOMAIN
                                          ErrorDialog = false,
#endif
                                      }
                              };

            process.Start();

            string readLine = null;
            while (readLine != ProcessHelper.EOF && !process.HasExited)
            {
                readLine = process.StandardOutput.ReadLine();
            }

            T output;
            string error;
            try
            {
                process.StandardInput.Send(instance);
                output = process.StandardOutput.Receive() as T;
                process.WaitForExit();
            }
            catch (Exception exception)
            {
                Context.GetLogger().Error(exception, "{Type}: Migrator communication failed", this.GetType().Name);
                error = process.StandardError.ReadToEnd();
                Context.GetLogger().Error(
                    "{Type}: Migrator exited with error {Error}. Installed on {InstalledPath}",
                    this.GetType().Name,
                    error,
                    installedPath);
                throw;
            }

            error = process.StandardError.ReadToEnd();
            if (!string.IsNullOrWhiteSpace(error))
            {
                Context.GetLogger().Error(
                    "{Type}: Migrator exited with error {Error}. Installed on {InstalledPath}",
                    this.GetType().Name,
                    error,
                    installedPath);
            }

            process.Dispose();
            return output;
        }

        /// <summary>
        /// Extracts packages for the <see cref="MigratorTemplate"/>
        /// </summary>
        /// <param name="configuration">
        /// The configuration
        /// </param>
        /// <param name="migrationId">
        /// The possible migration id
        /// </param>
        /// <param name="migratorTemplate">
        /// The migrator template to extract
        /// </param>
        /// <param name="executionDirectory">
        /// The execution directory
        /// </param>
        /// <param name="forceExtract">
        /// Whether to overwrite previous extraction
        /// </param>
        /// <param name="tempDir">
        /// The temporary data directory
        /// </param>
        /// <param name="errors">
        /// The list of errors to fill
        /// </param>
        /// <param name="context">
        /// The actor context in order not to loose it during async operations
        /// </param>
        /// <returns>
        /// The async task
        /// </returns>
        private async Task ExtractConfigurationMigrationTemplateAsync(
            Configuration configuration,
            int? migrationId,
            MigratorTemplate migratorTemplate,
            string executionDirectory,
            bool forceExtract,
            string tempDir,
            List<MigrationLogRecord> errors,
            IActorContext context)
        {
            var migratorExecutionDirectory = Path.Combine(executionDirectory, migratorTemplate.Code);
            if (Directory.Exists(migratorExecutionDirectory))
            {
                if (forceExtract)
                {
                    Directory.Delete(migratorExecutionDirectory, true);
                }
                else
                {
                    return;
                }
            }

            var migratorTempDirectory = Path.Combine(tempDir, migratorTemplate.Code);
            Directory.CreateDirectory(migratorExecutionDirectory);
            Directory.CreateDirectory(migratorTempDirectory);

            try
            {
                List<PackageDescription> packagesToInstall;
                if (!migratorTemplate.PackagesToInstall.TryGetValue(
                        PackageRepositoryExtensions.CurrentRuntime,
                        out packagesToInstall))
                {
                    throw new Exception($"Framework {PackageRepositoryExtensions.CurrentRuntime} is not supported");
                }

                var packages = packagesToInstall.Select(p => new PackageIdentity(p.Id, NuGetVersion.Parse(p.Version)))
                    .ToList();
                await this.nugetRepository.CreateServiceAsync(
                    packages,
                    this.runtime,
                    PackageRepositoryExtensions.CurrentRuntime,
                    migratorExecutionDirectory,
                    "KlusterKite.NodeManager.Migrator.Executor");
            }
            catch (Exception exception)
            {
                context.GetLogger().Error(
                    exception,
                    "{Type} Error on creating service for migrator template {MigratorTemplateCode} of configuration {ConfigurationId}",
                    this.GetType().Name,
                    migratorTemplate.Code,
                    configuration.Id);

                errors.Add(
                    new MigrationLogRecord
                        {
                            Type = EnMigrationLogRecordType.Error,
                            ConfigurationId = configuration.Id,
                            MigrationId = migrationId,
                            MigratorTemplateCode = migratorTemplate.Code,
                            MigratorTemplateName = migratorTemplate.Name,
                            Message = $"error on creating service: {exception.Message}",
                            Exception = exception
                        });
            }

            if (errors.Any())
            {
                Directory.Delete(migratorExecutionDirectory, true);
                return;
            }

            File.WriteAllText(Path.Combine(migratorExecutionDirectory, "config.hocon"), migratorTemplate.Configuration);
        }

        /// <summary>
        /// Extracts the specified packages for the configuration migrators
        /// </summary>
        /// <param name="configuration">
        /// The configuration
        /// </param>
        /// <param name="executionDirectory">
        /// The execution Directory.
        /// </param>
        /// <param name="migrationId">
        /// The migration Id.
        /// </param>
        /// <param name="forceExtract">
        /// A value indicating whether previous extracted packages should be overwritten
        /// </param>
        /// <returns>
        /// the success of the operation
        /// </returns>
        private async Task<List<MigrationLogRecord>> ExtractConfigurationMigratorsAsync(
            Configuration configuration,
            string executionDirectory,
            int? migrationId,
            bool forceExtract = false)
        {
            var errors = new List<MigrationLogRecord>();
            if (!forceExtract && Directory.Exists(executionDirectory))
            {
                return errors;
            }

            Directory.CreateDirectory(executionDirectory);

            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var context = Context;
                foreach (var migratorTemplate in configuration.Settings.MigratorTemplates)
                {
                    try
                    {
                        await this.ExtractConfigurationMigrationTemplateAsync(
                            configuration,
                            migrationId,
                            migratorTemplate,
                            executionDirectory,
                            forceExtract,
                            tempDir,
                            errors,
                            context);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(
                            new MigrationLogRecord
                                {
                                    Type = EnMigrationLogRecordType.Error,
                                    ConfigurationId = configuration.Id,
                                    MigrationId = migrationId,
                                    MigratorTemplateCode = migratorTemplate.Code,
                                    MigratorTemplateName = migratorTemplate.Name,
                                    Message = $"error on extracting template: {exception.Message}",
                                    Exception = exception
                                });
                    }
                }

                return errors;
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Gets the state of the configuration
        /// </summary>
        /// <param name="configuration">
        /// The configuration to check
        /// </param>
        /// <param name="executionDirectory">
        /// The execution Directory.
        /// </param>
        /// <param name="migrationId">
        /// The current migration id
        /// </param>
        /// <param name="errors">
        /// The list of execution errors
        /// </param>
        /// <returns>
        /// The state of configuration resources
        /// </returns>
        private List<MigratorTemplateConfigurationState> GetConfigurationResourcesState(
            Configuration configuration,
            string executionDirectory,
            int? migrationId,
            out List<MigrationLogRecord> errors)
        {
            var result = new List<MigratorTemplateConfigurationState>();
            errors = new List<MigrationLogRecord>();

            foreach (var migratorTemplate in configuration.Settings.MigratorTemplates.OrderByDescending(
                mt => mt.Priority))
            {
                var state =
                    new MigratorTemplateConfigurationState
                        {
                            Code = migratorTemplate.Code,
                            Template = migratorTemplate
                        };
                var configurationDir = Path.Combine(executionDirectory, migratorTemplate.Code);
                try
                {
                    var collector = new ConfigurationStateCollector();
                    collector = this.ExecuteMigrator(configurationDir, collector);
                    var collectorErrors = collector.Errors;
                    var migratorConfigurationStates = collector.Result;

                    if (collectorErrors.Any())
                    {
                        foreach (var error in collector.Errors)
                        {
                            error.ConfigurationId = configuration.Id;
                            error.MigrationId = migrationId;
                            error.MigratorTemplateCode = migratorTemplate.Code;
                            error.MigratorTemplateName = migratorTemplate.Name;
                            errors.Add(error);

                            Context.GetLogger().Error(
                                "{Type}: Error while requesting migration state for migrator "
                                + "template {MigratorTemplateCode} of configuration {ConfigurationId}: {ErrorMessage}\n {ErrorStackTrace}",
                                this.GetType().Name,
                                migratorTemplate.Code,
                                configuration.Id,
                                error.Message,
                                error.ErrorStackTrace);
                        }

                        continue;
                    }

                    state.MigratorsStates = migratorConfigurationStates;
                    result.Add(state);
                }
                catch (Exception exception)
                {
                    errors.Add(
                        new MigrationLogRecord
                            {
                                Type = EnMigrationLogRecordType.Error,
                                ConfigurationId = configuration.Id,
                                MigrationId = migrationId,
                                MigratorTemplateCode = migratorTemplate.Code,
                                MigratorTemplateName = migratorTemplate.Name,
                                Message =
                                    $"Error while requesting migration state: {exception.Message}",
                                Exception = exception
                            });
                    Context.GetLogger().Error(
                        exception,
                        "{Type}: Error while requesting migration state for migrator template {MigratorTemplateCode} of configuration {ConfigurationId}",
                        this.GetType().Name,
                        migratorTemplate.Code,
                        configuration.Id);
                }
            }

            return result;
        }

        /// <summary>
        /// Checks the current migrating resource check
        /// </summary>
        /// <param name="data">
        /// The state data
        /// </param>
        /// <param name="errors">
        /// The list of occurred errors
        /// </param>
        /// <returns>
        /// The check success
        /// </returns>
        private MigrationActorMigrationState GetMigrationState(Data data, out List<MigrationLogRecord> errors)
        {
            errors = new List<MigrationLogRecord>();
            List<MigrationLogRecord> sourceErrors;
            var sourceStates = this.GetConfigurationResourcesState(
                data.Migration.FromConfiguration,
                data.FromConfigurationExecutionDir,
                data.Migration.Id,
                out sourceErrors).ToList();

            List<MigrationLogRecord> destinationErrors;
            var destinationStates = this.GetConfigurationResourcesState(
                data.Migration.ToConfiguration,
                data.ToConfigurationExecutionDir,
                data.Migration.Id,
                out destinationErrors).ToList();

            if (sourceErrors.Any() || destinationErrors.Any())
            {
                errors = sourceErrors.Union(destinationErrors).ToList();
                return null;
            }

            var state = CreateMigrationState(sourceStates, destinationStates).ToList();
            var result = new MigrationActorMigrationState { TemplateStates = state };
            return result;
        }

        /// <summary>
        /// Loads current state without active migration
        /// </summary>
        /// <param name="configuration">
        /// The currently active configuration
        /// </param>
        /// <param name="forceExtract">
        /// A value indicating whether configuration Nuget packages should be overwritten
        /// </param>
        /// <param name="data">
        /// Current actor state data
        /// </param>
        /// <returns>
        /// The next actor state
        /// </returns>
        private State<EnState, Data> LoadConfigurationState(Configuration configuration, bool forceExtract, Data data)
        {
            var configurationExecutionDir = data?.ConfigurationExecutionDir
                                            ?? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            data = new Data { ConfigurationExecutionDir = configurationExecutionDir, Configuration = configuration };
            var extractionErrors = this
                .ExtractConfigurationMigratorsAsync(configuration, configurationExecutionDir, null, forceExtract)
                .GetAwaiter().GetResult();
            if (extractionErrors.Any())
            {
                this.Parent.Tell(new MigrationActorInitializationFailed { Errors = extractionErrors });
                return new State<EnState, Data>(EnState.InitializationFailed, data);
            }

            List<MigrationLogRecord> errors;
            var configurationStates = this
                .GetConfigurationResourcesState(configuration, configurationExecutionDir, null, out errors).ToList();
            if (errors.Any())
            {
                this.Parent.Tell(new MigrationActorInitializationFailed { Errors = errors });
                return new State<EnState, Data>(EnState.InitializationFailed, data);
            }

            data.ConfigurationState = new MigrationActorConfigurationState { States = configurationStates };

            this.Parent.Tell(data.ConfigurationState);
            return new State<EnState, Data>(EnState.Idle, data);
        }

        /// <summary>
        /// Loads current state for active migration
        /// </summary>
        /// <param name="migration">The current migration</param>
        /// <param name="forceExtract">A value indicating whether configuration Nuget packages should be overwritten</param>
        /// <param name="data">Current actor state data</param>
        /// <returns>The next actor state</returns>
        private State<EnState, Data> LoadMigrationState(Migration migration, bool forceExtract, Data data)
        {
            var fromConfigurationExecutionDir = data?.FromConfigurationExecutionDir
                                                ?? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var toConfigurationExecutionDir = data?.ToConfigurationExecutionDir
                                              ?? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            var extractionErrors = this
                .ExtractConfigurationMigratorsAsync(
                    migration.FromConfiguration,
                    fromConfigurationExecutionDir,
                    migration.Id,
                    forceExtract).GetAwaiter().GetResult()
                .Union(
                    this.ExtractConfigurationMigratorsAsync(
                        migration.ToConfiguration,
                        toConfigurationExecutionDir,
                        migration.Id,
                        forceExtract).GetAwaiter().GetResult()).ToList();

            if (extractionErrors.Any())
            {
                this.Parent.Tell(new MigrationActorInitializationFailed { Errors = extractionErrors });
                return new State<EnState, Data>(EnState.InitializationFailed, new Data());
            }

            data = new Data
                       {
                           Migration = migration,
                           FromConfigurationExecutionDir = fromConfigurationExecutionDir,
                           ToConfigurationExecutionDir = toConfigurationExecutionDir
                       };

            var state = this.GetMigrationState(data, out var errors);

            if (state == null)
            {
                this.Parent.Tell(new MigrationActorInitializationFailed { Errors = errors });
                return new State<EnState, Data>(EnState.InitializationFailed, data);
            }

            data.MigrationState = state;
            this.Parent.Tell(state);
            return new State<EnState, Data>(EnState.Migration, data);
        }

        /// <summary>
        /// Loads current migration state
        /// </summary>
        /// <param name="data">
        /// The data.
        /// </param>
        /// <param name="forceExtract">
        /// A value indicating whether previous extracted packages should be overwritten
        /// </param>
        /// <returns>
        /// The current state
        /// </returns>
        private State<EnState, Data> LoadState(Data data = null, bool forceExtract = false)
        {
            if (data == null)
            {
                data = this.StateData;
            }

            using (var ds = this.contextFactory.CreateContext<ConfigurationContext>(
                this.databaseProviderName,
                this.connectionString,
                this.databaseName))
            {
                var currentMigration = ds.Migrations.Include(nameof(Migration.FromConfiguration))
                    .Include(nameof(Migration.ToConfiguration)).FirstOrDefault(m => m.IsActive);

                if (currentMigration != null)
                {
                    return this.LoadMigrationState(currentMigration, forceExtract, data);
                }

                var configuration = ds.Configurations.First(r => r.State == EnConfigurationState.Active);
                return this.LoadConfigurationState(configuration, forceExtract, data);
            }
        }

        /// <summary>
        /// Handles the <see cref="List{T}"/> of <see cref="ResourceUpgrade"/> message in <see cref="EnState.Migration"/> state
        /// </summary>
        /// <param name="request">The request</param>
        /// <returns>The next state</returns>
        private State<EnState, Data> StateIdleHandleResourceUpgrade(List<ResourceUpgrade> request)
        {
            var plan = this.CreateConfigurationPlan(request, out var errors);

            if (errors.Count != 0 || this.StateData.ConfigurationState == null)
            {
                this.Sender.Tell(new RequestDeclined { Errors = errors });
                return this.Stay();
            }

            this.Sender.Tell(new RequestAcknowledged());
            this.Parent.Tell(new ProcessingTheRequest());

            var log = plan.Executions.Aggregate(
                (IEnumerable<MigrationLogRecord>)new List<MigrationLogRecord>(),
                (list, templatePlan) => list.Union(this.ExecuteMigration(templatePlan)));
            this.Parent.Tell(log.ToList());
            return this.LoadState();
        }

        /// <summary>
        /// Handles the <see cref="List{T}"/> of <see cref="ResourceUpgrade"/> message in <see cref="EnState.Migration"/> state
        /// </summary>
        /// <param name="request">The request</param>
        /// <returns>The next state</returns>
        private State<EnState, Data> StateMigrationHandleResourceUpgrade(List<ResourceUpgrade> request)
        {
            var plan = this.CreateMigrationPlan(request, out var errors);

            if (errors.Count != 0 || this.StateData.MigrationState == null)
            {
                Context.GetLogger().Error(
                    "{Type}: Failed to fulfill migration request. Could not create migration plan.",
                    this.GetType().Name);
                this.Parent.Tell(errors.ToList());
                return this.Stay();
            }

            var log = plan.Executions.Aggregate(
                (IEnumerable<MigrationLogRecord>)new List<MigrationLogRecord>(),
                (list, templatePlan) => list.Union(this.ExecuteMigration(templatePlan.Side, templatePlan)));

            this.Parent.Tell(log.ToList());
            return this.LoadState();
        }

        /// <summary>
        /// The current data
        /// </summary>
        public class Data
        {
            /// <summary>
            /// Gets or sets the current configuration
            /// </summary>
            public Configuration Configuration { get; set; }

            /// <summary>
            /// Gets or sets the configuration execution directory 
            /// </summary>
            public string ConfigurationExecutionDir { get; set; }

            /// <summary>
            /// Gets or sets the current configuration
            /// </summary>
            public MigrationActorConfigurationState ConfigurationState { get; set; }

            /// <summary>
            /// Gets or sets the directory to extract migrator code for the initial configuration configuration
            /// </summary>
            public string FromConfigurationExecutionDir { get; set; }

            /// <summary>
            /// Gets or sets the current migration
            /// </summary>
            public Migration Migration { get; set; }

            /// <summary>
            /// Gets or sets the migration state
            /// </summary>
            public MigrationActorMigrationState MigrationState { get; set; }

            /// <summary>
            /// Gets or sets the directory to extract migrator code for the destination configuration configuration
            /// </summary>
            public string ToConfigurationExecutionDir { get; set; }
        }

        /// <summary>
        /// The resource migration plan to resolve migration request
        /// </summary>
        private class MigrationPlan
        {
            /// <summary>
            /// Gets the list of migrator executions
            /// </summary>
            public List<MigratorTemplatePlan> Executions { get; } = new List<MigratorTemplatePlan>();
        }

        /// <summary>
        /// The migration plan to execute with specific migrator template
        /// </summary>
        private class MigratorTemplatePlan
        {
            /// <summary>
            /// Gets the list of commands to execute with migrators
            /// </summary>
            public List<MigratorMigrationsCommand> Commands { get; } =
                new List<MigratorMigrationsCommand>();

            /// <summary>
            /// Gets or sets the migrator template
            /// </summary>
            public MigratorTemplate Template { get; set; }

            /// <summary>
            /// Gets or sets the execution side
            /// </summary>
            public EnMigrationSide Side { get; set; }
        }
    }
}
// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConfigurationConnection.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The configuration management
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.WebApi
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using Akka.Actor;
    using Akka.Event;

    using JetBrains.Annotations;

    using KlusterKite.API.Attributes;
    using KlusterKite.API.Client;
    using KlusterKite.Data.CRUD;
    using KlusterKite.Data.CRUD.ActionMessages;
    using KlusterKite.NodeManager.Client;
    using KlusterKite.NodeManager.Client.ORM;
    using KlusterKite.NodeManager.Messages;
    using KlusterKite.Security.Attributes;
    using KlusterKite.Web.Authorization.Attributes;

    /// <summary>
    /// The configuration management
    /// </summary>
    public class ConfigurationConnection : Connection<Configuration, int>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationConnection"/> class.
        /// </summary>
        /// <param name="actorSystem">
        /// The actor system.
        /// </param>
        /// <param name="dataActorPath">
        /// The data actor path.
        /// </param>
        /// <param name="timeout">
        /// The timeout.
        /// </param>
        /// <param name="context">
        /// The context.
        /// </param>
        public ConfigurationConnection(
            ActorSystem actorSystem,
            string dataActorPath,
            TimeSpan? timeout,
            RequestContext context)
            : base(actorSystem, dataActorPath, timeout, context)
        {
        }

        /// <summary>
        /// Checks the draft configuration if it can be moved to ready state.
        /// </summary>
        /// <param name="id">The id of configuration draft</param>
        /// <returns>The mutation result</returns>
        [DeclareMutation("checks the draft configuration if it can be moved to ready state.")]
        [UsedImplicitly]
        [RequireSession]
        [RequireUser]
        [RequireUserPrivilege(Privileges.ConfigurationFinish)]
        public async Task<MutationResult<Configuration>> Check(int id)
        {
            try
            {
                var response =
                    await this.System.ActorSelection(this.DataActorPath)
                        .Ask<CrudActionResponse<Configuration>>(
                            new ConfigurationCheckRequest { Id = id, Context = this.Context },
                            this.Timeout);
                return CreateResponse(response);
            }
            catch (Exception exception)
            {
                this.System.Log.Error(exception, "{Type}: error on check", this.GetType().Name);
                return new MutationResult<Configuration> { Errors = new[] { new ErrorDescription(null, exception.Message) }.ToList() };
            }
        }

        /// <summary>
        /// Mutation that moves <see cref="Configuration.State"/> from <see cref="EnConfigurationState.Draft"/> to <see cref="EnConfigurationState.Ready"/>
        /// </summary>
        /// <param name="id">The id of configuration draft</param>
        /// <returns>The mutation result</returns>
        [DeclareMutation("moves configuration state from \"draft\" to \"ready\"")]
        [UsedImplicitly]
        [RequireSession]
        [RequireUser]
        [RequireUserPrivilege(Privileges.ConfigurationFinish)]
        public async Task<MutationResult<Configuration>> SetReady(int id)
        {
            try
            {
                var response =
                    await this.System.ActorSelection(this.DataActorPath)
                        .Ask<CrudActionResponse<Configuration>>(
                            new ConfigurationSetReadyRequest { Id = id, Context = this.Context },
                            this.Timeout);
                return CreateResponse(response);
            }
            catch (Exception exception)
            {
                this.System.Log.Error(exception, "{Type}: error on SetReady", this.GetType().Name);
                return new MutationResult<Configuration> { Errors = new[] { new ErrorDescription(null, exception.Message) }.ToList() };
            }
        }

        /// <summary>
        /// Mutation that moves <see cref="Configuration.State"/> from <see cref="EnConfigurationState.Ready"/> to <see cref="EnConfigurationState.Obsolete"/>
        /// </summary>
        /// <param name="id">The id of configuration draft</param>
        /// <returns>The mutation result</returns>
        [DeclareMutation("moves configuration state from \"ready\" to \"obsolete\"")]
        [UsedImplicitly]
        [RequireSession]
        [RequireUser]
        [RequireUserPrivilege(Privileges.ConfigurationFinish)]
        public async Task<MutationResult<Configuration>> SetObsolete(int id)
        {
            try
            {
                var response =
                    await this.System.ActorSelection(this.DataActorPath)
                        .Ask<CrudActionResponse<Configuration>>(
                            new ConfigurationSetObsoleteRequest { Id = id, Context = this.Context },
                            this.Timeout);
                return CreateResponse(response);
            }
            catch (Exception exception)
            {
                this.System.Log.Error(exception, "{Type}: error on SetObsolete", this.GetType().Name);
                return new MutationResult<Configuration> { Errors = new[] { new ErrorDescription(null, exception.Message) }.ToList() };
            }
        }

        /// <summary>
        /// Mutation that sets the <see cref="Configuration.IsStable"/>
        /// </summary>
        /// <param name="id">The id of configuration draft</param>
        /// <param name="isStable">A value indicating the new <see cref="Configuration.IsStable"/> value</param>
        /// <returns>The mutation result</returns>-
        [UsedImplicitly]
        [RequireSession]
        [RequireUser]
        [RequireUserPrivilege(Privileges.ClusterUpdate)]
        [DeclareMutation("moves configuration state from \"draft\" to \"ready\"")]
        public async Task<MutationResult<Configuration>> SetStable(int id, bool isStable)
        {
            try
            {
                var response =
                    await this.System.ActorSelection(this.DataActorPath)
                        .Ask<CrudActionResponse<Configuration>>(
                            new ConfigurationSetStableRequest { Id = id, Context = this.Context, IsStable = isStable },
                            this.Timeout);
                return CreateResponse(response);
            }
            catch (Exception exception)
            {
                this.System.Log.Error(exception, "{Type}: error on SetStable", this.GetType().Name);
                return new MutationResult<Configuration> { Errors = new[] { new ErrorDescription(null, exception.Message) }.ToList() };
            }
        }
    }
}
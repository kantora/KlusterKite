// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CurrentUserApi.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Publishes access to the authenticated user information
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.WebApi
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Akka.Actor;

    using JetBrains.Annotations;

    using KlusterKite.API.Attributes;
    using KlusterKite.API.Client;
    using KlusterKite.Core;
    using KlusterKite.NodeManager.Client.ORM;
    using KlusterKite.NodeManager.Messages;
    using KlusterKite.Security.Attributes;
    using KlusterKite.Web.Authorization.Attributes;

    /// <summary>
    /// Publishes access to the authenticated user information
    /// </summary>
    [ApiDescription("Publishes access to the authenticated user information", Name = "CurrentUserApi")]
    public class CurrentUserApi
    {
        /// <summary>
        /// The context.
        /// </summary>
        private readonly RequestContext context;

        /// <summary>
        /// The actor system
        /// </summary>
        private readonly ActorSystem system;

        /// <summary>
        /// Initializes a new instance of the <see cref="CurrentUserApi"/> class.
        /// </summary>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <param name="system">
        /// The actor system
        /// </param>
        public CurrentUserApi(RequestContext context, ActorSystem system)
        {
            this.context = context;
            this.system = system;
        }

        /// <summary>
        /// Gets the current user data
        /// </summary>
        [UsedImplicitly]
        [RequireSession]
        [DeclareField("Authenticated user")]
        public UserDescription KlusterKiteUser { 
            get { 
                return this.context?.Authentication?.User as UserDescription;
            } 
        }

        /// <summary>
        /// Gets the current user privileges
        /// </summary>
        [UsedImplicitly]
        [DeclareField("The current user privileges")]
        public List<string> KlusterKiteUserPrivileges => this.context?.Authentication?.UserScope.ToList();

        /// <summary>
        /// Gets the current user privileges
        /// </summary>
        [UsedImplicitly]
        [DeclareField("The current application privileges")]
        public List<string> KlusterKiteClientPrivileges => this.context?.Authentication?.ClientScope.ToList();

        /// <summary>
        /// Changes the current user password
        /// </summary>
        /// <param name="requestContext">The request context</param>
        /// <param name="oldPassword">The user current password</param>
        /// <param name="newPassword">The user new password</param>
        /// <returns>The success of the operation</returns>
        [UsedImplicitly]
        [RequireSession]
        [RequireUser]
        [DeclareMutation("Changes the current user password")]
        public Task<MutationResult<bool>> ChangePassword(
            RequestContext requestContext,
            [ApiDescription("The user current password")] string oldPassword,
            [ApiDescription("The user new password")] string newPassword)
        {
            var user = requestContext?.Authentication?.User as UserDescription;
            if (user == null)
            {
                return Task.FromResult<MutationResult<bool>>(null);
            }

            var request = new UserChangePasswordRequest
                              {
                                  NewPassword = newPassword,
                                  OldPassword = oldPassword,
                                  Login = user.Login
                              };

            return
                this.system.ActorSelection(NodeManagerApi.GetManagerActorProxyPath())
                    .Ask<MutationResult<bool>>(request, ConfigurationUtils.GetRestTimeout(this.system));
        }
    }
}

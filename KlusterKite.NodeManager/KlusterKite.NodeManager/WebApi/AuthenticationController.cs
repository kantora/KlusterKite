﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AuthenticationController.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Authenticate web api users
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.WebApi
{
    using System.Collections.Generic;
    
    using KlusterKite.NodeManager.Client.ORM;
    using KlusterKite.Security.Attributes;
    using KlusterKite.Web.Authorization;
    using KlusterKite.Web.Authorization.Attributes;

    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// Authenticate web api users
    /// </summary>
    [Route("api/1.x/klusterkite/nodemanager/authentication")]
    [RequireUser]
    public class AuthenticationController : Controller
    {
        /// <summary>
        /// Gets the currently authenticated user name
        /// </summary>
        /// <returns>The user name</returns>
        [Route("user")]
        [HttpGet]
        public IActionResult GetUser()
        {
            var session = this.GetSession();
            var description = session.User as UserDescription;

            if (description == null)
            {
                this.NotFound();
            }

            return this.Ok(description);
        }

        /// <summary>
        /// Gets the currently authenticated user scope list (the list of granted privileges)
        /// </summary>
        /// <returns>The user name</returns>
        [Route("userScope")]
        [HttpGet]
        public IActionResult GetMyScope()
        {
            var session = this.GetSession();
            return this.Ok(session.UserScope);
        }

        /// <summary>
        /// Gets the list of the defined privileges
        /// </summary>
        /// <returns>the list of the defined privileges</returns>
        [Route("definedPrivileges")]
        [HttpGet]
        [RequireUserPrivilege(Client.Privileges.GetPrivilegesList)]
        public IEnumerable<PrivilegeDescription> GetRegisteredPrivileges()
        {
            return Utils.DefinedPrivileges;
        }
    }
}

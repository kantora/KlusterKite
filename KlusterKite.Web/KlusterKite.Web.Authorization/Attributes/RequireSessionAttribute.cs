// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RequireSessionAttribute.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Defines the RequireSessionAttribute type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.Authorization.Attributes
{
    using System;

    using KlusterKite.Security.Attributes;
    using KlusterKite.Security.Client;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>
    /// Marks api method to check for authentication
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property)]
    public class RequireSessionAttribute : Attribute, IAuthorizationFilter
    {        
        /// <inheritdoc />
        public bool AllowMultiple => false;

        /// <summary>
        /// Gets or sets the severity of action
        /// </summary>
        public EnSeverity Severity { get; set; } = EnSeverity.Trivial;

        /// <inheritdoc />
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var session = context.HttpContext.GetSession();
            if (session == null)
            {
                SecurityLog.CreateRecord(
                    EnSecurityLogType.OperationDenied,
                    this.Severity,
                    context.HttpContext.GetRequestDescription(),
                    "Attempt to access {ControllerName} action {ActionName} without authenticated session",
                    (context.ActionDescriptor as ControllerActionDescriptor)?.ActionName,
                    (context.ActionDescriptor as ControllerActionDescriptor)?.ControllerName);
                context.Result = new StatusCodeResult(401);
            }
        }
    }
}

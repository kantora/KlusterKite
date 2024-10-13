// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ApiProvider.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The description of the API provider
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.GraphQL.Publisher
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using KlusterKite.API.Client;
    using KlusterKite.Security.Attributes;

    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The description of the API provider
    /// </summary>
    public abstract class ApiProvider
    {
        /// <summary>
        /// Gets or sets current provider API description
        /// </summary>
        public ApiDescription Description { get; set; }

        /// <summary>
        /// Retrieves specified data for api request
        /// </summary>
        /// <param name="requests">
        /// The request
        /// </param>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <returns>
        /// The resolved data
        /// </returns>
        public abstract ValueTask<JObject> GetData(List<ApiRequest> requests, RequestContext context);
    }
}

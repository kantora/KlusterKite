// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DirectProvider.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Testing publishing and resolving integration
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.GraphQL.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using KlusterKite.API.Client;
    using KlusterKite.Security.Attributes;

    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Direct publish of <see cref="KlusterKite.API.Provider.ApiProvider"/>
    /// </summary>
    public class DirectProvider : ApiProvider
    {
        /// <summary>
        /// The provider.
        /// </summary>
        private readonly API.Provider.ApiProvider provider;

        /// <summary>
        /// The action to output resolve errors
        /// </summary>
        private readonly Action<string> errorOutput;

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectProvider"/> class.
        /// </summary>
        /// <param name="provider">
        /// The provider.
        /// </param>
        /// <param name="errorOutput">
        /// The action to output resolve errors
        /// </param>
        public DirectProvider(API.Provider.ApiProvider provider, Action<string> errorOutput)
        {
            this.provider = provider;
            this.errorOutput = errorOutput;
            this.Description = provider.ApiDescription;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the result from provider should be packed to string and then restored to <see cref="JObject"/>
        /// </summary>
        /// <remarks>
        /// This is useful in tests to check that all data a serialized correctly
        /// </remarks>
        public bool UseJsonRepack { get; set; }

        /// <inheritdoc />
        public override async ValueTask<JObject> GetData(List<ApiRequest> requests, RequestContext context)
        {
            JObject result;
            var mutations = requests.OfType<MutationApiRequest>().ToList();
            if (mutations.Count > 0)
            {
                result = new JObject();
                foreach (var mutation in mutations)
                {
                    var midResult = await this.provider.ResolveMutation(
                                        mutation,
                                        context,
                                        exception =>
                                            this.errorOutput?.Invoke(
                                                $"Resolve error: {exception.Message}\n{exception.StackTrace}"));
                    result.Merge(midResult);
                }

                return this.UseJsonRepack ? Repack(result) : result;
            }

            result = await this.provider.ResolveQuery(
                       requests,
                       context,
                       exception =>
                           this.errorOutput?.Invoke($"Resolve error: {exception.Message}\n{exception.StackTrace}")) as JObject;
            return this.UseJsonRepack ? Repack(result) : result;
        }

        /// <summary>
        /// Packs the <see cref="JObject"/> to the string and then restores it
        /// </summary>
        /// <param name="source">The source object</param>
        /// <returns>The repacked object</returns>
        private static JObject Repack(JObject source)
        {
            return JObject.Parse(source.ToString());
        }
    }
}
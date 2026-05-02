// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DateTimeResolver.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Resolves value for <see cref="DateTime" /> objects
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.API.Provider.Resolvers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using KlusterKite.API.Client;
    using KlusterKite.Security.Attributes;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Resolves value for <see cref="DateTime"/> objects
    /// </summary>
    public class DateTimeResolver : IResolver
    {
        /// <inheritdoc />
        public Task<JToken> ResolveQuery(object source, ApiRequest request, ApiField apiField, RequestContext context, JsonSerializer argumentsSerializer, Action<Exception> onErrorCallback)
        {
            if (source is DateTime)
            {
                var dateTime = (DateTime)source;
                return Task.FromResult<JToken>(new JValue(dateTime.ToUniversalTime()));
            }

            return Task.FromResult<JToken>(new JValue(source));
        }

        /// <inheritdoc />
        public ApiType GetElementType()
        {
            return null;
        }

        /// <inheritdoc />
        public IEnumerable<ApiField> GetTypeArguments()
        {
            yield break;
        }
    }
}

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
            // EnScalarType.DateTime maps both DateTime and DateTimeOffset, so
            // handle both: new JValue(object) doesn't recognize DateTimeOffset
            // as a date and renders it as the underlying string/0, which the
            // monitoring UI then parses as Unix epoch.
            switch (source)
            {
                case DateTime dt:
                    return Task.FromResult<JToken>(new JValue(dt.ToUniversalTime()));
                case DateTimeOffset dto:
                    return Task.FromResult<JToken>(new JValue(dto.ToUniversalTime()));
                default:
                    return Task.FromResult<JToken>(new JValue(source));
            }
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

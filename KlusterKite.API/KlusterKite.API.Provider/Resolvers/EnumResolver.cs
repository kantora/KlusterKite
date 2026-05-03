// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EnumResolver.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Defines the EnumResolver type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.API.Provider.Resolvers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    using KlusterKite.API.Attributes;
    using KlusterKite.API.Client;
    using KlusterKite.Security.Attributes;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Resolves a enum value
    /// </summary>
    /// <typeparam name="T">The type of enum</typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType",
        Justification = "Making use of static properties in generic classes")]
    public class EnumResolver<T> : IResolver
    {
        /// <summary>
        /// A value indicating whether current enum is flags enum
        /// </summary>
        private static bool hasFlags;

        /// <summary>
        /// Initializes static members of the <see cref="EnumResolver{T}"/> class.
        /// </summary>
        static EnumResolver()
        {
            var type = typeof(T);
            if (!type.GetTypeInfo().IsSubclassOf(typeof(Enum)))
            {
                throw new Exception($"{type.Name} should be enum");
            }

            var names = Enum.GetNames(type);
            var descriptions = new Dictionary<string, string>();
            foreach (var name in names)
            {
                var property = type.GetTypeInfo().GetMember(name).FirstOrDefault();
                var attribute = property?.GetCustomAttribute<ApiDescriptionAttribute>();
                if (attribute != null)
                {
                    descriptions[name] = attribute.Description;
                }
            }

            GeneratedType = new ApiEnumType(ApiDescriptionAttribute.GetTypeName(type), names, descriptions);
            hasFlags = type.GetTypeInfo().GetCustomAttribute<FlagsAttribute>() != null;
        }

        /// <summary>
        /// Gets the generated api type for typed argument
        /// </summary>
        public static ApiType GeneratedType { get; }

        /// <inheritdoc />
        public Task<JToken> ResolveQuery(object source, ApiRequest request, ApiField apiField, RequestContext context, JsonSerializer argumentsSerializer, Action<Exception> onErrorCallback)
        {
            // A nullable enum (e.g. EnMigrationSteps?) can legitimately be
            // null when no migration is in progress. Without this guard
            // source.ToString() throws NullReferenceException, which the
            // GraphQL pipeline then surfaces as
            //   "Error trying to resolve field '<name>'. INVALID_OPERATION".
            // Return raw null (not JValue.CreateNull); GraphQL.NET 7.x's
            // EnumerationGraphType coercion rejects a wrapped JValue-null.
            if (source == null)
            {
                return Task.FromResult<JToken>(null);
            }

            var value = hasFlags ? new JValue((long)source) : new JValue(source.ToString());
            return Task.FromResult<JToken>(value);
        }

        /// <inheritdoc />
        public ApiType GetElementType()
        {
            return GeneratedType;
        }

        /// <inheritdoc />
        public IEnumerable<ApiField> GetTypeArguments()
        {
            yield break;
        }
    }
}

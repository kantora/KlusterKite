// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Converter.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Provides converter methods
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.GraphQL.Publisher.Internals
{
    using System.Collections.Generic;
    using System.Linq;

    using KlusterKite.Core.Utils;

    using global::GraphQL;

    using Newtonsoft.Json.Linq;
    using GraphQLParser.AST;
    using KlusterKite.Security.Attributes;
    using System.Globalization;

    /// <summary>
    /// Provides converter methods
    /// </summary>
    public static class Converter
    {
        /// <summary>
        /// The key to dictionary used to pass the request context to the execution options
        /// </summary>
        private const string RequestContextKey = "RequestContext";

        /// <summary>
        /// Converts arguments to JSON object
        /// </summary>
        /// <param name="arguments">
        /// Arguments list
        /// </param>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <returns>
        /// The corresponding JSON
        /// </returns>
        public static JObject ToJson(this IEnumerable<GraphQLArgument> arguments, IResolveFieldContext context)
        {
            var result = new JObject();
            if (arguments == null)
            {
                return result;
            }

            foreach (var argument in arguments)
            {
                var value = ToJson(argument.Value, context);
                result.Add(argument.Name.ToString(), value);
            }

            return result;
        }

        /// <summary>
        /// Converts <see cref="GraphQLObjectValue"/> to JSON object
        /// </summary>
        /// <param name="objectValue">
        /// The object value.
        /// </param>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <returns>
        /// The <see cref="JToken"/>.
        /// </returns>
        private static JToken ToJson(GraphQLObjectValue objectValue, IResolveFieldContext context)
        {
            var result = new JObject();
            foreach (var field in objectValue.Fields)
            {
                var value = ToJson(field.Value, context);
                result.Add(field.Name.ToString(), value);
            }

            return result;
        }

        /// <summary>
        /// Converts <see cref="GraphQLValue"/> to JSON object
        /// </summary>
        /// <param name="value">
        /// The abstract value.
        /// </param>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <returns>
        /// The <see cref="JToken"/>.
        /// </returns>
        private static JToken ToJson(GraphQLValue value, IResolveFieldContext context)
        {
            return value.Match<JToken>()
                    .With<GraphQLVariable>(r =>
                    {
                        context.Variables.ValueFor(r.Name, out var variableValue);
                        var token = JToken.FromObject(variableValue.Value);
                        return token;
                    })
                    .With<GraphQLIntValue>(v => new JValue(int.Parse(v.Value.ToString(), CultureInfo.InvariantCulture)))
                    .With<GraphQLFloatValue>(v => new JValue(v.Value))
                    .With<GraphQLStringValue>(v => new JValue(v.Value.ToString()))
                   // .With<GraphQLDecimalValue>(v => new JValue(v.Value))
                    .With<GraphQLFloatValue>(v => new JValue(v.Value))
                    .With<GraphQLBooleanValue>(v => new JValue(v.Value))
                    //.With<GraphQLLongValue>(v => new JValue(v.Value))
                    .With<GraphQLEnumValue>(v => new JValue(v.Name.StringValue))
                    .With<GraphQLListValue>(v => {
                        var values = v.Values ?? new List<GraphQLValue>();
                        return new JArray(values.Select(sv => ToJson(sv, context)));
                        }                    )
                    .With<GraphQLObjectValue>(sv => ToJson(sv, context))
                    .ResultOrDefault(v => null);
        }

        /// <summary>
        /// Converts the request context to the execution options user context
        /// </summary>
        /// <param name="context">The context to convert</param>
        /// <returns>GrqphQL execution options user context</returns>
        public static IDictionary<string, object?> ToExecutionOptionsUserContext(this RequestContext context)
        {
            return new Dictionary<string, object?>
                       {
                           { RequestContextKey, context },
                       };
        }

        /// <summary>
        /// Converts the execution options user context to the request context
        /// </summary>
        /// <param name="dict">The execution options user context to convert</param>
        /// <returns>the request context</returns>
        public static RequestContext? ToRequestContext(this IDictionary<string, object?> dict)
        {
            if (dict == null)
            {
                return null;
            }

            if (dict.TryGetValue(RequestContextKey, out var value) && value is RequestContext context)
            {
                return context;
            }

            return null;
        }
    }
}

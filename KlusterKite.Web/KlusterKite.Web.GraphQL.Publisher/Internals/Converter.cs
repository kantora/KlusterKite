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

    /// <summary>
    /// Provides converter methods
    /// </summary>
    internal static class Converter
    {
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
                        var token = JToken.FromObject(variableValue);
                        return token;
                    })
                    .With<GraphQLIntValue>(v => new JValue(v.Value))
                    .With<GraphQLFloatValue>(v => new JValue(v.Value))
                    .With<GraphQLStringValue>(v => new JValue(v.Value))
                   // .With<GraphQLDecimalValue>(v => new JValue(v.Value))
                    .With<GraphQLFloatValue>(v => new JValue(v.Value))
                    .With<GraphQLBooleanValue>(v => new JValue(v.Value))
                    //.With<GraphQLLongValue>(v => new JValue(v.Value))
                    .With<GraphQLEnumValue>(v => new JValue(v.Name))
                    .With<GraphQLListValue>(v => new JArray(v.Values.Select(sv => ToJson(sv, context))))
                    .With<GraphQLObjectValue>(sv => ToJson(sv, context))
                    .ResultOrDefault(v => null);
        }
    }
}

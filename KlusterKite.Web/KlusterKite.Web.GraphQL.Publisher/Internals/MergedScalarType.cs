// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MergedScalarType.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The merged type representing scalar value
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.GraphQL.Publisher.Internals
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using global::GraphQL;
    using global::GraphQL.Types;

    using KlusterKite.API.Client;
    using KlusterKite.Web.GraphQL.Publisher.GraphTypes;

    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The merged type representing scalar value
    /// </summary>
    internal class MergedScalarType : MergedType
    {
        /// <inheritdoc />
        public MergedScalarType(EnScalarType type)
            : base(type.ToString())
        {
            this.ScalarType = type;
        }

        /// <inheritdoc />
        public override string ComplexTypeName => $"Scalar-{this.ScalarType}";

        /// <summary>
        /// Gets the scalar type
        /// </summary>
        public EnScalarType ScalarType { get; }

        /// <inheritdoc />
        public override IGraphType ExtractInterface(ApiProvider provider, NodeInterface nodeInterface)
        {
            return this.GenerateGraphType(null, null);
        }

        /// <inheritdoc />
        public override string GetInterfaceName(ApiProvider provider)
        {
            return this.GenerateGraphType(null, null).Name;
        }

        /// <inheritdoc />
        public override IGraphType GenerateGraphType(NodeInterface nodeInterface, List<TypeInterface> interfaces)
        {
            switch (this.ScalarType)
            {
                case EnScalarType.Boolean:
                    return new BooleanGraphType();
                case EnScalarType.Float:
                    return new FloatGraphType();
                case EnScalarType.Decimal:
                    return new DecimalGraphType();
                case EnScalarType.Integer:
                    return new IntGraphType();
                case EnScalarType.String:
                    return new StringGraphType();
                case EnScalarType.Guid:
                    return new GuidGraphType();
                case EnScalarType.DateTime:
                    return new DateTimeGraphType();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <inheritdoc />
        public override async ValueTask<object> ResolveAsync(IResolveFieldContext context)
        {
            var resolve = await base.ResolveAsync(context);
            if (resolve is JArray)
            {
                return ((JArray)resolve).Select(element => (element as JValue)?.Value).ToArray();
            }

            switch (this.ScalarType)
            {
                case EnScalarType.Guid:
                    return Guid.Parse(resolve.ToString());
                case EnScalarType.DateTime:
                    return ((resolve as JValue)?.Value as DateTime?)?.ToUniversalTime();
                default:
                    return (resolve as JValue)?.Value;
            }

            
        }
    }
}
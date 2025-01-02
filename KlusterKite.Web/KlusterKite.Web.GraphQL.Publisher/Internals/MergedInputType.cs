// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MergedInputType.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The type used for arguments
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.GraphQL.Publisher.Internals
{
    using System.Collections.Generic;
    using System.Linq;
    using global::GraphQL.Types;

    using KlusterKite.Web.GraphQL.Publisher.GraphTypes;

    /// <summary>
    /// The type used for arguments
    /// </summary>
    internal class MergedInputType : MergedObjectType
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MergedInputType"/> class.
        /// </summary>
        /// <param name="originalTypeName">
        /// The original type name.
        /// </param>
        public MergedInputType(string originalTypeName)
            : base(originalTypeName)
        {
        }

        /// <inheritdoc />
        public override string ComplexTypeName => $"{base.ComplexTypeName}_Input";

        /// <inheritdoc />
        public override IGraphType GenerateGraphType(NodeInterface nodeInterface, List<TypeInterface> interfaces)
        {
            var fields = this.Fields
                .Select(this.ConvertApiField
                ).Select(field => {
                    field.Type = typeof(VirtualInputGraphType);
                    field.Resolver = null;
                    return field;
                });

            var inputGraphType = new VirtualInputGraphType(this.ComplexTypeName) { Description = this.Description };
            inputGraphType.AddFields(fields);
            
            return inputGraphType;
        }
    }
}

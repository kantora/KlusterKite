// --------------------------------------------------------------------------------------------------------------------
// <copyright file="VirtualInputGraphType.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The virtual graph type used to convert <see cref="ApiType" /> to <see cref="GraphType" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.GraphQL.Publisher.GraphTypes
{
    using System.Collections.Generic;

    using global::GraphQL.Types;

    using KlusterKite.API.Client;

    /// <summary>
    /// The virtual graph type used to convert <see cref="ApiObjectType"/> to <see cref="InputObjectGraphType"/>
    /// </summary>
    internal class VirtualInputGraphType : InputObjectGraphType, IInputObjectGraphType
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualInputGraphType"/> class.
        /// </summary>
        /// <param name="virtualTypeName">
        /// The virtual type name.
        /// </param>
        public VirtualInputGraphType(string virtualTypeName)
        {
            this.Name = virtualTypeName;
        }

        /// <summary>
        /// Adds the list of fields to the field list
        /// </summary>
        /// <param name="fieldTypes">The list of fields</param>
        public void AddFields(IEnumerable<FieldType> fieldTypes)
        {
            foreach (var fieldType in fieldTypes)
            {
                this.AddField(fieldType);
            }
        }
    }
}
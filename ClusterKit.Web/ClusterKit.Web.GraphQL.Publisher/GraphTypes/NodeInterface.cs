﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NodeInterface.cs" company="ClusterKit">
//   All rights reserved
// </copyright>
// <summary>
//   The node interface for the relay
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ClusterKit.Web.GraphQL.Publisher.GraphTypes
{
    using global::GraphQL.Types;

    /// <summary>
    /// The node interface for the relay
    /// </summary>
    public class NodeInterface : InterfaceGraphType
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NodeInterface"/> class.
        /// </summary>
        public NodeInterface()
        {
            var fieldType = new FieldType { Name = "id", ResolvedType = new IdGraphType(), Description = "The node id" };
            this.AddField(fieldType);
            this.Name = "Node";
            this.Description = "The Node interface as described in Relay documentation";
        }
    }
}

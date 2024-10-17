// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TempApiRequest.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The api request
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.GraphQL.Publisher
{
    using System.Collections.Generic;
    using global::GraphQL.Execution;


    /// <summary>
    /// The api request
    /// </summary>
    public class TempApiRequest
    {
        /// <summary>
        /// Gets or sets the field arguments
        /// </summary>
        public List<ArgumentValue> Arguments { get; set; }

        /// <summary>
        /// Gets or sets the field name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the list of requested subfields
        /// </summary>
        public List<TempApiRequest> Fields { get; set; }
    }
}
// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EnScalarType.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Supported primitive types (scalars)
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.API.Client
{
    /// <summary>
    /// Supported primitive types (scalars)
    /// </summary>
    public enum EnScalarType
    {
        /// <summary>
        /// The field is not of scalar type
        /// </summary>
        None,

        /// <summary>
        /// Boolean value
        /// </summary>
        Boolean,

        /// <summary>
        /// Floating point values
        /// </summary>
        Float,

        /// <summary>
        /// Decimal values
        /// </summary>
        Decimal,

        /// <summary>
        /// Integer values (32-bit)
        /// </summary>
        Integer,

        /// <summary>
        /// Long integer values (64-bit)
        /// </summary>
        Long,

        /// <summary>
        /// String values
        /// </summary>
        String,

        /// <summary>
        /// <see cref="System.Guid"/> values
        /// </summary>
        Guid,

        /// <summary>
        /// <see cref="System.DateTime"/> or <see cref="System.DateTimeOffset"/>
        /// </summary>
        DateTime,
    }
}

// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TypeMetadata.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Property or method return type description
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.API.Provider
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    using KlusterKite.API.Attributes;
    using KlusterKite.API.Client;

    /// <summary>
    /// Property or method return type description
    /// </summary>
    internal class TypeMetadata
    {
        /// <summary>
        /// The return types of a field
        /// </summary>
        public enum EnMetaType
        {
            /// <summary>
            /// This is scalar field
            /// </summary>
            Scalar,

            /// <summary>
            /// The field is an object
            /// </summary>
            Object,

            /// <summary>
            /// The field is an array
            /// </summary>
            Array,

            /// <summary>
            /// The field represents connection
            /// </summary>
            Connection
        }

        /// <summary>
        /// Gets or sets the converter type
        /// </summary>
        public Type ConverterType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether  this property / method has asynchronous access
        /// </summary>
        public bool IsAsync { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this property / method forwards resolve to other API provider
        /// </summary>
        public bool IsForwarding { get; set; }

        /// <summary>
        /// Gets or sets the type key property
        /// </summary>
        public PropertyInfo KeyProperty { get; set; }

        /// <summary>
        /// Gets or sets the type key property
        /// </summary>
        public string KeyPropertyName { get; set; }

        /// <summary>
        /// Gets or sets the meta type
        /// </summary>
        public EnMetaType MetaType { get; set; }

        /// <summary>
        /// Gets or sets the parsed scalar type
        /// </summary>
        public EnScalarType ScalarType { get; set; }

        /// <summary>
        /// Gets or sets the true returning type
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        /// Gets or sets the real type that represents connection
        /// </summary>
        public Type RealConnectionType { get; set; }

        /// <summary>
        /// Gets or sets the type name to look for resolver
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// Checks the type to be scalar
        /// </summary>
        /// <param name="type">The type</param>
        /// <returns>The corresponding scalar type</returns>
        public static EnScalarType CheckScalarType(Type type)
        {
            if (type == typeof(string))
            {
                return EnScalarType.String;
            }

            if (type == typeof(bool))
            {
                return EnScalarType.Boolean;
            }

            if (type == typeof(Guid))
            {
                return EnScalarType.Guid;
            }

            if (type == typeof(long) || type == typeof(ulong))
            {
                return EnScalarType.Long;
            }

            if (type == typeof(int) || type == typeof(short) || type == typeof(uint)
                || type == typeof(ushort)
                || (type.GetTypeInfo().IsSubclassOf(typeof(Enum)) && type.GetTypeInfo().GetCustomAttribute(typeof(FlagsAttribute)) != null))
            {
                return EnScalarType.Integer;
            }

            if (type == typeof(float) || type == typeof(double))
            {
                return EnScalarType.Float;
            }

            if (type == typeof(decimal))
            {
                return EnScalarType.Decimal;
            }

            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            {
                return EnScalarType.DateTime;
            }

            return EnScalarType.None;
        }

        /// <summary>
        /// Parses the metadata of returning type for the field
        /// </summary>
        /// <param name="type">The original field return type</param>
        /// <param name="attribute">The description attribute</param>
        /// <returns>The type metadata</returns>
        public static TypeMetadata GenerateTypeMetadata(Type type, PublishToApiAttribute attribute)
        {
            var metadata = new TypeMetadata();
            var asyncType = ApiDescriptionAttribute.CheckType(type, typeof(Task<>));
            if (asyncType != null)
            {
                metadata.IsAsync = true;
                type = asyncType.GenericTypeArguments[0];
            }

            var converter = (attribute as DeclareFieldAttribute)?.Converter;
            if (attribute.ReturnType != null)
            {
                type = attribute.ReturnType;
                metadata.IsForwarding = true;
            }
            else if (converter != null)
            {
                var valueConverter =
                    converter.GetInterfaces()
                        .FirstOrDefault(
                            i => i.GetTypeInfo().IsGenericType && i.GetGenericTypeDefinition() == typeof(IValueConverter<>));
                if (valueConverter == null)
                {
                    throw new InvalidOperationException(
                        $"Converter {converter.FullName} should implement the IValueConverter<>");
                }

                type = valueConverter.GenericTypeArguments[0];
                metadata.ConverterType = converter;
            }

            var nullable = ApiDescriptionAttribute.CheckType(type, typeof(Nullable<>));
            if (nullable != null)
            {
                type = nullable.GenericTypeArguments[0];
            }

            var scalarType = CheckScalarType(type);
            metadata.MetaType = EnMetaType.Scalar;
            if (scalarType == EnScalarType.None)
            {
                var enumerable = ApiDescriptionAttribute.CheckType(type, typeof(IEnumerable<>));
                var connection = ApiDescriptionAttribute.CheckType(type, typeof(INodeConnection<>));

                if (connection != null)
                {
                    metadata.MetaType = EnMetaType.Connection;
                    metadata.RealConnectionType = type;
                    type = connection.GenericTypeArguments[0];
                    scalarType = CheckScalarType(type);
                }
                else if (enumerable != null)
                {
                    metadata.MetaType = EnMetaType.Array;
                    type = enumerable.GenericTypeArguments[0];
                    scalarType = CheckScalarType(type);
                }
                else
                {
                    metadata.MetaType = EnMetaType.Object;
                }
            }

            if (scalarType != EnScalarType.None && type.GetTypeInfo().IsSubclassOf(typeof(Enum)))
            {
                type = Enum.GetUnderlyingType(type);
            }

            // todo: check forwarding type
            metadata.ScalarType = scalarType;
            metadata.Type = type;

            if (metadata.ScalarType == EnScalarType.None && !type.GetTypeInfo().IsSubclassOf(typeof(Enum)))
            {
                var keyProperty =
                    type.GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(p => p.GetCustomAttribute<DeclareFieldAttribute>()?.IsKey == true);
                if (keyProperty != null)
                {
                    metadata.KeyProperty = keyProperty;
                    metadata.KeyPropertyName = PublishToApiAttribute.GetMemberName(keyProperty);
                }
            }

            var typeName = ApiDescriptionAttribute.GetTypeName(type);
            metadata.TypeName = typeName;
            ////metadata.TypeName = metadata.GetFlags().HasFlag(EnFieldFlags.IsConnection) ? $"{typeName}_Connection" : typeName;
            return metadata;
        }

        /// <summary>
        /// Gets the <see cref="KlusterKite.API.Client.EnFieldFlags"/> from metadata
        /// </summary>
        /// <returns>The field flags</returns>
        public EnFieldFlags GetFlags()
        {
            EnFieldFlags flags;
            switch (this.MetaType)
            {
                case EnMetaType.Object:
                    flags = EnFieldFlags.None;
                    break;
                case EnMetaType.Array:
                    flags = EnFieldFlags.IsArray;
                    break;
                case EnMetaType.Connection:
                    flags = EnFieldFlags.IsConnection;
                    break;
                case EnMetaType.Scalar:
                    flags = EnFieldFlags.None;
                    break;
                default:
                    flags = EnFieldFlags.None;
                    break;
            }

            flags |= EnFieldFlags.Queryable;
            return flags;
        }
    }
}
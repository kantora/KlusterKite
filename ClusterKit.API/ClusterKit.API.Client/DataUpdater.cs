﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DataUpdater.cs" company="ClusterKit">
//   All rights reserved
// </copyright>
// <summary>
//   Utility to update class according to API mutation request
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ClusterKit.API.Client
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    using ClusterKit.API.Attributes;

    using JetBrains.Annotations;

    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Utility to update class according to API mutation request
    /// </summary>
    /// <typeparam name="TObject">The type of object to update</typeparam>
    public static class DataUpdater<TObject>
    {
        /// <summary>
        /// The initialization lock
        /// </summary>
        private static readonly object lockObject = new object();

        /// <summary>
        /// The list of discovered property copiers
        /// </summary>
        private static readonly Dictionary<string, Action<TObject, TObject, JObject>> PropertyCopiers
            = new Dictionary<string, Action<TObject, TObject, JObject>>();

        /// <summary>
        /// A value indicating whether type initialization was completed
        /// </summary>
        private static bool isInitializaed = false;

        /// <summary>
        /// Performs data copy from source to destination fields
        /// </summary>
        /// <param name="destination">
        /// The object to update
        /// </param>
        /// <param name="source">
        /// The object containing new data
        /// </param>
        /// <param name="request">
        /// The original api request used to update an object
        /// </param>
        /// <param name="inputName">
        /// The argument name
        /// </param>
        public static void Update(TObject destination, TObject source, ApiRequest request, string inputName = "newNode")
        {
            var objectJson = ((JObject)request.Arguments).Property(inputName)?.Value as JObject;
            Update(destination, source, objectJson);
        }

        /// <summary>
        /// Performs data copy from source to destination fields
        /// </summary>
        /// <param name="destination">
        /// The object to update
        /// </param>
        /// <param name="source">
        /// The object containing new data
        /// </param>
        /// <param name="objectJson">
        /// The json object definition
        /// </param>
        [UsedImplicitly]
        public static void Update(TObject destination, TObject source, JObject objectJson)
        {
            CheckInitialization();
            var fieldsModified = objectJson?.Properties();
            if (fieldsModified == null)
            {
                return;
            }

            foreach (var fieldName in fieldsModified)
            {
                Action<TObject, TObject, JObject> copier;
                // ReSharper disable once InconsistentlySynchronizedField
                if (PropertyCopiers.TryGetValue(fieldName.Name, out copier))
                {
                    copier(destination, source, fieldName.Value as JObject);
                }
            }
        }

        /// <summary>
        /// Checks current initialization states and perform initialization if needed
        /// </summary>
        private static void CheckInitialization()
        {
            if (isInitializaed)
            {
                return;
            }

            lock (lockObject)
            {
                if (isInitializaed)
                {
                    return;
                }

                Initialize();
            }
        }

        /// <summary>
        /// The type initialization
        /// </summary>
        private static void Initialize()
        {
            var properties =
                typeof(TObject).GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.SetProperty);

            foreach (var property in properties)
            {
                var publishAttribute = property.GetCustomAttribute<DeclareFieldAttribute>();
                if (publishAttribute == null || !publishAttribute.Access.HasFlag(EnAccessFlag.Writable))
                {
                    continue;
                }

                var propertyName = publishAttribute.Name ?? ApiDescriptionAttribute.ToCamelCase(property.Name);
                var destination = Expression.Parameter(typeof(TObject), "d");
                var source = Expression.Parameter(typeof(TObject), "s");
                var json = Expression.Parameter(typeof(JObject), "json");

                if (property.PropertyType.IsPrimitive
                    || DataUpdaterConfig.AdditionalScalarTypes.Contains(property.PropertyType)
                    || DataUpdaterConfig.AdditionalScalarTypes.Any(t => property.PropertyType.IsSubclassOf(t))
                    || property.PropertyType.GetInterface("System.Collections.IEnumerable") != null)
                {
                    // creating direct copy
                    var copy = Expression.Assign(
                        Expression.Property(destination, property),
                        Expression.Property(source, property));

                    var lambda = Expression.Lambda<Action<TObject, TObject, JObject>>(copy, destination, source, json);
                    PropertyCopiers[propertyName] = lambda.Compile();
                }
                else
                {
                    var updaterType = typeof(DataUpdater<>).MakeGenericType(property.PropertyType);
                    var method = updaterType.GetMethod(
                        nameof(Update),
                        new[] { property.PropertyType, property.PropertyType, typeof(JObject) });

                    var copy = Expression.Call(
                        null,
                        method,
                        Expression.Property(destination, property),
                        Expression.Property(source, property),
                        json);

                    var lambda = Expression.Lambda<Action<TObject, TObject, JObject>>(copy, destination, source, json);
                    PropertyCopiers[propertyName] = lambda.Compile();
                }
            }
        }
    }
}

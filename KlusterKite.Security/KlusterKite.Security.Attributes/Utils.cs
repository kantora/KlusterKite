// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Utils.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The budle of utils for current library
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Security.Attributes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

#if CORECLR
    using Microsoft.Extensions.DependencyModel;
#endif

    /// <summary>
    /// The bundle of utils for current library
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Gets the defined list of privileges
        /// </summary>
        public static IReadOnlyList<PrivilegeDescription> DefinedPrivileges { get; private set; } = new List<PrivilegeDescription>().AsReadOnly();

        /// <summary>
        /// Gets the list of defined privileges
        /// </summary>
        /// <param name="container">The privilege type container</param>
        /// <returns>The list of privilege descriptions</returns>
        public static IEnumerable<PrivilegeDescription> GetDefinedPrivileges(Type container)
        {
            if (container.GetTypeInfo().GetCustomAttribute(typeof(PrivilegesContainerAttribute), true) == null)
            {
                return Array.Empty<PrivilegeDescription>();
            }

            return SearchDefinedPrivileges(container)
                    .OrderBy(d => d.Privilege)
                    .ThenBy(d => d.Action)
                    .ToArray();
        }

        /// <summary> 
        /// Scans current configuration for defined privileges
        /// </summary>
        public static void CreatePrivilegesCache()
        {
            DefinedPrivileges =
                    GetLoadedAssemblies().SelectMany(
                        a =>
                            {
                                try
                                {
                                    return
                                        a.GetTypes()
                                            .Where(
                                                t =>
                                                    t.GetTypeInfo().IsAbstract && t.GetTypeInfo().IsSealed
                                                    && t.GetTypeInfo().GetCustomAttribute(typeof(PrivilegesContainerAttribute), true)
                                                    != null)
                                            .SelectMany(SearchDefinedPrivileges);
                                }
                                catch 
                                {
                                    // throw new Exception($"Could not get types from module {a.FullName}", e);
                                    return new PrivilegeDescription[0];
                                }
                            })
                    .OrderBy(d => d.AssemblyName)
                    .ThenBy(d => d.Privilege)
                    .ThenBy(d => d.Action)
                    .ToList()
                    .AsReadOnly();
        }

        /// <summary>
        /// Gets the list of loaded assemblies
        /// </summary>
        /// <returns>The list of loaded assemblies</returns>
        public static IEnumerable<Assembly> GetLoadedAssemblies()
        {
#if APPDOMAIN
            return AppDomain.CurrentDomain.GetAssemblies();
#elif CORECLR
            var assemblies = new List<Assembly>();
            var dependencies = DependencyContext.Default.RuntimeLibraries;
            foreach (var library in dependencies)
            {
                try
                {
                    var assembly = Assembly.Load(new AssemblyName(library.Name));
                    assemblies.Add(assembly);
                }
                catch
                {
                    //do nothing can't if can't load assembly
                }
            }
            return assemblies;
#else
#error Method not implemented
            throw new NotImplementedException();
#endif
        }

        /// <summary>
        /// Gets the list of defined privileges
        /// </summary>
        /// <param name="container">The privilege type container</param>
        /// <returns>The list of privilege descriptions</returns>
        private static IEnumerable<PrivilegeDescription> SearchDefinedPrivileges(Type container)
        {
            return
                container.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                    .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                    .SelectMany(
                        f =>
                            {
                                var descriptionAttribute =
                                    (PrivilegeDescriptionAttribute)
                                    f.GetCustomAttribute(typeof(PrivilegeDescriptionAttribute));
                                if (descriptionAttribute == null)
                                {
                                    return new[]
                                               {
                                                   new PrivilegeDescription(
                                                       container.GetTypeInfo().Assembly.GetName().Name,
                                                       null,
                                                       (string)f.GetValue(null))
                                               };
                                }

                                if (descriptionAttribute.Actions != null && descriptionAttribute.Actions.Length > 0)
                                {
                                    return
                                        descriptionAttribute.Actions.Select(
                                            action =>
                                                new PrivilegeDescription(
                                                    container.GetTypeInfo().Assembly.GetName().Name,
                                                    descriptionAttribute.Description,
                                                    $"{f.GetValue(null)}.{action}",
                                                    action,
                                                    descriptionAttribute.Target));
                                }

                                return new[]
                                           {
                                               new PrivilegeDescription(
                                                   container.GetTypeInfo().Assembly.GetName().Name,
                                                   descriptionAttribute.Description,
                                                   (string)f.GetValue(null),
                                                   target: descriptionAttribute.Target)
                                           };
                            });
        } 
    }
}

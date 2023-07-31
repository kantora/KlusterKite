// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AkkaUtils.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Just some helpers
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Core.Utils
{
    using System;
    using System.Linq;

    using Akka.Actor;
    using Akka.Event;
    using Akka.Routing;

    using JetBrains.Annotations;

    /// <summary>
    /// Just some helpers
    /// </summary>
    [UsedImplicitly]
    public static class AkkaUtils
    {
        /// <summary>
        /// The deserialize from akka.
        /// </summary>
        /// <param name="serializedData">
        /// The serialized data.
        /// </param>
        /// <param name="system">
        /// The system.
        /// </param>
        /// <typeparam name="T">
        /// The original object type
        /// </typeparam>
        /// <returns>
        /// The deserialized object
        /// </returns>
        [UsedImplicitly]
        public static T DeserializeFromAkka<T>(this byte[] serializedData, ActorSystem system)
        {
            return (T)system.Serialization.FindSerializerForType(typeof(T)).FromBinary(serializedData, typeof(T));
        }

        /// <summary>
        /// The deserialize from akka string.
        /// </summary>
        /// <param name="serializedData">
        /// The serialized data.
        /// </param>
        /// <param name="system">
        /// The system.
        /// </param>
        /// <typeparam name="T">
        /// The original object type
        /// </typeparam>
        /// <returns>
        /// The deserialized object
        /// </returns>
        [UsedImplicitly]
        public static T DeserializeFromAkkaString<T>(this string serializedData, ActorSystem system)
        {
            return Convert.FromBase64String(serializedData).DeserializeFromAkka<T>(system);
        }

        /// <summary>
        /// Workaround of <seealso href="https://github.com/akkadotnet/akka.net/issues/1321"/>  bug
        /// </summary>
        /// <param name="actorRef">Current actor reference</param>
        /// <param name="system">Akka actor system</param>
        /// <param name="childPath">Path to create router</param>
        /// <returns>Configured router</returns>
        public static RouterConfig GetFromConfiguration(this IActorRef actorRef, ActorSystem system, string childPath)
        {
            // todo: check with new akka
            var actorPath = $"/{string.Join("/", actorRef.Path.Elements.Skip(1))}/{childPath}";
            return GetFromConfiguration(system, actorPath);
        }

        /// <summary>
        /// Workaround of <seealso href="https://github.com/akkadotnet/akka.net/issues/1321"/> bug
        /// </summary>
        /// <param name="system">Akka actor system</param>
        /// <param name="actorPath">Path to child actor in deployment configuration</param>
        /// <returns>Configured router</returns>
        [UsedImplicitly]
        public static RouterConfig GetFromConfiguration(ActorSystem system, string actorPath)
        {
            var childConfig = system.Settings.Config.GetConfig("akka.actor.deployment")?.GetConfig(actorPath);

            if (childConfig == null)
            {
                system.Log.Warning("{Type}: there is no router config for path {ActorPath}", typeof(AkkaUtils).Name, actorPath);
                return NoRouter.Instance;
            }

            string routerName = childConfig.GetString("router");

            // todo: @kantora - realize all router parameters and types
            switch (routerName)
            {
                case "round-robin-pool":
                    system.Log.Info(
                        "{Type}: creating RoundRobinPool router for path {ActorPath}",
                        typeof(AkkaUtils).Name,
                        actorPath);
                    return new RoundRobinPool(childConfig);

                case "consistent-hashing-pool":
                    system.Log.Info(
                        "{Type}: creating ConsistentHashingPool router for path {ActorPath}",
                        typeof(AkkaUtils).Name,
                        actorPath);
                    return new ConsistentHashingPool(childConfig);

                default:
                    return NoRouter.Instance;
            }
        }

        /// <summary>
        /// The serialize to akka.
        /// </summary>
        /// <param name="objToSerialize">
        /// The object to serialize.
        /// </param>
        /// <param name="system">
        /// The system.
        /// </param>
        /// <returns>
        /// The byte array
        /// </returns>
        [UsedImplicitly]
        public static byte[] SerializeToAkka(this object objToSerialize, ActorSystem system)
        {
            return system.Serialization.FindSerializerFor(objToSerialize).ToBinary(objToSerialize);
        }

        /// <summary>
        /// The serialize to akka string.
        /// </summary>
        /// <param name="objToSerialize">
        /// The object to serialize.
        /// </param>
        /// <param name="system">
        /// The system.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        [UsedImplicitly]
        public static string SerializeToAkkaString(this object objToSerialize, ActorSystem system)
        {
            return Convert.ToBase64String(objToSerialize.SerializeToAkka(system));
        }
    }
}
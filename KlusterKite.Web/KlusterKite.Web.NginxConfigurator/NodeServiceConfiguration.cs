// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NodeServiceConfiguration.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Node service description
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.NginxConfigurator
{
    using System.Diagnostics.CodeAnalysis;

    using Akka.Actor;

    using JetBrains.Annotations;

    using KlusterKite.Web.Client.Messages;

    /// <summary>
    /// Node service description
    /// </summary>
    public class NodeServiceConfiguration
    {
        /// <summary>
        /// Gets or sets the node address
        /// </summary>
        [UsedImplicitly]
        public Address NodeAddress { get; set; }

        /// <summary>
        /// Link to node listening server root
        /// </summary>
        public string NodeUrl => $"{this.NodeAddress.Host}:{this.ServiceDescription.ListeningPort}";

        /// <summary>
        /// Gets or sets the local node service description
        /// </summary>
        public ServiceDescription ServiceDescription { get; set; }

        /// <summary>
        /// Compares two <seealso cref="NodeServiceConfiguration"/> for non-equality
        /// </summary>
        /// <param name="left">The left service configuration</param>
        /// <param name="right">The right service configuration</param>
        /// <returns>Whether two <seealso cref="NodeServiceConfiguration"/> are not equal</returns>
        public static bool operator !=(NodeServiceConfiguration left, NodeServiceConfiguration right)
        {
            // ReSharper disable once ArrangeStaticMemberQualifier
            return !NodeServiceConfiguration.Equals(left, right);
        }

        /// <summary>
        /// Compares two <seealso cref="NodeServiceConfiguration"/> for equality
        /// </summary>
        /// <param name="left">The left service configuration</param>
        /// <param name="right">The right service configuration</param>
        /// <returns>Whether two <seealso cref="NodeServiceConfiguration"/> are equal</returns>
        public static bool operator ==(NodeServiceConfiguration left, NodeServiceConfiguration right)
        {
            // ReSharper disable once ArrangeStaticMemberQualifier
            return NodeServiceConfiguration.Equals(left, right);
        }

        /// <summary>Determines whether the specified object is equal to the current object.</summary>
        /// <returns>true if the specified object  is equal to the current object; otherwise, false.</returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((NodeServiceConfiguration)obj);
        }

        /// <summary>Serves as the default hash function. </summary>
        /// <returns>A hash code for the current object.</returns>
        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode", Justification = "Properties are non readonly for serialization purposes")]
        public override int GetHashCode()
        {
            unchecked
            {
                return ((this.NodeAddress?.GetHashCode() ?? 0) * 397) ^ this.ServiceDescription.GetHashCode();
            }
        }

        /// <summary>Determines whether the specified object is equal to the current object.</summary>
        /// <returns>true if the specified object  is equal to the current object; otherwise, false.</returns>
        /// <param name="other">The object to compare with the current object. </param>
        private bool Equals(NodeServiceConfiguration other)
        {
            // ReSharper disable once ArrangeStaticMemberQualifier
            return NodeServiceConfiguration.Equals(this.NodeAddress, other.NodeAddress) && this.ServiceDescription.Equals(other.ServiceDescription);
        }
    }
}
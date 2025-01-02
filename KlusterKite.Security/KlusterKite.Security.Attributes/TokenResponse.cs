// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TokenResponse.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The token response description
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Security.Attributes
{
    using JetBrains.Annotations;

    using Newtonsoft.Json;

    /// <summary> 
    /// The token description
    /// </summary>
    [UsedImplicitly]
    public class TokenResponse
    {
        /// <summary>
        /// Gets or sets the access token value
        /// </summary>
        [JsonProperty("access_token")]
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        /// <summary>
        /// Gets or sets the token type
        /// </summary>
        [JsonProperty("token_type")]
        [System.Text.Json.Serialization.JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        /// <summary>
        /// Gets or sets the token expiration in seconds
        /// </summary>
        [JsonProperty("expires_in")]
        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int? Expires { get; set; }

        /// <summary>
        /// Gets or sets the refresh token value
        /// </summary>
        [JsonProperty("refresh_token")]
        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        /// <summary>
        /// Gets or sets the access scope
        /// </summary>
        [JsonProperty("scope")]
        [System.Text.Json.Serialization.JsonPropertyName("scope")]
        public string Scope { get; set; }

        /// <summary>
        /// Gets or sets the access scope
        /// </summary>
        [JsonProperty("state")]
        [System.Text.Json.Serialization.JsonPropertyName("state")]
        public string State { get; set; }
    }
}

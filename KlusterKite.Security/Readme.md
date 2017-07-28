# KlusterKite.Security

This module provides some first-level abstractions to help build AAA for the application.

## Authentication

This module doesn't provide actual authentication (see [Klusterkite.Web.Authentication](../Klusterkite.Web/Readme.md#Authentication)), it's provides nessesary abstractions for it.

A litle introduction. As authentication and authorization is based on OAuth2 we should have:
* `Client` - some external client application that uses provided API for the user interest or it's own behalf. This entity is represented by [`IClient`](../Docs/Doxygen/html/interface_kluster_kite_1_1_security_1_1_attributes_1_1_i_client.html) interface.
* `User` - the end user of whole service / application (with it's own authentication credentials). The `User` can use multiple `Clients` to access service. The `User` entity is represented by [`IUser`](../Docs/Doxygen/html/interface_kluster_kite_1_1_security_1_1_attributes_1_1_i_user.html) interface.

As KlusterKite service is based on plug-ins system, it is possible that users and clients are provided from various different scopes.
To defines such scope you need to define and register in the DI the [`IClientProvider`](../Docs/Doxygen/html/interface_kluster_kite_1_1_security_1_1_attributes_1_1_i_client.html) implementation. So IClientProvider is responsible for **Client** authentication and then authenticated **Client** authenticates end-user (if needed). The providers are used in the `IClientProvider.Priority` order (`DESC`) to authenticate **Client**. And, of course, you need to provide your own `IUser` implementation for users.

The result of the successfull authentication is authentication tickets (as from oAuth - [`AccessTicket`](../Docs/Doxygen/html/class_kluster_kite_1_1_security_1_1_attributes_1_1_access_ticket.html) and [`RefreshTicket`](../Docs/Doxygen/html/class_kluster_kite_1_1_security_1_1_attributes_1_1_refresh_ticket.html))

These tickets are exchanged for special tokens that are stored on client side. The way of token storage and exchange is not fixed and can be provided by the developer. To provide this, the [`ITokenManager`](../Docs/Doxygen/html/interface_kluster_kite_1_1_security_1_1_attributes_1_1_i_token_manager.html) should be implemented and registered in the DI. Only one such implementation can be used.

KlusterKite provides two predefined systems to work with tickets and tokens:
* `RedisSessionTokenManager` from `KlusterKite.Security.SessionRedis` package that generates `Guid` based tokens and uses `Redis` to store serialized tickets (with tokens as keys).
* `MoqTokenManager` from ` KlusterKite.Security.Attributes` package that just stores tickets in local dictionary and designed to be used in tests

## Authorization

### Privileges
The [`Klusterkite.API`](../Klusterkite.API/Readme.md) and [`KlusterKite.Web.Authorization`](../Klusterkite.Web/Readme.md#Authorization) packages provide utility to restrict access to certain API methods. All of them requires the defined privileges in the *User* and/or *Client* scopes. Privilege is defined by a simple string.

In order to provide the UI with the list of possible privileges it is recommended to define them like this:
```csharp
    /// <summary>
    /// The list of defined module privileges
    /// </summary>
    [PrivilegesContainer]
    public static class Privileges
    {
        /// <summary>
        /// The privilege to get the last cluster scan result
        /// </summary>
        [PrivilegeDescription("Get the last cluster scan result", Target = EnPrivilegeTarget.User)]
        public const string GetClusterTree = "KlusterKite.Monitoring.GetClusterTree";

        /// <summary>
        /// The privilege to initiate the new actor system scan
        /// </summary>
        [PrivilegeDescription("Initiate the new actor system scan", Target = EnPrivilegeTarget.User)]
        public const string InitiateScan = "KlusterKite.Monitoring.InitiateScan";
    }
```

So privilege values can be used as attribute value from the one hand and can be discovered from the other. [`KlusterKite.Security.Attributes.Utils`](../Docs/Doxygen/html/class_kluster_kite_1_1_security_1_1_attributes_1_1_utils.html) can be used later to discover all privileges from all attached plug-ins.

## Accounting

In order to provide logging of security crucial events there is static class [`SecurityLog`](../Docs/Doxygen/html/class_kluster_kite_1_1_security_1_1_client_1_1_security_log.html). It methods performs the standard Serilog writings enriched with `SecurityRecordType` and `SecuritySeverity` properties that can be used in log sinks for proper storage.

Also usually all user requests and user generated messages are provided with [`RequestContext`](../Docs/Doxygen/html/class_kluster_kite_1_1_security_1_1_attributes_1_1_request_context.html) typed properties that should be also put in the log record.

The log storage can be configured in the [`KlusterKite.Log`](../KlusterKite.Log/Readme.md).

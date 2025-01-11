// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestProvider.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Test api provider
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.API.Tests.Mock
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using KlusterKite.API.Attributes;
    using KlusterKite.API.Attributes.Authorization;
    using KlusterKite.API.Client;
    using KlusterKite.API.Client.Converters;
    using KlusterKite.API.Provider;
    using KlusterKite.Security.Attributes;

    using Newtonsoft.Json.Linq;

    using Xunit;

    /// <summary>
    /// Test api provider
    /// </summary>
    [UsedImplicitly]
    [ApiDescription(Description = "Tested API", Name = "TestApi")]
    public class TestProvider : ApiProvider
    {
        /// <summary>
        /// The list of objects
        /// </summary>
        private readonly List<TestObject> objects;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestProvider"/> class.
        /// </summary>
        /// <param name="objects">
        /// The list of pre-stored objects.
        /// </param>
        public TestProvider(List<TestObject> objects = null)
        {
            this.objects = objects ?? new List<TestObject>();
            this.Connection = new TestObjectConnection(this.objects.ToDictionary(o => o.Id));
            this.ListOfNested = new List<NestedProvider>
                                    {
                                        new NestedProvider(this.objects) { Uid = Guid.NewGuid() }
                                    };
        }

        /// <summary>
        /// Test enum
        /// </summary>
        [ApiDescription(Description = "The test flags")]
        [Flags]
        public enum EnFlags
        {
            /// <summary>
            /// Test enum item1
            /// </summary>
            FlagsItem1 = 1,

            /// <summary>
            /// Test enum item2
            /// </summary>
            FlagsItem2 = 2
        }

        /// <summary>
        /// Test enum
        /// </summary>
        [ApiDescription(Description = "The test enum", Name = "EnTest")]
        public enum EnTest
        {
            /// <summary>
            /// Test enum item1
            /// </summary>
            EnumItem1 = 1,

            /// <summary>
            /// Test enum item2
            /// </summary>
            EnumItem2 = 2
        }

        /// <summary>
        /// Gets or sets the list of nested providers
        /// </summary>
        [DeclareField]
        [UsedImplicitly]
        public List<NestedProvider> ListOfNested { get; set; }

        /// <summary>
        /// Async scalar field
        /// </summary>
        [DeclareField]
        [UsedImplicitly]
        public Task<List<decimal>> AsyncArrayOfScalarField => Task.FromResult(new List<decimal> { 4M, 5M });

        /// <summary>
        /// Gets the forwarded scalar property
        /// </summary>
        [DeclareField(ReturnType = typeof(string))]
        [UsedImplicitly]
        public Task<JValue> AsyncForwardedScalar => Task.FromResult(new JValue("AsyncForwardedScalar"));

        /// <summary>
        /// Async nested provider
        /// </summary>
        [DeclareField(Name = "nestedAsync")]
        [UsedImplicitly]
        public Task<NestedProvider> AsyncNestedProvider => Task.FromResult(new NestedProvider(this.objects));

        /// <summary>
        /// Async scalar field
        /// </summary>
        [DeclareField]
        [UsedImplicitly]
        public Task<string> AsyncScalarField => Task.FromResult("AsyncScalarField");

        /// <summary>
        /// Gets or sets the plain list of object without named id
        /// </summary>
        [DeclareField]
        [UsedImplicitly]
        public List<TestObjectNoId> ArrayOfObjectNoIds { get; set; } = new List<TestObjectNoId>
                                                                           {
                                                                               new TestObjectNoId("code1"),
                                                                               new TestObjectNoId("code2")
                                                                           };

        /// <summary>
        /// Gets the test object array
        /// </summary>
        [DeclareField]
        [UsedImplicitly]
        public IEnumerable<TestObject> Collection => this.objects.ToImmutableList();

        /// <summary>
        /// Gets the test objects connection
        /// </summary>
        [DeclareConnection(CanCreate = true, CanDelete = true, CanUpdate = true)]
        [UsedImplicitly]
        public TestObjectConnection Connection { get; }

        /// <summary>
        /// Gets a value indicating whether something is faulting
        /// </summary>
        [DeclareField]
        [UsedImplicitly]
        public bool FaultedSyncField
        {
            get
            {
                throw new Exception("test");
            }
        }

        /// <summary>
        /// Gets the forwarded array of scalars
        /// </summary>
        [DeclareField(ReturnType = typeof(int[]))]
        [UsedImplicitly]
        public JArray ForwardedArray => new JArray(new[] { 5, 6, 7 });

        /// <summary>
        /// Testing circular refs
        /// </summary>
        [DeclareField]
        [UsedImplicitly]
        public TestProvider Recursion => this;

        /// <summary>
        /// Async nested provider
        /// </summary>
        [DeclareField]
        [UsedImplicitly]
        public int[] SyncArrayOfScalarField => new[] { 1, 2, 3 };

        /// <summary>
        /// Sync enum field
        /// </summary>
        [DeclareField]
        [UsedImplicitly]
        public EnTest SyncEnumField => EnTest.EnumItem1;

        /// <summary>
        /// Sync nullable enum field with enum value
        /// </summary>
        [DeclareField]
        [UsedImplicitly]
        public EnTest? SyncEnumNullableField => EnTest.EnumItem1;

        /// <summary>
        /// Sync nullable enum field with null value
        /// </summary>
        [DeclareField]
        [UsedImplicitly]
        public EnTest? SyncEnumNullableNullField => null;

        /// <summary>
        /// Sync enum flags field
        /// </summary>
        [DeclareField]
        [UsedImplicitly]
        public EnFlags SyncFlagsField => EnFlags.FlagsItem1;

        /// <summary>
        /// Sync nested provider
        /// </summary>
        [DeclareField(Name = "nestedSync")]
        [UsedImplicitly]
        public NestedProvider SyncNestedProvider => new NestedProvider(this.objects);

        /// <summary>
        /// Sync scalar field
        /// </summary>
        [DeclareField]
        [UsedImplicitly]
        public string SyncScalarField => "SyncScalarField";

        /// <summary>
        /// Gets the DateTime
        /// </summary>
        [DeclareField]
        [UsedImplicitly]
        public DateTime DateTimeField
        {
            get
            {
                var date = new DateTime(1980, 9, 25, 10, 0, 0, DateTimeKind.Utc);
                //date = TimeZoneInfo.ConvertTime(date, TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time"));
                return date;
            }
        }

        /// <summary>
        /// Gets the DateTimeOffset
        /// </summary>
        [DeclareField]
        [UsedImplicitly]
        public DateTimeOffset DateTimeOffsetField
        {
            get
            {
                var date = new DateTimeOffset(1980, 9, 25, 13, 0, 0, TimeSpan.FromHours(3));
                return date;
            }
        }

        /// <summary>
        /// Gets or sets the list of third party objects
        /// </summary>
        [DeclareField(Converter = typeof(ArrayConverter<StringConverter, string>))]
        [UsedImplicitly]
        public List<ThirdPartyObject> ThirdParties { get; set; }

        /// <summary>
        /// Gets or sets the third party object
        /// </summary>
        [DeclareField(Converter = typeof(StringConverter))]
        [UsedImplicitly]
        public ThirdPartyObject ThirdParty { get; set; } = new ThirdPartyObject { Contents = "Third party" };
        
        /// <summary>
        /// Gets the field that requires a valid authentication session
        /// </summary>
        [UsedImplicitly]
        [DeclareField]
        [RequireSession]
        public string RequireSessionField => "success";

        /// <summary>
        /// Gets the field that requires a valid authentication session
        /// </summary>
        [UsedImplicitly]
        [DeclareField]
        [RequireUser]
        public string RequireUserField => "success";

        /// <summary>
        /// Gets the field that requires a valid authentication session
        /// </summary>
        [UsedImplicitly]
        [DeclareField]
        [RequirePrivilege("allow", Scope = EnPrivilegeScope.Any)]
        public string RequirePrivilegeAnyField => "success";

        /// <summary>
        /// Gets the field that requires a valid authentication session
        /// </summary>
        [UsedImplicitly]
        [DeclareField]
        [RequirePrivilege("allow", Scope = EnPrivilegeScope.Both)]
        public string RequirePrivilegeBothField => "success";

        /// <summary>
        /// Gets the field that requires a valid authentication session
        /// </summary>
        [UsedImplicitly]
        [DeclareField]
        [RequirePrivilege("allow", Scope = EnPrivilegeScope.User)]
        public string RequirePrivilegeUserField => "success";

        /// <summary>
        /// Gets the field that requires a valid authentication session
        /// </summary>
        [UsedImplicitly]
        [DeclareField]
        [RequirePrivilege("allow", Scope = EnPrivilegeScope.Client)]
        public string RequirePrivilegeClientField => "success";

        /// <summary>
        /// Gets the field that requires a valid authentication session
        /// </summary>
        [UsedImplicitly]
        [DeclareField]
        [RequirePrivilege("allow", IgnoreOnUserPresent = true, Scope = EnPrivilegeScope.Client)]
        public string RequirePrivilegeIgnoreOnUserPresentField => "success";

        /// <summary>
        /// Gets the field that requires a valid authentication session
        /// </summary>
        [UsedImplicitly]
        [DeclareField]
        [RequirePrivilege("allow", IgnoreOnUserNotPresent = true)]
        public string RequirePrivilegeIgnoreOnUserNotPresentField => "success";

        /// <summary>
        /// Gets the connection with required authorization
        /// </summary>
        [UsedImplicitly]
        [RequireSession]
        [RequirePrivilege("read", ConnectionActions = EnConnectionAction.Query)]
        [RequirePrivilege("create", ConnectionActions = EnConnectionAction.Create)]
        [RequirePrivilege("update", ConnectionActions = EnConnectionAction.Update)]
        [RequirePrivilege("delete", ConnectionActions = EnConnectionAction.Delete)]
        [DeclareConnection(CanCreate = true, CanUpdate = true, CanDelete = true)]
        public TestObjectConnection AuthorizedConnection => this.Connection;

        /// <summary>
        /// Gets the connection with required authorization
        /// </summary>
        [UsedImplicitly]
        [RequireSession]
        [RequirePrivilege("allow", AddActionNameToRequiredPrivilege = true)]
        [DeclareConnection(CanCreate = true, CanUpdate = true, CanDelete = true)]
        public TestObjectConnection AuthorizedNamedConnection => this.Connection;

        /// <summary>
        /// The list of abstract class entities
        /// </summary>
        [UsedImplicitly]
        [DeclareConnection]
        public List<TestLog> MultipleEndClassArray => new List<TestLog>
                                                          {
                                                              new TestLogFirst { Id = 1, FirstMessage = "first" },
                                                              new TestLogSecond { Id = 2, SecondMessage = "second" }
                                                          };

        /// <summary>
        /// Gets the field that logs its access
        /// </summary>
        [UsedImplicitly]
        [DeclareField]
        [LogAccess(Severity = EnSeverity.Crucial)]
        public string LoggedNoMessageField => "success";

        /// <summary>
        /// Gets the field that logs its access
        /// </summary>
        [UsedImplicitly]
        [DeclareField]
        [LogAccess(Severity = EnSeverity.Crucial, LogMessage = "LoggedWithMessageField accessed")]
        public string LoggedWithMessageField => "success";

        /// <summary>
        /// Gets the connection with required authorization
        /// </summary>
        [UsedImplicitly]
        [LogAccess(LogMessage = "Connection queried", ConnectionActions = EnConnectionAction.Query)]
        [LogAccess(LogMessage = "Connection created", ConnectionActions = EnConnectionAction.Create)]
        [LogAccess(LogMessage = "Connection updated", ConnectionActions = EnConnectionAction.Update)]
        [LogAccess(LogMessage = "Connection deleted", ConnectionActions = EnConnectionAction.Delete)]
        [DeclareConnection(CanCreate = true, CanUpdate = true, CanDelete = true)]
        public TestObjectConnection LoggedConnection => this.Connection;

        /// <summary>
        /// The mutation that requires authorization
        /// </summary>
        /// <returns>Always true</returns>
        [DeclareMutation]
        [UsedImplicitly]
        [LogAccess]
        public string LoggedMutation() => "ok";

        /// <summary>
        /// The mutation that requires authorization
        /// </summary>
        /// <returns>Always true</returns>
        [DeclareMutation]
        [UsedImplicitly]
        [RequireSession]
        [RequirePrivilege("allow")]
        public string AuthorizedMutation() => "ok";

        /// <summary>
        /// The method returning connection
        /// </summary>
        /// <param name="key">
        /// The method argument
        /// </param>
        /// <param name="obj">
        /// The object argument
        /// </param>
        /// <returns>
        /// The connection
        /// </returns>
        [UsedImplicitly]
        [DeclareField]
        public TestObjectConnection ConnectionMethod(string key, TestObject obj)
        {
            return this.Connection;
        }

        /// <summary>
        /// The method with <seealso cref="DateTime"/> argument
        /// </summary>
        /// <param name="date">The date</param>
        /// <returns>Whether specified date is 1980-09-25 10:00 UTC</returns>
        [DeclareField]
        [UsedImplicitly]
        public bool DateTimeMethod(DateTime date)
        {
            return date == new DateTime(1980, 9, 25, 10, 00, 00, DateTimeKind.Utc);
        }

        /// <summary>
        /// The method with <seealso cref="DateTimeOffset"/> argument
        /// </summary>
        /// <param name="date">The date</param>
        /// <returns>Whether specified date is 1980-09-25 10:00 UTC</returns>
        [DeclareField]
        [UsedImplicitly]
        public bool DateTimeOffsetMethod(DateTimeOffset date)
        {
            return date == new DateTimeOffset(1980, 9, 25, 10, 00, 00, TimeSpan.Zero);
        }

        /// <summary>
        /// Some public method
        /// </summary>
        /// <param name="intArg">
        /// Integer parameter
        /// </param>
        /// <param name="stringArg">
        /// String parameter
        /// </param>
        /// <param name="objArg">
        /// Object parameter
        /// </param>
        /// <param name="intArrayArg">
        /// The integer Array parameter.
        /// </param>
        /// <param name="requestContext">
        /// The request context
        /// </param>
        /// <param name="apiRequest">
        /// The sub-request
        /// </param>
        /// <returns>
        /// The test string
        /// </returns>
        [DeclareField]
        [UsedImplicitly]
        public Task<NestedProvider> AsyncObjectMethod(
            int intArg,
            string stringArg,
            NestedProvider objArg,
            int[] intArrayArg,
            RequestContext requestContext,
            ApiRequest apiRequest)
        {
            Assert.Equal(1, intArg);
            Assert.Equal("test", stringArg);
            Assert.NotNull(objArg);
            Assert.Equal("nested test", objArg.SyncScalarField);

            Assert.NotNull(intArrayArg);
            Assert.Equal(3, intArrayArg.Length);
            Assert.Equal(7, intArrayArg[0]);
            Assert.Equal(8, intArrayArg[1]);
            Assert.Equal(9, intArrayArg[2]);

            Assert.NotNull(requestContext);
            Assert.NotNull(apiRequest);
            Assert.Equal("asyncObjectMethod", apiRequest.FieldName);

            return Task.FromResult(new NestedProvider { SyncScalarField = "returned type" });
        }

        /// <summary>
        /// Async method that should be treated as read-only field
        /// </summary>
        /// <param name="requestContext">The request context</param>
        /// <param name="apiRequest">The request data</param>
        /// <returns>The nested provider</returns>
        [DeclareField]
        [UsedImplicitly]
        public Task<NestedProvider> AsyncObjectMethodAsField(RequestContext requestContext, ApiRequest apiRequest)
        {
            return Task.FromResult(new NestedProvider { SyncScalarField = "returned type" });
        }

        /// <summary>
        /// Faulted async method
        /// </summary>
        /// <returns>Faulted task</returns>
        [DeclareField]
        [UsedImplicitly]
        public Task<NestedProvider> FaultedASyncMethod()
        {
            return Task.FromException<NestedProvider>(new Exception("test exception"));
        }

        /// <summary>
        /// Some public method
        /// </summary>
        /// <param name="intArg">Integer parameter</param>
        /// <param name="stringArg">String parameter</param>
        /// <param name="objArg">Object parameter</param>
        /// <param name="requestContext">The request context</param>
        /// <param name="apiRequest">The sub-request</param>
        /// <returns>The test string</returns>
        [DeclareField]
        [UsedImplicitly]
        public string SyncScalarMethod(
            int intArg,
            string stringArg,
            NestedProvider objArg,
            RequestContext requestContext,
            ApiRequest apiRequest)
        {
            Assert.Equal(1, intArg);
            Assert.Equal("test", stringArg);
            Assert.NotNull(objArg);
            Assert.Equal("nested test", objArg.SyncScalarField);

            Assert.NotNull(requestContext);
            Assert.NotNull(apiRequest);
            Assert.Equal("syncScalarMethod", apiRequest.FieldName);

            return "ok";
        }

        /// <summary>
        /// Example of simple trigger mutation
        /// </summary>
        /// <returns>The success of the operation</returns>
        [UsedImplicitly]
        [DeclareMutation]
        public bool BoolMutation() => true;
    }
}
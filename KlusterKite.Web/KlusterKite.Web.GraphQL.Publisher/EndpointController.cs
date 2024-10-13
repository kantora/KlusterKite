// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EndpointController.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   GraphQL endpoint controller
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.GraphQL.Publisher
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using global::GraphQL;
    using global::GraphQL.Server.Transports.AspNetCore;
    using global::GraphQL.Server.Ui.GraphiQL;
    using global::GraphQL.Transport;
    using global::GraphQL.Types;
    using global::GraphQL.Validation;

    using JetBrains.Annotations;

    using KlusterKite.Web.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;

    using Newtonsoft.Json.Linq;

    /// <summary>
    /// GraphQL endpoint controller
    /// </summary>
    [Route("api/1.x/graphQL")]
    public class EndpointController : Controller
    {
        /// <summary>
        /// The executor.
        /// </summary>
        private readonly IDocumentExecuter executor;

        /// <summary>
        /// The schema provider.
        /// </summary>
        private readonly SchemaProvider schemaProvider;

        /// <summary>
        /// The writer.
        /// </summary>
        private readonly IGraphQLTextSerializer writer;

        /// <summary>
        /// Initializes a new instance of the <see cref="EndpointController"/> class.
        /// </summary>
        /// <param name="schemaProvider">
        /// The schema Provider.
        /// </param>
        /// <param name="executor">
        /// The executor.
        /// </param>
        /// <param name="writer">
        /// The writer.
        /// </param>
        public EndpointController(
            SchemaProvider schemaProvider,
                                  IDocumentExecuter executor,
                                  IGraphQLTextSerializer writer)
        {
            this.schemaProvider = schemaProvider;
            this.executor = executor;
            this.writer = writer;
            //var middleware = new GraphQLHttpMiddleware

            /*
            this.complexityConfiguration = new ComplexityConfiguration
                                               {
                                                   MaxDepth =
                                                       config.GetInt(
                                                           "KlusterKite.Web.GraphQL.MaxDepth"),
                                                   FieldImpact = 2.0,
                                                   MaxComplexity =
                                                       config.GetInt(
                                                           "KlusterKite.Web.GraphQL.MaxComplexity")
                                               };
                                               */
        }


        /// <summary>
        /// Processes the GraphQL post request
        /// </summary>
        /// <param name="query">The query data</param>
        /// <returns>GraphQL response</returns>
        [HttpPost]
        [HttpOptions]
        [Route("")]
        public async Task<IActionResult> Post()
        {
            if (HttpContext.Request.HasFormContentType)
            {
                var form = await HttpContext.Request.ReadFormAsync(HttpContext.RequestAborted);
                return await ExecuteGraphQLRequestAsync(BuildRequest(form["query"].ToString(), form["operationName"].ToString(), form["variables"].ToString(), form["extensions"].ToString()));
            }
            else if (HttpContext.Request.HasJsonContentType())
            {
                var request = await this.writer.ReadAsync<GraphQLRequest>(HttpContext.Request.Body, HttpContext.RequestAborted);
                return await ExecuteGraphQLRequestAsync(request);
            }
            return BadRequest();
        }

        private GraphQLRequest BuildRequest(string query, string operationName, string variables = null, string extensions = null)
        {
            return new GraphQLRequest
            {
                Query = query == "" ? null : query,
                OperationName = operationName == "" ? null : operationName,
                Variables = this.writer.Deserialize<Inputs>(variables == "" ? null : variables),
                Extensions = this.writer.Deserialize<Inputs>(extensions == "" ? null : extensions),
            };
        }

        private async Task<IActionResult> ExecuteGraphQLRequestAsync(GraphQLRequest? request)
        {
            try
            {
                var opts = new ExecutionOptions
                {
                    Query = request?.Query,
                    OperationName = request?.OperationName,
                    Variables = request?.Variables,
                    Extensions = request?.Extensions,
                    CancellationToken = HttpContext.RequestAborted,
                    RequestServices = HttpContext.RequestServices,
                    User = HttpContext.User,
                    Schema = this.schemaProvider.CurrentSchema
                };
                IValidationRule rule = HttpMethods.IsGet(HttpContext.Request.Method) ? new HttpGetValidationRule() : new HttpPostValidationRule();
                opts.ValidationRules = DocumentValidator.CoreRules.Append(rule);
                opts.CachedDocumentValidationRules = new[] { rule };
                return new ExecutionResultActionResult(await this.executor.ExecuteAsync(opts));
            }
            catch
            {
                return BadRequest();
            }
        }

        /*
        /// <summary>
        /// Processes the GraphQL post request
        /// </summary>
        /// <param name="query">The query data</param>
        /// <returns>GraphQL response</returns>
        [HttpPost]
        [HttpOptions]
        [Route("")]
        public async Task<IActionResult> Post([FromBody] JObject query)
        {

            var queryToExecute =
                (string)
                (query.Properties()
                         .FirstOrDefault(p => p.Name.Equals("query", StringComparison.OrdinalIgnoreCase))
                         ?.Value as JValue);

            if (string.IsNullOrWhiteSpace(queryToExecute))
            {
                return this.BadRequest();
            }

            var operationName =
                (string)
                (query.Properties()
                     .FirstOrDefault(p => p.Name.Equals("OperationName", StringComparison.OrdinalIgnoreCase))
                     ?.Value as JValue);

            var variablesToken =
                query.Properties()
                    .FirstOrDefault(
                        p => p.Name.Equals("variables", StringComparison.OrdinalIgnoreCase))?.Value;

            Inputs inputs = null;
            if (variablesToken is JObject)
            {
                inputs = variablesToken.ToString().ToInputs();
            }
            else if (variablesToken is JValue)
            {
                inputs = ((JValue)variablesToken).ToObject<string>()?.ToInputs();
            }

            var requestContext = this.GetRequestDescription();
            var schema = this.schemaProvider.CurrentSchema;
            if (schema == null)
            {
                return new StatusCodeResult(503);
            }

            var result = await this.executor.ExecuteAsync(
                             options =>
                                 {
                                     options.Schema = schema;
                                     options.Query = queryToExecute;
                                     options.OperationName = operationName;
                                     options.Inputs = inputs;
                                     options.UserContext = requestContext;

                                     // options.ComplexityConfiguration = this.complexityConfiguration;
                                     // options.FieldMiddleware.Use<InstrumentFieldsMiddleware>();
                                 }).ConfigureAwait(false);

            var json = this.writer.Write(result);
            var contentResult = this.Content(json, "application/json", Encoding.UTF8);

            contentResult.StatusCode = result.Errors?.Count > 0 ? 400 : 200;
            return contentResult;

        }
        //*/

        /// <summary>
        /// The GraphQL http request body description 
        /// </summary>
        public class QueryRequest
        {
            /// <summary>
            /// Gets or sets the operation name
            /// </summary>
            [UsedImplicitly]
            public string OperationName { get; set; }

            /// <summary>
            /// Gets or sets the query
            /// </summary>
            [UsedImplicitly]
            public string Query { get; set; }

            /// <summary>
            /// Gets or sets the list of defined variables
            /// </summary>
            [UsedImplicitly]
            public string Variables { get; set; }
        }
    }
}
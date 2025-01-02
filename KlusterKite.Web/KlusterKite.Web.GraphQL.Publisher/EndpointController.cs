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
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    using global::GraphQL;
    using global::GraphQL.Server.Transports.AspNetCore;
    using global::GraphQL.Server.Ui.GraphiQL;
    using global::GraphQL.Transport;
    using global::GraphQL.Types;
    using global::GraphQL.Validation;

    using JetBrains.Annotations;

    using KlusterKite.Web.Authorization;
    using KlusterKite.Web.GraphQL.Publisher.Internals;
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
                string body;
                using (var sr = new StreamReader(HttpContext.Request.Body))
                {
                    body = await sr.ReadToEndAsync();
                }

                GraphQLRequest request;
                try
                {
                    request = this.writer.Deserialize<GraphQLRequest>(body);
                }
                catch (Newtonsoft.Json.JsonException)
                {
                    return BadRequest("Could not parse request");
                }
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
                var requestContext = this.GetRequestDescription();
                var opts = new ExecutionOptions
                {
                    Query = request?.Query,
                    OperationName = request?.OperationName,
                    Variables = request?.Variables,
                    Extensions = request?.Extensions,
                    CancellationToken = HttpContext.RequestAborted,
                    RequestServices = HttpContext.RequestServices,                   
                    Schema = this.schemaProvider.CurrentSchema,
                    UserContext = requestContext.ToExecutionOptionsUserContext(),
                    //ThrowOnUnhandledException = true,
                };
                IValidationRule rule = HttpMethods.IsGet(HttpContext.Request.Method) ? new HttpGetValidationRule() : new HttpPostValidationRule();
                opts.ValidationRules = DocumentValidator.CoreRules.Append(rule);
                opts.CachedDocumentValidationRules = [rule];
                var result = await this.executor.ExecuteAsync(opts);
                if (!result.Executed && result.Errors.Count == 1 && result.Errors.First().Code == "INVALID_OPERATION")
                {
                    return this.Problem(result.Errors.First().Message, statusCode: (int)HttpStatusCode.ServiceUnavailable);
                }
                return new ExecutionResultActionResult(result);
            }
            catch
            {
                return BadRequest();
            }
        }


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
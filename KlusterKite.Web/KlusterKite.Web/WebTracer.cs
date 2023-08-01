// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WebTracer.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Debug configuration
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web
{
    using System;
    using System.Diagnostics;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Akka.Actor;
    using Akka.Event;
    using Akka.Configuration;

    using JetBrains.Annotations;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Debug configuration
    /// </summary>
    [UsedImplicitly]
    public class WebTracer : BaseWebHostingConfigurator
    {
        /// <inheritdoc />
        public override IApplicationBuilder ConfigureApplication(IApplicationBuilder app, Config config)
        {
            app = app.UseMiddleware<TraceMiddleware>();
            return app;
        }

        /// <summary>
        /// Request process logging. Used to catch request loss
        /// </summary>
        [UsedImplicitly]
        public class TraceMiddleware
        {
            /// <summary>
            /// The current request number
            /// </summary>
            private static long requestNumber;

            /// <summary>
            /// A value indicating whether all requests should be logged
            /// </summary>
            private readonly bool traceRequests;

            /// <summary>
            /// The next request processor in the request processing pipeline
            /// </summary>
            private readonly RequestDelegate next;

            /// <summary>
            /// The actor system
            /// </summary>
            private readonly ActorSystem system;

            /// <summary>
            /// Initializes a new instance of the <see cref="TraceMiddleware"/> class.
            /// </summary>
            /// <param name="next">
            /// The next.
            /// </param>
            /// <param name="system">
            /// The system.
            /// </param>
            public TraceMiddleware(RequestDelegate next, ActorSystem system)
            {
                this.next = next;
                this.system = system;
                this.traceRequests = system.Settings.Config.GetBoolean("KlusterKite.Web.Debug.Trace");
            }

            /// <summary>Process an individual request.</summary>
            /// <param name="context">The request context</param>
            /// <returns>The async process task</returns>
            [UsedImplicitly]
            public async Task Invoke(HttpContext context)
            {
                if (this.traceRequests)
                {
                    var n = Interlocked.Increment(ref requestNumber);
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    this.system.Log.Info(
                        "{Type}: started Request {RequestNumber} {RequestPath}",
                        this.GetType().Name,
                        n,
                        context.Request.Path);

                    await this.ProcessRequest(context);

                    this.system.Log.Info(
                        "{Type}: finished Request {RequestNumber} {RequestPath} in {ElapsedMilliseconds}ms",
                        this.GetType().Name,
                        n,
                        context.Request.Path,
                        stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    await this.ProcessRequest(context);
                }
            }

            /// <summary>Process an individual request.</summary>
            /// <param name="context">The request context</param>
            /// <returns>The async process task</returns>
            private async Task ProcessRequest(HttpContext context)
            {
                try
                {
                    await this.next.Invoke(context);
                }
                catch (ReflectionTypeLoadException exception)
                {
                    this.system.Log.Error(
                        exception,
                        "{Type}: error resolving {RequestPath}",
                        this.GetType().Name,
                        context.Request.Path);
                    foreach (var loaderException in exception.LoaderExceptions)
                    {
                        this.system.Log.Error(
                            loaderException,
                            "{Type}: loader exception",
                            this.GetType().Name);
                    }

                    throw;
                }
                catch (Exception exception)
                {
                    this.system.Log.Error(
                        exception,
                        "{Type}: error resolving {RequestPath}",
                        this.GetType().Name,
                        context.Request.Path);
                    throw;
                }
            }
        }
    }
}
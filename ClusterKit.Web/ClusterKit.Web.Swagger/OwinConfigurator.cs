﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="OwinConfigurator.cs" company="ClusterKit">
//   All rights reserved
// </copyright>
// <summary>
//   External additional owin configuration.
//   Should be registered in DI resolver
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ClusterKit.Web.Swagger
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Web.Http;

    using Akka.Configuration;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Practices.ServiceLocation;
    using Serilog;

    using Swashbuckle.Application;

    /// <summary>
    /// External additional configuration.
    /// Should be registered in DI resolver
    /// </summary>
    public class OwinConfigurator : BaseWebHostingConfigurator
    {
        /// <summary>
        /// Add additional http configuration
        /// </summary>
        /// <param name="config">The configuration</param>
        public void ConfigureApi(HttpConfiguration config)
        {
            /*
            var akkaConfig = ServiceLocator.Current.GetInstance<Config>();
            if (akkaConfig == null)
            {
                Log.Error("{Type}: akka config is null", this.GetType().FullName);
                return;
            }

            var publishDocUrl = akkaConfig.GetString("ClusterKit.Web.Swagger.Publish.publishDocPath", "swagger");
            var publishUiUrl = akkaConfig.GetString("ClusterKit.Web.Swagger.Publish.publishUiPath", "swagger/ui");

            Log.Information("Swagger: setting up swagger on {SwaggerUrl}", publishUiUrl);
            config.EnableSwagger(
                $"{publishDocUrl}/{{apiVersion}}",
                c =>
                    {
                        c.SingleApiVersion(
                            akkaConfig.GetString("ClusterKit.Web.Swagger.Publish.apiVersion", "v1"),
                            akkaConfig.GetString("ClusterKit.Web.Swagger.Publish.apiTitle", "Cluster API"));
                        var commentFiles = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory);
                        foreach (
                            var commentFile in
                                commentFiles.Where(
                                    cf =>
                                    ".xml".Equals(
                                        Path.GetExtension(Path.GetFileName(cf)),
                                        StringComparison.InvariantCultureIgnoreCase)))
                        {
                            c.IncludeXmlComments(Path.GetFullPath(commentFile));
                        }

                        c.UseFullTypeNameInSchemaIds();
                    }).EnableSwaggerUi(
                        $"{publishUiUrl}/{{*assetPath}}",
                        c =>
                            {
                                c.DisableValidator();
                            });

            Log.Information("Swagger was set up successfully");
            */
        }

        /// <inheritdoc />
        public IWebHostBuilder ConfigureApp(IWebHostBuilder hostBuilder)
        {
            return hostBuilder;
        }

        /// <inheritdoc />
        public void Configure(IApplicationBuilder app)
        {
            // todo: enable swagger
            //app.
        }
    }
}
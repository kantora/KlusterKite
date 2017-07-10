﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MockResourceSeeder.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Seeds the mock resources
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.Mock
{
    using System;

    using Akka.Configuration;

    using KlusterKite.NodeManager.Migrator;

    /// <summary>
    /// Seeds the mock resources
    /// </summary>
    public class MockResourceSeeder : BaseSeeder
    {
        /// <summary>
        /// The migrator config
        /// </summary>
        private readonly Config config;

        /// <summary>
        /// Initializes a new instance of the <see cref="MockResourceSeeder"/> class.
        /// </summary>
        /// <param name="config">
        /// The config.
        /// </param>
        public MockResourceSeeder(Config config)
        {
            this.config = config;
        }

        /// <inheritdoc />
        public override void Seed()
        {
            Console.WriteLine(@"MockResourceSeeder: started");
            var migrator = new MockResourceMigrator(this.config);
            foreach (var resource in migrator.GetMigratableResources())
            {
                Console.WriteLine($@"MockResourceSeeder: migrating {resource.Code}");
                foreach (var log in migrator.Migrate(resource, migrator.LatestPoint))
                {
                    Console.WriteLine(log);
                }
            }
        }
    }
}

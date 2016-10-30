﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ContextFactoryTest.cs" company="ClusterKit">
//   All rights reserved
// </copyright>
// <summary>
//   Tests <see cref="BaseContextFactory{TContext,TMigrationConfiguration}" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ClusterKit.Data.Tests
{
    using System.Data.Common;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;

    using ClusterKit.Data.EF;

    using JetBrains.Annotations;

    using Xunit;

    /// <summary>
    /// Tests <see cref="BaseContextFactory{TContext,TMigrationConfiguration}"/>
    /// </summary>
    public class ContextFactoryTest
    {
        /// <summary>
        /// Tests that <see cref="BaseContextFactory{TContext,TMigrationConfiguration}"/> can create contexts
        /// </summary>
        [Fact]
        public void CreatorTest()
        {
            var creator = BaseContextFactory<TestContext, TestContextMigrationConfiguration>.Creator;
            Assert.NotNull(creator);

            var context = creator(null, true);
            Assert.NotNull(context);
        }

        /// <summary>
        /// Test valid context
        /// </summary>
        [UsedImplicitly]
        private class TestContext : DbContext
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="TestContext"/> class.
            /// </summary>
            /// <param name="existingConnection">
            /// The existing connection.
            /// </param>
            /// <param name="contextOwnsConnection">
            /// The context owns connection.
            /// </param>
            public TestContext(DbConnection existingConnection, bool contextOwnsConnection = true)
            {
            }
        }

        /// <summary>
        /// Test context migration
        /// </summary>
        private class TestContextMigrationConfiguration : DbMigrationsConfiguration<TestContext>
        {
        }
    }
}
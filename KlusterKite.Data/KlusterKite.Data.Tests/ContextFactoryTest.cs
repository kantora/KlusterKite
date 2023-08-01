// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ContextFactoryTest.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Tests <see cref="BaseContextFactory{TContext,TMigrationConfiguration}" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Data.Tests
{
    using KlusterKite.Data.EF;
    using KlusterKite.Data.EF.InMemory;
    using KlusterKite.Data.Tests.Mock;
    using Xunit;

    /// <summary>
    /// Tests <see cref="BaseContextFactory"/>
    /// </summary>
    public class ContextFactoryTest
    {
        /// <summary>
        /// Tests that <see cref="BaseContextFactory"/> can create contexts
        /// </summary>
        [Fact]
        public void CreatorTest()
        {
            var contextFactory = new InMemoryContextFactory();
            var context = contextFactory.CreateContext<TestDataContext>(null, "testDB");
            Assert.NotNull(context);
        }
    }
}
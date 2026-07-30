using Fixturely.IntegrationTests.Infrastructure;
using Xunit;

namespace Fixturely.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationTestWebAppFactory>
{
    public const string Name = "Fixturely Integration Tests";
}

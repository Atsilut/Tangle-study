using System.Net;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public abstract class GroupIntegrationMatrixTestBase(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    protected Task<GroupIntegrationScenario> CreateScenarioAsync(string prefix) =>
        GroupIntegrationScenario.CreateAsync(Client, Factory, prefix);

    protected static HttpStatusCode OutcomeStatus(GroupExpectedOutcome expected) => expected switch
    {
        GroupExpectedOutcome.NotFound => HttpStatusCode.NotFound,
        GroupExpectedOutcome.Unauthorized => HttpStatusCode.Unauthorized,
        GroupExpectedOutcome.ArgumentException => HttpStatusCode.BadRequest,
        _ => throw new ArgumentOutOfRangeException(nameof(expected), expected, null),
    };
}

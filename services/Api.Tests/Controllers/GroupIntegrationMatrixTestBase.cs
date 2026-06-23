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

    /// <summary>2-1 target missing via <see cref="EntityNotFoundException"/> (e.g. group/member checks).</summary>
    protected static Task AssertGroupNotFoundAsync(HttpResponseMessage res) =>
        IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.NotFound, "Group not found");
}

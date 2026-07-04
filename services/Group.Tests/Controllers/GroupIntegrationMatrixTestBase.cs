using System.Net;
using Group.Tests.Infrastructure;

namespace Group.Tests.Controllers;

[Collection(GroupIntegrationTestCollection.Name)]
public abstract class GroupIntegrationMatrixTestBase(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    protected GroupIntegrationScenario CreateScenario(string prefix) =>
        GroupIntegrationScenario.Create(Client, Factory, prefix);

    protected static HttpStatusCode OutcomeStatus(GroupExpectedOutcome expected) => expected switch
    {
        GroupExpectedOutcome.NotFound => HttpStatusCode.NotFound,
        GroupExpectedOutcome.Unauthorized => HttpStatusCode.Unauthorized,
        GroupExpectedOutcome.ArgumentException => HttpStatusCode.BadRequest,
        _ => throw new ArgumentOutOfRangeException(nameof(expected), expected, null),
    };

    protected static Task AssertGroupNotFoundAsync(HttpResponseMessage res) =>
        IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.NotFound, "Group not found");
}

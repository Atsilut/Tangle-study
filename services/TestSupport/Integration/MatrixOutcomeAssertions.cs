using System.Net;

namespace Tangle.TestSupport.Integration;

public static class MatrixOutcomeAssertions
{
    public static HttpStatusCode ToStatusCode<TEnum>(TEnum expected) where TEnum : struct, Enum =>
        expected.ToString() switch
        {
            "Ok" => HttpStatusCode.OK,
            "Unauthorized" => HttpStatusCode.Unauthorized,
            "NotFound" => HttpStatusCode.NotFound,
            "BadRequest" or "ArgumentException" => HttpStatusCode.BadRequest,
            "Conflict" => HttpStatusCode.Conflict,
            _ => throw new ArgumentOutOfRangeException(nameof(expected), expected, null),
        };
}

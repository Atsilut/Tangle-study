namespace Tangle.TestSupport.Integration;

public static class IntegrationTestConstants
{
    public const string TestJwtSecret = TestWebHostConfiguration.JwtSecret;
    public const string TestJwtIssuer = TestWebHostConfiguration.JwtIssuer;
    public const string TestJwtAudience = TestWebHostConfiguration.JwtAudience;
    public const string TestWorkerCallbackSecret = "test-media-worker-secret";
    public const string TestBlobConnectionString =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";
}

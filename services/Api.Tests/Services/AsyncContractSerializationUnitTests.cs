using System.Text.Json;
using Api.Global.Events;
using Api.Global.Queue;

namespace Api.Tests.Services;

public class AsyncContractSerializationUnitTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ChatMessageCreatedJob_SerializesSchemaVersion()
    {
        var job = new ChatMessageCreatedJob(1, 2, 3, "hi", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"));

        var json = JsonSerializer.Serialize(job, SerializerOptions);

        Assert.Contains("\"schemaVersion\":1", json);
    }

    [Fact]
    public void MediaUploadedJob_SerializesSchemaVersion()
    {
        var job = new MediaUploadedJob(9, "Post", "Image", "image/jpeg", "raw/1/a.jpg", 500, 2_147_483_648);

        var json = JsonSerializer.Serialize(job, SerializerOptions);

        Assert.Contains("\"schemaVersion\":1", json);
    }

    [Fact]
    public void LocationClusterJob_SerializesSchemaVersion()
    {
        var job = new LocationClusterJob(37m, 38m, 126m, 127m, 3);

        var json = JsonSerializer.Serialize(job, SerializerOptions);

        Assert.Contains("\"schemaVersion\":1", json);
    }

    [Fact]
    public void ChatMessageCreatedEvent_SerializesSchemaVersion()
    {
        var evt = new ChatMessageCreatedEvent(1, 2, 3, "hi", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"));

        var json = JsonSerializer.Serialize(evt, SerializerOptions);

        Assert.Contains("\"schemaVersion\":1", json);
    }

    [Fact]
    public void UserNicknameChangedEvent_SerializesSchemaVersion()
    {
        var evt = new UserNicknameChangedEvent(5, "alice", IsDeleted: false, DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(evt, SerializerOptions);

        Assert.Contains("\"schemaVersion\":1", json);
    }
}

namespace Stack.Tests.Harness;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MediaHarnessTestCollection : ICollectionFixture<MediaHarnessTestCollection>
{
    public const string Name = "MediaHarness";
}

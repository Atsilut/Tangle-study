namespace Tangle.TestSupport.Harness;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HarnessTestCollection : ICollectionFixture<HarnessTestCollection>
{
    public const string Name = "HarnessTests";
}

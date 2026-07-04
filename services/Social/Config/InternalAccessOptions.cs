namespace Social.Config;

public class InternalAccessOptions
{
    public const string SectionName = "InternalAccess";

    public string Secret { get; set; } = string.Empty;
}

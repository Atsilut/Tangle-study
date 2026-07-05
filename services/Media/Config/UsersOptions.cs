namespace Media.Config;

public class UsersOptions
{
    public const string SectionName = "Users";

    public string BaseUrl { get; set; } = string.Empty;

    public string InternalSecret { get; set; } = string.Empty;
}

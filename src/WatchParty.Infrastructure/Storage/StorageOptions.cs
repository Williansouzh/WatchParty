namespace WatchParty.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string RootPath { get; init; } = "data/media";
}

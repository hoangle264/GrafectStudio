namespace GrafcetStudio.CodeGen.Template;

/// <summary>Represents a named template source entry.</summary>
public class TemplateEntry
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string UploadName { get; init; } = string.Empty;

    public string StorageKey { get; init; } = string.Empty;

    public string? PartialName { get; init; }

    public string? CacheKey { get; init; }

    public string Description { get; init; } = string.Empty;

    public int Order { get; init; }

    public IList<string> AcceptAliases { get; init; } = new List<string>();

    public string Content { get; init; } = string.Empty;

    public string Source => Content;
}

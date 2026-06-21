using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Events;

public class GenerateCodePayload
{
    public string DevPath { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string TemplatePath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
}

public class AiRequestPayload
{
    public string Type { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string DiagramContext { get; set; } = string.Empty;
    public string RequestJson { get; set; } = string.Empty;
    public string FixtureName { get; set; } = string.Empty;
    public bool Stream { get; set; }
}

public class ExportCodePayload
{
    public List<CodegenFile> Files { get; set; } = new();
    public string Platform { get; set; } = string.Empty;
}

public class BrowseCodegenPathPayload
{
    public string Target { get; set; } = string.Empty;
}

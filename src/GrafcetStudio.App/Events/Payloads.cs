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

public class AiAgentStartPayload
{
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    /// <summary>Recent chat as a JSON array of {role, text}; empty for a new conversation.</summary>
    public string HistoryJson { get; set; } = string.Empty;
}

public class AiAgentToolResultPayload
{
    public string SessionId { get; set; } = string.Empty;
    public string CallId { get; set; } = string.Empty;
    public string ResultJson { get; set; } = string.Empty;
}

public class AiSettingsPayload
{
    /// <summary>"get", "save", "clear", "list", "grants-get", "grant" or "revoke".</summary>
    public string Action { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    /// <summary>Comma separated context scopes, used by "grant".</summary>
    public string Scopes { get; set; } = string.Empty;
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

public class ReadTemplateFilePayload
{
    public string RequestId { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
}

public class PreviewTemplatePayload
{
    public string RequestId { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
}

public class SetTemplateRootPayload
{
    public string Path { get; set; } = string.Empty;
}

public class ListTemplateFilesPayload
{
    public string RequestId { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
}

public class ValidateTemplatePayload
{
    public string RequestId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class WriteTemplateFilePayload
{
    public string RequestId { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class PushSiemensLadPayload
{
    public string DevPath { get; set; } = string.Empty;
    public string Platform { get; set; } = "siemens-lad";
    public string TemplatePath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string PlcName { get; set; } = string.Empty;
    public string TargetFolderPath { get; set; } = string.Empty;
    public string OverwriteMode { get; set; } = string.Empty;
    public string TiaVersion { get; set; } = string.Empty;
}

namespace GrafcetStudio.App.Events;

public class GenerateCodePayload
{
    public string Platform { get; set; } = string.Empty;
    public string Steps { get; set; } = string.Empty;
    public string Transitions { get; set; } = string.Empty;
    public string Actions { get; set; } = string.Empty;
    public string Variables { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
}

public class AiRequestPayload
{
    public string Type { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string DiagramContext { get; set; } = string.Empty;
}

public class ExportCodePayload
{
    public string Code { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
}

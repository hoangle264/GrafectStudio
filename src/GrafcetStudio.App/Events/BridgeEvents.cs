using Prism.Events;

namespace GrafcetStudio.App.Events;

public class GenerateCodeRequestedEvent : PubSubEvent<GenerateCodePayload>
{
}

public class AiRequestedEvent : PubSubEvent<AiRequestPayload>
{
}

public class AiAgentStartRequestedEvent : PubSubEvent<AiAgentStartPayload>
{
}

public class AiAgentToolResultEvent : PubSubEvent<AiAgentToolResultPayload>
{
}

public class AiSettingsRequestedEvent : PubSubEvent<AiSettingsPayload>
{
}

public class SaveFileRequestedEvent : PubSubEvent<string>
{
}

public class OpenFileRequestedEvent : PubSubEvent<object>
{
}

public class ExportCodeRequestedEvent : PubSubEvent<ExportCodePayload>
{
}

public class BrowseCodegenPathRequestedEvent : PubSubEvent<BrowseCodegenPathPayload>
{
}

public class ReadTemplateFileRequestedEvent : PubSubEvent<ReadTemplateFilePayload>
{
}

public class ListTemplateVariablesRequestedEvent : PubSubEvent<PreviewTemplatePayload>
{
}

public class PreviewTemplateRequestedEvent : PubSubEvent<PreviewTemplatePayload>
{
}

public class SetTemplateRootRequestedEvent : PubSubEvent<SetTemplateRootPayload>
{
}

public class ListTemplateFilesRequestedEvent : PubSubEvent<ListTemplateFilesPayload>
{
}

public class ValidateTemplateRequestedEvent : PubSubEvent<ValidateTemplatePayload>
{
}

public class WriteTemplateFileRequestedEvent : PubSubEvent<WriteTemplateFilePayload>
{
}

public class PushSiemensLadRequestedEvent : PubSubEvent<PushSiemensLadPayload>
{
}

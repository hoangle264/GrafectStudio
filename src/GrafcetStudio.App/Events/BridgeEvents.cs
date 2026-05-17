using Prism.Events;

namespace GrafcetStudio.App.Events;

public class GenerateCodeRequestedEvent : PubSubEvent<GenerateCodePayload>
{
}

public class AiRequestedEvent : PubSubEvent<AiRequestPayload>
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

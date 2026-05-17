using GrafcetStudio.App.Events;
using Prism.Events;
using System.Threading.Tasks;

namespace GrafcetStudio.App.Services;

public class MockAiService
{
    private readonly IWebViewBridgeService _webViewBridgeService;

    public MockAiService(IEventAggregator eventAggregator, IWebViewBridgeService webViewBridgeService)
    {
        _webViewBridgeService = webViewBridgeService;
        eventAggregator.GetEvent<AiRequestedEvent>().Subscribe(async payload => await HandleAiRequestAsync(payload));
    }

    private async Task HandleAiRequestAsync(AiRequestPayload payload)
    {
        for (var i = 1; i <= 5; i++)
        {
            await Task.Delay(100);
            await _webViewBridgeService.SendAiChunkAsync($"[MOCK chunk {i}] Processing: {payload.Prompt}");
        }

        await _webViewBridgeService.SendAiChunkAsync("__STREAM_END__");
    }
}

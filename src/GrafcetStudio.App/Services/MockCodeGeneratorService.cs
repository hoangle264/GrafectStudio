using GrafcetStudio.App.Events;
using Prism.Events;
using System.Threading.Tasks;

namespace GrafcetStudio.App.Services;

public class MockCodeGeneratorService
{
    private readonly IWebViewBridgeService _webViewBridgeService;

    public MockCodeGeneratorService(IEventAggregator eventAggregator, IWebViewBridgeService webViewBridgeService)
    {
        _webViewBridgeService = webViewBridgeService;
        eventAggregator.GetEvent<GenerateCodeRequestedEvent>().Subscribe(async payload => await HandleGenerateCodeAsync(payload));
    }

    private async Task HandleGenerateCodeAsync(GenerateCodePayload payload)
    {
        await Task.Delay(500);
        await _webViewBridgeService.SendGeneratedCodeAsync($"// [MOCK] Generated code for platform: {payload.Platform}\nLD X1\nOUT Y1");
    }
}

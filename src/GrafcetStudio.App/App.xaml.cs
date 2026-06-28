using GrafcetStudio.App.Generators;
using GrafcetStudio.App.Generators.Siemens;
using GrafcetStudio.App.Services.Ai;
using GrafcetStudio.App.Services.Siemens;
using GrafcetStudio.App.Services;
using GrafcetStudio.CodeGen.Profile;
using GrafcetStudio.CodeGen.Template;
using GrafcetStudio.Domain.Resolution;
using HandlebarsDotNet;
using Prism.DryIoc;
using Prism.Ioc;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Windows;

namespace GrafcetStudio.App;

public partial class App : PrismApplication
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Any(arg => string.Equals(arg, "--validate-ai-mock", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                Console.WriteLine("AI mock validation starting.");
                Task.Run(RunAiMockValidationAsync).GetAwaiter().GetResult();
                Environment.Exit(0);
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.Message);
                Environment.Exit(1);
            }

            return;
        }

        base.OnStartup(e);
    }

    protected override IContainerExtension CreateContainerExtension() => new DryIocContainerExtension();

    protected override Window CreateShell() => Container.Resolve<MainWindow>();

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IWebViewBridgeService, WebViewBridgeService>();
        containerRegistry.RegisterSingleton<IFileService, FileService>();
        RegisterSiemensTiaServices(containerRegistry);
        containerRegistry.RegisterSingleton<ISequenceResolver, SequenceResolver>();
        containerRegistry.RegisterSingleton<ICodeGenerator, KeyenceMnemonicGenerator>();
        containerRegistry.RegisterInstance<ICodeGenerator>(new ProfiledMnemonicGenerator(ProfileRegistry.Kv8000.Id));
        containerRegistry.RegisterInstance<ICodeGenerator>(new ProfiledMnemonicGenerator(ProfileRegistry.Melsec.Id));
        containerRegistry.RegisterInstance<ICodeGenerator>(new ProfiledMnemonicGenerator(ProfileRegistry.Omron.Id));
        containerRegistry.RegisterInstance<ICodeGenerator>(new ProfiledMnemonicGenerator(ProfileRegistry.Siemens.Id));
        containerRegistry.RegisterSingleton<ICodeGenerator, SiemensLadDslGenerator>();
        containerRegistry.RegisterSingleton<ICodeGenerator, RuntimePlanGenerator>();
        containerRegistry.RegisterSingleton<ICodeGenerator, TwinCatStGenerator>();
        containerRegistry.RegisterSingleton<UnitConfigGenerator>();
        containerRegistry.RegisterSingleton<ISystemControlGenerator, SystemControlGenerator>();
        containerRegistry.RegisterSingleton<ICodeGenerator, MultiFileGenerator>();
        containerRegistry.RegisterSingleton<IMapIOGenerator, MapIOGenerator>();
        containerRegistry.RegisterSingleton<IErrorGenerator, ErrorGenerator>();
        containerRegistry.RegisterSingleton<IDeviceManagerGenerator, DeviceManagerGenerator>();
        containerRegistry.RegisterSingleton<ICodeGeneratorService, CodeGeneratorService>();
        containerRegistry.RegisterInstance<IHandlebars>(Handlebars.Create());
        containerRegistry.RegisterSingleton<TemplateManager>();
        containerRegistry.RegisterSingleton<ConfigService>();
        containerRegistry.RegisterSingleton<CodeGenerationOrchestrator>();
        containerRegistry.RegisterSingleton<FileIOOrchestrator>();
        containerRegistry.RegisterSingleton<SiemensTiaPushOrchestrator>();
        RegisterAiServices(containerRegistry);
        containerRegistry.RegisterSingleton<AiRequestOrchestrator>();

        Container.Resolve<CodeGenerationOrchestrator>().Init();
        Container.Resolve<FileIOOrchestrator>().Init();
        Container.Resolve<SiemensTiaPushOrchestrator>().Init();
        Container.Resolve<AiRequestOrchestrator>();
    }

    private static async Task RunAiMockValidationAsync()
    {
        var requestJson = JsonSerializer.Serialize(new
        {
            schemaVersion = AiContractGuard.SchemaVersion,
            id = "ai-req-host-mock-validation",
            intent = "create-variable",
            message = "Create a safe mock validation variable.",
            context = new { variables = new { user = Array.Empty<object>(), imported = Array.Empty<object>() } }
        });

        var sanitized = AiContractGuard.SanitizeRequestJson(requestJson);
        if (!sanitized.Ok || sanitized.Request is null)
        {
            throw new InvalidOperationException("AI mock validation request failed sanitization: " + string.Join("; ", sanitized.Errors));
        }

        var service = new MockAiCompletionService();
        var result = await service.CompleteAsync(new AiCompletionRequest(sanitized.Request));
        if (!result.Ok || !result.RawText.Contains("\"intent\":\"create-variable\"", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("AI mock validation did not produce a create-variable proposal.");
        }

        await ValidateMockStreamAsync(service, sanitized.Request, null, shouldProduceFinal: true);
        await ValidateMockStreamAsync(service, sanitized.Request, "partial-json", shouldProduceFinal: true);
        await ValidateMockStreamAsync(service, sanitized.Request, "malformed-json", shouldProduceFinal: true);
        await ValidateMockStreamFailureAsync(service, sanitized.Request, "stream-cancel", TimeSpan.FromSeconds(2));
        await ValidateMockStreamFailureAsync(service, sanitized.Request, "stream-error", TimeSpan.FromSeconds(2));
        await ValidateMockStreamFailureAsync(service, sanitized.Request, "stream-timeout", TimeSpan.FromMilliseconds(50));

        Console.WriteLine("AI mock validation passed.");
    }


    private static async Task ValidateMockStreamAsync(MockAiCompletionService service, SanitizedAiRequest request, string? fixtureName, bool shouldProduceFinal)
    {
        var finalText = string.Empty;
        var deltaCount = 0;
        await foreach (var chunk in service.StreamAsync(new AiCompletionRequest(request, fixtureName)))
        {
            if (chunk.Kind == "delta") deltaCount++;
            if (chunk.IsFinal) finalText = chunk.Text;
        }

        if (shouldProduceFinal && (string.IsNullOrWhiteSpace(finalText) || deltaCount == 0))
        {
            throw new InvalidOperationException("AI mock streaming validation did not produce partial chunks and a final payload for fixture: " + (fixtureName ?? "default"));
        }
    }

    private static async Task ValidateMockStreamFailureAsync(MockAiCompletionService service, SanitizedAiRequest request, string fixtureName, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            await foreach (var _ in service.StreamAsync(new AiCompletionRequest(request, fixtureName), cancellation.Token))
            {
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException("AI mock streaming failure fixture did not fail: " + fixtureName);
    }

    private static void RegisterSiemensTiaServices(IContainerRegistry containerRegistry)
    {
        var mode = Environment.GetEnvironmentVariable(BridgeSiemensTiaProjectService.ImportModeEnvironmentVariable)?.Trim();
        if (string.Equals(mode, "bridge", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(mode))
        {
            containerRegistry.RegisterInstance<ISiemensTiaProjectService>(new BridgeSiemensTiaProjectService());
            return;
        }

        if (string.Equals(mode, "reflection", StringComparison.OrdinalIgnoreCase))
        {
            containerRegistry.RegisterInstance<ISiemensTiaProjectService>(new ReflectionSiemensTiaProjectService());
            return;
        }

        containerRegistry.RegisterSingleton<ISiemensTiaProjectService, UnavailableSiemensTiaProjectService>();
    }
    private static void RegisterAiServices(IContainerRegistry containerRegistry)
    {
        var mode = Environment.GetEnvironmentVariable("GRAFCETSTUDIO_AI_MODE")?.Trim();
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")?.Trim()
            ?? Environment.GetEnvironmentVariable("GRAFCETSTUDIO_GEMINI_API_KEY")?.Trim();
        var model = Environment.GetEnvironmentVariable("GRAFCETSTUDIO_GEMINI_MODEL")?.Trim();

        if (string.Equals(mode, "gemini", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(apiKey))
        {
            containerRegistry.RegisterInstance<IAiCompletionService>(new GeminiAiCompletionService(new HttpClient(), apiKey, model));
            return;
        }

        containerRegistry.RegisterSingleton<IAiCompletionService, MockAiCompletionService>();
    }
}



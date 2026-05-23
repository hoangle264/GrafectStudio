using GrafcetStudio.App.Generators;
using GrafcetStudio.App.Services;
using Prism.DryIoc;
using Prism.Ioc;
using System.Windows;

namespace GrafcetStudio.App;

public partial class App : PrismApplication
{
    protected override IContainerExtension CreateContainerExtension() => new DryIocContainerExtension();

    protected override Window CreateShell() => Container.Resolve<MainWindow>();

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IWebViewBridgeService, WebViewBridgeService>();
        containerRegistry.RegisterSingleton<IFileService, FileService>();
        containerRegistry.RegisterSingleton<ICodeGenerator, KeyenceMnemonicGenerator>();
        containerRegistry.RegisterSingleton<ICodeGenerator, TwinCatStGenerator>();
        containerRegistry.RegisterSingleton<ICodeGeneratorService, CodeGeneratorService>();
        containerRegistry.RegisterSingleton<ConfigService>();
        containerRegistry.RegisterSingleton<CodeGenerationOrchestrator>();
        containerRegistry.RegisterSingleton<FileIOOrchestrator>();
        containerRegistry.RegisterSingleton<MockAiService>();

        Container.Resolve<CodeGenerationOrchestrator>().Init();
        Container.Resolve<FileIOOrchestrator>().Init();
        Container.Resolve<MockAiService>();
    }
}

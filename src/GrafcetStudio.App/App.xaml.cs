using GrafcetStudio.App.Generators;
using GrafcetStudio.App.Services;
using GrafcetStudio.CodeGen.Profile;
using GrafcetStudio.CodeGen.Template;
using GrafcetStudio.Domain.Resolution;
using HandlebarsDotNet;
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
        containerRegistry.RegisterSingleton<ISequenceResolver, SequenceResolver>();
        containerRegistry.RegisterSingleton<ICodeGenerator, KeyenceMnemonicGenerator>();
        containerRegistry.RegisterInstance<ICodeGenerator>(new ProfiledMnemonicGenerator(ProfileRegistry.Kv8000.Id));
        containerRegistry.RegisterInstance<ICodeGenerator>(new ProfiledMnemonicGenerator(ProfileRegistry.Melsec.Id));
        containerRegistry.RegisterInstance<ICodeGenerator>(new ProfiledMnemonicGenerator(ProfileRegistry.Omron.Id));
        containerRegistry.RegisterInstance<ICodeGenerator>(new ProfiledMnemonicGenerator(ProfileRegistry.Siemens.Id));
        containerRegistry.RegisterSingleton<ICodeGenerator, RuntimePlanGenerator>();
        containerRegistry.RegisterSingleton<ICodeGenerator, TwinCatStGenerator>();
        containerRegistry.RegisterSingleton<ICodeGenerator, UnitConfigGenerator>();
        containerRegistry.RegisterSingleton<ICodeGeneratorService, CodeGeneratorService>();
        containerRegistry.RegisterInstance<IHandlebars>(Handlebars.Create());
        containerRegistry.RegisterSingleton<TemplateManager>();
        containerRegistry.RegisterSingleton<ConfigService>();
        containerRegistry.RegisterSingleton<CodeGenerationOrchestrator>();
        containerRegistry.RegisterSingleton<FileIOOrchestrator>();
        containerRegistry.RegisterSingleton<MockAiService>();

        Container.Resolve<CodeGenerationOrchestrator>().Init();
        Container.Resolve<FileIOOrchestrator>().Init();
        Container.Resolve<MockAiService>();
    }
}



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
    }
}

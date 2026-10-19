using System;
using System.Threading;
using System.Threading.Tasks;

namespace GrafcetStudio.App.Services.Siemens;

public sealed class UnavailableSiemensTiaProjectService : ISiemensTiaProjectService
{
    public const string DefaultMessage = "TIA push/import is not configured. GrafcetStudio can still generate Siemens Openness XML for manual import, but no TIA adapter mode is enabled. Set GRAFCETSTUDIO_TIA_IMPORT_MODE=bridge or reflection and configure the corresponding bridge/runtime before using direct push.";

    public Task<SiemensPushResult> PushBlockXmlAsync(SiemensPushRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(SiemensPushResult.Failure(
            SiemensTiaProjectServiceStatus.NotConfigured,
            DefaultMessage,
            request));
    }
}


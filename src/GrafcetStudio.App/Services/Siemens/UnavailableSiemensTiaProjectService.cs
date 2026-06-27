using System;
using System.Threading;
using System.Threading.Tasks;

namespace GrafcetStudio.App.Services.Siemens;

public sealed class UnavailableSiemensTiaProjectService : ISiemensTiaProjectService
{
    public const string DefaultMessage = "TIA Openness push/import is not configured. GrafcetStudio generated Siemens Openness XML successfully, but this build does not include a TIA Openness adapter. Install/configure a Siemens TIA project service implementation via reflection or an external bridge before using direct push.";

    public Task<SiemensPushResult> PushBlockXmlAsync(SiemensPushRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(SiemensPushResult.Failure(
            SiemensTiaProjectServiceStatus.TiaOpennessUnavailable,
            DefaultMessage,
            request));
    }
}

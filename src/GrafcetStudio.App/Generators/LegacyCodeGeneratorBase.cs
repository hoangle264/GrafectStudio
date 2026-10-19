using System.Collections.Generic;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Generators;

/// <summary>
/// Adapter base for legacy generators that still render a single text blob.
/// </summary>
public abstract class LegacyCodeGeneratorBase : ICodeGenerator
{
    public abstract string Platform { get; }

    public IEnumerable<CodegenFile> GenerateFiles(CodegenPayload payload)
    {
        yield return new CodegenFile
        {
            Path = GetDefaultPath(payload),
            Content = GenerateLegacy(payload)
        };
    }

    protected virtual string GetDefaultPath(CodegenPayload payload) => $"{Platform}.st";

    protected abstract string GenerateLegacy(CodegenPayload payload);
}

using System.Collections.Generic;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Generators;

/// <summary>
/// Contract for a platform-specific code generator.
/// Historically generators returned a single string. The multi-file refactor
/// returns a collection of CodegenFile entries so the host and UI can preview
/// each generated file independently.
/// </summary>
public interface ICodeGenerator
{
    string Platform { get; }
    IEnumerable<CodegenFile> GenerateFiles(CodegenPayload payload);
}

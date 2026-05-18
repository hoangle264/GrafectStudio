using System.Collections.Generic;
using GrafcetStudio.CodeGen.Models;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.CodeGen;

/// <summary>Defines code generation contract for diagrams and output section.</summary>
public interface ICodeGenerator
{
    GenerationResult GenerateAll(IList<string> diagIds, GenerationOptions opts);

    DiagramResult GenerateDiagram(DiagramMeta meta, DiagramState state, GenerationOptions opts);

    IList<string> GenerateOutputSection(IList<DiagramEntry> entries, IDictionary<string, IList<SignalActionEntry>> signalActionMap);
}

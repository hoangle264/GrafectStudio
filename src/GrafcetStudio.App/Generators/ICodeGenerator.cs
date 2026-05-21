using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Generators;

public interface ICodeGenerator
{
    string Platform { get; }
    string Generate(CodegenPayload payload);
}

using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Generators;

public interface ICodeGeneratorService
{
    CodegenOutput Generate(string platform, CodegenPayload data);
}

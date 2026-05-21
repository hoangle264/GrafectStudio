using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.App.Generators;

public interface ICodeGeneratorService
{
    string Generate(string platform, CodegenPayload data);
}

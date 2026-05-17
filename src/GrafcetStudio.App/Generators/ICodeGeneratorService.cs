using GrafcetStudio.App.Models;

namespace GrafcetStudio.App.Generators;

public interface ICodeGeneratorService
{
    string Generate(string platform, CodegenPayload data);
}

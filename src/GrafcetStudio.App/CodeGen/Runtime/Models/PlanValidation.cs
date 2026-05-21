namespace GrafcetStudio.CodeGen.Runtime.Models;

public class PlanValidation
{
    public string Status { get; init; } = "ok";

    public IList<string> Errors { get; init; } = new List<string>();

    public IList<string> Warnings { get; init; } = new List<string>();
}

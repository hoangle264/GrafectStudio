using System.Collections.Generic;

namespace GrafcetStudio.CodeGen;

/// <summary>Defines rendering parameters for one ST step block.</summary>
public class StStepRenderParams
{
    public string StepNum { get; init; } = string.Empty;

    public string ExecAddress { get; init; } = string.Empty;

    public string DoneAddress { get; init; } = string.Empty;

    public IList<string> PrevDoneVars { get; init; } = new List<string>();

    public string? ActivationCond { get; init; }

    public string? FeedbackCond { get; init; }

    public string? StepLabel { get; init; }
}

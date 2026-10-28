using GrafcetStudio.CodeGen.Models;
using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.Domain.Enums;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.CodeGen.Runtime;

public static class DeviceCommandResolver
{
    public static ActionResolveResult? Resolve(
        StepAction action,
        string stepExecAddress,
        IList<DeviceVariable> variables,
        DeviceLibraryRoot library)
    {
        var separatorIndex = action.Variable.IndexOf('.');
        if (separatorIndex <= 0 || separatorIndex == action.Variable.Length - 1) return null;

        var deviceLabel = action.Variable[..separatorIndex];
        var commandId = action.Variable[(separatorIndex + 1)..];
        var deviceVar = variables.FirstOrDefault(variable => string.Equals(variable.Label, deviceLabel, StringComparison.OrdinalIgnoreCase));
        if (deviceVar is null) return null;

        if (!library.ById.TryGetValue(deviceVar.Format, out var entry)) return null;
        if (!TryResolveCommand(entry, commandId, out var command)) return null;
        var diagnostics = new List<Diagnostic>();
        var driveAddr = ResolveSignalRef(deviceVar, command.DriveSignal, action.Variable, diagnostics);
        var feedbackSignals = new List<FeedbackSignalResult>();
        var interlockAddress = string.Empty;
        if (command.Complete is not null)
        {
            feedbackSignals.Add(new FeedbackSignalResult
            {
                PhysicalAddress = ResolveSignalRef(deviceVar, command.Complete.Sensor, action.Variable, diagnostics),
                SignalName = command.Complete.Sensor,
                Label = command.Complete.SensorLabel
            });
        }

        if (command.Interlock is not null && !string.IsNullOrWhiteSpace(command.Interlock.Signal))
        {
            interlockAddress = ResolveSignalRef(deviceVar, command.Interlock.Signal, action.Variable, diagnostics);
        }

        return new ActionResolveResult
        {
            Status = "ok",
            ActionSymbol = action.Variable,
            Qualifier = action.Qualifier.ToString(),
            Execute = new ExecuteSignalResult { PhysicalAddress = driveAddr, SignalName = command.DriveSignal },
            FeedbackSignals = feedbackSignals,
            OutputBindings = new List<OutputBinding>
            {
                new()
                {
                    PhysicalOutputRef = driveAddr,
                    SourceExecuteBitRef = stepExecAddress,
                    ActionSymbol = action.Variable,
                    Qualifier = action.Qualifier.ToString(),
                    DeviceLabel = deviceLabel,
                    DeviceFormat = deviceVar.Format,
                    CommandId = commandId,
                    ActionLabel = command.ActionLabel,
                    DriveSignal = command.DriveSignal,
                    InterlockSignal = command.Interlock?.Signal ?? string.Empty,
                    InterlockAddress = interlockAddress,
                    InterlockLabel = command.Interlock?.Label ?? string.Empty,
                    InterlockRequiredState = command.Interlock?.RequiredState ?? string.Empty,
                    FeedbackSignals = feedbackSignals.ToList()
                }
            },
            Diagnostics = diagnostics
        };
    }

    private static bool TryResolveCommand(DeviceLibraryEntry entry, string commandId, out DeviceCommand command)
    {
        if (entry.Commands.TryGetValue(commandId, out command!)) return true;

        var matched = entry.Commands.Values.FirstOrDefault(candidate =>
            string.Equals(candidate.DriveSignal, commandId, StringComparison.OrdinalIgnoreCase));
        if (matched is not null)
        {
            command = matched;
            return true;
        }

        command = null!;
        return false;
    }

    /// <summary>
    /// Returns the signal's address, or falls back to its label <c>{Device}.{Signal}</c>
    /// (recording an info diagnostic) when the signal has no address assigned.
    /// </summary>
    private static string ResolveSignalRef(DeviceVariable variable, string signalName, string actionSymbol, List<Diagnostic> diagnostics)
    {
        if (TryGetSignalAddress(variable, signalName, out var address)) return address;

        var label = variable.GetSignalAddressOrLabel(signalName);
        diagnostics.Add(new Diagnostic
        {
            Level = DiagnosticLevel.Info,
            Code = "DEVICE_SIGNAL_LABEL_FALLBACK",
            Message = $"Signal '{signalName}' for action '{actionSymbol}' has no address; using label '{label}'."
        });
        return label;
    }

    public static bool TryGetSignalAddress(DeviceVariable variable, string signalName, out string address)
    {
        if (variable.SignalAddresses.TryGetValue(signalName, out var found) && !string.IsNullOrWhiteSpace(found))
        {
            address = found;
            return true;
        }

        address = string.Empty;
        return false;
    }
}


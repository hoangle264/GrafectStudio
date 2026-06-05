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
        if (!TryGetSignalAddress(deviceVar, command.DriveSignal, out var driveAddr)) return null;

        var diagnostics = new List<Diagnostic>();
        var feedbackSignals = new List<FeedbackSignalResult>();
        if (command.Complete is not null)
        {
            if (TryGetSignalAddress(deviceVar, command.Complete.Sensor, out var feedbackAddr))
            {
                feedbackSignals.Add(new FeedbackSignalResult
                {
                    PhysicalAddress = feedbackAddr,
                    SignalName = command.Complete.Sensor,
                    Label = command.Complete.SensorLabel
                });
            }
            else
            {
                diagnostics.Add(new Diagnostic
                {
                    Level = DiagnosticLevel.Warning,
                    Code = "DEVICE_COMMAND_FEEDBACK_MISSING",
                    Message = $"Feedback signal '{command.Complete.Sensor}' for action '{action.Variable}' was not found."
                });
            }
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

    private static bool TryGetSignalAddress(DeviceVariable variable, string signalName, out string address)
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


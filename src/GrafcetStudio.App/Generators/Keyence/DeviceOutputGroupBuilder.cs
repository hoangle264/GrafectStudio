using GrafcetStudio.CodeGen.Runtime;
using GrafcetStudio.CodeGen.Runtime.Models;
using GrafcetStudio.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafcetStudio.App.Generators.Keyence;

internal static class DeviceOutputGroupBuilder
{
    private readonly record struct FlattenedSourceItem(AggregatedOutputBinding Binding, OutputBindingSource Source);

    public static IList<DeviceOutputGroup> BuildDeviceOutputGroups(
        IList<AggregatedOutputBinding> mergedBindings,
        IList<DeviceVariable> variables,
        IList<DeviceType> deviceTypes,
        IDictionary<string, string> unitAddresses)
    {
        var variablesByLabel = variables.ToDictionaryIgnoreCase(variable => variable.Label);
        var deviceTypesByName = deviceTypes.ToDictionaryIgnoreCase(deviceType => deviceType.Name);
        var flattenedSources = mergedBindings
            .SelectMany(binding => binding.Sources.Select(source => new FlattenedSourceItem(binding, source)))
            .Where(item => !string.IsNullOrWhiteSpace(item.Source.DeviceLabel))
            .ToList();

        return flattenedSources
            .GroupBy(item => item.Source.DeviceLabel, StringComparer.OrdinalIgnoreCase)
            .Select(deviceGroup => BuildDeviceOutputGroup(deviceGroup, variablesByLabel, deviceTypesByName, unitAddresses, variables))
            .OrderBy(group => group.DeviceLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static DeviceOutputGroup BuildDeviceOutputGroup(
        IGrouping<string, FlattenedSourceItem> deviceGroup,
        IDictionary<string, DeviceVariable> variablesByLabel,
        IDictionary<string, DeviceType> deviceTypesByName,
        IDictionary<string, string> unitAddresses,
        IList<DeviceVariable> variables)
    {
        var firstSource = deviceGroup.First().Source;
        variablesByLabel.TryGetValue(deviceGroup.Key, out var variable);
        deviceTypesByName.TryGetValue(firstSource.DeviceFormat, out var deviceType);
        var deviceKind = Common.GeneratorContextBuilder.NormalizeDeviceKind(firstSource.DeviceFormat);

        var signals = variable is null || deviceType is null
            ? new List<object>()
            : deviceType.Signals.Select(signal => new
            {
                name = signal.Name,
                dataType = signal.DataType,
                varType = signal.VarType.ToString(),
                comment = signal.Comment,
                address = variable.GetSignalAddress(signal.Id) ?? variable.GetSignalAddress(signal.Name)
            }).Cast<object>().ToList();

        var commands = deviceGroup
            .Where(item => !string.IsNullOrWhiteSpace(item.Source.CommandId))
            .GroupBy(item => item.Source.CommandId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(commandGroup => commandGroup.Key, StringComparer.OrdinalIgnoreCase)
            .Select((commandGroup, commandIndex) => BuildDeviceCommandOutput(commandGroup, unitAddresses))
            .ToList();

        return new DeviceOutputGroup
        {
            DeviceLabel = deviceGroup.Key,
            DeviceFormat = firstSource.DeviceFormat,
            DeviceKind = deviceKind,
            Address = variable?.Address,
            SignalAddresses = variable?.SignalAddresses ?? new Dictionary<string, string>(),
            UnitAddresses = new Dictionary<string, string>(unitAddresses, StringComparer.OrdinalIgnoreCase),
            Signals = signals,
            Commands = commands
        };
    }

    private static DeviceCommandOutput BuildDeviceCommandOutput(
        IGrouping<string, FlattenedSourceItem> commandGroup,
        IDictionary<string, string> unitAddresses)
    {
        var commandSource = commandGroup.First().Source;
        var commandBindings = commandGroup.Select(item => item.Binding).ToList();
        var aggregationMode = commandGroup
            .Select(item => item.Binding.AggregationMode)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "OR";
        var physicalOutputRef = commandGroup
            .Select(item => item.Binding.PhysicalOutputRef)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        var interlockSignal = commandGroup
            .Select(item => item.Source.InterlockSignal)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        var interlockAddress = commandGroup
            .Select(item => item.Source.InterlockAddress)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        var interlockLabel = commandGroup
            .Select(item => item.Source.InterlockLabel)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        var interlockRequiredState = commandGroup
            .Select(item => item.Source.InterlockRequiredState)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

        var flowCommands = BuildCommandFlowOutputs(commandGroup.Select(item => item.Source), unitAddresses);
        var originCommandCount = flowCommands.Count(command => command.IsOrigin);
        var autoCommandCount = flowCommands.Count(command => command.IsAuto);
        var interlockExpression = BuildInterlockExpression(interlockAddress, interlockRequiredState);

        var feedbackSignals = commandGroup
            .SelectMany(item => item.Source.FeedbackSignals)
            .GroupBy(signal => $"{signal.SignalName}\u001F{signal.PhysicalAddress}", StringComparer.OrdinalIgnoreCase)
            .Select(signalGroup => signalGroup.First())
            .ToList();

        return new DeviceCommandOutput
        {
            CommandId = commandSource.CommandId,
            ActionLabel = commandSource.ActionLabel,
            DriveSignal = commandSource.DriveSignal,
            InterlockSignal = interlockSignal,
            InterlockAddress = interlockAddress,
            InterlockLabel = interlockLabel,
            InterlockRequiredState = interlockRequiredState,
            HasInterlock = !string.IsNullOrWhiteSpace(interlockExpression),
            PhysicalOutputRef = physicalOutputRef,
            AggregationMode = aggregationMode,
            SourceSteps = commandBindings
                .SelectMany(binding => binding.SourceSteps)
                .NotEmpty()
                .DistinctIgnoreCase()
                .ToList(),
            SourceExecuteBitRefs = commandBindings
                .SelectMany(binding => binding.SourceExecuteBitRefs)
                .NotEmpty()
                .DistinctIgnoreCase()
                .ToList(),
            SourceDoneBitRefs = commandBindings
                .SelectMany(binding => binding.SourceDoneBitRefs)
                .NotEmpty()
                .DistinctIgnoreCase()
                .ToList(),
            FlowCommands = flowCommands,
            FlowCommandCount = flowCommands.Count,
            OriginCommandCount = originCommandCount,
            AutoCommandCount = autoCommandCount,
            HasOriginCommands = originCommandCount > 0,
            HasAutoCommands = autoCommandCount > 0,
            FeedbackSignals = feedbackSignals
        };
    }

    public static DeviceVariable? FindUnitVariable(IList<DeviceVariable> variables, string unitLabel)
    {
        if (string.IsNullOrWhiteSpace(unitLabel)) return null;

        return variables.FirstOrDefault(variable => string.Equals(variable.Label, unitLabel, StringComparison.OrdinalIgnoreCase))
            ?? variables.FirstOrDefault(variable => variable.Label.Contains(unitLabel, StringComparison.OrdinalIgnoreCase));
    }


    private static IList<DeviceCommandFlowOutput> BuildCommandFlowOutputs(
        IEnumerable<OutputBindingSource> sources,
        IDictionary<string, string> unitAddresses)
    {
        var commands = sources
            .Where(source => !string.IsNullOrWhiteSpace(source.SourceExecuteBitRef) || !string.IsNullOrWhiteSpace(source.SourceDoneBitRef))
            .GroupBy(source => $"{source.FlowType}\u001F{source.FlowId}\u001F{source.FlowName}\u001F{source.CommandId}\u001F{source.ActionLabel}\u001F{source.SourceStep}\u001F{source.SourceExecuteBitRef}\u001F{source.SourceDoneBitRef}\u001F{source.ActionSymbol}\u001F{source.Qualifier}", StringComparer.OrdinalIgnoreCase)
            .Select(commandGroup =>
            {
                var source = commandGroup.First();
                var flowType = string.Equals(source.FlowType, "origin", StringComparison.OrdinalIgnoreCase) ? "origin" : "auto";
                var modeFlagAddress = ResolveModeFlagAddress(flowType, unitAddresses);

                return new DeviceCommandFlowOutput
                {
                    Id = source.FlowId,
                    Name = source.FlowName,
                    FlowType = flowType,
                    IsOrigin = string.Equals(flowType, "origin", StringComparison.OrdinalIgnoreCase),
                    IsAuto = string.Equals(flowType, "auto", StringComparison.OrdinalIgnoreCase),
                    CommandId = source.CommandId ?? string.Empty,
                    ActionLabel = source.ActionLabel ?? string.Empty,
                    SourceStep = source.SourceStep ?? string.Empty,
                    SourceExecuteBit = source.SourceExecuteBitRef ?? string.Empty,
                    SourceDoneBit = source.SourceDoneBitRef ?? string.Empty,
                    actionSymbol = source.ActionSymbol ?? string.Empty,
                    qualifier = source.Qualifier ?? string.Empty,
                    modeFlagAddress = modeFlagAddress
                };
            })
            .OrderBy(item => item.IsAuto)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.CommandId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.SourceExecuteBit, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return commands
            .Select((command, index) => new DeviceCommandFlowOutput
            {
                Id = command.Id,
                Name = command.Name,
                FlowType = command.FlowType,
                IsOrigin = command.IsOrigin,
                IsAuto = command.IsAuto,
                CommandId = command.CommandId,
                ActionLabel = command.ActionLabel,
                SourceStep = command.SourceStep,
                SourceExecuteBit = command.SourceExecuteBit,
                SourceDoneBit = command.SourceDoneBit,
                actionSymbol = command.actionSymbol,
                qualifier = command.qualifier,
                modeFlagAddress = command.modeFlagAddress,
                Index = index,
                Number = index + 1,
                TotalCount = commands.Count,
                IsFirst = index == 0,
                IsLast = index == commands.Count - 1,
                IsSingle = commands.Count == 1
            })
            .ToList();
    }

    private static string BuildInterlockExpression(string? interlockAddress, string? requiredState)
    {
        if (string.IsNullOrWhiteSpace(interlockAddress)) return string.Empty;

        return ExpressionHelper.IsFalseState((requiredState ?? string.Empty).Trim())
            ? ExpressionHelper.NegateExpression(interlockAddress)
            : interlockAddress.Trim();
    }

    private static string ResolveModeFlagAddress(string flowType, IDictionary<string, string> unitAddresses)
    {
        var key = string.Equals(flowType, "origin", StringComparison.OrdinalIgnoreCase)
            ? "flagOrigin"
            : string.Equals(flowType, "manual", StringComparison.OrdinalIgnoreCase)
                ? "flagManual"
                : "flagAuto";

        return TryGetAddress(unitAddresses, key, out var address) ? address : string.Empty;
    }

    private static bool TryGetAddress(IDictionary<string, string> addresses, string key, out string address)
    {
        if (addresses.TryGetValue(key, out address!) && !string.IsNullOrWhiteSpace(address))
        {
            address = address.Trim();
            return true;
        }

        var match = addresses.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase));
        address = match.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(address)) return false;

        address = address.Trim();
        return true;
    }


    public static IList<AggregatedOutputBinding> MergeOutputBindings(IEnumerable<AggregatedOutputBinding> bindings)
    {
        return bindings
            .GroupBy(binding => binding.PhysicalOutputRef, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AggregatedOutputBinding
            {
                PhysicalOutputRef = group.Key,
                SourceExecuteBitRefs = group
                    .SelectMany(binding => binding.SourceExecuteBitRefs)
                    .NotEmpty()
                    .DistinctIgnoreCase()
                    .ToList(),
                SourceDoneBitRefs = group
                    .SelectMany(binding => binding.SourceDoneBitRefs)
                    .NotEmpty()
                    .DistinctIgnoreCase()
                    .ToList(),
                SourceSteps = group
                    .SelectMany(binding => binding.SourceSteps)
                    .NotEmpty()
                    .DistinctIgnoreCase()
                    .ToList(),
                AggregationMode = group.Select(binding => binding.AggregationMode).FirstOrDefault(mode => !string.IsNullOrWhiteSpace(mode)) ?? "OR",
                Sources = group
                    .SelectMany(binding => binding.Sources)
                    .GroupBy(source => new
                    {
                        Flow = source.FlowId.ToUpperInvariant(),
                        Type = source.FlowType.ToUpperInvariant(),
                        Source = source.SourceExecuteBitRef.ToUpperInvariant(),
                        Done = source.SourceDoneBitRef.ToUpperInvariant(),
                        Action = source.ActionSymbol.ToUpperInvariant(),
                        Command = source.CommandId.ToUpperInvariant(),
                        Interlock = source.InterlockSignal.ToUpperInvariant()
                    })
                    .Select(sourceGroup => sourceGroup.First())
                    .ToList()
            })
            .OrderBy(binding => binding.PhysicalOutputRef, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

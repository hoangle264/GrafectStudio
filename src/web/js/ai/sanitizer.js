"use strict";
var GrafcetStudioAISanitizer;
(function (GrafcetStudioAISanitizer) {
    const scopes = ['variable', 'unit', 'diagram', 'step', 'io', 'structure'];
    const defaultMaxItems = 200;
    const intentScopeMap = {
        'create-variable': ['variable', 'unit', 'diagram'],
        'clone-variable': ['variable', 'unit', 'diagram'],
        'map-io': ['variable', 'io'],
        'create-flow': ['variable', 'unit', 'diagram', 'step', 'io'],
        'create-structure': ['structure']
    };
    function isRecord(value) {
        return !!value && typeof value === 'object' && !Array.isArray(value);
    }
    function isString(value) {
        return typeof value === 'string';
    }
    function isFiniteNumber(value) {
        return typeof value === 'number' && Number.isFinite(value);
    }
    function getRecordField(source, key) {
        const value = source[key];
        return isRecord(value) ? value : null;
    }
    function arrayFrom(value) {
        return Array.isArray(value) ? value : [];
    }
    function normalizeMaxItems(maxItems) {
        if (isFiniteNumber(maxItems) && maxItems > 0)
            return Math.floor(maxItems);
        return defaultMaxItems;
    }
    function hasPathLikeValue(value) {
        return /[A-Za-z]:[\\/]/.test(value)
            || /\\\\[^\\/]+[\\/][^\\/]+/.test(value)
            || /(^|\s)\/(Users|home|var|etc|tmp|mnt|Volumes|config|templates|src|bin|obj)(\/|\b)/i.test(value)
            || /(^|\s|['"])[A-Za-z0-9_.-]+[\\/][A-Za-z0-9_.-]+/.test(value);
    }
    function hasSecretLikeValue(value) {
        return /(api[_-]?key|secret|password|passwd|token|bearer\s+[A-Za-z0-9._-]+|AIza[0-9A-Za-z_-]{10,}|sk-[A-Za-z0-9_-]{10,}|ghp_[A-Za-z0-9_]{10,})/i.test(value);
    }
    function safeString(value) {
        if (!isString(value))
            return undefined;
        const trimmed = value.trim();
        if (!trimmed)
            return undefined;
        if (hasPathLikeValue(trimmed) || hasSecretLikeValue(trimmed))
            return undefined;
        return trimmed;
    }
    function safeNullableString(value) {
        if (value === undefined)
            return undefined;
        if (value === null)
            return null;
        return safeString(value);
    }
    function assignString(target, key, value) {
        const sanitized = safeString(value);
        if (sanitized !== undefined)
            target[key] = sanitized;
    }
    function assignNullableString(target, key, value) {
        const sanitized = safeNullableString(value);
        if (sanitized !== undefined)
            target[key] = sanitized;
    }
    function assignNumber(target, key, value) {
        if (isFiniteNumber(value))
            target[key] = value;
    }
    function assignBoolean(target, key, value) {
        if (typeof value === 'boolean')
            target[key] = value;
    }
    function assignStringArray(target, key, value) {
        if (!Array.isArray(value))
            return;
        const result = value.map(safeString).filter(function (item) { return item !== undefined; });
        if (result.length)
            target[key] = result;
    }
    function sanitizeStringRecord(value) {
        if (!isRecord(value))
            return undefined;
        const result = {};
        Object.keys(value).forEach(function (key) {
            const safeKey = safeString(key);
            const safeValue = safeString(value[key]);
            if (safeKey !== undefined && safeValue !== undefined)
                result[safeKey] = safeValue;
        });
        return Object.keys(result).length ? result : undefined;
    }
    function take(items, maxItems) {
        return items.slice(0, normalizeMaxItems(maxItems));
    }
    function rootRecord(raw) {
        return isRecord(raw) ? raw : {};
    }
    function projectRecord(raw) {
        const root = rootRecord(raw);
        return getRecordField(root, 'project') || root;
    }
    function sanitizeSelection(rawSelection) {
        if (!isRecord(rawSelection))
            return undefined;
        const result = {};
        assignString(result, 'unitId', rawSelection.unitId);
        assignString(result, 'diagramId', rawSelection.diagramId);
        assignString(result, 'variableLabel', rawSelection.variableLabel);
        assignString(result, 'physicalIOId', rawSelection.physicalIOId);
        return Object.keys(result).length ? result : undefined;
    }
    function sanitizeProjectVariable(raw) {
        if (!isRecord(raw))
            return null;
        const result = {};
        assignString(result, 'id', raw.id);
        assignString(result, 'label', raw.label);
        assignString(result, 'format', raw.format);
        assignNullableString(result, 'address', raw.address);
        const signalAddresses = sanitizeStringRecord(raw.signalAddresses);
        if (signalAddresses)
            result.signalAddresses = signalAddresses;
        assignString(result, 'kind', raw.kind);
        assignString(result, 'dataType', raw.dataType);
        assignString(result, 'source', raw.source || raw._source);
        if (!result.label || !result.format)
            return null;
        return result;
    }
    function variableSources(raw) {
        const root = rootRecord(raw);
        if (Array.isArray(raw))
            return raw;
        if (Array.isArray(root.variables))
            return root.variables;
        const variables = getRecordField(root, 'variables') || getRecordField(projectRecord(raw), 'variables');
        if (!variables)
            return isRecord(raw) && raw.label ? [raw] : [];
        return arrayFrom(variables.imported).concat(arrayFrom(variables.user));
    }
    function sanitizeVariables(raw, maxItems) {
        const result = [];
        take(variableSources(raw), maxItems).forEach(function (item) {
            const sanitized = sanitizeProjectVariable(item);
            if (sanitized)
                result.push(sanitized);
        });
        return result;
    }
    GrafcetStudioAISanitizer.sanitizeVariables = sanitizeVariables;
    function sanitizeUnit(raw) {
        if (!isRecord(raw))
            return null;
        const result = {};
        assignString(result, 'id', raw.id);
        assignString(result, 'name', raw.name);
        assignString(result, 'label', raw.label);
        if (!result.id && !result.name && !result.label)
            return null;
        return result;
    }
    GrafcetStudioAISanitizer.sanitizeUnit = sanitizeUnit;
    function unitSources(raw) {
        const root = rootRecord(raw);
        const project = projectRecord(raw);
        if (Array.isArray(raw))
            return raw;
        if (Array.isArray(root.units))
            return root.units;
        if (Array.isArray(project.units))
            return project.units;
        if (isRecord(root.unit))
            return [root.unit];
        return isRecord(raw) && (raw.id || raw.name || raw.label) ? [raw] : [];
    }
    function sanitizeDiagram(raw) {
        if (!isRecord(raw))
            return null;
        const result = {};
        assignString(result, 'id', raw.id);
        assignString(result, 'name', raw.name);
        assignString(result, 'mode', raw.mode);
        assignString(result, 'unitId', raw.unitId);
        assignString(result, 'unit', raw.unit);
        assignString(result, 'addressMode', raw.addressMode);
        assignString(result, 'boolAddressMode', raw.boolAddressMode);
        if (isFiniteNumber(raw.baseMr))
            result.baseMr = raw.baseMr;
        else if (raw.baseMr === null)
            result.baseMr = null;
        assignString(result, 'activeWord', raw.activeWord);
        assignString(result, 'completeWord', raw.completeWord);
        if (!result.id && !result.name && !result.mode)
            return null;
        return result;
    }
    GrafcetStudioAISanitizer.sanitizeDiagram = sanitizeDiagram;
    function diagramSources(raw) {
        const root = rootRecord(raw);
        const project = projectRecord(raw);
        if (Array.isArray(raw))
            return raw;
        if (Array.isArray(root.diagrams))
            return root.diagrams;
        if (Array.isArray(project.diagrams))
            return project.diagrams;
        if (isRecord(root.diagram))
            return [root.diagram];
        return isRecord(raw) && (raw.id || raw.name || raw.mode) ? [raw] : [];
    }
    function sanitizeStepCompletion(raw) {
        if (!isRecord(raw))
            return null;
        const result = {};
        assignString(result, 'sensor', raw.sensor);
        assignString(result, 'sensorLabel', raw.sensorLabel);
        assignString(result, 'address', raw.address);
        if (!result.sensor || !result.sensorLabel || !result.address)
            return null;
        return result;
    }
    function sanitizeStepAction(raw) {
        if (!isRecord(raw))
            return null;
        const result = {};
        assignString(result, 'variable', raw.variable);
        assignNullableString(result, 'address', raw.address);
        assignString(result, 'qualifier', raw.qualifier);
        if (isFiniteNumber(raw.time) || isString(raw.time)) {
            const timeString = isString(raw.time) ? safeString(raw.time) : raw.time;
            if (timeString !== undefined)
                result.time = timeString;
        }
        assignNumber(result, 'timeMs', raw.timeMs);
        const complete = sanitizeStepCompletion(raw.complete);
        if (complete)
            result.complete = complete;
        assignNullableString(result, 'sensorRef', raw.sensorRef);
        if (!result.variable)
            return null;
        return result;
    }
    function sanitizeStep(raw) {
        if (!isRecord(raw))
            return null;
        const result = {};
        assignString(result, 'id', raw.id);
        assignNumber(result, 'number', raw.number);
        assignString(result, 'label', raw.label);
        assignBoolean(result, 'initial', raw.initial !== undefined ? raw.initial : raw.isInitial);
        assignNumber(result, 'x', raw.x);
        assignNumber(result, 'y', raw.y);
        assignNumber(result, 'w', raw.w);
        assignNumber(result, 'h', raw.h);
        assignNullableString(result, 'execAddress', raw.execAddress);
        assignNullableString(result, 'doneAddress', raw.doneAddress);
        const actions = arrayFrom(raw.actions).map(sanitizeStepAction).filter(function (item) { return item !== null; });
        if (actions.length)
            result.actions = actions;
        if (!result.id)
            return null;
        return result;
    }
    GrafcetStudioAISanitizer.sanitizeStep = sanitizeStep;
    function sanitizeTransition(raw) {
        if (!isRecord(raw))
            return null;
        const result = {};
        assignString(result, 'id', raw.id);
        assignString(result, 'label', raw.label);
        assignString(result, 'condition', raw.condition);
        assignNumber(result, 'x', raw.x);
        assignNumber(result, 'y', raw.y);
        assignNumber(result, 'w', raw.w);
        assignNumber(result, 'h', raw.h);
        assignStringArray(result, 'fromStepIds', raw.fromStepIds);
        assignStringArray(result, 'toStepIds', raw.toStepIds);
        if (!result.id)
            return null;
        return result;
    }
    function sanitizeConnection(raw) {
        if (!isRecord(raw))
            return null;
        const result = {};
        assignString(result, 'from', raw.from);
        assignString(result, 'to', raw.to);
        assignString(result, 'fromId', raw.fromId);
        assignString(result, 'toId', raw.toId);
        assignNullableString(result, 'parallelGroupId', raw.parallelGroupId);
        if ((!result.from && !result.fromId) || (!result.to && !result.toId))
            return null;
        return result;
    }
    function sanitizeFlow(raw) {
        if (!isRecord(raw))
            return null;
        const result = {};
        assignString(result, 'id', raw.id);
        assignString(result, 'name', raw.name);
        assignString(result, 'type', raw.type);
        assignString(result, 'mode', raw.mode);
        const diagram = sanitizeDiagram(raw.diagram);
        if (diagram)
            result.diagram = diagram;
        result.steps = arrayFrom(raw.steps).map(sanitizeStep).filter(function (item) { return item !== null; });
        result.transitions = arrayFrom(raw.transitions).map(sanitizeTransition).filter(function (item) { return item !== null; });
        const connections = arrayFrom(raw.connections).map(sanitizeConnection).filter(function (item) { return item !== null; });
        if (connections.length)
            result.connections = connections;
        if (!result.steps.length && !result.transitions.length)
            return null;
        return result;
    }
    function stepSources(raw) {
        const root = rootRecord(raw);
        const state = getRecordField(root, 'state') || root;
        return {
            steps: arrayFrom(root.steps).concat(arrayFrom(state.steps)),
            transitions: arrayFrom(root.transitions).concat(arrayFrom(state.transitions)),
            connections: arrayFrom(root.connections).concat(arrayFrom(state.connections)),
            flows: arrayFrom(root.flows)
        };
    }
    function sanitizePhysicalIO(raw) {
        if (!isRecord(raw))
            return null;
        const result = {};
        assignString(result, 'id', raw.id);
        assignString(result, 'deviceTag', raw.deviceTag);
        assignString(result, 'plcAddress', raw.plcAddress);
        assignString(result, 'direction', raw.direction || raw.Direction);
        if (!result.id || !result.deviceTag || !result.plcAddress || !result.direction)
            return null;
        return result;
    }
    function sanitizeIOMappingEntry(raw) {
        if (!isRecord(raw))
            return null;
        const result = {};
        assignString(result, 'physicalIOId', raw.physicalIOId);
        assignString(result, 'appVariable', raw.appVariable);
        assignString(result, 'status', raw.status);
        assignNumber(result, 'matchScore', raw.matchScore);
        if (!result.physicalIOId || result.appVariable === undefined || !result.status || result.matchScore === undefined)
            return null;
        return result;
    }
    function sanitizeIoMapping(raw, maxItems) {
        const root = rootRecord(raw);
        const project = projectRecord(raw);
        const io = getRecordField(root, 'ioMapping') || getRecordField(project, 'ioMapping') || root;
        return {
            physicalIOs: take(arrayFrom(io.physicalIOs), maxItems).map(sanitizePhysicalIO).filter(function (item) { return item !== null; }),
            entries: take(arrayFrom(io.entries), maxItems).map(sanitizeIOMappingEntry).filter(function (item) { return item !== null; })
        };
    }
    GrafcetStudioAISanitizer.sanitizeIoMapping = sanitizeIoMapping;
    function sanitizeExistingStructures(raw) {
        const root = rootRecord(raw);
        const project = getRecordField(root, 'project');
        const source = project && Array.isArray(project.devices) ? project.devices : (Array.isArray(root.devices) ? root.devices : []);
        const result = [];
        for (let index = 0; index < source.length && result.length < 50; index++) {
            const item = source[index];
            if (!isRecord(item))
                continue;
            const name = safeString(item.name);
            if (name !== undefined)
                result.push(name);
        }
        return result;
    }
    GrafcetStudioAISanitizer.sanitizeExistingStructures = sanitizeExistingStructures;
    function isAllowedForIntent(scope, intent) {
        if (!GrafcetStudioAIContracts.isAiIntent(intent))
            return true;
        return intentScopeMap[intent].indexOf(scope) >= 0;
    }
    function hasScope(options, scope) {
        return options.scopes.indexOf(scope) >= 0 && isAllowedForIntent(scope, options.intent);
    }
    function sanitizeScope(scope, raw, options) {
        const maxItems = options ? options.maxItems : undefined;
        switch (scope) {
            case 'variable': return sanitizeVariables(raw, maxItems);
            case 'unit': return unitSources(raw).map(sanitizeUnit).filter(function (item) { return item !== null; });
            case 'diagram': return diagramSources(raw).map(sanitizeDiagram).filter(function (item) { return item !== null; });
            case 'step':
                const parts = stepSources(raw);
                return {
                    steps: take(parts.steps, maxItems).map(sanitizeStep).filter(function (item) { return item !== null; }),
                    transitions: take(parts.transitions, maxItems).map(sanitizeTransition).filter(function (item) { return item !== null; }),
                    connections: take(parts.connections, maxItems).map(sanitizeConnection).filter(function (item) { return item !== null; }),
                    flows: take(parts.flows, maxItems).map(sanitizeFlow).filter(function (item) { return item !== null; })
                };
            case 'io': return sanitizeIoMapping(raw, maxItems);
            case 'structure': return sanitizeExistingStructures(raw);
            default: return undefined;
        }
    }
    GrafcetStudioAISanitizer.sanitizeScope = sanitizeScope;
    function sanitizeAiContext(raw, options) {
        const result = {};
        const selection = sanitizeSelection(options.selection || (isRecord(raw) ? raw.selection : undefined));
        if (selection)
            result.selection = selection;
        if (hasScope(options, 'variable')) {
            const variables = sanitizeVariables(raw, options.maxItems);
            if (variables.length)
                result.variables = variables;
        }
        if (hasScope(options, 'unit')) {
            const units = take(unitSources(raw), options.maxItems).map(sanitizeUnit).filter(function (item) { return item !== null; });
            if (units.length) {
                result.units = units;
                result.unit = units[0];
            }
        }
        if (hasScope(options, 'diagram')) {
            const diagrams = take(diagramSources(raw), options.maxItems).map(sanitizeDiagram).filter(function (item) { return item !== null; });
            if (diagrams.length) {
                result.diagrams = diagrams;
                result.diagram = diagrams[0];
            }
        }
        if (hasScope(options, 'step')) {
            const parts = stepSources(raw);
            const steps = take(parts.steps, options.maxItems).map(sanitizeStep).filter(function (item) { return item !== null; });
            const transitions = take(parts.transitions, options.maxItems).map(sanitizeTransition).filter(function (item) { return item !== null; });
            const connections = take(parts.connections, options.maxItems).map(sanitizeConnection).filter(function (item) { return item !== null; });
            const flows = take(parts.flows, options.maxItems).map(sanitizeFlow).filter(function (item) { return item !== null; });
            if (steps.length)
                result.steps = steps;
            if (transitions.length)
                result.transitions = transitions;
            if (connections.length)
                result.connections = connections;
            if (flows.length)
                result.flows = flows;
        }
        if (hasScope(options, 'io')) {
            const ioMapping = sanitizeIoMapping(raw, options.maxItems);
            if (ioMapping.physicalIOs.length || ioMapping.entries.length)
                result.ioMapping = ioMapping;
        }
        if (hasScope(options, 'structure')) {
            const existingStructures = sanitizeExistingStructures(raw);
            if (existingStructures.length)
                result.existingStructures = existingStructures;
        }
        return result;
    }
    GrafcetStudioAISanitizer.sanitizeAiContext = sanitizeAiContext;
    GrafcetStudioAISanitizer.api = {
        scopes,
        sanitizeAiContext,
        sanitizeScope,
        sanitizeVariables,
        sanitizeUnit,
        sanitizeDiagram,
        sanitizeStep,
        sanitizeIoMapping,
        sanitizeExistingStructures
    };
})(GrafcetStudioAISanitizer || (GrafcetStudioAISanitizer = {}));

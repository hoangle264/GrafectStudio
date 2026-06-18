"use strict";
var GrafcetStudioAIContracts;
(function (GrafcetStudioAIContracts) {
    GrafcetStudioAIContracts.schemaVersion = '1.0.0';
    const intents = ['create-variable', 'clone-variable', 'map-io', 'create-flow', 'create-structure'];
    const proposalStatuses = ['draft', 'validated', 'applied', 'discarded', 'invalid'];
    function isRecord(value) {
        return !!value && typeof value === 'object' && !Array.isArray(value);
    }
    function isString(value) {
        return typeof value === 'string';
    }
    function isOptionalString(value) {
        return value == null || isString(value);
    }
    function isStringArray(value) {
        return Array.isArray(value) && value.every(isString);
    }
    function hasKnownSchemaVersion(value, errors, path) {
        if (value.schemaVersion !== GrafcetStudioAIContracts.schemaVersion) {
            errors.push(path + '.schemaVersion must be "' + GrafcetStudioAIContracts.schemaVersion + '".');
            return false;
        }
        return true;
    }
    function isAiIntent(value) {
        return isString(value) && intents.indexOf(value) >= 0;
    }
    GrafcetStudioAIContracts.isAiIntent = isAiIntent;
    function isAiProposalStatus(value) {
        return isString(value) && proposalStatuses.indexOf(value) >= 0;
    }
    function validateVariableProposal(value, errors, path) {
        if (!isRecord(value)) {
            errors.push(path + ' must be an object.');
            return false;
        }
        if (!isString(value.label) || !value.label.trim())
            errors.push(path + '.label must be a non-empty string.');
        if (!isString(value.format) || !value.format.trim())
            errors.push(path + '.format must be a non-empty string.');
        if (!isOptionalString(value.address))
            errors.push(path + '.address must be a string or null when provided.');
        if (value.signalAddresses != null && !isStringRecord(value.signalAddresses))
            errors.push(path + '.signalAddresses must map signal ids to addresses.');
        if (value.kind != null && !isString(value.kind))
            errors.push(path + '.kind must be a string when provided.');
        if (value.dataType != null && !isString(value.dataType))
            errors.push(path + '.dataType must be a string when provided.');
        if (value.comment != null && !isString(value.comment))
            errors.push(path + '.comment must be a string when provided.');
        if (value.source != null && !isString(value.source))
            errors.push(path + '.source must be a string when provided.');
        return errors.length === 0;
    }
    function isStringRecord(value) {
        return isRecord(value) && Object.keys(value).every(function (key) { return isString(value[key]); });
    }
    function validateIOMappingEntry(value, errors, path) {
        if (!isRecord(value)) {
            errors.push(path + ' must be an object.');
            return false;
        }
        if (!isString(value.physicalIOId) || !value.physicalIOId.trim())
            errors.push(path + '.physicalIOId must be a non-empty string.');
        if (!isString(value.appVariable))
            errors.push(path + '.appVariable must be a string.');
        if (!isString(value.status) || !value.status.trim())
            errors.push(path + '.status must be a non-empty string.');
        if (typeof value.matchScore !== 'number' || !Number.isFinite(value.matchScore))
            errors.push(path + '.matchScore must be a finite number.');
        return errors.length === 0;
    }
    function validateStep(value, errors, path) {
        if (!isRecord(value)) {
            errors.push(path + ' must be an object.');
            return false;
        }
        if (!isString(value.id) || !value.id.trim())
            errors.push(path + '.id must be a non-empty string.');
        if (value.number != null && (typeof value.number !== 'number' || !Number.isFinite(value.number)))
            errors.push(path + '.number must be a finite number when provided.');
        if (value.label != null && !isString(value.label))
            errors.push(path + '.label must be a string when provided.');
        if (value.initial != null && typeof value.initial !== 'boolean')
            errors.push(path + '.initial must be a boolean when provided.');
        if (value.actions != null && !Array.isArray(value.actions))
            errors.push(path + '.actions must be an array when provided.');
        return errors.length === 0;
    }
    function validateTransition(value, errors, path) {
        if (!isRecord(value)) {
            errors.push(path + ' must be an object.');
            return false;
        }
        if (!isString(value.id) || !value.id.trim())
            errors.push(path + '.id must be a non-empty string.');
        if (value.label != null && !isString(value.label))
            errors.push(path + '.label must be a string when provided.');
        if (value.condition != null && !isString(value.condition))
            errors.push(path + '.condition must be a string when provided.');
        if (value.fromStepIds != null && !isStringArray(value.fromStepIds))
            errors.push(path + '.fromStepIds must be a string array when provided.');
        if (value.toStepIds != null && !isStringArray(value.toStepIds))
            errors.push(path + '.toStepIds must be a string array when provided.');
        return errors.length === 0;
    }
    function validateConnection(value, errors, path) {
        if (!isRecord(value)) {
            errors.push(path + ' must be an object.');
            return false;
        }
        const from = value.from != null ? value.from : value.fromId;
        const to = value.to != null ? value.to : value.toId;
        if (!isString(from) || !from.trim())
            errors.push(path + '.from/fromId must be a non-empty string.');
        if (!isString(to) || !to.trim())
            errors.push(path + '.to/toId must be a non-empty string.');
        return errors.length === 0;
    }
    function validateFlowProposal(value, errors, path) {
        if (!isRecord(value)) {
            errors.push(path + ' must be an object.');
            return false;
        }
        if (value.id != null && !isString(value.id))
            errors.push(path + '.id must be a string when provided.');
        if (value.name != null && !isString(value.name))
            errors.push(path + '.name must be a string when provided.');
        if (!Array.isArray(value.steps))
            errors.push(path + '.steps must be an array.');
        else
            value.steps.forEach(function (step, index) { validateStep(step, errors, path + '.steps[' + index + ']'); });
        if (!Array.isArray(value.transitions))
            errors.push(path + '.transitions must be an array.');
        else
            value.transitions.forEach(function (transition, index) { validateTransition(transition, errors, path + '.transitions[' + index + ']'); });
        if (value.connections != null) {
            if (!Array.isArray(value.connections))
                errors.push(path + '.connections must be an array when provided.');
            else
                value.connections.forEach(function (connection, index) { validateConnection(connection, errors, path + '.connections[' + index + ']'); });
        }
        return errors.length === 0;
    }
    const allowedStructureDataTypes = ['Bool', 'Int', 'Real', 'Word', 'DWord', 'Time'];
    const allowedStructureVarTypes = ['Input', 'Output', 'Var'];
    function validateSignalProposal(value, errors, path) {
        if (!isRecord(value)) {
            errors.push(path + ' must be an object.');
            return false;
        }
        if (!isString(value.name) || !value.name.trim())
            errors.push(path + '.name must be a non-empty string.');
        if (!isString(value.dataType) || allowedStructureDataTypes.indexOf(value.dataType) < 0)
            errors.push(path + '.dataType must be one of: ' + allowedStructureDataTypes.join(', ') + '.');
        if (!isString(value.varType) || allowedStructureVarTypes.indexOf(value.varType) < 0)
            errors.push(path + '.varType must be one of: ' + allowedStructureVarTypes.join(', ') + '.');
        if (value.comment != null && !isString(value.comment))
            errors.push(path + '.comment must be a string when provided.');
        return errors.length === 0;
    }
    function validateCreateStructureProposal(value, errors, path) {
        if (!isRecord(value)) {
            errors.push(path + ' must be an object.');
            return false;
        }
        if (!isString(value.name) || !value.name.trim())
            errors.push(path + '.name must be a non-empty string.');
        if (!Array.isArray(value.signals) || value.signals.length === 0)
            errors.push(path + '.signals must be a non-empty array.');
        else
            value.signals.forEach(function (signal, index) { validateSignalProposal(signal, errors, path + '.signals[' + index + ']'); });
        return errors.length === 0;
    }
    function validateProposalData(intent, value, errors) {
        if (!isRecord(value)) {
            errors.push('proposal.data must be an object.');
            return false;
        }
        switch (intent) {
            case 'create-variable':
                validateVariableProposal(value.variable, errors, 'proposal.data.variable');
                if (value.bucket != null && !isString(value.bucket))
                    errors.push('proposal.data.bucket must be a string when provided.');
                break;
            case 'clone-variable':
                if (!isRecord(value.source))
                    errors.push('proposal.data.source must be an object.');
                else if (!isOptionalString(value.source.id) || !isOptionalString(value.source.label) || (!value.source.id && !value.source.label))
                    errors.push('proposal.data.source requires id or label.');
                if (!Array.isArray(value.variables) || value.variables.length === 0)
                    errors.push('proposal.data.variables must be a non-empty array.');
                else
                    value.variables.forEach(function (variable, index) { validateVariableProposal(variable, errors, 'proposal.data.variables[' + index + ']'); });
                break;
            case 'map-io':
                if (!Array.isArray(value.entries))
                    errors.push('proposal.data.entries must be an array.');
                else
                    value.entries.forEach(function (entry, index) { validateIOMappingEntry(entry, errors, 'proposal.data.entries[' + index + ']'); });
                break;
            case 'create-flow':
                validateFlowProposal(value.flow, errors, 'proposal.data.flow');
                break;
            case 'create-structure':
                validateCreateStructureProposal(value, errors, 'proposal.data');
                break;
            default:
                errors.push('proposal.intent is not supported.');
                break;
        }
        return errors.length === 0;
    }
    function validateAiRequest(value) {
        const errors = [];
        if (!isRecord(value))
            return { ok: false, errors: ['request must be an object.'] };
        hasKnownSchemaVersion(value, errors, 'request');
        if (!isString(value.id) || !value.id.trim())
            errors.push('request.id must be a non-empty string.');
        if (!isAiIntent(value.intent))
            errors.push('request.intent must be one of: ' + intents.join(', ') + '.');
        if (!isString(value.message) || !value.message.trim())
            errors.push('request.message must be a non-empty string.');
        if (!isRecord(value.context))
            errors.push('request.context must be an object.');
        return errors.length ? { ok: false, errors } : { ok: true, value: value, errors: [] };
    }
    GrafcetStudioAIContracts.validateAiRequest = validateAiRequest;
    function isAiRequest(value) {
        return validateAiRequest(value).ok;
    }
    GrafcetStudioAIContracts.isAiRequest = isAiRequest;
    function validateAiProposal(value) {
        const errors = [];
        if (!isRecord(value))
            return { ok: false, errors: ['proposal must be an object.'] };
        hasKnownSchemaVersion(value, errors, 'proposal');
        if (!isString(value.id) || !value.id.trim())
            errors.push('proposal.id must be a non-empty string.');
        if (!isAiIntent(value.intent))
            errors.push('proposal.intent must be one of: ' + intents.join(', ') + '.');
        if (!isAiProposalStatus(value.status))
            errors.push('proposal.status must be one of: ' + proposalStatuses.join(', ') + '.');
        if (value.requestId != null && !isString(value.requestId))
            errors.push('proposal.requestId must be a string when provided.');
        if (value.summary != null && !isString(value.summary))
            errors.push('proposal.summary must be a string when provided.');
        if (value.warnings != null && !isStringArray(value.warnings))
            errors.push('proposal.warnings must be a string array when provided.');
        if (value.errors != null && !isStringArray(value.errors))
            errors.push('proposal.errors must be a string array when provided.');
        if (isAiIntent(value.intent))
            validateProposalData(value.intent, value.data, errors);
        return errors.length ? { ok: false, errors } : { ok: true, value: value, errors: [] };
    }
    GrafcetStudioAIContracts.validateAiProposal = validateAiProposal;
    function isAiProposal(value) {
        return validateAiProposal(value).ok;
    }
    GrafcetStudioAIContracts.isAiProposal = isAiProposal;
    GrafcetStudioAIContracts.api = {
        schemaVersion: GrafcetStudioAIContracts.schemaVersion,
        intents,
        isAiIntent,
        isAiRequest,
        validateAiRequest,
        isAiProposal,
        validateAiProposal
    };
})(GrafcetStudioAIContracts || (GrafcetStudioAIContracts = {}));

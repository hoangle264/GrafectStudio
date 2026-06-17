"use strict";
var GrafcetStudioAIApply;
(function (GrafcetStudioAIApply) {
    const appliedProposalIds = Object.create(null);
    function fail(proposalId, dryRun, errors, warnings, affectedIds) {
        return { ok: false, proposalId, dryRun, affectedIds: affectedIds || [], warnings: warnings || [], errors, changed: false };
    }
    function success(proposalId, dryRun, affectedIds, warnings, changed) {
        return { ok: true, proposalId, dryRun, affectedIds, warnings: warnings || [], errors: [], changed: changed === undefined ? !dryRun : changed };
    }
    function getDefaultContext() {
        if (typeof project === 'undefined' || !project)
            return null;
        return {
            getProject: function () { return project; },
            saveProject: typeof saveProject === 'function' ? saveProject : undefined,
            renderTree: typeof renderTree === 'function' ? renderTree : undefined,
            renderGlobalVarTable: typeof renderGlobalVarTable === 'function' ? renderGlobalVarTable : undefined
        };
    }
    function resolveContext(options) { return options && options.context ? options.context : getDefaultContext(); }
    function trimString(value) { return String(value == null ? '' : value).trim(); }
    function normalizeBucket(bucket) { return trimString(bucket).toLowerCase() === 'imported' ? 'imported' : 'user'; }
    function sanitizeIdPart(value) { return value.trim().toLowerCase().replace(/[^a-z0-9_-]+/g, '-').replace(/^-+|-+$/g, '') || 'proposal'; }
    function makeVariableId(proposalId, label) { return 'ai-var-' + sanitizeIdPart(proposalId) + '-' + sanitizeIdPart(label); }
    function cloneSignalAddresses(source) {
        if (!source)
            return undefined;
        const out = {};
        Object.keys(source).sort().forEach(function (key) { out[key] = source[key]; });
        return out;
    }
    function unique(values) {
        const seen = Object.create(null);
        const out = [];
        values.forEach(function (value) { if (!seen[value]) {
            seen[value] = true;
            out.push(value);
        } });
        return out;
    }
    function toProjectVariable(proposal) {
        const data = proposal.data;
        const variable = data.variable;
        const out = {
            id: makeVariableId(proposal.id, variable.label),
            label: trimString(variable.label),
            format: trimString(variable.format),
            dataType: variable.dataType ? trimString(variable.dataType) : trimString(variable.format),
            kind: variable.kind || (variable.signalAddresses ? 'struct' : 'primitive'),
            source: variable.source || 'manual'
        };
        if (variable.address != null)
            out.address = trimString(variable.address);
        if (variable.comment != null)
            out.comment = String(variable.comment);
        const signalAddresses = cloneSignalAddresses(variable.signalAddresses);
        if (signalAddresses)
            out.signalAddresses = signalAddresses;
        return out;
    }
    function getVariableBuckets(project) {
        const variables = project.variables || { imported: [], user: [] };
        return [variables.imported || [], variables.user || [], project.excelVars || []];
    }
    function forEachVariable(project, cb) {
        getVariableBuckets(project).forEach(function (list) { (list || []).forEach(function (v) { if (v)
            cb(v); }); });
    }
    function flattenAddresses(variable) {
        const addresses = [];
        const primary = trimString(variable.address);
        if (primary)
            addresses.push(primary.toLowerCase());
        const signalAddresses = variable.signalAddresses || {};
        Object.keys(signalAddresses).forEach(function (key) { const address = trimString(signalAddresses[key]); if (address)
            addresses.push(address.toLowerCase()); });
        return addresses;
    }
    function validateCreateVariablePreconditions(proposal, context) {
        const projectState = context.getProject();
        const data = proposal.data;
        const variable = data.variable;
        const candidateLabel = trimString(variable.label).toLowerCase();
        const candidateId = makeVariableId(proposal.id, variable.label);
        const candidateAddresses = flattenAddresses(variable);
        const warnings = [];
        const errors = [];
        if (!projectState || typeof projectState !== 'object')
            errors.push('Project state is not available.');
        if (!candidateLabel)
            errors.push('Variable label is required.');
        const seenProposedAddresses = Object.create(null);
        candidateAddresses.forEach(function (address) {
            if (seenProposedAddresses[address])
                errors.push('Proposed variable contains duplicate address "' + address + '".');
            seenProposedAddresses[address] = true;
        });
        if (projectState) {
            forEachVariable(projectState, function (existing) {
                if (existing.id && existing.id === candidateId)
                    errors.push('Variable id already exists: ' + candidateId + '.');
                if (trimString(existing.label).toLowerCase() === candidateLabel)
                    errors.push('Variable label already exists: ' + variable.label + '.');
                const existingAddresses = flattenAddresses(existing);
                candidateAddresses.forEach(function (address) { if (existingAddresses.indexOf(address) >= 0)
                    errors.push('Variable address already exists: ' + address + '.'); });
            });
        }
        if (trimString(variable.source) && trimString(variable.source).toLowerCase() !== 'manual')
            warnings.push('create-variable source is preserved, but no source file import is performed.');
        return errors.length ? fail(proposal.id, true, unique(errors), warnings, [candidateId]) : success(proposal.id, true, [candidateId], warnings, false);
    }
    function validateCloneVariablePreconditions(proposal, context) {
        const projectState = context.getProject();
        const data = proposal.data;
        const sourceId = trimString(data.source && data.source.id);
        const sourceLabel = trimString(data.source && data.source.label).toLowerCase();
        let found = false;
        forEachVariable(projectState, function (existing) { if ((sourceId && existing.id === sourceId) || (sourceLabel && trimString(existing.label).toLowerCase() === sourceLabel))
            found = true; });
        return found ? success(proposal.id, true, [], [], false) : fail(proposal.id, true, ['Source variable for clone-variable proposal was not found.']);
    }
    function validateMapIOPreconditions(proposal, context) {
        const projectState = context.getProject();
        const data = proposal.data;
        const physicalIds = Object.create(null);
        const variableLabels = Object.create(null);
        const errors = [];
        ((projectState.ioMapping && projectState.ioMapping.physicalIOs) || []).forEach(function (io) { if (io && io.id)
            physicalIds[io.id] = true; });
        forEachVariable(projectState, function (variable) { if (variable.label)
            variableLabels[trimString(variable.label).toLowerCase()] = true; });
        data.entries.forEach(function (entry, index) {
            if (!physicalIds[entry.physicalIOId])
                errors.push('IO mapping entry ' + index + ' references missing physicalIOId: ' + entry.physicalIOId + '.');
            if (!variableLabels[trimString(entry.appVariable).toLowerCase()])
                errors.push('IO mapping entry ' + index + ' references missing appVariable: ' + entry.appVariable + '.');
        });
        return errors.length ? fail(proposal.id, true, unique(errors)) : success(proposal.id, true, data.entries.map(function (e) { return e.physicalIOId; }), [], false);
    }
    function connectionEndpoint(connection, key) { return trimString(key === 'from' ? (connection.from || connection.fromId) : (connection.to || connection.toId)); }
    function validateCreateFlowPreconditions(proposal) {
        const data = proposal.data;
        const flow = data.flow;
        const errors = [];
        const ids = Object.create(null);
        (flow.steps || []).forEach(function (step) { if (ids[step.id])
            errors.push('Flow proposal contains duplicate node id: ' + step.id + '.'); ids[step.id] = true; });
        (flow.transitions || []).forEach(function (transition) { if (ids[transition.id])
            errors.push('Flow proposal contains duplicate node id: ' + transition.id + '.'); ids[transition.id] = true; });
        (flow.connections || []).forEach(function (connection, index) {
            const from = connectionEndpoint(connection, 'from');
            const to = connectionEndpoint(connection, 'to');
            if (!ids[from])
                errors.push('Flow connection ' + index + ' references missing from id: ' + from + '.');
            if (!ids[to])
                errors.push('Flow connection ' + index + ' references missing to id: ' + to + '.');
        });
        return errors.length ? fail(proposal.id, true, unique(errors)) : success(proposal.id, true, Object.keys(ids), [], false);
    }
    function validateProposalForApply(proposal, dryRun, context) {
        const validation = GrafcetStudioAIContracts.validateAiProposal(proposal);
        const proposalId = validation.value ? validation.value.id : (proposal && typeof proposal === 'object' && 'id' in proposal ? String(proposal.id || '') : 'unknown');
        if (!validation.ok || !validation.value)
            return { result: fail(proposalId || 'unknown', dryRun, validation.errors) };
        const validProposal = validation.value;
        if (!context)
            return { result: fail(validProposal.id, dryRun, ['Apply context is not available.']) };
        if (validProposal.status !== 'validated')
            return { result: fail(validProposal.id, dryRun, ['Proposal status must be validated before apply; current status is ' + validProposal.status + '.']) };
        if (appliedProposalIds[validProposal.id])
            return { result: fail(validProposal.id, dryRun, ['Proposal has already been applied: ' + validProposal.id + '.']) };
        return { proposal: validProposal };
    }
    function dryRunProposal(proposal, options) {
        const context = resolveContext(options);
        const base = validateProposalForApply(proposal, true, context);
        if (base.result || !base.proposal || !context)
            return base.result || fail('unknown', true, ['Unknown dry-run failure.']);
        switch (base.proposal.intent) {
            case 'create-variable': return validateCreateVariablePreconditions(base.proposal, context);
            case 'clone-variable': return validateCloneVariablePreconditions(base.proposal, context);
            case 'map-io': return validateMapIOPreconditions(base.proposal, context);
            case 'create-flow': return validateCreateFlowPreconditions(base.proposal);
            default: return fail(base.proposal.id, true, ['Unsupported proposal intent.']);
        }
    }
    GrafcetStudioAIApply.dryRunProposal = dryRunProposal;
    function ensureProjectVariables(projectState) {
        if (!projectState.variables || Array.isArray(projectState.variables))
            projectState.variables = { imported: [], user: [] };
        if (!Array.isArray(projectState.variables.imported))
            projectState.variables.imported = [];
        if (!Array.isArray(projectState.variables.user))
            projectState.variables.user = [];
        return projectState.variables;
    }
    function applyCreateVariable(proposal, context, preflight) {
        const projectState = context.getProject();
        const data = proposal.data;
        const bucket = normalizeBucket(data.bucket);
        const variable = toProjectVariable(proposal);
        ensureProjectVariables(projectState)[bucket].push(variable);
        proposal.status = 'applied';
        appliedProposalIds[proposal.id] = true;
        if (context.saveProject)
            context.saveProject();
        if (context.renderTree)
            context.renderTree();
        if (context.renderGlobalVarTable)
            context.renderGlobalVarTable();
        if (context.refresh)
            context.refresh();
        return success(proposal.id, false, preflight.affectedIds.length ? preflight.affectedIds : [variable.id || variable.label], preflight.warnings, true);
    }
    function applyProposal(proposal, options) {
        if (options && options.dryRun)
            return dryRunProposal(proposal, options);
        const context = resolveContext(options);
        const base = validateProposalForApply(proposal, false, context);
        if (base.result || !base.proposal || !context)
            return base.result || fail('unknown', false, ['Unknown apply failure.']);
        const preflight = dryRunProposal(base.proposal, { context: context, dryRun: true });
        if (!preflight.ok)
            return { ok: false, proposalId: base.proposal.id, dryRun: false, affectedIds: preflight.affectedIds, warnings: preflight.warnings, errors: preflight.errors, changed: false };
        switch (base.proposal.intent) {
            case 'create-variable': return applyCreateVariable(base.proposal, context, preflight);
            case 'clone-variable': return fail(base.proposal.id, false, ['Apply is not implemented for clone-variable yet; dry-run preconditions are available.'], preflight.warnings, preflight.affectedIds);
            case 'map-io': return fail(base.proposal.id, false, ['Apply is not implemented for map-io yet; dry-run preconditions are available.'], preflight.warnings, preflight.affectedIds);
            case 'create-flow': return fail(base.proposal.id, false, ['Apply is not implemented for create-flow yet; dry-run preconditions are available.'], preflight.warnings, preflight.affectedIds);
            default: return fail(base.proposal.id, false, ['Unsupported proposal intent.']);
        }
    }
    GrafcetStudioAIApply.applyProposal = applyProposal;
    function hasAppliedProposal(proposalId) { return !!appliedProposalIds[proposalId]; }
    GrafcetStudioAIApply.hasAppliedProposal = hasAppliedProposal;
    function resetAppliedProposalTracking() { Object.keys(appliedProposalIds).forEach(function (id) { delete appliedProposalIds[id]; }); }
    GrafcetStudioAIApply.resetAppliedProposalTracking = resetAppliedProposalTracking;
    GrafcetStudioAIApply.api = { dryRunProposal, applyProposal, hasAppliedProposal, resetAppliedProposalTracking };
})(GrafcetStudioAIApply || (GrafcetStudioAIApply = {}));

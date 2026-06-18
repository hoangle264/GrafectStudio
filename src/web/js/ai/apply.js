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
            renderGlobalVarTable: typeof renderGlobalVarTable === 'function' ? renderGlobalVarTable : undefined,
            syncVariableSignalAddressesFromDeviceTypes: typeof syncVariableSignalAddressesFromDeviceTypes === 'function' ? syncVariableSignalAddressesFromDeviceTypes : undefined
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
    function toProjectVariable(proposalId, variable) {
        const out = {
            id: makeVariableId(proposalId, variable.label),
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
    function hasCloneSource(projectState, data) {
        const sourceId = trimString(data.source && data.source.id);
        const sourceLabel = trimString(data.source && data.source.label).toLowerCase();
        let found = false;
        forEachVariable(projectState, function (existing) {
            if ((sourceId && existing.id === sourceId) || (sourceLabel && trimString(existing.label).toLowerCase() === sourceLabel))
                found = true;
        });
        return found;
    }
    function collectExistingAddressMap(projectState) {
        const addresses = Object.create(null);
        forEachVariable(projectState, function (existing) { flattenAddresses(existing).forEach(function (address) { addresses[address] = true; }); });
        return addresses;
    }
    function collectExistingLabelMap(projectState) {
        const labels = Object.create(null);
        forEachVariable(projectState, function (existing) { const label = trimString(existing.label).toLowerCase(); if (label)
            labels[label] = true; });
        return labels;
    }
    function analyzeCloneVariables(proposal, projectState) {
        const data = proposal.data;
        const warnings = [];
        const affectedIds = [];
        const acceptedVariables = [];
        const labels = collectExistingLabelMap(projectState);
        const addresses = collectExistingAddressMap(projectState);
        data.variables.forEach(function (variable, index) {
            const label = trimString(variable.label);
            const labelKey = label.toLowerCase();
            const displayName = label || 'variable #' + (index + 1);
            const candidateAddresses = flattenAddresses(variable);
            const localAddressSeen = Object.create(null);
            const conflicts = [];
            if (!labelKey)
                conflicts.push('empty label');
            else if (labels[labelKey])
                conflicts.push('duplicate label');
            candidateAddresses.forEach(function (address) {
                if (localAddressSeen[address])
                    conflicts.push('duplicate address inside proposed variable: ' + address);
                if (addresses[address])
                    conflicts.push('duplicate address: ' + address);
                localAddressSeen[address] = true;
            });
            if (conflicts.length) {
                warnings.push('Skipped clone variable "' + displayName + '" because of ' + unique(conflicts).join(', ') + '.');
                return;
            }
            if (labelKey)
                labels[labelKey] = true;
            candidateAddresses.forEach(function (address) { addresses[address] = true; });
            acceptedVariables.push(variable);
            affectedIds.push(makeVariableId(proposal.id, variable.label));
        });
        return { affectedIds, warnings, acceptedVariables };
    }
    function validateCloneVariablePreconditions(proposal, context) {
        const projectState = context.getProject();
        const data = proposal.data;
        const errors = [];
        if (!projectState || typeof projectState !== 'object')
            errors.push('Project state is not available.');
        if (!Array.isArray(data.variables) || data.variables.length === 0)
            errors.push('Clone-variable proposal requires at least one variable.');
        if (!errors.length && !hasCloneSource(projectState, data))
            errors.push('Source variable for clone-variable proposal was not found.');
        if (errors.length)
            return fail(proposal.id, true, unique(errors));
        const analysis = analyzeCloneVariables(proposal, projectState);
        return analysis.affectedIds.length
            ? success(proposal.id, true, analysis.affectedIds, analysis.warnings, false)
            : fail(proposal.id, true, ['No clone variables can be applied.'], analysis.warnings);
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
    function ensureProjectDiagrams(projectState) {
        if (!Array.isArray(projectState.diagrams))
            projectState.diagrams = [];
        return projectState.diagrams;
    }
    function ensureDiagramState(diagram) {
        const holder = diagram;
        if (!holder.state || typeof holder.state !== 'object')
            holder.state = { steps: [], transitions: [], connections: [] };
        if (!Array.isArray(holder.state.steps))
            holder.state.steps = [];
        if (!Array.isArray(holder.state.transitions))
            holder.state.transitions = [];
        if (!Array.isArray(holder.state.connections))
            holder.state.connections = [];
        return holder.state;
    }
    function syncDiagramStorage(diagramId, state) {
        if (typeof saveDiagramData !== 'function')
            return;
        const existing = typeof loadDiagramData === 'function' ? loadDiagramData(diagramId) : null;
        saveDiagramData(diagramId, state, existing && typeof existing.nextId === 'number' ? existing.nextId : 1, existing && typeof existing.nextStepNum === 'number' ? existing.nextStepNum : 1, existing && typeof existing.viewX === 'number' ? existing.viewX : 60, existing && typeof existing.viewY === 'number' ? existing.viewY : 40, existing && typeof existing.viewScale === 'number' ? existing.viewScale : 1);
    }
    function resolveFlowTarget(projectState, proposal, flow) {
        const flowId = trimString(flow.id);
        const flowName = trimString(flow.name);
        const diagrams = ensureProjectDiagrams(projectState);
        const existing = diagrams.find(function (diagram) { return !!diagram && ((flowId && diagram.id === flowId) || (flowName && trimString(diagram.name).toLowerCase() === flowName.toLowerCase())); });
        if (existing)
            return existing;
        const diagramId = flowId || ('ai-flow-' + sanitizeIdPart(proposal.id));
        const diagram = { id: diagramId, name: flowName || diagramId, mode: flow.mode || 'Manual', diagramType: 'Grafcet' };
        diagrams.push(diagram);
        return diagram;
    }
    function cloneStep(step) { return JSON.parse(JSON.stringify(step)); }
    function cloneTransition(transition) { return JSON.parse(JSON.stringify(transition)); }
    function cloneConnection(connection) { return JSON.parse(JSON.stringify(connection)); }
    const allowedStructureDataTypes = ['Bool', 'Int', 'Real', 'Word', 'DWord', 'Time'];
    const allowedStructureVarTypes = ['Input', 'Output', 'Var'];
    function normalizeStructureName(name) { return typeof name === 'string' ? name.trim() : ''; }
    function makeSignalIdFromName(name) {
        if (typeof GrafcetStudioTree !== 'undefined' && GrafcetStudioTree.makeSignalIdFromName)
            return GrafcetStudioTree.makeSignalIdFromName(name);
        return name.trim().replace(/[^a-zA-Z0-9_]+/g, '_').replace(/^_+|_+$/g, '');
    }
    function ensureProjectDevices(projectState) {
        if (!Array.isArray(projectState.devices))
            projectState.devices = [];
        return projectState.devices;
    }
    function hasStructureWithName(projectState, name) {
        const normalized = name.trim().toLowerCase();
        return Array.isArray(projectState.devices) && projectState.devices.some(function (device) {
            return !!device && typeof device.name === 'string' && device.name.trim().toLowerCase() === normalized;
        });
    }
    function resolveStructureSignals(data, warnings, errors) {
        const used = Object.create(null);
        const signals = [];
        (Array.isArray(data.signals) ? data.signals : []).forEach(function (signal, index) {
            const name = normalizeStructureName(signal && signal.name);
            if (!name) {
                warnings.push('Signal ' + index + ' was skipped because name is empty.');
                return;
            }
            if (allowedStructureDataTypes.indexOf(signal.dataType) < 0) {
                if (errors)
                    errors.push('Signal ' + index + ' has invalid dataType: ' + signal.dataType + '.');
                return;
            }
            if (allowedStructureVarTypes.indexOf(signal.varType) < 0) {
                if (errors)
                    errors.push('Signal ' + index + ' has invalid varType: ' + signal.varType + '.');
                return;
            }
            const id = makeSignalIdFromName(name);
            if (!id) {
                warnings.push('Signal "' + name + '" was skipped because it cannot create a valid id.');
                return;
            }
            const key = id.toLowerCase();
            if (used[key]) {
                warnings.push('Signal "' + name + '" was skipped because id "' + id + '" is duplicated.');
                return;
            }
            used[key] = true;
            const out = { id, name, dataType: signal.dataType, varType: signal.varType, address: '' };
            const comment = normalizeStructureName(signal.comment);
            if (comment)
                out.comment = comment;
            else
                out.comment = '';
            signals.push(out);
        });
        return signals;
    }
    function getFlowNodeY(index) { return 120 + (index * 140); }
    function normalizeFlowAction(action) {
        const raw = action;
        let variable = trimString(raw.variable);
        if (!variable && typeof raw.expression === 'string')
            variable = raw.expression.split('=')[0].trim();
        if (!variable)
            return null;
        return {
            qualifier: trimString(raw.qualifier) || 'N',
            variable,
            address: raw.address == null ? '' : trimString(raw.address),
            time: raw.time == null ? '' : trimString(raw.time)
        };
    }
    function compareFlowLayoutNodes(a, b) {
        if (a.kind !== b.kind)
            return a.kind === "step" ? -1 : 1;
        return a.id.localeCompare(b.id);
    }
    function orderFlowLayoutNodes(flow) {
        const fallbackNodes = [];
        const nodeById = Object.create(null);
        (Array.isArray(flow.steps) ? flow.steps : []).forEach(function (step) {
            const id = trimString(step.id);
            if (!id)
                return;
            const item = { kind: "step", node: step, id };
            fallbackNodes.push(item);
            nodeById[id] = item;
        });
        (Array.isArray(flow.transitions) ? flow.transitions : []).forEach(function (transition) {
            const id = trimString(transition.id);
            if (!id)
                return;
            const item = { kind: "transition", node: transition, id };
            fallbackNodes.push(item);
            nodeById[id] = item;
        });
        fallbackNodes.sort(compareFlowLayoutNodes);
        const fallbackIndex = Object.create(null);
        fallbackNodes.forEach(function (item, index) { fallbackIndex[item.id] = index; });
        const outgoing = Object.create(null);
        const incoming = Object.create(null);
        (Array.isArray(flow.connections) ? flow.connections : []).forEach(function (connection) {
            const from = connectionEndpoint(connection, 'from');
            const to = connectionEndpoint(connection, 'to');
            if (!from || !to || !nodeById[from] || !nodeById[to])
                return;
            if (!outgoing[from])
                outgoing[from] = [];
            outgoing[from].push(to);
            incoming[to] = true;
        });
        Object.keys(outgoing).forEach(function (id) {
            outgoing[id].sort(function (a, b) { return (fallbackIndex[a] || 0) - (fallbackIndex[b] || 0); });
        });
        const initialStep = (Array.isArray(flow.steps) ? flow.steps : []).find(function (step) { return step.initial === true && !!nodeById[trimString(step.id)]; });
        const firstRootStep = (Array.isArray(flow.steps) ? flow.steps : []).find(function (step) { const id = trimString(step.id); return !!id && !!nodeById[id] && !incoming[id]; });
        const firstStep = (Array.isArray(flow.steps) ? flow.steps : []).find(function (step) { return !!nodeById[trimString(step.id)]; });
        const startId = trimString(initialStep && initialStep.id) || trimString(firstRootStep && firstRootStep.id) || trimString(firstStep && firstStep.id);
        if (!startId || !nodeById[startId])
            return fallbackNodes;
        const orderedNodes = [];
        const visited = Object.create(null);
        function visit(id) {
            if (visited[id] || !nodeById[id])
                return;
            visited[id] = true;
            orderedNodes.push(nodeById[id]);
            (outgoing[id] || []).forEach(visit);
        }
        visit(startId);
        fallbackNodes.forEach(function (item) { if (!visited[item.id])
            orderedNodes.push(item); });
        return orderedNodes.length ? orderedNodes : fallbackNodes;
    }
    function materializeFlowLayout(state, flow) {
        const orderedNodes = orderFlowLayoutNodes(flow);
        let nextStepNumber = state.steps.reduce(function (max, step) { return Math.max(max, Number(step.number) || 0); }, 0);
        let stepIndex = 0;
        orderedNodes.forEach(function (item, index) {
            const node = item.node;
            if (typeof node.x !== 'number' || !Number.isFinite(node.x))
                node.x = 160;
            if (typeof node.y !== 'number' || !Number.isFinite(node.y))
                node.y = getFlowNodeY(index);
            if (item.kind === 'step') {
                const step = item.node;
                if (!Number.isFinite(Number(step.number)) || Number(step.number) < 1)
                    step.number = ++nextStepNumber;
                if (state.steps.length === 0 && stepIndex === 0 && typeof step.initial !== 'boolean')
                    step.initial = true;
                step.actions = Array.isArray(step.actions) ? step.actions.map(normalizeFlowAction).filter(function (action) { return action !== null; }) : [];
                stepIndex += 1;
            }
        });
        const baseState = state;
        if (!Array.isArray(baseState.parallels))
            baseState.parallels = [];
        if (!Array.isArray(baseState.vars))
            baseState.vars = [];
    }
    function validateCreateFlowPreconditions(proposal) {
        const data = proposal.data;
        const flow = data.flow;
        const errors = [];
        const warnings = [];
        const ids = Object.create(null);
        (flow.steps || []).forEach(function (step, index) {
            const stepId = trimString(step.id);
            if (!stepId)
                errors.push('Flow step ' + index + ' requires a non-empty id.');
            if (stepId && ids[stepId])
                errors.push('Flow proposal contains duplicate node id: ' + stepId + '.');
            if (stepId)
                ids[stepId] = true;
        });
        (flow.transitions || []).forEach(function (transition, index) {
            const transitionId = trimString(transition.id);
            if (!transitionId)
                errors.push('Flow transition ' + index + ' requires a non-empty id.');
            if (transitionId && ids[transitionId])
                errors.push('Flow proposal contains duplicate node id: ' + transitionId + '.');
            if (transitionId)
                ids[transitionId] = true;
        });
        (flow.connections || []).forEach(function (connection, index) {
            const from = connectionEndpoint(connection, 'from');
            const to = connectionEndpoint(connection, 'to');
            if (!from || !to)
                errors.push('Flow connection ' + index + ' requires non-empty from/to endpoints.');
            if (from && !ids[from])
                errors.push('Flow connection ' + index + ' references missing from id: ' + from + '.');
            if (to && !ids[to])
                errors.push('Flow connection ' + index + ' references missing to id: ' + to + '.');
        });
        if (!errors.length) {
            const nodeCount = (flow.steps || []).length + (flow.transitions || []).length;
            if (Object.keys(ids).length !== nodeCount)
                warnings.push('Flow proposal contains duplicate ids; proposal should use unique node ids only.');
        }
        return errors.length ? fail(proposal.id, true, unique(errors), warnings) : success(proposal.id, true, Object.keys(ids), warnings, false);
    }
    function applyCreateFlow(proposal, context, preflight) {
        const projectState = context.getProject();
        const data = proposal.data;
        const flow = data.flow;
        const warnings = preflight.warnings.slice();
        const target = resolveFlowTarget(projectState, proposal, flow);
        const state = ensureDiagramState(target);
        if (state.steps.length || state.transitions.length || state.connections.length)
            warnings.push('Flow target already had diagram state; appending proposal nodes to existing state.');
        materializeFlowLayout(state, flow);
        const steps = (flow.steps || []).map(cloneStep);
        const transitions = (flow.transitions || []).map(cloneTransition);
        const connections = (flow.connections || []).map(cloneConnection);
        state.steps = state.steps.concat(steps);
        state.transitions = state.transitions.concat(transitions);
        state.connections = state.connections.concat(connections);
        syncDiagramStorage(target.id, state);
        if (flow.name)
            target.name = flow.name;
        if (flow.type)
            target.diagramType = flow.type;
        if (flow.mode)
            target.mode = flow.mode;
        proposal.status = 'applied';
        appliedProposalIds[proposal.id] = true;
        if (context.saveProject)
            context.saveProject();
        if (context.renderTree)
            context.renderTree();
        if (context.refresh)
            context.refresh();
        return success(proposal.id, false, preflight.affectedIds.length ? preflight.affectedIds : steps.map(function (step) { return step.id; }).concat(transitions.map(function (transition) { return transition.id; })), warnings, true);
    }
    function validateCreateStructurePreconditions(proposal, context) {
        const projectState = context.getProject();
        const data = proposal.data;
        const name = normalizeStructureName(data.name);
        const warnings = [];
        const errors = [];
        if (!projectState || typeof projectState !== 'object')
            errors.push('Project state is not available.');
        else if (!Array.isArray(projectState.devices))
            errors.push('Project devices collection is not available.');
        if (!name)
            errors.push('Structure name is required.');
        if (projectState && name && hasStructureWithName(projectState, name))
            errors.push('Structure name already exists');
        const signals = resolveStructureSignals(data, warnings, errors);
        if (!signals.length)
            errors.push('At least one valid signal is required.');
        return errors.length ? fail(proposal.id, true, unique(errors), warnings) : success(proposal.id, true, [name], warnings, false);
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
            case 'create-structure': return validateCreateStructurePreconditions(base.proposal, context);
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
        const variable = toProjectVariable(proposal.id, data.variable);
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
    function applyCloneVariable(proposal, context, _preflight) {
        const projectState = context.getProject();
        const data = proposal.data;
        if (!hasCloneSource(projectState, data))
            return fail(proposal.id, false, ['Source variable for clone-variable proposal was not found.']);
        const analysis = analyzeCloneVariables(proposal, projectState);
        if (!analysis.acceptedVariables.length)
            return fail(proposal.id, false, ['No clone variables were applied.'], analysis.warnings);
        const userVariables = ensureProjectVariables(projectState).user;
        const affectedIds = [];
        analysis.acceptedVariables.forEach(function (variable) {
            const projectVariable = toProjectVariable(proposal.id, variable);
            userVariables.push(projectVariable);
            affectedIds.push(projectVariable.id || projectVariable.label);
        });
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
        return success(proposal.id, false, affectedIds, analysis.warnings, true);
    }
    function applyCreateStructure(proposal, context, preflight) {
        const projectState = context.getProject();
        const data = proposal.data;
        const name = normalizeStructureName(data.name);
        const warnings = preflight.warnings.slice();
        if (hasStructureWithName(projectState, name))
            return fail(proposal.id, false, ['Structure name already exists'], warnings);
        const signals = resolveStructureSignals(data, warnings);
        if (!signals.length)
            return fail(proposal.id, false, ['At least one valid signal is required.'], warnings);
        const device = { id: 'dev-ai-' + Date.now(), name, categoryId: 'cat-other', open: true, signals };
        ensureProjectDevices(projectState).push(device);
        if (context.syncVariableSignalAddressesFromDeviceTypes)
            context.syncVariableSignalAddressesFromDeviceTypes();
        proposal.status = 'applied';
        appliedProposalIds[proposal.id] = true;
        if (context.saveProject)
            context.saveProject();
        if (context.renderTree)
            context.renderTree();
        if (context.refresh)
            context.refresh();
        return success(proposal.id, false, [device.id], warnings, true);
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
            case 'clone-variable': return applyCloneVariable(base.proposal, context, preflight);
            case 'map-io': return fail(base.proposal.id, false, ['Apply is not implemented for map-io yet; dry-run preconditions are available.'], preflight.warnings, preflight.affectedIds);
            case 'create-flow': return applyCreateFlow(base.proposal, context, preflight);
            case 'create-structure': return applyCreateStructure(base.proposal, context, preflight);
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

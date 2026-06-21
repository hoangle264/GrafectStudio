"use strict";
var GrafcetStudioCodegenPayload;
(function (GrafcetStudioCodegenPayload) {
    function parseWordAddress(address) {
        const match = String(address || '').trim().match(/^([A-Za-z]+)(\d+)$/);
        if (!match)
            throw new Error('Invalid word address: ' + address);
        return { prefix: match[1].toUpperCase(), number: Number(match[2]), width: match[2].length };
    }
    function formatWordAddress(base, offset) {
        const parsed = parseWordAddress(base);
        const next = parsed.number + offset;
        const numberText = parsed.width > 1 ? String(next).padStart(parsed.width, '0') : String(next);
        return parsed.prefix + numberText;
    }
    function formatMrAddress(number) {
        return '@MR' + Number(number);
    }
    function normalizeBoolAddressMode(mode) {
        const value = String(mode || '').trim().toLowerCase();
        return value === 'block' ? 'block' : 'linear';
    }
    function resolveBoolMr(baseMr, offset, boolAddressMode) {
        const base = Number(baseMr || 0);
        if (normalizeBoolAddressMode(boolAddressMode) === 'block') {
            return base + Math.floor(offset / 16) * 100 + (offset % 16);
        }
        return base + offset;
    }
    function resolveStepAddress(step, flow) {
        const stepNumber = Number(step && step.number);
        if (!Number.isInteger(stepNumber) || stepNumber < 1) {
            throw new Error('Step number must be an integer >= 1');
        }
        const mode = String((flow && flow.addressMode) || 'bool').toLowerCase();
        if (mode === 'word') {
            if (stepNumber > 32)
                throw new Error('Word address mode supports at most 32 steps per flow');
            const bitIndex = stepNumber - 1;
            const wordOffset = Math.floor(bitIndex / 16);
            const bit = bitIndex % 16;
            return {
                execAddress: '@' + formatWordAddress(flow.activeWord || 'DM0', wordOffset) + '.' + bit,
                doneAddress: '@' + formatWordAddress(flow.completeWord || 'DM100', wordOffset) + '.' + bit
            };
        }
        const pairOffset = (stepNumber - 1) * 2;
        return {
            execAddress: formatMrAddress(resolveBoolMr(flow.baseMr, pairOffset, flow.boolAddressMode)),
            doneAddress: formatMrAddress(resolveBoolMr(flow.baseMr, pairOffset + 1, flow.boolAddressMode))
        };
    }
    GrafcetStudioCodegenPayload.resolveStepAddress = resolveStepAddress;
    function getFlowAddressRange(flow, steps) {
        if (!flow || flow.addressMode !== 'bool')
            return null;
        const maxStepNumber = (steps || []).reduce((max, step) => Math.max(max, Number(step.number) || 0), 0);
        const start = Number(flow.baseMr || 0);
        return { start, end: start + Math.max(1, maxStepNumber) * 2 - 1 };
    }
    function validateStepNumbers(steps, flowName) {
        const seen = new Set();
        (steps || []).forEach(function (step) {
            const stepNumber = Number(step.number);
            if (!Number.isInteger(stepNumber) || stepNumber < 1)
                throw new Error('Flow "' + flowName + '" has a step.number that is not >= 1.');
            if (seen.has(stepNumber))
                throw new Error('Flow "' + flowName + '" has duplicate step.number ' + stepNumber + '.');
            seen.add(stepNumber);
        });
    }
    function validateUnitAddressConfig(context, unitDiagrams) {
        const boolFlows = [];
        const usedWords = new Map();
        (unitDiagrams || []).forEach(function (diagram) {
            if (context.ensureFlowAddressConfig)
                context.ensureFlowAddressConfig(diagram, true);
            const data = context.loadDiagramData(diagram.id);
            const steps = ((data && data.state && data.state.steps) || []);
            validateStepNumbers(steps, diagram.name || diagram.id);
            if (diagram.addressMode === 'word') {
                if (steps.some(step => Number(step.number) > 32)) {
                    throw new Error('Flow "' + (diagram.name || diagram.id) + '" uses word mode but has step.number > 32.');
                }
                const maxStepNumber = steps.reduce((max, step) => Math.max(max, Number(step.number) || 0), 0);
                const wordCount = maxStepNumber > 16 ? 2 : 1;
                [diagram.activeWord || 'DM0', diagram.completeWord || 'DM100'].forEach(function (word) {
                    for (let offset = 0; offset < wordCount; offset++) {
                        const key = formatWordAddress(word, offset).toUpperCase();
                        if (usedWords.has(key))
                            throw new Error('Word address ' + key + ' is used by both "' + usedWords.get(key) + '" and "' + (diagram.name || diagram.id) + '".');
                        usedWords.set(key, diagram.name || diagram.id);
                    }
                });
                return;
            }
            const range = getFlowAddressRange(diagram, steps);
            boolFlows.forEach(function (existing) {
                if (range && existing.range && range.start <= existing.range.end && existing.range.start <= range.end) {
                    throw new Error('MR range overlap between "' + existing.name + '" and "' + (diagram.name || diagram.id) + '".');
                }
            });
            boolFlows.push({ name: diagram.name || diagram.id, range });
        });
    }
    GrafcetStudioCodegenPayload.validateUnitAddressConfig = validateUnitAddressConfig;
    function normalizeCSharpSignal(context, deviceTypeName, signal) {
        const canonicalUnitSignals = new Map((context.projectUnitStructSignals || []).map(item => [item.id, item]));
        const canonical = deviceTypeName === 'Unit Station' ? canonicalUnitSignals.get(signal && signal.id) : null;
        const normalized = Object.assign({}, signal || {}, canonical || {});
        const signalId = normalized.id || normalized.name || '';
        const signalName = normalized.name || signalId;
        return Object.assign({}, normalized, { id: signalId, name: signalName });
    }
    function getCSharpDeviceTypes(context) {
        return ((context.project && context.project.devices) || []).map(deviceType => {
            if (!deviceType)
                return deviceType;
            return Object.assign({}, deviceType, {
                signals: (deviceType.signals || []).map(signal => normalizeCSharpSignal(context, deviceType.name, signal))
            });
        });
    }
    function getCylinderSignalAddress(rawAddresses, signal) {
        const key = String((signal && signal.name) || (signal && signal.id) || '').toLowerCase().replace(/[^a-z0-9]/g, '');
        const byStructName = rawAddresses[(signal && signal.name) || ''];
        if (byStructName)
            return byStructName;
        if (key === 'lsh')
            return rawAddresses.LSH || rawAddresses.cyl_lsh || '';
        if (key === 'lsl')
            return rawAddresses.LSL || rawAddresses.cyl_lsl || '';
        if (key === 'locka')
            return rawAddresses.LockA || rawAddresses.cyl_lockA || '';
        if (key === 'lockb')
            return rawAddresses.LockB || rawAddresses.cyl_lockB || '';
        if (key === 'dissnslsh' || key === 'dissnsh')
            return rawAddresses.DisSnsLSH || rawAddresses.DisSnsH || rawAddresses.cyl_disSnsH || '';
        if (key === 'dissnslsl' || key === 'dissnsl')
            return rawAddresses.DisSnsLSL || rawAddresses.DisSnsL || rawAddresses.cyl_disSnsL || '';
        if (key === 'state')
            return rawAddresses.State || rawAddresses.cyl_state || '';
        if (key === 'errora' || key === 'erra')
            return rawAddresses.ErrorA || rawAddresses.ErrA || rawAddresses.cyl_errA || '';
        if (key === 'errorb' || key === 'errb')
            return rawAddresses.ErrorB || rawAddresses.ErrB || rawAddresses.cyl_errB || '';
        if (key === 'coila')
            return rawAddresses.CoilA || rawAddresses.cyl_coilA || '';
        if (key === 'coilb')
            return rawAddresses.CoilB || rawAddresses.cyl_coilB || '';
        if (key === 'hmimanbtn' || key === 'hmiman')
            return rawAddresses.HmiManBtn || rawAddresses.HmiMan || rawAddresses.cyl_hmiMan || '';
        return rawAddresses[(signal && signal.id) || ''] || '';
    }
    function getCSharpSignalAddresses(context, variable) {
        const format = variable && (variable.format || variable.dataType || '');
        const deviceType = ((context.project && context.project.devices) || []).find(device => device && device.name === format);
        const rawAddresses = (variable && variable.signalAddresses) || {};
        if (!deviceType || !Array.isArray(deviceType.signals))
            return Object.assign({}, rawAddresses);
        const signalAddresses = {};
        deviceType.signals.forEach(signal => {
            const normalized = normalizeCSharpSignal(context, deviceType.name, signal);
            if (!normalized.id)
                return;
            const address = format === 'Cylinder'
                ? getCylinderSignalAddress(rawAddresses, normalized)
                : (rawAddresses[normalized.name] || rawAddresses[signal && signal.name] || rawAddresses[signal && signal.id]);
            signalAddresses[normalized.id] = address || '';
        });
        return signalAddresses;
    }
    function getCSharpVariables(context, diagramState) {
        const vars = [];
        const seen = new Set();
        const add = function (variable) {
            if (!variable || !variable.label)
                return;
            const signalAddresses = getCSharpSignalAddresses(context, variable);
            if (seen.has(variable.label))
                return;
            seen.add(variable.label);
            vars.push({
                label: variable.label,
                format: variable.format || variable.dataType || '',
                address: variable.address || null,
                signalAddresses: signalAddresses
            });
        };
        (diagramState.vars || []).forEach(add);
        if (context.ensureProjectVariables) {
            const grouped = context.ensureProjectVariables();
            (grouped.imported || []).forEach(add);
            (grouped.user || []).forEach(add);
        }
        Object.keys((context.project && context.project.unitConfig) || {}).forEach(key => {
            const cfg = context.project.unitConfig[key] || { label: key };
            const signalAddresses = Object.assign({}, cfg.signalAddresses || {});
            (context.unitSignals || []).forEach(signal => {
                if (!signal || !signal.id || !signal.path)
                    return;
                const address = signal.path.split('.').reduce((current, part) => {
                    return current && typeof current === 'object' && current[part] != null
                        ? current[part]
                        : '';
                }, cfg) || '';
                if (address)
                    signalAddresses[signal.id] = String(address);
            });
            add({ label: cfg.label || key, format: 'Unit Station', address: null, signalAddresses });
        });
        (context.project.excelVars || []).forEach(add);
        return vars;
    }
    function buildCSharpFlow(context, diagramId) {
        const diagram = (context.project.diagrams || []).find(item => item.id === diagramId) || { id: diagramId, name: diagramId, mode: '' };
        const data = context.loadDiagramData(diagramId);
        const state = ((data && data.state) || { steps: [], transitions: [], connections: [], vars: [] });
        if (context.ensureFlowAddressConfig)
            context.ensureFlowAddressConfig(diagram, true);
        const steps = (state.steps || []).map(step => {
            const address = resolveStepAddress(step, diagram);
            return {
                id: step.id || '',
                number: Number(step.number || 0),
                label: step.label || '',
                initial: !!step.initial,
                execAddress: address.execAddress,
                doneAddress: address.doneAddress,
                actions: (step.actions || []).map(action => ({
                    variable: action.variable || '',
                    address: action.address || null,
                    qualifier: action.qualifier || 'N',
                    timeMs: Number(action.timeMs || action.time || 0)
                }))
            };
        });
        const stepIds = new Set(steps.map(step => step.id));
        const transitions = (state.transitions || []).map((transition) => ({
            id: transition.id || '',
            label: transition.label || '',
            condition: transition.condition || '',
            fromStepIds: (state.connections || [])
                .filter((connection) => connection.to === transition.id && !!connection.from && stepIds.has(connection.from))
                .map((connection) => connection.from || ''),
            toStepIds: (state.connections || [])
                .filter((connection) => connection.from === transition.id && !!connection.to && stepIds.has(connection.to))
                .map((connection) => connection.to || '')
        }));
        return {
            diagram: {
                id: diagram.id || diagramId,
                name: diagram.name || diagramId,
                mode: diagram.mode || '',
                controlState: diagram.controlState || diagram.mode || 'Auto',
                category: diagram.category || 'normal',
                orchestratorConfig: diagram.category === 'orchestrator' ? (diagram.orchestratorConfig || { elements: [] }) : undefined,
                unitId: diagram.unitId || '',
                unit: diagram.unit || '',
                addressMode: diagram.addressMode || 'bool',
                boolAddressMode: diagram.boolAddressMode || 'linear',
                baseMr: diagram.baseMr == null || diagram.baseMr === '' ? null : Number(diagram.baseMr),
                activeWord: diagram.activeWord || '',
                completeWord: diagram.completeWord || ''
            },
            steps,
            transitions,
            variables: getCSharpVariables(context, state)
        };
    }
    GrafcetStudioCodegenPayload.buildCSharpFlow = buildCSharpFlow;
    function normalizeFlowType(mode) {
        const value = String(mode || '').trim().toLowerCase();
        return value === 'origin' ? 'origin' : 'auto';
    }
    function buildCSharpUnitPayload(context, platform, unitId) {
        const units = context.project.units || [];
        const selectedUnit = unitId && unitId !== '__none__'
            ? units.find(unit => unit.id === unitId)
            : null;
        const unitDiagrams = (context.project.diagrams || []).filter(diagram => unitId === '__none__' ? !diagram.unitId : diagram.unitId === unitId);
        validateUnitAddressConfig(context, unitDiagrams);
        const allVars = [];
        const seenVars = new Set();
        const addVar = (variable) => {
            if (!variable || !variable.label || seenVars.has(variable.label))
                return;
            seenVars.add(variable.label);
            allVars.push(variable);
        };
        const flows = unitDiagrams.map(diagram => {
            const flow = buildCSharpFlow(context, diagram.id);
            (flow.variables || []).forEach(addVar);
            return {
                id: flow.diagram && flow.diagram.id,
                name: flow.diagram && flow.diagram.name,
                type: normalizeFlowType(flow.diagram && flow.diagram.mode),
                controlState: flow.diagram && (flow.diagram.controlState || flow.diagram.mode),
                category: flow.diagram && (flow.diagram.category || 'normal'),
                orchestratorConfig: flow.diagram && flow.diagram.category === 'orchestrator'
                    ? (flow.diagram.orchestratorConfig || { elements: [] })
                    : undefined,
                diagram: flow.diagram,
                steps: flow.steps,
                transitions: flow.transitions
            };
        });
        const assets = context.getAssets();
        return {
            platform,
            deviceLibraryPath: assets.deviceLibraryPath,
            templateRootPath: assets.templateRootPath,
            outputPath: assets.outputPath,
            project: {
                id: context.project.id || '',
                name: context.project.name || '',
                machineName: context.project.machineName || ''
            },
            unit: {
                id: selectedUnit ? selectedUnit.id : (unitId || ''),
                name: selectedUnit ? (selectedUnit.name || selectedUnit.id) : (unitId === '__none__' ? 'No unit' : ''),
                label: selectedUnit ? (selectedUnit.name || selectedUnit.id) : (unitId === '__none__' ? 'No unit' : '')
            },
            flows,
            variables: allVars,
            deviceTypes: getCSharpDeviceTypes(context)
        };
    }
    GrafcetStudioCodegenPayload.buildCSharpUnitPayload = buildCSharpUnitPayload;
    function buildCSharpPayload(context, platform, unitId) {
        if (context.syncVariableSignalAddressesFromDeviceTypes && context.syncVariableSignalAddressesFromDeviceTypes()) {
            if (context.saveProject)
                context.saveProject();
        }
        const resolvedUnitId = unitId || (context.getDefaultUnitId ? context.getDefaultUnitId() : '') || '';
        return buildCSharpUnitPayload(context, platform, resolvedUnitId);
    }
    GrafcetStudioCodegenPayload.buildCSharpPayload = buildCSharpPayload;
    GrafcetStudioCodegenPayload.api = {
        buildCSharpPayload,
        buildCSharpFlow,
        buildCSharpUnitPayload,
        validateUnitAddressConfig,
        resolveStepAddress
    };
})(GrafcetStudioCodegenPayload || (GrafcetStudioCodegenPayload = {}));
GrafcetStudioInterop.registerBridge('codegenPayload', GrafcetStudioCodegenPayload.api);


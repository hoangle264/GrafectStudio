"use strict";
var GrafcetStudioTables;
(function (GrafcetStudioTables) {
    function parallelsOf(state) {
        return (state.parallels || []);
    }
    function buildStepTableData(context, state) {
        const rows = (state.steps || []).slice().sort(function (a, b) { return (Number(a.number) || 0) - (Number(b.number) || 0); }).map(function (step) {
            const number = Number(step.number) || 0;
            return {
                id: step.id,
                number,
                numberText: String(number).padStart(2, '0'),
                label: step.label || '',
                initial: !!step.initial,
                actions: context.getStepActionsStatic(step)
            };
        });
        return {
            rows,
            stats: {
                steps: rows.length,
                initial: rows.filter(row => row.initial).length,
                actions: rows.reduce(function (total, row) { return total + row.actions.length; }, 0)
            }
        };
    }
    GrafcetStudioTables.buildStepTableData = buildStepTableData;
    function buildTransitionTableData(context, state) {
        const transitions = state.transitions || [];
        const steps = state.steps || [];
        const connections = state.connections || [];
        const parallels = parallelsOf(state);
        const rows = [];
        transitions.forEach(function (transition) {
            const fromSteps = context.resolveStepsThrough(transition.id, 'upstream', connections, steps, parallels);
            const toSteps = context.resolveStepsThrough(transition.id, 'downstream', connections, steps, parallels);
            if (fromSteps.length === 0 && toSteps.length === 0)
                rows.push({ tid: transition.id, label: transition.label || '', fromStep: null, toStep: null, condition: transition.condition || '' });
            else if (fromSteps.length === 0)
                toSteps.forEach(toStep => rows.push({ tid: transition.id, label: transition.label || '', fromStep: null, toStep, condition: transition.condition || '' }));
            else if (toSteps.length === 0)
                fromSteps.forEach(fromStep => rows.push({ tid: transition.id, label: transition.label || '', fromStep, toStep: null, condition: transition.condition || '' }));
            else
                fromSteps.forEach(fromStep => toSteps.forEach(toStep => rows.push({ tid: transition.id, label: transition.label || '', fromStep, toStep, condition: transition.condition || '' })));
        });
        return {
            rows,
            stats: {
                transitions: transitions.length,
                withCondition: transitions.filter(transition => !!transition.condition).length,
                stepPairs: rows.length
            }
        };
    }
    GrafcetStudioTables.buildTransitionTableData = buildTransitionTableData;
    function buildBranchTableData(_context, state) {
        const parallels = parallelsOf(state);
        const steps = state.steps || [];
        const transitions = state.transitions || [];
        const connections = state.connections || [];
        const rows = parallels.map(function (parallel) {
            const isSplit = parallel.type === 'split';
            const ports = Number(parallel.ports) || 3;
            const width = Number(parallel.width) || 0;
            const singleConns = connections.filter(function (connection) {
                return isSplit
                    ? connection.to === parallel.id && connection.toPort === 'top'
                    : connection.from === parallel.id && connection.fromPort === 'bottom';
            });
            const singleTransitions = singleConns.map(function (connection) {
                return transitions.find(item => item.id === (isSplit ? connection.from : connection.to));
            }).filter(function (item) { return !!item; });
            const branchSteps = [];
            for (let index = 0; index < ports; index++) {
                const branchPort = isSplit ? 'bottom-' + index : 'top-' + index;
                const branchConns = connections.filter(function (connection) {
                    return isSplit
                        ? connection.from === parallel.id && connection.fromPort === branchPort
                        : connection.to === parallel.id && connection.toPort === branchPort;
                });
                branchSteps.push(branchConns.map(function (connection) {
                    return steps.find(item => item.id === (isSplit ? connection.to : connection.from));
                }).filter(function (item) { return !!item; }));
            }
            const descriptionParts = [ports + ' branches, width=' + width + 'px'];
            if (singleTransitions.length) {
                descriptionParts.push(isSplit
                    ? 'Triggered by: ' + singleTransitions.map(transition => transition.id + (transition.condition ? ' [' + transition.condition + ']' : '')).join(', ')
                    : 'Converges to: ' + singleTransitions.map(transition => transition.id).join(', '));
            }
            return { id: parallel.id, type: parallel.type || '', isSplit, ports, width, singleTransitions, branchSteps, descriptionParts };
        });
        return {
            rows,
            stats: {
                split: parallels.filter(parallel => parallel.type === 'split').length,
                join: parallels.filter(parallel => parallel.type === 'join').length
            }
        };
    }
    GrafcetStudioTables.buildBranchTableData = buildBranchTableData;
    function buildVariablesTableData(context, state) {
        const variables = (state.vars || []);
        const rows = variables.map(function (variable, index) {
            const deviceType = (context.project.devices || []).find(device => device.name === (variable.format || '')) || null;
            const signalRows = deviceType ? (deviceType.signals || []).map(function (signal) {
                return {
                    kind: 'signal',
                    label: (variable.label || '?') + '.' + signal.name,
                    dataFormat: signal.dataType || 'Bool',
                    address: (variable.signalAddresses || {})[signal.id] || '',
                    varType: signal.varType || '',
                    comment: signal.comment || ''
                };
            }) : [];
            return {
                kind: 'variable',
                index: index + 1,
                label: variable.label || '',
                format: variable.format || '',
                address: variable.address || '',
                comment: variable.comment || '',
                deviceType,
                signalRows
            };
        });
        return {
            rows,
            stats: {
                variables: variables.length,
                deviceInstances: rows.filter(row => !!row.deviceType).length
            }
        };
    }
    GrafcetStudioTables.buildVariablesTableData = buildVariablesTableData;
    GrafcetStudioTables.api = {
        buildStepTableData,
        buildTransitionTableData,
        buildBranchTableData,
        buildVariablesTableData
    };
})(GrafcetStudioTables || (GrafcetStudioTables = {}));
GrafcetStudioInterop.registerBridge('tables', GrafcetStudioTables.api);

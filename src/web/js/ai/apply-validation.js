"use strict";
var GrafcetStudioAIApplyValidation;
(function (GrafcetStudioAIApplyValidation) {
    function assert(condition, message, errors) {
        if (!condition)
            errors.push(message);
    }
    function makeProject() {
        return {
            id: 'proj-ai-apply-test',
            name: 'AI Apply Test',
            machineName: 'Machine',
            units: [],
            diagrams: [],
            devices: [],
            variables: { imported: [], user: [] },
            excelVars: [],
            unitConfig: {},
            ioMapping: { physicalIOs: [{ id: 'pio-1', deviceTag: 'X0', plcAddress: 'X0.0', direction: 'Input' }], entries: [] }
        };
    }
    function makeCreateVariableProposal(id, label, address) {
        return {
            schemaVersion: GrafcetStudioAIContracts.schemaVersion,
            id,
            intent: 'create-variable',
            status: 'validated',
            summary: 'Apply validation create-variable fixture.',
            data: {
                bucket: 'user',
                variable: { label, format: 'BOOL', dataType: 'BOOL', kind: 'primitive', address, comment: 'Apply validation variable.', source: 'manual' }
            }
        };
    }
    function makeCloneVariableProposal(id, sourceLabel, variables) {
        return {
            schemaVersion: GrafcetStudioAIContracts.schemaVersion,
            id,
            intent: 'clone-variable',
            status: 'validated',
            summary: 'Apply validation clone-variable fixture.',
            data: { source: { label: sourceLabel }, variables }
        };
    }
    function makeCreateStructureProposal(id, name, signals) {
        return {
            schemaVersion: GrafcetStudioAIContracts.schemaVersion,
            id,
            intent: 'create-structure',
            status: 'validated',
            summary: 'Apply validation create-structure fixture.',
            data: { name, signals }
        };
    }
    function makeCreateFlowProposal(id, flow) {
        return {
            schemaVersion: GrafcetStudioAIContracts.schemaVersion,
            id,
            intent: 'create-flow',
            status: 'validated',
            summary: 'Apply validation create-flow fixture.',
            data: { flow }
        };
    }
    function makeContext(project, counts) {
        return {
            getProject: function () { return project; },
            saveProject: function () { counts.saveProject += 1; },
            renderTree: function () { counts.renderTree += 1; },
            renderGlobalVarTable: function () { counts.renderGlobalVarTable += 1; },
            refresh: function () { counts.refresh += 1; },
            syncVariableSignalAddressesFromDeviceTypes: function () { return true; }
        };
    }
    function runApplyValidation() {
        const errors = [];
        GrafcetStudioAIApply.resetAppliedProposalTracking();
        const project = makeProject();
        const callbackCounts = { saveProject: 0, renderTree: 0, renderGlobalVarTable: 0, refresh: 0 };
        const context = makeContext(project, callbackCounts);
        const proposal = makeCreateVariableProposal('ai-prop-apply-test-1', 'AiApplyMotorReady', 'MR900');
        const dryRunSuccess = GrafcetStudioAIApply.dryRunProposal(proposal, { context: context });
        assert(dryRunSuccess.ok, 'dry-run should succeed for a unique validated create-variable proposal.', errors);
        assert(project.variables.user.length === 0, 'dry-run success must not mutate project variables.', errors);
        assert(callbackCounts.saveProject === 0 && callbackCounts.renderTree === 0 && callbackCounts.renderGlobalVarTable === 0, 'dry-run must not call save/render callbacks.', errors);
        const invalidStatusProposal = makeCreateVariableProposal('ai-prop-apply-test-invalid', 'AiApplyInvalidStatus', 'MR901');
        invalidStatusProposal.status = 'draft';
        const dryRunFailure = GrafcetStudioAIApply.dryRunProposal(invalidStatusProposal, { context: context });
        assert(!dryRunFailure.ok, 'dry-run should fail when proposal status is not validated.', errors);
        assert(project.variables.user.length === 0, 'failed dry-run must not mutate project variables.', errors);
        const applySuccess = GrafcetStudioAIApply.applyProposal(proposal, { context: context });
        assert(applySuccess.ok, 'apply should succeed for a unique validated create-variable proposal.', errors);
        assert(applySuccess.changed === true, 'successful apply should report changed=true.', errors);
        assert(project.variables.user.length === 1, 'successful create-variable apply should append exactly one user variable.', errors);
        assert(project.variables.user[0].label === 'AiApplyMotorReady', 'successful apply should preserve variable label.', errors);
        assert(proposal.status === 'applied', 'successful apply should mark proposal status as applied.', errors);
        assert(callbackCounts.saveProject === 1 && callbackCounts.renderTree === 1 && callbackCounts.renderGlobalVarTable === 1 && callbackCounts.refresh === 1, 'successful apply should call each persistence/render callback once.', errors);
        const duplicateProposal = makeCreateVariableProposal('ai-prop-apply-test-duplicate', 'AiApplyMotorReady', 'MR902');
        const duplicateResult = GrafcetStudioAIApply.dryRunProposal(duplicateProposal, { context: context });
        assert(!duplicateResult.ok, 'dry-run should fail for duplicate variable label.', errors);
        assert(project.variables.user.length === 1, 'duplicate dry-run failure must not mutate state.', errors);
        const doubleApplyResult = GrafcetStudioAIApply.applyProposal(proposal, { context: context });
        assert(!doubleApplyResult.ok, 'second apply of same proposal must fail.', errors);
        assert(project.variables.user.length === 1, 'double-apply failure must not append another variable.', errors);
        assert(callbackCounts.saveProject === 1 && callbackCounts.renderTree === 1 && callbackCounts.renderGlobalVarTable === 1 && callbackCounts.refresh === 1, 'failed apply attempts must not call persistence/render callbacks.', errors);
        const beforeFailedPrecondition = project.variables.user.length;
        const addressDuplicateProposal = makeCreateVariableProposal('ai-prop-apply-test-address-duplicate', 'AiApplyOtherLabel', 'MR900');
        const failedPreconditionResult = GrafcetStudioAIApply.applyProposal(addressDuplicateProposal, { context: context });
        assert(!failedPreconditionResult.ok, 'apply should fail when dry-run precondition detects duplicate address.', errors);
        assert(project.variables.user.length === beforeFailedPrecondition, 'failed precondition apply must not mutate state.', errors);
        const cloneProject = makeProject();
        cloneProject.variables.imported.push({ id: 'src-cylinder', label: 'Cylinder', format: 'Cylinder', kind: 'struct', source: 'manual' });
        cloneProject.variables.user.push({ id: 'existing-clone', label: 'ExistingClone', format: 'BOOL', address: 'MR950', kind: 'primitive' });
        const cloneCounts = { saveProject: 0, renderTree: 0, renderGlobalVarTable: 0, refresh: 0 };
        const cloneContext = makeContext(cloneProject, cloneCounts);
        const cloneAllUniqueProposal = makeCloneVariableProposal('ai-prop-clone-all-unique', 'Cylinder', [
            { label: 'Cyl_A', format: 'Cylinder', kind: 'struct', source: 'manual' },
            { label: 'Cyl_B', format: 'Cylinder', kind: 'struct', source: 'manual' },
            { label: 'Cyl_C', format: 'Cylinder', kind: 'struct', source: 'manual' }
        ]);
        const cloneAllUniqueResult = GrafcetStudioAIApply.applyProposal(cloneAllUniqueProposal, { context: cloneContext });
        assert(cloneAllUniqueResult.ok, 'clone-variable apply should succeed for 3 unique variables.', errors);
        assert(cloneAllUniqueResult.affectedIds.length === 3, 'clone-variable all unique apply should affect all 3 variables.', errors);
        assert(cloneProject.variables.user.length === 4, 'clone-variable all unique apply should append 3 user variables.', errors);
        assert(cloneAllUniqueProposal.status === 'applied', 'clone-variable apply should mark proposal applied.', errors);
        const clonePartialProposal = makeCloneVariableProposal('ai-prop-clone-partial', 'Cylinder', [
            { label: 'Cyl_D', format: 'Cylinder', kind: 'struct', source: 'manual' },
            { label: 'ExistingClone', format: 'BOOL', address: 'MR951', kind: 'primitive', source: 'manual' },
            { label: 'Cyl_E', format: 'Cylinder', kind: 'struct', source: 'manual' }
        ]);
        const clonePartialConflictResult = GrafcetStudioAIApply.applyProposal(clonePartialProposal, { context: cloneContext });
        assert(clonePartialConflictResult.ok, 'clone-variable should partially apply when one variable conflicts.', errors);
        assert(clonePartialConflictResult.affectedIds.length === 2 && clonePartialConflictResult.warnings.length >= 1, 'clone-variable partial apply should affect 2 and warn for skipped duplicate.', errors);
        const cloneInternalProposal = makeCloneVariableProposal('ai-prop-clone-internal', 'Cylinder', [
            { label: 'Cyl_F', format: 'BOOL', address: 'MR960', kind: 'primitive', source: 'manual' },
            { label: 'Cyl_F', format: 'BOOL', address: 'MR961', kind: 'primitive', source: 'manual' },
            { label: 'Cyl_G', format: 'BOOL', address: 'MR960', kind: 'primitive', source: 'manual' }
        ]);
        const cloneInternalConflictResult = GrafcetStudioAIApply.applyProposal(cloneInternalProposal, { context: cloneContext });
        assert(cloneInternalConflictResult.ok, 'clone-variable should apply first valid variable when later variables conflict internally.', errors);
        assert(cloneInternalConflictResult.affectedIds.length === 1, 'clone-variable internal conflicts should leave only first valid variable affected.', errors);
        const beforeAllConflict = cloneProject.variables.user.length;
        const cloneAllConflictProposal = makeCloneVariableProposal('ai-prop-clone-all-conflict', 'Cylinder', [
            { label: 'ExistingClone', format: 'BOOL', address: 'MR950', kind: 'primitive', source: 'manual' },
            { label: 'Cyl_A', format: 'Cylinder', kind: 'struct', source: 'manual' }
        ]);
        const cloneAllConflictResult = GrafcetStudioAIApply.applyProposal(cloneAllConflictProposal, { context: cloneContext });
        assert(!cloneAllConflictResult.ok, 'clone-variable should fail when all variables conflict.', errors);
        assert(cloneProject.variables.user.length === beforeAllConflict, 'clone-variable all-conflict failure must not mutate.', errors);
        const cloneMissingSourceResult = GrafcetStudioAIApply.applyProposal(makeCloneVariableProposal('ai-prop-clone-missing-source', 'MissingCylinder', [
            { label: 'Cyl_Missing', format: 'Cylinder', kind: 'struct', source: 'manual' }
        ]), { context: cloneContext });
        assert(!cloneMissingSourceResult.ok, 'clone-variable should fail when source is missing.', errors);
        const beforeDryRunConflict = cloneProject.variables.user.length;
        const cloneDryRunConflictResult = GrafcetStudioAIApply.dryRunProposal(makeCloneVariableProposal('ai-prop-clone-dry-conflict', 'Cylinder', [
            { label: 'ExistingClone', format: 'BOOL', address: 'MR950', kind: 'primitive', source: 'manual' },
            { label: 'Cyl_H', format: 'Cylinder', kind: 'struct', source: 'manual' }
        ]), { context: cloneContext });
        assert(cloneDryRunConflictResult.ok && cloneDryRunConflictResult.warnings.length >= 1, 'clone-variable dry-run should warn conflicts while succeeding if one variable remains.', errors);
        assert(cloneProject.variables.user.length === beforeDryRunConflict, 'clone-variable dry-run must not mutate project.', errors);
        const cloneDoubleApplyResult = GrafcetStudioAIApply.applyProposal(cloneAllUniqueProposal, { context: cloneContext });
        assert(!cloneDoubleApplyResult.ok, 'clone-variable double apply should fail.', errors);
        const flowProject = makeProject();
        const flowCounts = { saveProject: 0, renderTree: 0, renderGlobalVarTable: 0, refresh: 0 };
        const flowContext = makeContext(flowProject, flowCounts);
        const flowProposal = makeCreateFlowProposal('ai-prop-flow-1', {
            id: 'flow-basic-cycle',
            name: 'Basic Cycle',
            type: 'Grafcet',
            mode: 'Auto',
            steps: [
                { id: 'step-idle', number: 1, label: 'Idle', initial: true, actions: [] },
                { id: 'step-run', number: 2, label: 'Run', initial: false, actions: [] }
            ],
            transitions: [
                { id: 'trans-start', label: 'Start', condition: 'StartCommand', fromStepIds: ['step-idle'], toStepIds: ['step-run'] }
            ],
            connections: [
                { from: 'step-idle', to: 'trans-start' },
                { from: 'trans-start', to: 'step-run' }
            ]
        });
        const flowDryRunSuccess = GrafcetStudioAIApply.dryRunProposal(flowProposal, { context: flowContext });
        assert(flowDryRunSuccess.ok, 'create-flow dry-run should succeed for a valid flow proposal.', errors);
        assert(flowProject.diagrams.length === 1, 'create-flow dry-run must not mutate project.', errors);
        const flowApplySuccess = GrafcetStudioAIApply.applyProposal(flowProposal, { context: flowContext });
        assert(flowApplySuccess.ok, 'create-flow apply should succeed for a valid flow proposal.', errors);
        assert(flowProject.diagrams.length === 1, 'create-flow apply should add or resolve one diagram.', errors);
        const appliedFlowState = flowProject.diagrams[0].state || { steps: [], transitions: [], connections: [] };
        assert(appliedFlowState.steps.length === 2, 'create-flow apply should materialize both steps.', errors);
        assert(appliedFlowState.transitions.length === 1, 'create-flow apply should materialize the transition.', errors);
        assert(appliedFlowState.connections.length === 2, 'create-flow apply should materialize the connections.', errors);
        assert(appliedFlowState.steps.every(function (step) { return typeof step.x === 'number' && Number.isFinite(step.x) && typeof step.y === 'number' && Number.isFinite(step.y); }), 'create-flow apply should backfill finite step layout coordinates.', errors);
        assert(appliedFlowState.transitions.every(function (transition) { return typeof transition.x === 'number' && Number.isFinite(transition.x) && typeof transition.y === 'number' && Number.isFinite(transition.y); }), 'create-flow apply should backfill finite transition layout coordinates.', errors);
        assert(appliedFlowState.steps.find(function (step) { return step.id === 'step-run'; }).y === 400, 'create-flow layout should follow step -> transition -> step connection order.', errors);
        assert(appliedFlowState.transitions.find(function (transition) { return transition.id === 'trans-start'; }).y === 260, 'create-flow layout should place transition between connected steps.', errors);
        assert(flowProposal.status === 'applied', 'create-flow apply should mark proposal applied.', errors);
        assert(flowCounts.saveProject === 1 && flowCounts.renderTree === 1 && flowCounts.refresh === 1, 'create-flow apply should trigger persistence/render callbacks.', errors);
        const flowBackfillProject = makeProject();
        const flowBackfillContext = makeContext(flowBackfillProject, { saveProject: 0, renderTree: 0, renderGlobalVarTable: 0, refresh: 0 });
        const flowBackfillProposal = makeCreateFlowProposal('ai-prop-flow-backfill', {
            id: 'flow-backfill',
            name: 'Backfill Flow',
            steps: [
                { id: 'step-1', label: 'Step 1', actions: [{ variable: 'cy14.CoilA', qualifier: 'N', address: '', time: '' }] },
                { id: 'step-2', label: 'Step 2', actions: [{ expression: 'cy14.CoilB = 1' }] }
            ],
            transitions: [{ id: 'trans-1', label: 'Go', condition: 'cy14.SensorA' }],
            connections: [{ from: 'step-1', to: 'trans-1' }, { from: 'trans-1', to: 'step-2' }]
        });
        const flowBackfillResult = GrafcetStudioAIApply.applyProposal(flowBackfillProposal, { context: flowBackfillContext });
        const flowBackfillState = flowBackfillProject.diagrams[0].state || { steps: [], transitions: [], connections: [] };
        assert(flowBackfillResult.ok, 'create-flow apply should accept steps missing number/layout.', errors);
        assert(flowBackfillState.steps[0].number === 1 && flowBackfillState.steps[1].number === 2, 'create-flow apply should backfill sequential step numbers.', errors);
        assert(flowBackfillState.steps[0].initial === true, 'create-flow apply should mark first step initial when missing and target is empty.', errors);
        assert(flowBackfillState.steps.every(function (step) { return typeof step.x === 'number' && Number.isFinite(step.x) && typeof step.y === 'number' && Number.isFinite(step.y); }), 'create-flow apply should persist backfilled step coordinates on cloned state nodes.', errors);
        assert(flowBackfillState.transitions.every(function (transition) { return typeof transition.x === 'number' && Number.isFinite(transition.x) && typeof transition.y === 'number' && Number.isFinite(transition.y); }), 'create-flow apply should persist backfilled transition coordinates on cloned state nodes.', errors);
        assert(flowBackfillState.steps.find(function (step) { return step.id === 'step-2'; }).y === 400, 'create-flow backfill layout should follow connection order for generated steps.', errors);
        assert(flowBackfillState.transitions.find(function (transition) { return transition.id === 'trans-1'; }).y === 260, 'create-flow backfill layout should place generated transition between generated steps.', errors);
        assert(!!flowBackfillState.steps[1].actions && flowBackfillState.steps[1].actions[0].variable === 'cy14.CoilB' && flowBackfillState.steps[1].actions[0].qualifier === 'N', 'create-flow apply should normalize expression actions to UI action schema.', errors);
        const flowDuplicateResult = GrafcetStudioAIApply.applyProposal(flowProposal, { context: flowContext });
        assert(!flowDuplicateResult.ok, 'create-flow double apply should fail.', errors);
        const flowMissingEndpointResult = GrafcetStudioAIApply.dryRunProposal(makeCreateFlowProposal('ai-prop-flow-missing', {
            id: 'flow-missing',
            name: 'Missing Endpoint',
            steps: [{ id: 'step-a', number: 1, label: 'A', initial: true }],
            transitions: [{ id: 'trans-a', label: 'A', condition: 'X' }],
            connections: [{ from: '', to: 'trans-a' }]
        }), { context: flowContext });
        assert(!flowMissingEndpointResult.ok, 'create-flow dry-run should fail when from/to is missing.', errors);
        const flowUnknownEndpointResult = GrafcetStudioAIApply.dryRunProposal(makeCreateFlowProposal('ai-prop-flow-unknown', {
            id: 'flow-unknown',
            name: 'Unknown Endpoint',
            steps: [{ id: 'step-a', number: 1, label: 'A', initial: true }],
            transitions: [{ id: 'trans-a', label: 'A', condition: 'X' }],
            connections: [{ from: 'step-a', to: 'missing-node' }]
        }), { context: flowContext });
        assert(!flowUnknownEndpointResult.ok, 'create-flow dry-run should fail when a connection endpoint is missing from the proposal.', errors);
        const structureProject = makeProject();
        const structureCounts = { saveProject: 0, renderTree: 0, renderGlobalVarTable: 0, refresh: 0 };
        const structureContext = makeContext(structureProject, structureCounts);
        const structureProposal = makeCreateStructureProposal('ai-prop-structure-1', 'ServoAxis', [
            { name: 'Enable', dataType: 'Bool', varType: 'Output' }
        ]);
        const structureDryRunSuccess = GrafcetStudioAIApply.dryRunProposal(structureProposal, { context: structureContext });
        assert(structureDryRunSuccess.ok, 'create-structure dry-run should succeed for a unique structure name.', errors);
        assert(structureProject.devices.length === 0, 'create-structure dry-run must not mutate project devices.', errors);
        structureProject.devices.push({ id: 'existing-servo', name: 'servoaxis', categoryId: 'cat-other', signals: [] });
        const structureDuplicateResult = GrafcetStudioAIApply.dryRunProposal(makeCreateStructureProposal('ai-prop-structure-dup', 'ServoAxis', [
            { name: 'Enable', dataType: 'Bool', varType: 'Output' }
        ]), { context: structureContext });
        assert(!structureDuplicateResult.ok, 'create-structure dry-run should fail for duplicate structure name.', errors);
        structureProject.devices = [];
        const collisionProposal = makeCreateStructureProposal('ai-prop-structure-apply', 'ServoAxis', [
            { name: 'Enable', dataType: 'Bool', varType: 'Output' },
            { name: 'Enable!', dataType: 'Bool', varType: 'Input' },
            { name: '   ', dataType: 'Bool', varType: 'Input' },
            { name: 'Ready', dataType: 'Bool', varType: 'Input' }
        ]);
        const structureApplySuccess = GrafcetStudioAIApply.applyProposal(collisionProposal, { context: structureContext });
        assert(structureApplySuccess.ok, 'create-structure apply should succeed when at least one valid signal remains.', errors);
        assert(structureProject.devices.length === 1, 'create-structure apply should push one device.', errors);
        assert(structureProject.devices[0].name === 'ServoAxis' && structureProject.devices[0].categoryId === 'cat-other', 'created structure should use requested name and default category.', errors);
        assert(structureProject.devices[0].signals.length === 2, 'create-structure apply should skip duplicate/invalid signals and keep valid unique signals.', errors);
        assert(collisionProposal.status === 'applied', 'create-structure apply should mark proposal applied.', errors);
        assert(structureCounts.renderGlobalVarTable === 0, 'create-structure apply must not call renderGlobalVarTable.', errors);
        const beforeInvalidStructureCount = structureProject.devices.length;
        const structureInvalidSignalsResult = GrafcetStudioAIApply.applyProposal(makeCreateStructureProposal('ai-prop-structure-invalid-signals', 'ValveBlock', [
            { name: '   ', dataType: 'Bool', varType: 'Input' }
        ]), { context: structureContext });
        assert(!structureInvalidSignalsResult.ok, 'create-structure apply should fail if no valid signals remain.', errors);
        assert(structureProject.devices.length === beforeInvalidStructureCount, 'failed create-structure apply must not mutate devices.', errors);
        return {
            ok: errors.length === 0,
            errors,
            dryRunSuccess,
            dryRunFailure,
            applySuccess,
            duplicateResult,
            doubleApplyResult,
            failedPreconditionResult,
            structureDryRunSuccess,
            structureDuplicateResult,
            structureApplySuccess,
            structureInvalidSignalsResult,
            cloneAllUniqueResult,
            clonePartialConflictResult,
            cloneInternalConflictResult,
            cloneAllConflictResult,
            cloneMissingSourceResult,
            cloneDryRunConflictResult,
            cloneDoubleApplyResult,
            flowDryRunSuccess,
            flowApplySuccess,
            flowDuplicateResult,
            flowMissingEndpointResult,
            flowUnknownEndpointResult,
            callbackCounts
        };
    }
    GrafcetStudioAIApplyValidation.runApplyValidation = runApplyValidation;
})(GrafcetStudioAIApplyValidation || (GrafcetStudioAIApplyValidation = {}));

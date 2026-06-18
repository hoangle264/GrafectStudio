namespace GrafcetStudioAIApplyValidation {
  type Project = GrafcetStudioProject.Project;
  type AiProposal = GrafcetStudioAIContracts.AiProposal;

  export interface ApplyValidationResult {
    ok: boolean;
    errors: string[];
    dryRunSuccess: GrafcetStudioAIContracts.ApplyResult;
    dryRunFailure: GrafcetStudioAIContracts.ApplyResult;
    applySuccess: GrafcetStudioAIContracts.ApplyResult;
    duplicateResult: GrafcetStudioAIContracts.ApplyResult;
    doubleApplyResult: GrafcetStudioAIContracts.ApplyResult;
    failedPreconditionResult: GrafcetStudioAIContracts.ApplyResult;
    structureDryRunSuccess: GrafcetStudioAIContracts.ApplyResult;
    structureDuplicateResult: GrafcetStudioAIContracts.ApplyResult;
    structureApplySuccess: GrafcetStudioAIContracts.ApplyResult;
    structureInvalidSignalsResult: GrafcetStudioAIContracts.ApplyResult;
    cloneAllUniqueResult: GrafcetStudioAIContracts.ApplyResult;
    clonePartialConflictResult: GrafcetStudioAIContracts.ApplyResult;
    cloneInternalConflictResult: GrafcetStudioAIContracts.ApplyResult;
    cloneAllConflictResult: GrafcetStudioAIContracts.ApplyResult;
    cloneMissingSourceResult: GrafcetStudioAIContracts.ApplyResult;
    cloneDryRunConflictResult: GrafcetStudioAIContracts.ApplyResult;
    cloneDoubleApplyResult: GrafcetStudioAIContracts.ApplyResult;
    callbackCounts: { saveProject: number; renderTree: number; renderGlobalVarTable: number; refresh: number };
  }

  function assert(condition: boolean, message: string, errors: string[]): void {
    if (!condition) errors.push(message);
  }

  function makeProject(): Project {
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

  function makeCreateVariableProposal(id: string, label: string, address: string): AiProposal {
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

  function makeCloneVariableProposal(id: string, sourceLabel: string, variables: GrafcetStudioAIContracts.AiVariableProposal[]): AiProposal {
    return {
      schemaVersion: GrafcetStudioAIContracts.schemaVersion,
      id,
      intent: 'clone-variable',
      status: 'validated',
      summary: 'Apply validation clone-variable fixture.',
      data: { source: { label: sourceLabel }, variables }
    };
  }


  function makeCreateStructureProposal(id: string, name: string, signals: GrafcetStudioAIContracts.AiSignalProposal[]): AiProposal {
    return {
      schemaVersion: GrafcetStudioAIContracts.schemaVersion,
      id,
      intent: 'create-structure',
      status: 'validated',
      summary: 'Apply validation create-structure fixture.',
      data: { name, signals }
    };
  }

  function makeContext(project: Project, counts: { saveProject: number; renderTree: number; renderGlobalVarTable: number; refresh: number }): GrafcetStudioAIApply.ApplyContext {
    return {
      getProject: function() { return project; },
      saveProject: function() { counts.saveProject += 1; },
      renderTree: function() { counts.renderTree += 1; },
      renderGlobalVarTable: function() { counts.renderGlobalVarTable += 1; },
      refresh: function() { counts.refresh += 1; },
      syncVariableSignalAddressesFromDeviceTypes: function() { return true; }
    };
  }

  export function runApplyValidation(): ApplyValidationResult {
    const errors: string[] = [];
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
      callbackCounts
    };
  }
}

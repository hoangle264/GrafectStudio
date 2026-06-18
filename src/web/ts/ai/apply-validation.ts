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
      callbackCounts
    };
  }
}

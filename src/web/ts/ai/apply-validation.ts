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

  function makeContext(project: Project, counts: { saveProject: number; renderTree: number; renderGlobalVarTable: number; refresh: number }): GrafcetStudioAIApply.ApplyContext {
    return {
      getProject: function() { return project; },
      saveProject: function() { counts.saveProject += 1; },
      renderTree: function() { counts.renderTree += 1; },
      renderGlobalVarTable: function() { counts.renderGlobalVarTable += 1; },
      refresh: function() { counts.refresh += 1; }
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

    return {
      ok: errors.length === 0,
      errors,
      dryRunSuccess,
      dryRunFailure,
      applySuccess,
      duplicateResult,
      doubleApplyResult,
      failedPreconditionResult,
      callbackCounts
    };
  }
}

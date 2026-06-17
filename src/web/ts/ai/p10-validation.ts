namespace GrafcetStudioAIP10Validation {
  type Project = GrafcetStudioProject.Project;
  type AiProposal = GrafcetStudioAIContracts.AiProposal;

  export interface P10ValidationResult {
    ok: boolean;
    errors: string[];
    sanitizer: GrafcetStudioAISanitizerValidation.SanitizerValidationResult;
    contextBuilder: GrafcetStudioAIContextBuilderValidation.ContextBuilderValidationResult;
    parser: GrafcetStudioAIProposalParserValidation.ProposalParserValidationResult;
    mockService: GrafcetStudioAIMockServiceValidation.MockServiceValidationResult;
    apply: GrafcetStudioAIApplyValidation.ApplyValidationResult;
    oversizedContext: GrafcetStudioAIContextBuilder.BuildAiRequestResult;
    mockEndToEnd: GrafcetStudioAIContracts.ApplyResult;
    fullFlow: GrafcetStudioAIContracts.ApplyResult;
    realGemini: { skipped: boolean; reason: string };
  }

  function assert(condition: boolean, message: string, errors: string[]): void {
    if (!condition) errors.push(message);
  }

  function containsForbiddenText(value: unknown): boolean {
    return /C:\\Users|\\\\BUILD-SERVER|templates\\|templateRootPath|machineName|apiKey|secret|password|token|sk-test-secret|AIza/i.test(JSON.stringify(value));
  }

  function makeProject(id: string): Project {
    return {
      id,
      name: 'P10 AI Validation Project',
      machineName: 'BUILD-SERVER-SECRET',
      templateRootPath: 'C:\\Users\\Nitro\\templates',
      devices: [],
      units: [{ id: 'unit-p10', name: 'Station P10', templatePath: 'templates\\unit.tpl' }],
      diagrams: [{ id: 'diagram-p10', name: 'Main', mode: 'Main', unitId: 'unit-p10', machineName: 'BUILD-SERVER-SECRET' }],
      variables: {
        imported: [{ id: 'var-p10-imported', label: 'Existing_StartCommand', format: 'BOOL', address: 'X0', source: 'csv', apiKey: 'sk-test-secret' }],
        user: []
      },
      excelVars: [],
      unitConfig: {},
      ioMapping: {
        physicalIOs: [{ id: 'io-p10-start', deviceTag: 'StartPB', plcAddress: 'X0.0', direction: 'Input', password: 'secret' }],
        entries: []
      },
      localConfig: { token: 'sk-test-secret' }
    };
  }

  function makeOversizedRawProject(): unknown {
    const variables: GrafcetStudioProject.ProjectVariable[] = [];
    for (let index = 0; index < 250; index++) {
      variables.push({
        id: 'oversized-var-' + index,
        label: 'OversizedVariable' + index,
        format: 'BOOL',
        address: 'MR' + index,
        source: 'manual',
        comment: 'secret=sk-test-secret path=C:\\Users\\Nitro\\oversized.csv'
      });
    }

    return {
      project: Object.assign(makeProject('proj-p10-oversized'), {
        variables: { imported: variables, user: [] }
      }),
      selection: { unitId: 'unit-p10', diagramId: 'diagram-p10', apiKey: 'AIzaFakeSecret' }
    };
  }

  function makeApplyContext(project: Project, counts: { saveProject: number; renderTree: number; renderGlobalVarTable: number; refresh: number }): GrafcetStudioAIApply.ApplyContext {
    return {
      getProject: function() { return project; },
      saveProject: function() { counts.saveProject += 1; },
      renderTree: function() { counts.renderTree += 1; },
      renderGlobalVarTable: function() { counts.renderGlobalVarTable += 1; },
      refresh: function() { counts.refresh += 1; }
    };
  }

  function cloneProposal(proposal: AiProposal, proposalId: string, label: string, address: string): AiProposal {
    const copy = JSON.parse(JSON.stringify(proposal)) as AiProposal;
    copy.id = proposalId;
    copy.status = 'validated';
    if (copy.intent === 'create-variable') {
      const data = copy.data as GrafcetStudioAIContracts.CreateVariableProposalData;
      data.variable.label = label;
      data.variable.address = address;
    }
    return copy;
  }

  function runMockEndToEnd(errors: string[]): GrafcetStudioAIContracts.ApplyResult {
    GrafcetStudioAIApply.resetAppliedProposalTracking();
    const project = makeProject('proj-p10-mock-e2e');
    const callbackCounts = { saveProject: 0, renderTree: 0, renderGlobalVarTable: 0, refresh: 0 };
    const requestResult = GrafcetStudioAIContextBuilder.buildAiRequest({
      message: 'Create a mock start command variable from sanitized context.',
      selectedIntent: 'create-variable',
      rawProject: { project, selection: { unitId: 'unit-p10', diagramId: 'diagram-p10', token: 'sk-test-secret' } },
      budget: { maxItems: 20, maxContextChars: 4000 }
    });

    assert(requestResult.ok && !!requestResult.request, 'mock E2E should build a sanitized AiRequest.', errors);
    assert(!containsForbiddenText(requestResult.request), 'mock E2E request must not contain secrets, paths, or machine names.', errors);
    if (!requestResult.ok || !requestResult.request) return failedApplyResult('ai-prop-p10-mock-e2e', true, 'request build failed');

    const mockResponse = GrafcetStudioAIMockService.generateMockResponse(requestResult.request, { proposalId: 'ai-prop-p10-mock-e2e' });
    assert(mockResponse.ok && !!mockResponse.proposal, 'mock E2E should generate a valid proposal through parser/validator.', errors);
    if (!mockResponse.ok || !mockResponse.proposal) return failedApplyResult('ai-prop-p10-mock-e2e', true, 'mock response failed');

    const proposal = cloneProposal(mockResponse.proposal, 'ai-prop-p10-mock-e2e', 'P10_MockStartCommand', 'MR910');
    const preview = GrafcetStudioAIApply.dryRunProposal(proposal, { context: makeApplyContext(project, callbackCounts), dryRun: true });
    assert(preview.ok && project.variables.user.length === 0, 'mock E2E preview should pass without mutating project.', errors);

    const applyResult = GrafcetStudioAIApply.applyProposal(proposal, { context: makeApplyContext(project, callbackCounts) });
    assert(applyResult.ok, 'mock E2E apply should succeed after preview passes.', errors);
    assert(project.variables.user.length === 1, 'mock E2E apply should add exactly one user variable.', errors);
    assert(callbackCounts.saveProject === 1 && callbackCounts.renderTree === 1 && callbackCounts.renderGlobalVarTable === 1 && callbackCounts.refresh === 1, 'mock E2E apply should save and refresh exactly once.', errors);
    return applyResult;
  }

  function runFullFlow(errors: string[]): GrafcetStudioAIContracts.ApplyResult {
    GrafcetStudioAIApply.resetAppliedProposalTracking();
    const project = makeProject('proj-p10-full-flow');
    const callbackCounts = { saveProject: 0, renderTree: 0, renderGlobalVarTable: 0, refresh: 0 };
    const requestResult = GrafcetStudioAIContextBuilder.buildAiRequest({
      message: 'Create a validated variable proposal for the UI preview and host mock flow.',
      selectedIntent: 'create-variable',
      rawProject: project,
      selection: { unitId: 'unit-p10', diagramId: 'diagram-p10' },
      requestId: 'ai-req-p10-full-flow',
      budget: { maxItems: 20, maxContextChars: 4000 }
    });

    assert(requestResult.ok && !!requestResult.request, 'full flow should start with a JS chat UI/context-builder request.', errors);
    if (!requestResult.ok || !requestResult.request) return failedApplyResult('ai-prop-p10-full-flow', true, 'request build failed');

    const hostMockRaw = GrafcetStudioAIMockService.getFixtureRawText('create-variable');
    const hostReceiveResult = GrafcetStudioAIMockService.receiveHostResponse(hostMockRaw);
    assert(hostReceiveResult.ok && !!hostReceiveResult.proposal, 'full flow should accept C# mock service raw JSON through parser.', errors);
    if (!hostReceiveResult.ok || !hostReceiveResult.proposal) return failedApplyResult('ai-prop-p10-full-flow', true, 'host mock parse failed');

    const proposal = cloneProposal(hostReceiveResult.proposal, 'ai-prop-p10-full-flow', 'P10_FullFlowStartCommand', 'MR920');
    const preview = GrafcetStudioAIApply.dryRunProposal(proposal, { context: makeApplyContext(project, callbackCounts), dryRun: true });
    assert(preview.ok, 'full flow preview should pass before apply.', errors);
    assert(project.variables.user.length === 0, 'full flow preview must not mutate before apply.', errors);

    const applyResult = GrafcetStudioAIApply.applyProposal(proposal, { context: makeApplyContext(project, callbackCounts) });
    assert(applyResult.ok && applyResult.changed === true, 'full flow apply should succeed and report changed state.', errors);
    assert(project.variables.user.length === 1, 'full flow apply should mutate only during apply.', errors);
    assert(callbackCounts.saveProject === 1 && callbackCounts.renderTree === 1 && callbackCounts.renderGlobalVarTable === 1 && callbackCounts.refresh === 1, 'full flow apply should trigger save/render callbacks.', errors);
    return applyResult;
  }

  function failedApplyResult(proposalId: string, dryRun: boolean, error: string): GrafcetStudioAIContracts.ApplyResult {
    return { ok: false, proposalId, dryRun, affectedIds: [], warnings: [], errors: [error], changed: false };
  }

  export function runP10IntegrationSecurityValidation(): P10ValidationResult {
    const errors: string[] = [];
    const sanitizer = GrafcetStudioAISanitizerValidation.runSanitizerValidation();
    const contextBuilder = GrafcetStudioAIContextBuilderValidation.runContextBuilderValidation();
    const parser = GrafcetStudioAIProposalParserValidation.runProposalParserValidation();
    const mockService = GrafcetStudioAIMockServiceValidation.runMockServiceValidation();
    const apply = GrafcetStudioAIApplyValidation.runApplyValidation();

    if (!sanitizer.ok) errors.push.apply(errors, sanitizer.errors);
    if (!contextBuilder.ok) errors.push.apply(errors, contextBuilder.errors);
    if (!parser.ok) errors.push.apply(errors, parser.errors);
    if (!mockService.ok) errors.push.apply(errors, mockService.errors);
    if (!apply.ok) errors.push.apply(errors, apply.errors);

    const oversizedContext = GrafcetStudioAIContextBuilder.buildAiRequest({
      message: 'Create variable with oversized sanitized project context that must be reduced safely.',
      selectedIntent: 'create-variable',
      rawProject: makeOversizedRawProject(),
      budget: { maxItems: 250, maxContextChars: 1500, maxMessageChars: 200 }
    });
    assert(oversizedContext.ok && !!oversizedContext.request, 'oversized context should be reduced into a valid AiRequest.', errors);
    assert(!!oversizedContext.contextSize && oversizedContext.contextSize <= 1500, 'oversized context should respect maxContextChars.', errors);
    assert(oversizedContext.warnings.some(function(warning) { return /reduced|truncated/i.test(warning); }), 'oversized context should report a reduction/truncation warning.', errors);
    assert(!containsForbiddenText(oversizedContext.request), 'oversized context output must not contain path-like or secret-like fields.', errors);

    const mockEndToEnd = runMockEndToEnd(errors);
    const fullFlow = runFullFlow(errors);
    const realGemini = { skipped: true, reason: 'Real Gemini non-streaming integration is intentionally skipped unless GEMINI_API_KEY/GRAFCETSTUDIO_GEMINI_API_KEY and network access are provided outside committed tests.' };

    return {
      ok: errors.length === 0,
      errors,
      sanitizer,
      contextBuilder,
      parser,
      mockService,
      apply,
      oversizedContext,
      mockEndToEnd,
      fullFlow,
      realGemini
    };
  }
}

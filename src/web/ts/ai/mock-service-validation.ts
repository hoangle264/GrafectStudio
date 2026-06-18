namespace GrafcetStudioAIMockServiceValidation {
  export interface MockServiceValidationResult {
    ok: boolean;
    fixtureResult: GrafcetStudioAIMockService.MockFixtureValidationResult;
    createVariableResult: GrafcetStudioAIMockService.MockAiResponse;
    createStructureResult: GrafcetStudioAIMockService.MockAiResponse;
    cloneVariableResult: GrafcetStudioAIMockService.MockAiResponse;
    malformedJsonResult: GrafcetStudioAIMockService.MockAiResponse;
    wrongShapeResult: GrafcetStudioAIMockService.MockAiResponse;
    errors: string[];
  }

  function makeRequest(intent: GrafcetStudioAIContracts.AiIntent): GrafcetStudioAIContracts.AiRequest {
    return {
      schemaVersion: GrafcetStudioAIContracts.schemaVersion,
      id: 'ai-req-mock-validation',
      intent,
      message: 'Create a mock variable for validation.',
      context: {
        project: { id: 'mock-project', name: 'Mock Project' },
        variables: []
      }
    };
  }

  export function runMockServiceValidation(): MockServiceValidationResult {
    const errors: string[] = [];
    const fixtureResult = GrafcetStudioAIMockService.validateMockFixtures();
    if (!fixtureResult.ok) errors.push.apply(errors, fixtureResult.errors);

    const createVariableResult = GrafcetStudioAIMockService.generateMockResponse(makeRequest('create-variable'));
    if (!createVariableResult.ok || !createVariableResult.proposal || createVariableResult.proposal.intent !== 'create-variable') {
      errors.push('create-variable mock response must pass AiProposal validation and preserve intent.');
    }

    const createStructureResult = GrafcetStudioAIMockService.generateMockResponse(makeRequest('create-structure'));
    if (!createStructureResult.ok || !createStructureResult.proposal || createStructureResult.proposal.intent !== 'create-structure') {
      errors.push('create-structure mock response must pass AiProposal validation and preserve intent.');
    }

    const cloneVariableResult = GrafcetStudioAIMockService.generateMockResponse(makeRequest('clone-variable'));
    const cloneData = cloneVariableResult.proposal && cloneVariableResult.proposal.data as GrafcetStudioAIContracts.CloneVariableProposalData | undefined;
    if (!cloneVariableResult.ok || !cloneVariableResult.proposal || cloneVariableResult.proposal.intent !== 'clone-variable') {
      errors.push('clone-variable mock response must pass AiProposal validation and preserve intent.');
    } else if (!cloneData || !Array.isArray(cloneData.variables) || cloneData.variables.length < 2) {
      errors.push('clone-variable fixture must have variables array with at least 2 items.');
    } else if ('variable' in (cloneVariableResult.proposal.data as unknown as Record<string, unknown>)) {
      errors.push('clone-variable fixture must not use data.variable singular.');
    }

    const malformedJsonResult = GrafcetStudioAIMockService.generateMockResponse(makeRequest('create-variable'), { fixtureName: 'malformed-json' });
    if (malformedJsonResult.ok) errors.push('malformed-json mock response must intentionally fail.');

    const wrongShapeResult = GrafcetStudioAIMockService.generateMockResponse(makeRequest('create-variable'), { fixtureName: 'wrong-proposal-shape' });
    if (wrongShapeResult.ok) errors.push('wrong-proposal-shape mock response must intentionally fail.');

    return {
      ok: errors.length === 0,
      fixtureResult,
      createVariableResult,
      createStructureResult,
      cloneVariableResult,
      malformedJsonResult,
      wrongShapeResult,
      errors
    };
  }
}

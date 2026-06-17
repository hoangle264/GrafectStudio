"use strict";
var GrafcetStudioAIMockServiceValidation;
(function (GrafcetStudioAIMockServiceValidation) {
    function makeRequest(intent) {
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
    function runMockServiceValidation() {
        const errors = [];
        const fixtureResult = GrafcetStudioAIMockService.validateMockFixtures();
        if (!fixtureResult.ok)
            errors.push.apply(errors, fixtureResult.errors);
        const createVariableResult = GrafcetStudioAIMockService.generateMockResponse(makeRequest('create-variable'));
        if (!createVariableResult.ok || !createVariableResult.proposal || createVariableResult.proposal.intent !== 'create-variable') {
            errors.push('create-variable mock response must pass AiProposal validation and preserve intent.');
        }
        const malformedJsonResult = GrafcetStudioAIMockService.generateMockResponse(makeRequest('create-variable'), { fixtureName: 'malformed-json' });
        if (malformedJsonResult.ok)
            errors.push('malformed-json mock response must intentionally fail.');
        const wrongShapeResult = GrafcetStudioAIMockService.generateMockResponse(makeRequest('create-variable'), { fixtureName: 'wrong-proposal-shape' });
        if (wrongShapeResult.ok)
            errors.push('wrong-proposal-shape mock response must intentionally fail.');
        return {
            ok: errors.length === 0,
            fixtureResult,
            createVariableResult,
            malformedJsonResult,
            wrongShapeResult,
            errors
        };
    }
    GrafcetStudioAIMockServiceValidation.runMockServiceValidation = runMockServiceValidation;
})(GrafcetStudioAIMockServiceValidation || (GrafcetStudioAIMockServiceValidation = {}));

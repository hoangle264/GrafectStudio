namespace GrafcetStudioAIContextBuilderValidation {
  export interface ContextBuilderValidationResult {
    ok: boolean;
    errors: string[];
    validResult: GrafcetStudioAIContextBuilder.BuildAiRequestResult;
    invalidResult: GrafcetStudioAIContextBuilder.BuildAiRequestResult;
  }

  function assert(condition: boolean, message: string, errors: string[]): void {
    if (!condition) errors.push(message);
  }

  function containsForbiddenText(value: unknown): boolean {
    return /rawSecret|C:\\Users|apiKey|sk-test-secret|machineName|templateRootPath/i.test(JSON.stringify(value));
  }

  export function runContextBuilderValidation(): ContextBuilderValidationResult {
    const errors: string[] = [];
    const raw = {
      project: {
        id: 'raw-project-id-should-not-pass',
        machineName: 'BUILD-SERVER-01',
        templateRootPath: 'C:\\Users\\Nitro\\templates',
        rawSecret: 'sk-test-secret-123',
        variables: {
          imported: [{ id: 'v1', label: 'StartPB', format: 'BOOL', address: 'X0', apiKey: 'sk-test-secret' }],
          user: [{ id: 'v2', label: 'MotorRun', format: 'BOOL', address: 'Y0' }]
        },
        units: [{ id: 'u1', name: 'Station 1', localPath: 'C:\\Users\\Nitro\\station.json' }],
        diagrams: [{ id: 'd1', name: 'Main', mode: 'Main', unitId: 'u1' }]
      },
      selection: { unitId: 'u1', diagramId: 'd1', rawSecret: 'sk-test-secret' }
    };

    const validResult = GrafcetStudioAIContextBuilder.buildAiRequest({
      message: 'Create a variable for motor run',
      selectedIntent: 'create-variable',
      scope: ['variable', 'unit', 'diagram', 'io'],
      rawProject: raw,
      budget: { maxItems: 10, maxContextChars: 3000 }
    });

    assert(validResult.ok, 'valid context builder input should produce an AiRequest.', errors);
    assert(!!validResult.request && GrafcetStudioAIContracts.isAiRequest(validResult.request), 'valid context builder output must pass AiRequest runtime validation.', errors);
    assert(!!validResult.request && validResult.request.intent === 'create-variable', 'selectedIntent must drive the request intent.', errors);
    assert(!!validResult.request && !validResult.request.context.project, 'context builder must not pass raw project object through.', errors);
    assert(!!validResult.request && !!validResult.request.context.variables, 'context should include sanitized variables for create-variable.', errors);
    assert(!!validResult.request && !validResult.request.context.ioMapping, 'context should omit disallowed scope for create-variable.', errors);
    assert(!containsForbiddenText(validResult.request), 'AiRequest must not contain path-like or secret-like raw fields.', errors);

    const invalidResult = GrafcetStudioAIContextBuilder.buildAiRequest({
      message: '   ',
      selectedIntent: 'bad-intent',
      scope: 'variable',
      rawProject: raw
    });

    assert(!invalidResult.ok, 'invalid context builder input should return an explicit error result.', errors);
    assert(invalidResult.errors.length > 0, 'invalid context builder input should include errors.', errors);

    return { ok: errors.length === 0, errors, validResult, invalidResult };
  }
}

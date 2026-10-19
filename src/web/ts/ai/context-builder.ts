namespace GrafcetStudioAIContextBuilder {
  export type AiContextScope = GrafcetStudioAISanitizer.AiContextScope;

  export interface ContextBudgetLimits {
    maxItems?: number;
    maxContextChars?: number;
    maxMessageChars?: number;
  }

  export interface BuildAiRequestInput {
    message: unknown;
    selectedIntent?: unknown;
    scope?: unknown;
    rawProject: unknown;
    selection?: GrafcetStudioAIContracts.AiContextSelection;
    requestId?: string;
    allowAutoDetect?: boolean;
    budget?: ContextBudgetLimits;
  }

  export interface BuildAiRequestResult {
    ok: boolean;
    request?: GrafcetStudioAIContracts.AiRequest;
    errors: string[];
    warnings: string[];
    detectedIntent?: GrafcetStudioAIContracts.AiIntent;
    appliedScopes?: AiContextScope[];
    budget?: Required<ContextBudgetLimits>;
    contextSize?: number;
  }

  export interface ContextBuilderApi {
    readonly defaultBudget: Required<ContextBudgetLimits>;
    detectIntent(message: unknown): GrafcetStudioAIContracts.AiIntent | null;
    buildAiRequest(input: BuildAiRequestInput): BuildAiRequestResult;
  }

  export const defaultBudget: Required<ContextBudgetLimits> = {
    maxItems: 100,
    maxContextChars: 24000,
    maxMessageChars: 4000
  };

  const intentScopeMap: Record<GrafcetStudioAIContracts.AiIntent, readonly AiContextScope[]> = {
    'create-variable': ['variable', 'unit', 'diagram'],
    'clone-variable': ['variable', 'unit', 'diagram'],
    'map-io': ['variable', 'io'],
    'create-flow': ['variable', 'unit', 'diagram', 'step', 'io'],
    'create-structure': ['structure']
  };

  function isRecord(value: unknown): value is Record<string, unknown> {
    return !!value && typeof value === 'object' && !Array.isArray(value);
  }

  function normalizeBudget(budget?: ContextBudgetLimits): Required<ContextBudgetLimits> {
    return {
      maxItems: normalizePositiveInteger(budget ? budget.maxItems : undefined, defaultBudget.maxItems),
      maxContextChars: normalizePositiveInteger(budget ? budget.maxContextChars : undefined, defaultBudget.maxContextChars),
      maxMessageChars: normalizePositiveInteger(budget ? budget.maxMessageChars : undefined, defaultBudget.maxMessageChars)
    };
  }

  function normalizePositiveInteger(value: unknown, fallback: number): number {
    return typeof value === 'number' && Number.isFinite(value) && value > 0 ? Math.floor(value) : fallback;
  }

  function isScope(value: unknown): value is AiContextScope {
    return typeof value === 'string' && (GrafcetStudioAISanitizer.api.scopes as readonly string[]).indexOf(value) >= 0;
  }

  function normalizeScopes(scope: unknown, intent: GrafcetStudioAIContracts.AiIntent): { scopes: AiContextScope[]; errors: string[]; warnings: string[] } {
    const errors: string[] = [];
    const warnings: string[] = [];
    const allowed = intentScopeMap[intent];
    const rawScopes = scope === undefined || scope === null || scope === '' ? allowed.slice() : (Array.isArray(scope) ? scope : [scope]);
    const result: AiContextScope[] = [];

    rawScopes.forEach(function(item) {
      if (!isScope(item)) {
        errors.push('scope contains unsupported value: ' + String(item) + '.');
        return;
      }
      if (allowed.indexOf(item) < 0) {
        warnings.push('scope "' + item + '" is not used for intent "' + intent + '" and was omitted.');
        return;
      }
      if (result.indexOf(item) < 0) result.push(item);
    });

    if (!result.length) errors.push('scope must include at least one scope allowed for intent "' + intent + '".');
    return { scopes: result, errors, warnings };
  }

  function normalizeMessage(message: unknown, maxMessageChars: number): { message: string; errors: string[]; warnings: string[] } {
    const errors: string[] = [];
    const warnings: string[] = [];
    if (typeof message !== 'string') return { message: '', errors: ['message must be a non-empty string.'], warnings };
    const trimmed = message.trim();
    if (!trimmed) return { message: '', errors: ['message must be a non-empty string.'], warnings };
    if (trimmed.length > maxMessageChars) {
      warnings.push('message exceeded maxMessageChars and was truncated.');
      return { message: trimmed.slice(0, maxMessageChars), errors, warnings };
    }
    return { message: trimmed, errors, warnings };
  }

  export function detectIntent(message: unknown): GrafcetStudioAIContracts.AiIntent | null {
    if (typeof message !== 'string') return null;
    const text = message.toLowerCase();
    if (/\b(clone|duplicate|copy|nhân bản|sao ch[eé]p)\b/.test(text)) return 'clone-variable';
    if (/\b(map|mapping|io|i\/o|input|output|gán io|ánh xạ)\b/.test(text)) return 'map-io';
    if (/\b(struct|structure|device type)\b/.test(text)) return 'create-structure';
    if (/\b(flow|grafcet|step|transition|sequence|trình tự|bước)\b/.test(text)) return 'create-flow';
    if (/\b(variable|var|biến|tag|address|địa chỉ)\b/.test(text)) return 'create-variable';
    return null;
  }

  function resolveIntent(input: BuildAiRequestInput, message: string, warnings: string[], errors: string[]): GrafcetStudioAIContracts.AiIntent | null {
    const detected = detectIntent(message);
    if (GrafcetStudioAIContracts.isAiIntent(input.selectedIntent)) {
      return input.selectedIntent;
    }
    if (input.selectedIntent !== undefined && input.selectedIntent !== null && input.selectedIntent !== '') {
      errors.push('selectedIntent must be one of: create-variable, clone-variable, map-io, create-flow, create-structure.');
      return null;
    }
    if (input.allowAutoDetect && detected) {
      warnings.push('selectedIntent was not provided; optional detectIntent fallback selected "' + detected + '".');
      return detected;
    }
    errors.push('selectedIntent is required for MVP context building.');
    return null;
  }

  function makeRequestId(): string {
    const randomPart = Math.random().toString(36).slice(2, 10);
    return 'ai-req-' + Date.now().toString(36) + '-' + randomPart;
  }

  function measureContext(context: GrafcetStudioAIContracts.AiContext): number {
    return JSON.stringify(context).length;
  }

  function shrinkContext(context: GrafcetStudioAISanitizer.SanitizedAiContext, maxContextChars: number, warnings: string[]): GrafcetStudioAISanitizer.SanitizedAiContext | null {
    if (measureContext(context) <= maxContextChars) return context;

    const result: GrafcetStudioAISanitizer.SanitizedAiContext = {};
    if (context.selection) result.selection = context.selection;
    if (context.unit) result.unit = context.unit;
    if (context.diagram) result.diagram = context.diagram;
    if (context.variables) result.variables = shrinkTopLevelArray(result, 'variables', context.variables, maxContextChars);
    if (context.ioMapping) {
      result.ioMapping = { physicalIOs: [], entries: [] };
      result.ioMapping.physicalIOs = shrinkNestedIoArray(result, 'physicalIOs', context.ioMapping.physicalIOs, maxContextChars);
      result.ioMapping.entries = shrinkNestedIoArray(result, 'entries', context.ioMapping.entries, maxContextChars);
    }
    if (context.steps) result.steps = shrinkTopLevelArray(result, 'steps', context.steps, maxContextChars);
    if (context.transitions) result.transitions = shrinkTopLevelArray(result, 'transitions', context.transitions, maxContextChars);
    if (context.connections) result.connections = shrinkTopLevelArray(result, 'connections', context.connections, maxContextChars);
    if (context.flows) result.flows = shrinkTopLevelArray(result, 'flows', context.flows, maxContextChars);
    if (context.existingStructures) result.existingStructures = shrinkTopLevelArray(result, 'existingStructures', context.existingStructures, maxContextChars);

    if (measureContext(result) > maxContextChars) return null;
    warnings.push('context exceeded maxContextChars and was reduced to fit the budget.');
    return result;
  }

  function shrinkTopLevelArray<T>(context: GrafcetStudioAISanitizer.SanitizedAiContext, key: keyof GrafcetStudioAISanitizer.SanitizedAiContext, items: T[], maxContextChars: number): T[] {
    const result: T[] = [];
    for (let index = 0; index < items.length; index++) {
      result.push(items[index]);
      (context as Record<string, unknown>)[key as string] = result;
      if (measureContext(context) > maxContextChars) {
        result.pop();
        break;
      }
    }
    return result;
  }

  function shrinkNestedIoArray<T>(context: GrafcetStudioAISanitizer.SanitizedAiContext, key: keyof GrafcetStudioProject.IOMapping, items: T[], maxContextChars: number): T[] {
    const result: T[] = [];
    if (!context.ioMapping) context.ioMapping = { physicalIOs: [], entries: [] };
    for (let index = 0; index < items.length; index++) {
      result.push(items[index]);
      (context.ioMapping as unknown as Record<string, unknown>)[key as string] = result;
      if (measureContext(context) > maxContextChars) {
        result.pop();
        break;
      }
    }
    return result;
  }

  export function buildAiRequest(input: BuildAiRequestInput): BuildAiRequestResult {
    const errors: string[] = [];
    const warnings: string[] = [];
    if (!isRecord(input)) return { ok: false, errors: ['input must be an object.'], warnings };

    const budget = normalizeBudget(input.budget);
    const normalizedMessage = normalizeMessage(input.message, budget.maxMessageChars);
    errors.push.apply(errors, normalizedMessage.errors);
    warnings.push.apply(warnings, normalizedMessage.warnings);
    if (errors.length) return { ok: false, errors, warnings, budget };

    const detectedIntent = detectIntent(normalizedMessage.message) || undefined;
    const intent = resolveIntent(input, normalizedMessage.message, warnings, errors);
    if (!intent) return { ok: false, errors, warnings, detectedIntent, budget };

    const scopeResult = normalizeScopes(input.scope, intent);
    errors.push.apply(errors, scopeResult.errors);
    warnings.push.apply(warnings, scopeResult.warnings);
    if (errors.length) return { ok: false, errors, warnings, detectedIntent, appliedScopes: scopeResult.scopes, budget };

    const sanitized = GrafcetStudioAISanitizer.sanitizeAiContext(input.rawProject, {
      intent,
      scopes: scopeResult.scopes,
      selection: input.selection,
      maxItems: budget.maxItems
    });
    const limitedContext = shrinkContext(sanitized, budget.maxContextChars, warnings);
    if (!limitedContext) {
      errors.push('sanitized context exceeds maxContextChars even after reduction.');
      return { ok: false, errors, warnings, detectedIntent, appliedScopes: scopeResult.scopes, budget, contextSize: measureContext(sanitized) };
    }

    const request: GrafcetStudioAIContracts.AiRequest = {
      schemaVersion: GrafcetStudioAIContracts.schemaVersion,
      id: typeof input.requestId === 'string' && input.requestId.trim() ? input.requestId.trim() : makeRequestId(),
      intent,
      message: normalizedMessage.message,
      context: limitedContext
    };

    const validation = GrafcetStudioAIContracts.validateAiRequest(request);
    if (!validation.ok || !validation.value) {
      errors.push.apply(errors, validation.errors);
      return { ok: false, errors, warnings, detectedIntent, appliedScopes: scopeResult.scopes, budget, contextSize: measureContext(limitedContext) };
    }

    return {
      ok: true,
      request: validation.value,
      errors: [],
      warnings,
      detectedIntent,
      appliedScopes: scopeResult.scopes,
      budget,
      contextSize: measureContext(limitedContext)
    };
  }

  export const api: ContextBuilderApi = {
    defaultBudget,
    detectIntent,
    buildAiRequest
  };
}

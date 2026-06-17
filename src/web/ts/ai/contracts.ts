namespace GrafcetStudioAIContracts {
  export const schemaVersion = '1.0.0';

  export type AiIntent = 'create-variable' | 'clone-variable' | 'map-io' | 'create-flow';
  export type AiProposalStatus = 'draft' | 'validated' | 'applied' | 'discarded' | 'invalid';

  export interface AiContextSelection {
    unitId?: string;
    diagramId?: string;
    variableLabel?: string;
    physicalIOId?: string;
    [key: string]: unknown;
  }

  export interface AiContext {
    project?: GrafcetStudioProject.ProjectInfo;
    unit?: GrafcetStudioProject.UnitInfo;
    diagram?: GrafcetStudioProject.DiagramInfo;
    variables?: GrafcetStudioProject.ProjectVariable[];
    deviceTypes?: GrafcetStudioProject.DeviceType[];
    ioMapping?: GrafcetStudioProject.IOMapping;
    flows?: AiFlowProposal[];
    selection?: AiContextSelection;
    [key: string]: unknown;
  }

  export interface AiRequest {
    schemaVersion: string;
    id: string;
    intent: AiIntent;
    message: string;
    context: AiContext;
  }

  export interface AiVariableProposal {
    label: string;
    format: string;
    address?: string | null;
    signalAddresses?: Record<string, string>;
    kind?: GrafcetStudioProject.VariableKind;
    dataType?: string;
    comment?: string;
    source?: GrafcetStudioProject.VariableSource;
  }

  export interface CreateVariableProposalData {
    variable: AiVariableProposal;
    bucket?: 'user' | 'imported' | string;
  }

  export interface CloneVariableProposalData {
    source: {
      id?: string;
      label?: string;
    };
    variable: AiVariableProposal;
  }

  export interface MapIOProposalData {
    entries: GrafcetStudioProject.IOMappingEntry[];
  }

  export interface AiFlowProposal extends GrafcetStudioProject.FlowInfo {
    connections?: GrafcetStudioProject.Connection[];
  }

  export interface CreateFlowProposalData {
    flow: AiFlowProposal;
  }

  export type AiProposalData = CreateVariableProposalData | CloneVariableProposalData | MapIOProposalData | CreateFlowProposalData;

  export interface AiProposal {
    schemaVersion: string;
    id: string;
    intent: AiIntent;
    status: AiProposalStatus;
    data: AiProposalData;
    requestId?: string;
    summary?: string;
    warnings?: string[];
    errors?: string[];
  }

  export interface ApplyResult {
    ok: boolean;
    proposalId: string;
    dryRun: boolean;
    affectedIds: string[];
    warnings: string[];
    errors: string[];
    changed?: boolean;
  }

  export interface ValidationResult<T> {
    ok: boolean;
    value?: T;
    errors: string[];
  }

  export interface ContractsApi {
    readonly schemaVersion: string;
    readonly intents: readonly AiIntent[];
    isAiIntent(value: unknown): value is AiIntent;
    isAiRequest(value: unknown): value is AiRequest;
    validateAiRequest(value: unknown): ValidationResult<AiRequest>;
    isAiProposal(value: unknown): value is AiProposal;
    validateAiProposal(value: unknown): ValidationResult<AiProposal>;
  }

  const intents: readonly AiIntent[] = ['create-variable', 'clone-variable', 'map-io', 'create-flow'];
  const proposalStatuses: readonly AiProposalStatus[] = ['draft', 'validated', 'applied', 'discarded', 'invalid'];

  function isRecord(value: unknown): value is Record<string, unknown> {
    return !!value && typeof value === 'object' && !Array.isArray(value);
  }

  function isString(value: unknown): value is string {
    return typeof value === 'string';
  }

  function isOptionalString(value: unknown): boolean {
    return value == null || isString(value);
  }

  function isStringArray(value: unknown): value is string[] {
    return Array.isArray(value) && value.every(isString);
  }

  function hasKnownSchemaVersion(value: Record<string, unknown>, errors: string[], path: string): boolean {
    if (value.schemaVersion !== schemaVersion) {
      errors.push(path + '.schemaVersion must be "' + schemaVersion + '".');
      return false;
    }
    return true;
  }

  export function isAiIntent(value: unknown): value is AiIntent {
    return isString(value) && (intents as readonly string[]).indexOf(value) >= 0;
  }

  function isAiProposalStatus(value: unknown): value is AiProposalStatus {
    return isString(value) && (proposalStatuses as readonly string[]).indexOf(value) >= 0;
  }

  function validateVariableProposal(value: unknown, errors: string[], path: string): value is AiVariableProposal {
    if (!isRecord(value)) {
      errors.push(path + ' must be an object.');
      return false;
    }
    if (!isString(value.label) || !value.label.trim()) errors.push(path + '.label must be a non-empty string.');
    if (!isString(value.format) || !value.format.trim()) errors.push(path + '.format must be a non-empty string.');
    if (!isOptionalString(value.address)) errors.push(path + '.address must be a string or null when provided.');
    if (value.signalAddresses != null && !isStringRecord(value.signalAddresses)) errors.push(path + '.signalAddresses must map signal ids to addresses.');
    if (value.kind != null && !isString(value.kind)) errors.push(path + '.kind must be a string when provided.');
    if (value.dataType != null && !isString(value.dataType)) errors.push(path + '.dataType must be a string when provided.');
    if (value.comment != null && !isString(value.comment)) errors.push(path + '.comment must be a string when provided.');
    if (value.source != null && !isString(value.source)) errors.push(path + '.source must be a string when provided.');
    return errors.length === 0;
  }

  function isStringRecord(value: unknown): value is Record<string, string> {
    return isRecord(value) && Object.keys(value).every(function(key) { return isString(value[key]); });
  }

  function validateIOMappingEntry(value: unknown, errors: string[], path: string): value is GrafcetStudioProject.IOMappingEntry {
    if (!isRecord(value)) {
      errors.push(path + ' must be an object.');
      return false;
    }
    if (!isString(value.physicalIOId) || !value.physicalIOId.trim()) errors.push(path + '.physicalIOId must be a non-empty string.');
    if (!isString(value.appVariable)) errors.push(path + '.appVariable must be a string.');
    if (!isString(value.status) || !value.status.trim()) errors.push(path + '.status must be a non-empty string.');
    if (typeof value.matchScore !== 'number' || !Number.isFinite(value.matchScore)) errors.push(path + '.matchScore must be a finite number.');
    return errors.length === 0;
  }

  function validateStep(value: unknown, errors: string[], path: string): value is GrafcetStudioProject.Step {
    if (!isRecord(value)) {
      errors.push(path + ' must be an object.');
      return false;
    }
    if (!isString(value.id) || !value.id.trim()) errors.push(path + '.id must be a non-empty string.');
    if (value.number != null && (typeof value.number !== 'number' || !Number.isFinite(value.number))) errors.push(path + '.number must be a finite number when provided.');
    if (value.label != null && !isString(value.label)) errors.push(path + '.label must be a string when provided.');
    if (value.initial != null && typeof value.initial !== 'boolean') errors.push(path + '.initial must be a boolean when provided.');
    if (value.actions != null && !Array.isArray(value.actions)) errors.push(path + '.actions must be an array when provided.');
    return errors.length === 0;
  }

  function validateTransition(value: unknown, errors: string[], path: string): value is GrafcetStudioProject.Transition {
    if (!isRecord(value)) {
      errors.push(path + ' must be an object.');
      return false;
    }
    if (!isString(value.id) || !value.id.trim()) errors.push(path + '.id must be a non-empty string.');
    if (value.label != null && !isString(value.label)) errors.push(path + '.label must be a string when provided.');
    if (value.condition != null && !isString(value.condition)) errors.push(path + '.condition must be a string when provided.');
    if (value.fromStepIds != null && !isStringArray(value.fromStepIds)) errors.push(path + '.fromStepIds must be a string array when provided.');
    if (value.toStepIds != null && !isStringArray(value.toStepIds)) errors.push(path + '.toStepIds must be a string array when provided.');
    return errors.length === 0;
  }

  function validateConnection(value: unknown, errors: string[], path: string): value is GrafcetStudioProject.Connection {
    if (!isRecord(value)) {
      errors.push(path + ' must be an object.');
      return false;
    }
    const from = value.from != null ? value.from : value.fromId;
    const to = value.to != null ? value.to : value.toId;
    if (!isString(from) || !from.trim()) errors.push(path + '.from/fromId must be a non-empty string.');
    if (!isString(to) || !to.trim()) errors.push(path + '.to/toId must be a non-empty string.');
    return errors.length === 0;
  }

  function validateFlowProposal(value: unknown, errors: string[], path: string): value is AiFlowProposal {
    if (!isRecord(value)) {
      errors.push(path + ' must be an object.');
      return false;
    }
    if (value.id != null && !isString(value.id)) errors.push(path + '.id must be a string when provided.');
    if (value.name != null && !isString(value.name)) errors.push(path + '.name must be a string when provided.');
    if (!Array.isArray(value.steps)) errors.push(path + '.steps must be an array.');
    else value.steps.forEach(function(step, index) { validateStep(step, errors, path + '.steps[' + index + ']'); });
    if (!Array.isArray(value.transitions)) errors.push(path + '.transitions must be an array.');
    else value.transitions.forEach(function(transition, index) { validateTransition(transition, errors, path + '.transitions[' + index + ']'); });
    if (value.connections != null) {
      if (!Array.isArray(value.connections)) errors.push(path + '.connections must be an array when provided.');
      else value.connections.forEach(function(connection, index) { validateConnection(connection, errors, path + '.connections[' + index + ']'); });
    }
    return errors.length === 0;
  }

  function validateProposalData(intent: AiIntent, value: unknown, errors: string[]): value is AiProposalData {
    if (!isRecord(value)) {
      errors.push('proposal.data must be an object.');
      return false;
    }
    switch (intent) {
      case 'create-variable':
        validateVariableProposal(value.variable, errors, 'proposal.data.variable');
        if (value.bucket != null && !isString(value.bucket)) errors.push('proposal.data.bucket must be a string when provided.');
        break;
      case 'clone-variable':
        if (!isRecord(value.source)) errors.push('proposal.data.source must be an object.');
        else if (!isOptionalString(value.source.id) || !isOptionalString(value.source.label) || (!value.source.id && !value.source.label)) errors.push('proposal.data.source requires id or label.');
        validateVariableProposal(value.variable, errors, 'proposal.data.variable');
        break;
      case 'map-io':
        if (!Array.isArray(value.entries)) errors.push('proposal.data.entries must be an array.');
        else value.entries.forEach(function(entry, index) { validateIOMappingEntry(entry, errors, 'proposal.data.entries[' + index + ']'); });
        break;
      case 'create-flow':
        validateFlowProposal(value.flow, errors, 'proposal.data.flow');
        break;
      default:
        errors.push('proposal.intent is not supported.');
        break;
    }
    return errors.length === 0;
  }

  export function validateAiRequest(value: unknown): ValidationResult<AiRequest> {
    const errors: string[] = [];
    if (!isRecord(value)) return { ok: false, errors: ['request must be an object.'] };
    hasKnownSchemaVersion(value, errors, 'request');
    if (!isString(value.id) || !value.id.trim()) errors.push('request.id must be a non-empty string.');
    if (!isAiIntent(value.intent)) errors.push('request.intent must be one of: ' + intents.join(', ') + '.');
    if (!isString(value.message) || !value.message.trim()) errors.push('request.message must be a non-empty string.');
    if (!isRecord(value.context)) errors.push('request.context must be an object.');
    return errors.length ? { ok: false, errors } : { ok: true, value: value as unknown as AiRequest, errors: [] };
  }

  export function isAiRequest(value: unknown): value is AiRequest {
    return validateAiRequest(value).ok;
  }

  export function validateAiProposal(value: unknown): ValidationResult<AiProposal> {
    const errors: string[] = [];
    if (!isRecord(value)) return { ok: false, errors: ['proposal must be an object.'] };
    hasKnownSchemaVersion(value, errors, 'proposal');
    if (!isString(value.id) || !value.id.trim()) errors.push('proposal.id must be a non-empty string.');
    if (!isAiIntent(value.intent)) errors.push('proposal.intent must be one of: ' + intents.join(', ') + '.');
    if (!isAiProposalStatus(value.status)) errors.push('proposal.status must be one of: ' + proposalStatuses.join(', ') + '.');
    if (value.requestId != null && !isString(value.requestId)) errors.push('proposal.requestId must be a string when provided.');
    if (value.summary != null && !isString(value.summary)) errors.push('proposal.summary must be a string when provided.');
    if (value.warnings != null && !isStringArray(value.warnings)) errors.push('proposal.warnings must be a string array when provided.');
    if (value.errors != null && !isStringArray(value.errors)) errors.push('proposal.errors must be a string array when provided.');
    if (isAiIntent(value.intent)) validateProposalData(value.intent, value.data, errors);
    return errors.length ? { ok: false, errors } : { ok: true, value: value as unknown as AiProposal, errors: [] };
  }

  export function isAiProposal(value: unknown): value is AiProposal {
    return validateAiProposal(value).ok;
  }

  export const api: ContractsApi = {
    schemaVersion,
    intents,
    isAiIntent,
    isAiRequest,
    validateAiRequest,
    isAiProposal,
    validateAiProposal
  };
}


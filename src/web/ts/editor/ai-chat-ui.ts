"use strict";

type AiChatProposal = GrafcetStudioAIContracts.AiProposal;
type AiChatApplyResult = GrafcetStudioAIContracts.ApplyResult;
type AiChatVariableProposal = GrafcetStudioAIContracts.AiVariableProposal;
type AiChatDiagramState = GrafcetStudioProject.DiagramState;

declare const VARS_TAB_ID: string;
declare function isVirtualTab(id: string): boolean;
declare function loadDiagramData(id: string): GrafcetStudioProject.StoredDiagramData | null;
declare function render(): void;
declare function applyView(): void;
declare function renderTree(): void;
declare function renderGlobalVarTable(): void;
declare function syncStructDataFromProjectData(): boolean;
declare function syncVariableSignalAddressesFromDeviceTypes(): boolean;
declare function saveProject(): void;

interface AiChatMessage {
  id: string;
  role: 'user' | 'assistant';
  text: string;
  error?: boolean;
  proposalId?: string;
  streaming?: boolean;
  streamStatus?: string[];
  streamText?: string;
  streamDone?: boolean;
}

interface AiChatProposalRecord {
  proposal: AiChatProposal;
  preview: AiChatApplyResult;
  sourceText: string;
  editing: boolean;
  editError: string;
}

const AI_CHAT_STATE: {
  messages: AiChatMessage[];
  proposals: Record<string, AiChatProposalRecord>;
  pendingRawText: string;
  pendingRequestId: string | null;
  pendingStreamMessageId: string | null;
  pendingStreamFinalText: string;
} = {
  messages: [],
  proposals: {},
  pendingRawText: '',
  pendingRequestId: null,
  pendingStreamMessageId: null,
  pendingStreamFinalText: ''
};

function aiChatBridge(): GrafcetStudioAIBridge.AiBridgeApi | null {
  return window.GrafcetStudioAI || (window.GrafcetStudio && (window.GrafcetStudio as unknown as { ai?: GrafcetStudioAIBridge.AiBridgeApi }).ai) || null;
}

function aiChatEscape(value: unknown): string {
  return String(value == null ? '' : value).replace(/[&<>"']/g, function(ch) {
    return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' } as Record<string, string>)[ch];
  });
}

function aiChatText(value: unknown, fallback?: string): string {
  const text = String(value == null ? '' : value).trim();
  return text || fallback || '';
}

function aiChatClone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value));
}

function aiChatGetBody(): HTMLElement | null {
  return document.getElementById('ai-chat-body');
}

function setRightPanelTab(tab: string): void {
  const mode = tab === 'ai' ? 'ai' : 'props';
  const panel = document.getElementById('right-panel');
  const title = document.getElementById('rpanel-head-title');
  const propsTab = document.getElementById('right-tab-props');
  const aiTab = document.getElementById('right-tab-ai');
  if (panel) {
    panel.classList.toggle('right-panel-tab-ai', mode === 'ai');
    panel.classList.toggle('right-panel-tab-props', mode !== 'ai');
  }
  if (title) title.textContent = mode === 'ai' ? 'AI PREVIEW' : 'PROPERTIES';
  if (propsTab) {
    propsTab.classList.toggle('active', mode !== 'ai');
    propsTab.setAttribute('aria-selected', mode !== 'ai' ? 'true' : 'false');
  }
  if (aiTab) {
    aiTab.classList.toggle('active', mode === 'ai');
    aiTab.setAttribute('aria-selected', mode === 'ai' ? 'true' : 'false');
  }
}

function toggleAiChatPanel(show?: boolean): void {
  const panel = document.getElementById('ai-chat-panel');
  if (!panel) return;
  panel.classList.toggle('show', show !== false);
  setRightPanelTab(show === false ? 'props' : 'ai');
  if (show !== false && !AI_CHAT_STATE.messages.length) {
    aiChatAddMessage('assistant', 'Create or receive an AI proposal here. Preview and validation are required before Apply is enabled.');
  }
}

function aiChatAddMessage(role: 'user' | 'assistant', text: string, options?: Partial<AiChatMessage>): AiChatMessage {
  const message: AiChatMessage = Object.assign({ id: 'ai-msg-' + Date.now() + '-' + Math.random().toString(36).slice(2, 7), role, text }, options || {});
  AI_CHAT_STATE.messages.push(message);
  aiChatRender();
  return message;
}

function aiChatFindMessage(messageId: string | null): AiChatMessage | null {
  for (let index = 0; index < AI_CHAT_STATE.messages.length; index++) {
    if (AI_CHAT_STATE.messages[index].id === messageId) return AI_CHAT_STATE.messages[index];
  }
  return null;
}

function aiChatResetPendingStream(): void {
  AI_CHAT_STATE.pendingRawText = '';
  AI_CHAT_STATE.pendingStreamFinalText = '';
  AI_CHAT_STATE.pendingStreamMessageId = null;
}

function aiChatEnsureStreamMessage(): AiChatMessage {
  let message = aiChatFindMessage(AI_CHAT_STATE.pendingStreamMessageId);
  if (!message) {
    message = aiChatAddMessage('assistant', 'AI streaming started...', { streaming: true, streamStatus: [], streamText: '', streamDone: false });
    AI_CHAT_STATE.pendingStreamMessageId = message.id;
  }
  return message;
}

function aiChatAppendStreamStatus(text: string): void {
  const message = aiChatEnsureStreamMessage();
  const value = aiChatText(text, 'Streaming update.');
  if (value) message.streamStatus!.push(value);
  message.text = message.streamStatus!.slice(-3).join('\n');
  aiChatRender();
}

function aiChatAppendStreamDelta(text: unknown): void {
  const message = aiChatEnsureStreamMessage();
  message.streamText += String(text == null ? '' : text);
  AI_CHAT_STATE.pendingRawText += String(text == null ? '' : text);
  message.text = 'Receiving proposal JSON... ' + message.streamText!.length + ' chars';
  aiChatRender();
}

function aiChatFinalizeStream(rawText: string): { ok: boolean; proposal?: AiChatProposal; rawText?: string; errors?: string[] } {
  const bridge = aiChatBridge();
  const finalText = String(rawText == null || rawText === '' ? AI_CHAT_STATE.pendingRawText : rawText);
  const message = aiChatEnsureStreamMessage();
  message.streamDone = true;
  message.text = 'Validating final AI proposal...';
  const result: GrafcetStudioAIMockService.MockAiResponse | { ok: boolean; errors: string[] } = bridge && bridge.mockService
    ? bridge.mockService.receiveHostResponse(finalText)
    : { ok: false, errors: ['AI bridge is not available.'] };
  if (result.ok && 'proposal' in result && result.proposal) {
    aiChatAddProposal(result.proposal, result.rawText);
    message.text = 'Streaming complete. Proposal is ready for preview.';
  } else {
    message.error = true;
    message.text = (result.errors || ['AI streaming response failed validation.']).join('; ');
  }
  aiChatResetPendingStream();
  aiChatRender();
  return result;
}

function aiChatFailStream(text: string): void {
  const message = aiChatEnsureStreamMessage();
  message.error = true;
  message.streamDone = true;
  message.text = aiChatText(text, 'AI streaming failed before producing a complete validated proposal.');
  aiChatResetPendingStream();
  aiChatRender();
}
function aiChatReloadActiveCanvasFromStorage(): boolean {
  if (!activeDiagramId || activeDiagramId === VARS_TAB_ID || activeDiagramId === IO_MAPPING_TAB_ID || String(activeDiagramId).indexOf("__struct__:") === 0) return false;
  if (typeof loadDiagramData !== "function") return false;
  const data = loadDiagramData(activeDiagramId);
  if (!data) return false;
  state = data.state || { steps: [], transitions: [], connections: [] } as AiChatDiagramState;
  if (!Array.isArray(state.steps)) state.steps = [];
  if (!Array.isArray(state.transitions)) state.transitions = [];
  if (!Array.isArray((state as unknown as { parallels?: unknown[] }).parallels)) (state as unknown as { parallels?: unknown[] }).parallels = [];
  if (!Array.isArray(state.connections)) state.connections = [];
  if (!Array.isArray((state as unknown as { vars?: unknown[] }).vars)) (state as unknown as { vars?: unknown[] }).vars = [];
  nextId = data.nextId || 1;
  nextStepNum = Math.max(1, data.nextStepNum || 1);
  viewX = data.viewX ?? 60;
  viewY = data.viewY ?? 40;
  viewScale = data.viewScale ?? 1;
  if (typeof render === "function") render();
  if (typeof applyView === "function") applyView();
  return true;
}

function aiChatMakeApplyContext(): GrafcetStudioAIApply.ApplyContext {
  return {
    getProject: function() { return project; },
    saveProject: typeof saveProject === "function" ? saveProject : undefined,
    renderTree: typeof renderTree === "function" ? renderTree : undefined,
    renderGlobalVarTable: typeof renderGlobalVarTable === "function" ? renderGlobalVarTable : undefined,
    refresh: function() {
      if (typeof syncStructDataFromProjectData === 'function') syncStructDataFromProjectData();
      if (typeof syncVariableSignalAddressesFromDeviceTypes === 'function') syncVariableSignalAddressesFromDeviceTypes();
      if (activeDiagramId === VARS_TAB_ID && typeof renderGlobalVarTable === 'function') renderGlobalVarTable();
      aiChatReloadActiveCanvasFromStorage();
    }
  };
}

function aiChatValidatePreview(proposal: AiChatProposal): AiChatApplyResult {
  const bridge = aiChatBridge();
  if (!bridge || !bridge.contracts || !bridge.applyLayer) {
    return { ok: false, proposalId: proposal.id, dryRun: true, errors: ['AI bridge is not available.'], warnings: [], affectedIds: [] };
  }
  const validation = bridge.contracts.validateAiProposal(proposal);
  if (!validation.ok) return { ok: false, proposalId: proposal.id, dryRun: true, errors: validation.errors, warnings: [], affectedIds: [] };
  if (validation.value && validation.value.status !== 'validated') {
    return { ok: false, proposalId: proposal.id, dryRun: true, errors: ['Proposal must be validated before preview/apply; current status is ' + validation.value.status + '.'], warnings: [], affectedIds: [] };
  }
  return bridge.applyLayer.dryRunProposal(validation.value || proposal, { context: aiChatMakeApplyContext(), dryRun: true });
}

function aiChatAddProposal(proposal: AiChatProposal, sourceText?: string): AiChatProposalRecord {
  const bridge = aiChatBridge();
  const safeProposal = bridge && bridge.proposalParser
    ? bridge.proposalParser.normalizeAiProposal(aiChatClone(proposal))
    : aiChatClone(proposal);
  const preview = aiChatValidatePreview(safeProposal);
  const record: AiChatProposalRecord = {
    proposal: safeProposal,
    preview,
    sourceText: sourceText || '',
    editing: false,
    editError: ''
  };
  AI_CHAT_STATE.proposals[safeProposal.id] = record;
  aiChatAddMessage('assistant', safeProposal.summary || 'AI proposal preview.', { proposalId: safeProposal.id });
  return record;
}

function aiChatVariableRows(variable: Partial<AiChatVariableProposal>, extraRows?: Array<[string, unknown]>): Array<[string, unknown]> {
  const signalAddresses = variable.signalAddresses ? Object.keys(variable.signalAddresses).map(function(key) {
    return key + ': ' + variable.signalAddresses![key];
  }).join(', ') : '';
  return (extraRows || []).concat([
    ['Label', variable.label],
    ['Format', variable.format || variable.dataType],
    ['Kind', variable.kind],
    ['Address', variable.address],
    ['Source', variable.source],
    ['Signals', signalAddresses],
    ['Comment', variable.comment]
  ]);
}

function aiChatRenderRows(rows: Array<[string, unknown]>): string {
  return '<table class="ai-preview-table"><tbody>' + rows.filter(function(row) { return aiChatText(row[1], ''); }).map(function(row) {
    return '<tr><td>' + aiChatEscape(row[0]) + '</td><td>' + aiChatEscape(row[1]) + '</td></tr>';
  }).join('') + '</tbody></table>';
}

function aiChatRenderPreview(proposal: AiChatProposal): string {
  const data = (proposal && proposal.data ? proposal.data : {}) as Record<string, unknown>;
  if (proposal.intent === 'create-variable') {
    const createData = data as unknown as GrafcetStudioAIContracts.CreateVariableProposalData;
    return aiChatRenderRows(aiChatVariableRows(createData.variable || {}, [['Bucket', createData.bucket || 'user']]));
  }
  if (proposal.intent === 'clone-variable') {
    const cloneData = data as unknown as GrafcetStudioAIContracts.CloneVariableProposalData;
    const source = cloneData.source || {};
    const variables = Array.isArray(cloneData.variables) ? cloneData.variables : [];
    const sourceRows = aiChatRenderRows([
      ['Source Label', source.label],
      ['Source Id', source.id],
      ['Target Count', variables.length]
    ]);
    const variableTables = variables.map(function(variable, index) {
      return '<div class="ai-proposal-summary">Clone #' + aiChatEscape(index + 1) + '</div>' + aiChatRenderRows(aiChatVariableRows(variable || {}, []));
    }).join('');
    return sourceRows + variableTables;
  }
  return '<pre class="ai-preview-json">' + aiChatEscape(JSON.stringify(data, null, 2)) + '</pre>';
}

function aiChatRenderList(items: unknown[] | undefined, className: string): string {
  const list = Array.isArray(items) ? items.filter(Boolean) : [];
  if (!list.length) return '';
  return '<ul class="ai-message-list ' + className + '">' + list.map(function(item) { return '<li>' + aiChatEscape(item) + '</li>'; }).join('') + '</ul>';
}

function aiChatCanApply(record: AiChatProposalRecord | null): boolean {
  return !!record && !!record.preview && record.preview.ok && record.proposal.status === 'validated';
}

function aiChatRenderProposal(record: AiChatProposalRecord): string {
  const proposal = record.proposal;
  const preview = record.preview || { ok: false, errors: ['Preview was not run.'], warnings: [] };
  const appliedOrDiscarded = proposal.status === 'applied' || proposal.status === 'discarded';
  const canApply = aiChatCanApply(record) && !appliedOrDiscarded;
  const editBox = record.editing
    ? '<textarea class="ai-chat-input" id="ai-edit-' + aiChatEscape(proposal.id) + '" rows="8">' + aiChatEscape(JSON.stringify(proposal, null, 2)) + '</textarea>' +
      '<div class="ai-proposal-actions"><button class="btn a" onclick="aiChatSaveEdit(\'' + aiChatEscape(proposal.id) + '\')">Save Edit</button><button class="btn" onclick="aiChatCancelEdit(\'' + aiChatEscape(proposal.id) + '\')">Cancel</button></div>'
    : '';
  return '<div class="ai-proposal-head"><span class="ai-proposal-title">' + aiChatEscape(proposal.intent) + '</span><span class="ai-proposal-status">' + aiChatEscape(proposal.status) + '</span></div>' +
    '<div class="ai-proposal-summary">' + aiChatEscape(proposal.summary || 'Review this proposal before applying.') + '</div>' +
    aiChatRenderPreview(proposal) +
    aiChatRenderList(proposal.warnings, 'ai-warnings') +
    aiChatRenderList(preview.warnings, 'ai-warnings') +
    aiChatRenderList(proposal.errors, 'ai-errors') +
    aiChatRenderList(preview.errors, 'ai-errors') +
    (record.editError ? aiChatRenderList([record.editError], 'ai-errors') : '') +
    '<div class="ai-proposal-actions">' +
      '<button class="btn a" onclick="aiChatApplyProposal(\'' + aiChatEscape(proposal.id) + '\')" ' + (canApply ? '' : 'disabled') + '>Apply</button>' +
      '<button class="btn r" onclick="aiChatDiscardProposal(\'' + aiChatEscape(proposal.id) + '\')" ' + (appliedOrDiscarded ? 'disabled' : '') + '>Discard</button>' +
      '<button class="btn" onclick="aiChatEditProposal(\'' + aiChatEscape(proposal.id) + '\')" ' + (appliedOrDiscarded ? 'disabled' : '') + '>Edit</button>' +
    '</div>' + editBox;
}

function aiChatRender(): void {
  const body = aiChatGetBody();
  if (!body) return;
  body.innerHTML = AI_CHAT_STATE.messages.map(function(message) {
    const record = message.proposalId ? AI_CHAT_STATE.proposals[message.proposalId] : null;
    const classes = ['ai-bubble', message.role === 'user' ? 'ai-bubble-user' : 'ai-bubble-assistant'];
    if (message.error) classes.push('ai-bubble-error');
    return '<div class="' + classes.join(' ') + '">' +
      (record ? aiChatRenderProposal(record) : aiChatEscape(message.text)) +
    '</div>';
  }).join('');
  body.scrollTop = body.scrollHeight;
}

function aiChatApplyProposal(proposalId: string): void {
  const record = AI_CHAT_STATE.proposals[proposalId];
  if (!record || record.proposal.status === 'applied' || record.proposal.status === 'discarded') return;
  record.preview = aiChatValidatePreview(record.proposal);
  if (!aiChatCanApply(record)) {
    aiChatRender();
    return;
  }
  try {
    const result = aiChatBridge()!.applyLayer.applyProposal(record.proposal, { context: aiChatMakeApplyContext() });
    if (!result.ok) record.preview = result;
    else record.preview = Object.assign({}, result, { warnings: result.warnings || [], errors: [] });
  } catch (error) {
    record.preview = { ok: false, proposalId, dryRun: false, affectedIds: [], warnings: [], errors: [error instanceof Error ? error.message : String(error)], changed: false };
  }
  aiChatRender();
}

function aiChatDiscardProposal(proposalId: string): void {
  const record = AI_CHAT_STATE.proposals[proposalId];
  if (!record || record.proposal.status === 'applied' || record.proposal.status === 'discarded') return;
  record.proposal.status = 'discarded';
  record.preview = { ok: false, proposalId, dryRun: true, affectedIds: [], warnings: [], errors: ['Proposal was discarded.'], changed: false };
  aiChatRender();
}

function aiChatEditProposal(proposalId: string): void {
  const record = AI_CHAT_STATE.proposals[proposalId];
  if (!record || record.proposal.status === 'applied' || record.proposal.status === 'discarded') return;
  record.editing = true;
  record.editError = '';
  aiChatRender();
}

function aiChatCancelEdit(proposalId: string): void {
  const record = AI_CHAT_STATE.proposals[proposalId];
  if (!record) return;
  record.editing = false;
  record.editError = '';
  aiChatRender();
}

function aiChatSaveEdit(proposalId: string): void {
  const record = AI_CHAT_STATE.proposals[proposalId];
  const input = document.getElementById('ai-edit-' + proposalId) as HTMLTextAreaElement | null;
  if (!record || !input) return;
  try {
    const edited = JSON.parse(input.value);
    const bridge = aiChatBridge()!;
    const normalized = bridge.proposalParser.normalizeAiProposal(edited);
    const validation = bridge.contracts.validateAiProposal(normalized);
    if (!validation.ok || !validation.value) {
      record.editError = validation.errors.join('; ');
      aiChatRender();
      return;
    }
    record.proposal = validation.value;
    record.preview = aiChatValidatePreview(record.proposal);
    record.editing = false;
    record.editError = '';
    if (record.proposal.id !== proposalId) {
      delete AI_CHAT_STATE.proposals[proposalId];
      AI_CHAT_STATE.proposals[record.proposal.id] = record;
      AI_CHAT_STATE.messages.forEach(function(message) { if (message.proposalId === proposalId) message.proposalId = record.proposal.id; });
    }
  } catch (error) {
    record.editError = 'Edited proposal JSON is invalid: ' + (error instanceof Error ? error.message : String(error));
  }
  aiChatRender();
}

function aiChatBuildRequest(message: string, intent: string): GrafcetStudioAIContextBuilder.BuildAiRequestResult {
  const bridge = aiChatBridge()!;
  return bridge.contextBuilder.buildAiRequest({
    message,
    selectedIntent: intent,
    rawProject: project,
    selection: { diagramId: activeDiagramId && !isVirtualTab(activeDiagramId) ? activeDiagramId : undefined }
  });
}

function aiChatSend(): void {
  toggleAiChatPanel(true);
  const input = document.getElementById('ai-chat-input') as HTMLInputElement | HTMLTextAreaElement | null;
  const intentInput = document.getElementById('ai-chat-intent') as HTMLInputElement | HTMLSelectElement | null;
  const message = aiChatText(input && input.value, 'Create a variable proposal.');
  const intent = aiChatText(intentInput && intentInput.value, 'create-variable');
  aiChatAddMessage('user', message);
  if (input) input.value = '';
  const bridge = aiChatBridge();
  if (!bridge) {
    aiChatAddMessage('assistant', 'AI bridge is not available.', { error: true });
    return;
  }
  const requestResult = aiChatBuildRequest(message, intent);
  if (!requestResult.ok || !requestResult.request) {
    aiChatAddMessage('assistant', requestResult.errors.join('; '), { error: true });
    return;
  }
  aiChatResetPendingStream();
  AI_CHAT_STATE.pendingRequestId = requestResult.request.id;
  const chromeHost = (window as Window & { chrome?: { webview?: { postMessage?: (message: unknown) => void } } }).chrome;
  if (chromeHost && chromeHost.webview && typeof chromeHost.webview.postMessage === 'function') {
    chromeHost.webview.postMessage({ type: 'AI_REQUEST', payload: Object.assign({}, requestResult.request, { prompt: message, mockFixture: intent, stream: true }) });
  } else {
    const result = bridge.mockService.generateMockResponse(requestResult.request);
    if (result.ok && result.proposal) aiChatAddProposal(result.proposal, result.rawText);
    else aiChatAddMessage('assistant', (result.errors || ['Mock AI response failed.']).join('; '), { error: true });
  }
}

function aiChatInsertMockProposal(): void {
  toggleAiChatPanel(true);
  const bridge = aiChatBridge();
  const intentInput = document.getElementById('ai-chat-intent') as HTMLInputElement | HTMLSelectElement | null;
  const intent = aiChatText(intentInput && intentInput.value, 'create-variable') as GrafcetStudioAIContracts.AiIntent;
  if (!bridge) {
    aiChatAddMessage('assistant', 'AI bridge is not available.', { error: true });
    return;
  }
  const proposal = bridge.mockService.getFixtureProposal(intent, 'ai-req-ui-preview');
  aiChatAddProposal(proposal, JSON.stringify(proposal));
}

function receiveAiChunk(chunk: string): void {
  toggleAiChatPanel(true);
  if (chunk === '__STREAM_END__') {
    aiChatFinalizeStream(AI_CHAT_STATE.pendingRawText);
    return;
  }
  aiChatAppendStreamDelta(chunk);
}

function receiveAiStreamEvent(event: { kind?: string; text?: unknown } | null): void {
  toggleAiChatPanel(true);
  const payload = event || {};
  const kind = String(payload.kind || 'delta');
  const text = String(payload.text == null ? '' : payload.text);
  if (kind === 'start') {
    aiChatResetPendingStream();
    aiChatAppendStreamStatus(text || 'AI streaming started.');
    return;
  }
  if (kind === 'status') {
    aiChatAppendStreamStatus(text);
    return;
  }
  if (kind === 'delta') {
    aiChatAppendStreamDelta(text);
    return;
  }
  if (kind === 'final') {
    AI_CHAT_STATE.pendingStreamFinalText = text;
    aiChatFinalizeStream(text);
    return;
  }
  if (kind === 'error') {
    aiChatFailStream(text);
    return;
  }
  if (kind === 'end') {
    if (AI_CHAT_STATE.pendingStreamMessageId && AI_CHAT_STATE.pendingRawText) aiChatFinalizeStream(AI_CHAT_STATE.pendingStreamFinalText || AI_CHAT_STATE.pendingRawText);
    return;
  }
  aiChatAppendStreamDelta(text);
}
function runAiChatUiValidation(): { ok: boolean; errors: string[] } {
  const bridge = aiChatBridge();
  if (!bridge) return { ok: false, errors: ['AI bridge is not available.'] };
  const errors: string[] = [];
  const originalProject = project;
  const originalMessages = AI_CHAT_STATE.messages;
  const originalProposals = AI_CHAT_STATE.proposals;
  project = { id: 'proj-ai-ui-test', name: 'AI UI Test', machineName: 'Machine', diagrams: [], units: [], devices: [], variables: { imported: [], user: [] }, excelVars: [], unitConfig: {}, ioMapping: { physicalIOs: [], entries: [] } };
  AI_CHAT_STATE.messages = [];
  AI_CHAT_STATE.proposals = {};
  try {
    const proposal = bridge.mockService.getFixtureProposal('create-variable', 'ai-req-ui-test', 'ai-prop-ui-test');
    const record = aiChatAddProposal(proposal, JSON.stringify(proposal));
    if (!record.preview.ok) errors.push('render proposal preview should dry-run successfully.');
    aiChatApplyProposal(proposal.id);
    const userVars = (project.variables.user as unknown[]);
    if (record.proposal.status !== 'applied' || userVars.length !== 1) errors.push('apply should mark proposal applied and add one user variable.');
    aiChatApplyProposal(proposal.id);
    if (userVars.length !== 1) errors.push('double apply guard should prevent another variable.');
    const discardProposal = bridge.mockService.getFixtureProposal('create-variable', 'ai-req-ui-test', 'ai-prop-ui-discard');
    const discardData = discardProposal.data as unknown as GrafcetStudioAIContracts.CreateVariableProposalData;
    discardData.variable.label = 'AiUiDiscard';
    discardData.variable.address = 'MR901';
    const discardRecord = aiChatAddProposal(discardProposal, JSON.stringify(discardProposal));
    aiChatDiscardProposal(discardProposal.id);
    if (discardRecord.proposal.status !== 'discarded' || userVars.length !== 1) errors.push('discard should update status without mutation.');
    const editProposal = bridge.mockService.getFixtureProposal('create-variable', 'ai-req-ui-test', 'ai-prop-ui-edit');
    const editData = editProposal.data as unknown as GrafcetStudioAIContracts.CreateVariableProposalData;
    editData.variable.label = 'AiUiEdit';
    editData.variable.address = 'MR902';
    const editRecord = aiChatAddProposal(editProposal, JSON.stringify(editProposal));
    (editRecord.proposal.data as unknown as GrafcetStudioAIContracts.CreateVariableProposalData).variable.label = 'AiUiEdited';
    editRecord.preview = aiChatValidatePreview(editRecord.proposal);
    if (!editRecord.preview.ok || (editRecord.proposal.data as unknown as GrafcetStudioAIContracts.CreateVariableProposalData).variable.label !== 'AiUiEdited') errors.push('edit path should revalidate edited proposal data.');
    const errorProposal = bridge.mockService.getFixtureProposal('create-variable', 'ai-req-ui-test', 'ai-prop-ui-error');
    (errorProposal.data as unknown as GrafcetStudioAIContracts.CreateVariableProposalData).variable.label = 'AiUiEdited';
    const errorRecord = aiChatAddProposal(errorProposal, JSON.stringify(errorProposal));
    if (errorRecord.preview.ok) errors.push('error state should be captured for duplicate variable preview.');
  } catch (error) {
    errors.push(error instanceof Error ? error.message : String(error));
  } finally {
    project = originalProject;
    AI_CHAT_STATE.messages = originalMessages;
    AI_CHAT_STATE.proposals = originalProposals;
    aiChatRender();
  }
  return { ok: errors.length === 0, errors };
}

function runAiChatStreamingValidation(): { ok: boolean; errors: string[] } {
  const bridge = aiChatBridge();
  if (!bridge) return { ok: false, errors: ['AI bridge is not available.'] };
  const errors: string[] = [];
  const originalProject = project;
  const originalMessages = AI_CHAT_STATE.messages;
  const originalProposals = AI_CHAT_STATE.proposals;
  const originalRawText = AI_CHAT_STATE.pendingRawText;
  const originalRequestId = AI_CHAT_STATE.pendingRequestId;
  const originalStreamMessageId = AI_CHAT_STATE.pendingStreamMessageId;
  const originalFinalText = AI_CHAT_STATE.pendingStreamFinalText;
  project = { id: 'proj-ai-stream-test', name: 'AI Stream Test', machineName: 'Machine', diagrams: [], units: [], devices: [], variables: { imported: [], user: [] }, excelVars: [], unitConfig: {}, ioMapping: { physicalIOs: [], entries: [] } };
  AI_CHAT_STATE.messages = [];
  AI_CHAT_STATE.proposals = {};
  aiChatResetPendingStream();
  try {
    const raw = bridge.mockService.getFixtureRawText('create-variable');
    receiveAiStreamEvent({ kind: 'start', text: 'test start' });
    receiveAiStreamEvent({ kind: 'status', text: 'status update' });
    receiveAiStreamEvent({ kind: 'delta', text: raw.slice(0, 13) });
    if (Object.keys(AI_CHAT_STATE.proposals).length !== 0) errors.push('partial streaming JSON must not create a proposal before final.');
    receiveAiStreamEvent({ kind: 'delta', text: raw.slice(13) });
    receiveAiStreamEvent({ kind: 'final', text: raw });
    if (Object.keys(AI_CHAT_STATE.proposals).length !== 1) errors.push('final streaming JSON should create exactly one validated preview proposal.');

    receiveAiStreamEvent({ kind: 'start', text: 'malformed start' });
    receiveAiStreamEvent({ kind: 'delta', text: bridge.mockService.getFixtureRawText('malformed-json') });
    const malformedResult = aiChatFinalizeStream('');
    if (malformedResult.ok) errors.push('malformed final JSON must fail parser validation.');

    receiveAiStreamEvent({ kind: 'start', text: 'error start' });
    receiveAiStreamEvent({ kind: 'error', text: 'mock error' });
    const lastMessage = AI_CHAT_STATE.messages[AI_CHAT_STATE.messages.length - 1];
    if (!lastMessage || !lastMessage.error) errors.push('streaming error should render an error message and no proposal.');
  } catch (error) {
    errors.push(error instanceof Error ? error.message : String(error));
  } finally {
    project = originalProject;
    AI_CHAT_STATE.messages = originalMessages;
    AI_CHAT_STATE.proposals = originalProposals;
    AI_CHAT_STATE.pendingRawText = originalRawText;
    AI_CHAT_STATE.pendingRequestId = originalRequestId;
    AI_CHAT_STATE.pendingStreamMessageId = originalStreamMessageId;
    AI_CHAT_STATE.pendingStreamFinalText = originalFinalText;
    aiChatRender();
  }
  return { ok: errors.length === 0, errors };
}
(window as unknown as { setRightPanelTab: typeof setRightPanelTab }).setRightPanelTab = setRightPanelTab;
(window as unknown as { toggleAiChatPanel: typeof toggleAiChatPanel }).toggleAiChatPanel = toggleAiChatPanel;
(window as unknown as { aiChatSend: typeof aiChatSend }).aiChatSend = aiChatSend;
(window as unknown as { aiChatInsertMockProposal: typeof aiChatInsertMockProposal }).aiChatInsertMockProposal = aiChatInsertMockProposal;
(window as unknown as { aiChatApplyProposal: typeof aiChatApplyProposal }).aiChatApplyProposal = aiChatApplyProposal;
(window as unknown as { aiChatDiscardProposal: typeof aiChatDiscardProposal }).aiChatDiscardProposal = aiChatDiscardProposal;
(window as unknown as { aiChatEditProposal: typeof aiChatEditProposal }).aiChatEditProposal = aiChatEditProposal;
(window as unknown as { aiChatCancelEdit: typeof aiChatCancelEdit }).aiChatCancelEdit = aiChatCancelEdit;
(window as unknown as { aiChatSaveEdit: typeof aiChatSaveEdit }).aiChatSaveEdit = aiChatSaveEdit;
(window as unknown as { receiveAiChunk: typeof receiveAiChunk }).receiveAiChunk = receiveAiChunk;
(window as unknown as { runAiChatUiValidation: typeof runAiChatUiValidation }).runAiChatUiValidation = runAiChatUiValidation;
(window as unknown as { runAiChatStreamingValidation: typeof runAiChatStreamingValidation }).runAiChatStreamingValidation = runAiChatStreamingValidation;

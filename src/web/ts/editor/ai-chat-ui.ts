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
declare function renderIOMappingTable(filter?: string): void;

interface AiChatMessage {
  id: string;
  role: 'user' | 'assistant';
  text: string;
  error?: boolean;
  proposalId?: string;
  permissionId?: string;
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

type AiPermissionChoice = 'once' | 'session' | 'project' | 'deny';

interface AiPermissionRequest {
  id: string;
  request: GrafcetStudioAIContracts.AiRequest;
  intent: string;
  message: string;
  scopes: string[];
  missing: string[];
  status: 'pending' | AiPermissionChoice;
}

const AI_PERMISSION_STATE: {
  liveAi: boolean | null;
  sessionScopes: string[];
  projectId: string;
  projectScopes: string[];
  requests: Record<string, AiPermissionRequest>;
} = {
  liveAi: null,
  sessionScopes: [],
  projectId: '',
  projectScopes: [],
  requests: {}
};

const AI_SCOPE_LABELS: Record<string, string> = {
  variable: 'Variables',
  unit: 'Units',
  diagram: 'Diagram info',
  step: 'Steps, transitions and connections',
  io: 'IO mapping',
  structure: 'Device structures',
  robot: 'Robot flows and variables (ABB code generation)'
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

const AI_CHAT_FLOAT_KEY = 'grafcet.aiChat.float';
const AI_CHAT_DOCK_PARENT_ID = 'right-panel-content';

interface AiChatFloatRect { left: number; top: number; width: number; height: number; }

function aiChatIsFloating(): boolean {
  const panel = document.getElementById('ai-chat-panel');
  return !!panel && panel.classList.contains('ai-floating');
}

function aiChatLoadFloatRect(): AiChatFloatRect {
  const fallback: AiChatFloatRect = { left: Math.max(20, window.innerWidth - 640), top: 90, width: 360, height: 480 };
  try {
    const raw = localStorage.getItem(AI_CHAT_FLOAT_KEY);
    if (raw) return Object.assign(fallback, JSON.parse(raw));
  } catch (_) { /* storage unavailable */ }
  return fallback;
}

function aiChatSaveFloatRect(panel: HTMLElement): void {
  try {
    localStorage.setItem(AI_CHAT_FLOAT_KEY, JSON.stringify({
      left: panel.offsetLeft, top: panel.offsetTop, width: panel.offsetWidth, height: panel.offsetHeight
    }));
  } catch (_) { /* storage unavailable */ }
}

function aiChatClampFloat(panel: HTMLElement): void {
  const maxLeft = Math.max(0, window.innerWidth - Math.min(panel.offsetWidth, 120));
  const maxTop = Math.max(0, window.innerHeight - 40);
  panel.style.left = Math.min(Math.max(0, panel.offsetLeft), maxLeft) + 'px';
  panel.style.top = Math.min(Math.max(0, panel.offsetTop), maxTop) + 'px';
}

function aiChatInitFloatDrag(panel: HTMLElement): void {
  const head = panel.querySelector('.ai-chat-head') as HTMLElement | null;
  if (!head || head.dataset.dragBound) return;
  head.dataset.dragBound = '1';
  head.addEventListener('pointerdown', function(ev: PointerEvent) {
    if (!aiChatIsFloating() || (ev.target as HTMLElement).closest('button')) return;
    const startX = ev.clientX - panel.offsetLeft;
    const startY = ev.clientY - panel.offsetTop;
    head.setPointerCapture(ev.pointerId);
    const onMove = function(e: PointerEvent) {
      panel.style.left = (e.clientX - startX) + 'px';
      panel.style.top = (e.clientY - startY) + 'px';
      aiChatClampFloat(panel);
    };
    const onUp = function() {
      head.removeEventListener('pointermove', onMove);
      head.removeEventListener('pointerup', onUp);
      head.removeEventListener('pointercancel', onUp);
      aiChatSaveFloatRect(panel);
    };
    head.addEventListener('pointermove', onMove);
    head.addEventListener('pointerup', onUp);
    head.addEventListener('pointercancel', onUp);
  });
  if (typeof ResizeObserver !== 'undefined') {
    new ResizeObserver(function() { if (aiChatIsFloating()) aiChatSaveFloatRect(panel); }).observe(panel);
  }
  window.addEventListener('resize', function() { if (aiChatIsFloating()) aiChatClampFloat(panel); });
}

function setAiChatFloating(floating: boolean): void {
  const panel = document.getElementById('ai-chat-panel');
  const dock = document.getElementById(AI_CHAT_DOCK_PARENT_ID);
  const btn = document.getElementById('ai-chat-float-btn');
  if (!panel || !dock || aiChatIsFloating() === floating) return;
  if (floating) {
    const rect = aiChatLoadFloatRect();
    document.body.appendChild(panel);
    panel.classList.add('ai-floating', 'show');
    panel.style.left = rect.left + 'px';
    panel.style.top = rect.top + 'px';
    panel.style.width = rect.width + 'px';
    panel.style.height = rect.height + 'px';
    aiChatClampFloat(panel);
    aiChatInitFloatDrag(panel);
    setRightPanelTab('props');
  } else {
    panel.classList.remove('ai-floating');
    ['left', 'top', 'width', 'height'].forEach(function(k) { panel.style.removeProperty(k); });
    dock.appendChild(panel);
    setRightPanelTab('ai');
  }
  if (btn) {
    btn.title = floating ? 'Dock to panel' : 'Pop out';
    btn.innerHTML = floating ? '&#10514;' : '&#10138;';
  }
}

function toggleAiChatFloating(): void {
  setAiChatFloating(!aiChatIsFloating());
}

function setRightPanelTab(tab: string): void {
  if (tab === 'ai' && aiChatIsFloating()) {
    const floatPanel = document.getElementById('ai-chat-panel');
    if (floatPanel) floatPanel.classList.add('show');
    return;
  }
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
  if (show === false && aiChatIsFloating()) setAiChatFloating(false);
  panel.classList.toggle('show', show !== false);
  setRightPanelTab(show === false ? 'props' : 'ai');
  if (show !== false) {
    aiSettingsPost('get');
    aiPermissionRefreshGrants();
  }
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
  if (typeof historyCommit === "function") historyCommit();
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
      if (activeDiagramId === IO_MAPPING_TAB_ID && typeof renderIOMappingTable === 'function') renderIOMappingTable((document.getElementById('iomap-filter') as HTMLSelectElement | null)?.value || 'All');
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
      (record ? aiChatRenderProposal(record) : message.permissionId && AI_PERMISSION_STATE.requests[message.permissionId] ? aiPermissionRenderCard(AI_PERMISSION_STATE.requests[message.permissionId]) : aiChatEscape(message.text)) +
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
  const scopes = (requestResult.appliedScopes || []).slice();
  const missing = aiPermissionMissingScopes(scopes);
  if (aiPermissionNeeded() && missing.length) {
    aiPermissionAsk(requestResult.request, intent, message, scopes, missing);
    return;
  }
  aiChatDispatch(requestResult.request, intent, message);
}

function aiChatDispatch(request: GrafcetStudioAIContracts.AiRequest, intent: string, message: string): void {
  const bridge = aiChatBridge();
  if (!bridge) return;
  aiChatResetPendingStream();
  AI_CHAT_STATE.pendingRequestId = request.id;
  const chromeHost = (window as Window & { chrome?: { webview?: { postMessage?: (message: unknown) => void } } }).chrome;
  if (chromeHost && chromeHost.webview && typeof chromeHost.webview.postMessage === 'function') {
    chromeHost.webview.postMessage({ type: 'AI_REQUEST', payload: Object.assign({}, request, { prompt: message, mockFixture: intent, stream: true }) });
  } else {
    const result = bridge.mockService.generateMockResponse(request);
    if (result.ok && result.proposal) aiChatAddProposal(result.proposal, result.rawText);
    else aiChatAddMessage('assistant', (result.errors || ['Mock AI response failed.']).join('; '), { error: true });
  }
}

function aiPermissionCurrentProjectId(): string {
  return project && typeof project.id === 'string' ? project.id : '';
}

/** Data leaves the machine only through the desktop host with a Gemini key; unknown counts as live. */
function aiPermissionNeeded(): boolean {
  const host = (window as Window & { chrome?: { webview?: unknown } }).chrome;
  return !!(host && host.webview) && AI_PERMISSION_STATE.liveAi !== false;
}

function aiPermissionMissingScopes(scopes: string[]): string[] {
  const projectScopes = AI_PERMISSION_STATE.projectId === aiPermissionCurrentProjectId() ? AI_PERMISSION_STATE.projectScopes : [];
  return scopes.filter(function(scope) {
    return AI_PERMISSION_STATE.sessionScopes.indexOf(scope) < 0 && projectScopes.indexOf(scope) < 0;
  });
}

function aiPermissionAsk(request: GrafcetStudioAIContracts.AiRequest, intent: string, message: string, scopes: string[], missing: string[]): void {
  const id = 'ai-perm-' + Date.now() + '-' + Math.random().toString(36).slice(2, 7);
  AI_PERMISSION_STATE.requests[id] = { id, request, intent, message, scopes, missing, status: 'pending' };
  aiChatAddMessage('assistant', 'Permission required.', { permissionId: id });
}

function aiPermissionRenderCard(item: AiPermissionRequest): string {
  const json = JSON.stringify(item.request.context, null, 2);
  const size = json.length >= 1024 ? (json.length / 1024).toFixed(1) + ' KB' : json.length + ' chars';
  const scopeList = item.scopes.map(function(scope) {
    const label = AI_SCOPE_LABELS[scope] || scope;
    return '<li>' + aiChatEscape(label) + (item.missing.indexOf(scope) >= 0 ? '' : ' <span class="ai-perm-granted">(already allowed)</span>') + '</li>';
  }).join('');
  const pending = item.status === 'pending';
  const disabled = pending ? '' : ' disabled';
  const canProject = !!aiPermissionCurrentProjectId();
  const statusText: Record<string, string> = {
    once: 'Allowed once. Request sent.',
    session: 'Allowed for this session. Request sent.',
    project: 'Allowed for this project. Request sent.',
    deny: 'Denied. Nothing was sent.'
  };
  return '<div class="ai-proposal-head"><span class="ai-proposal-title">Send project data to AI?</span></div>' +
    '<div class="ai-proposal-summary">This request will send the following to Gemini (' + aiChatEscape(size) + '):</div>' +
    '<ul class="ai-message-list">' + scopeList + '</ul>' +
    '<details class="ai-perm-details"><summary>View data</summary><pre class="ai-preview-json">' + aiChatEscape(json) + '</pre></details>' +
    (pending
      ? '<div class="ai-proposal-actions">' +
          '<button class="btn a" onclick="aiPermissionDecide(\'' + item.id + '\', \'once\')"' + disabled + '>Allow once</button>' +
          '<button class="btn" onclick="aiPermissionDecide(\'' + item.id + '\', \'session\')"' + disabled + '>Allow this session</button>' +
          (canProject ? '<button class="btn" onclick="aiPermissionDecide(\'' + item.id + '\', \'project\')"' + disabled + '>Allow for this project</button>' : '') +
          '<button class="btn r" onclick="aiPermissionDecide(\'' + item.id + '\', \'deny\')"' + disabled + '>Deny</button>' +
        '</div>'
      : '<div class="ai-proposal-summary">' + aiChatEscape(statusText[item.status] || '') + '</div>');
}

function aiPermissionDecide(id: string, choice: AiPermissionChoice): void {
  const item = AI_PERMISSION_STATE.requests[id];
  if (!item || item.status !== 'pending') return;
  item.status = choice;
  if (choice === 'deny') {
    aiChatRender();
    return;
  }
  if (choice === 'session') {
    item.missing.forEach(function(scope) { if (AI_PERMISSION_STATE.sessionScopes.indexOf(scope) < 0) AI_PERMISSION_STATE.sessionScopes.push(scope); });
  }
  if (choice === 'project') {
    const projectId = aiPermissionCurrentProjectId();
    if (AI_PERMISSION_STATE.projectId !== projectId) {
      AI_PERMISSION_STATE.projectId = projectId;
      AI_PERMISSION_STATE.projectScopes = [];
    }
    item.missing.forEach(function(scope) { if (AI_PERMISSION_STATE.projectScopes.indexOf(scope) < 0) AI_PERMISSION_STATE.projectScopes.push(scope); });
    aiSettingsPost('grant', { projectId, scopes: item.missing.join(',') });
  }
  aiChatRender();
  aiChatDispatch(item.request, item.intent, item.message);
}

let aiPermissionRobotPending: { send: () => void; containerId: string; json: string } | null = null;

/**
 * Gate for ABB robot code generation: the host builds the prompt from the codegen payload and sends it to the AI,
 * so the same permission rules apply. Calls send() immediately when allowed, otherwise renders a card in containerId.
 */
function aiPermissionRequestRobotCodegen(payload: unknown, send: () => void, containerId: string): void {
  if (!aiPermissionNeeded() || !aiPermissionMissingScopes(['robot']).length) {
    send();
    return;
  }
  const json = JSON.stringify(payload, null, 2);
  aiPermissionRobotPending = { send, containerId, json };
  const container = document.getElementById(containerId);
  if (!container) return;
  const size = json.length >= 1024 ? (json.length / 1024).toFixed(1) + ' KB' : json.length + ' chars';
  const canProject = !!aiPermissionCurrentProjectId();
  container.innerHTML = '<div class="ai-bubble ai-bubble-assistant">' +
    '<div class="ai-proposal-head"><span class="ai-proposal-title">Send robot flows to AI?</span></div>' +
    '<div class="ai-proposal-summary">ABB code is written by Gemini. The prompt is built from this data (' + aiChatEscape(size) + '): flows, steps, transitions and the variables they use.</div>' +
    '<details class="ai-perm-details"><summary>View data</summary><pre class="ai-preview-json">' + aiChatEscape(json) + '</pre></details>' +
    '<div class="ai-proposal-actions">' +
      '<button class="btn a" onclick="aiPermissionDecideRobot(\'once\')">Allow once</button>' +
      '<button class="btn" onclick="aiPermissionDecideRobot(\'session\')">Allow this session</button>' +
      (canProject ? '<button class="btn" onclick="aiPermissionDecideRobot(\'project\')">Allow for this project</button>' : '') +
      '<button class="btn r" onclick="aiPermissionDecideRobot(\'deny\')">Deny</button>' +
    '</div></div>';
}

function aiPermissionDecideRobot(choice: AiPermissionChoice): void {
  const pending = aiPermissionRobotPending;
  if (!pending) return;
  aiPermissionRobotPending = null;
  const container = document.getElementById(pending.containerId);
  if (choice === 'deny') {
    if (container) container.textContent = 'Robot code generation cancelled. Nothing was sent to the AI.';
    return;
  }
  if (choice === 'session' && AI_PERMISSION_STATE.sessionScopes.indexOf('robot') < 0) AI_PERMISSION_STATE.sessionScopes.push('robot');
  if (choice === 'project') {
    const projectId = aiPermissionCurrentProjectId();
    if (AI_PERMISSION_STATE.projectId !== projectId) {
      AI_PERMISSION_STATE.projectId = projectId;
      AI_PERMISSION_STATE.projectScopes = [];
    }
    if (AI_PERMISSION_STATE.projectScopes.indexOf('robot') < 0) AI_PERMISSION_STATE.projectScopes.push('robot');
    aiSettingsPost('grant', { projectId, scopes: 'robot' });
  }
  if (container) container.textContent = 'Generating ABB RAPID with AI...';
  pending.send();
}

function aiPermissionRefreshGrants(): void {
  const projectId = aiPermissionCurrentProjectId();
  if (projectId) aiSettingsPost('grants-get', { projectId });
  else aiPermissionRenderGrants();
}

function aiPermissionRevoke(): void {
  AI_PERMISSION_STATE.sessionScopes = [];
  const projectId = aiPermissionCurrentProjectId();
  if (projectId) aiSettingsPost('revoke', { projectId });
  AI_PERMISSION_STATE.projectScopes = [];
  aiPermissionRenderGrants();
}

function receiveAiGrants(payload: { projectId: string; scopes: string[]; error?: string }): void {
  if (payload.projectId !== aiPermissionCurrentProjectId()) return;
  AI_PERMISSION_STATE.projectId = payload.projectId;
  AI_PERMISSION_STATE.projectScopes = Array.isArray(payload.scopes) ? payload.scopes.slice() : [];
  aiPermissionRenderGrants(payload.error);
}

function aiPermissionRenderGrants(error?: string): void {
  const line = document.getElementById('ai-settings-grants');
  if (!line) return;
  const all = AI_PERMISSION_STATE.projectScopes.concat(AI_PERMISSION_STATE.sessionScopes.filter(function(s) { return AI_PERMISSION_STATE.projectScopes.indexOf(s) < 0; }));
  const labels = all.map(function(scope) { return AI_SCOPE_LABELS[scope] || scope; });
  line.textContent = (error ? error + ' | ' : '') + (labels.length ? 'Shared with AI: ' + labels.join(', ') : 'No data shared with AI yet. You will be asked first.');
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
interface AiSettingsState { configured: boolean; source: string; model: string; error?: string; }

function aiSettingsPost(action: string, extra?: Record<string, string>): boolean {
  const host = (window as Window & { chrome?: { webview?: { postMessage?: (message: unknown) => void } } }).chrome;
  if (!host || !host.webview || typeof host.webview.postMessage !== 'function') return false;
  host.webview.postMessage({ type: 'AI_SETTINGS', payload: Object.assign({ action }, extra || {}) });
  return true;
}

function aiSettingsToggle(): void {
  const form = document.getElementById('ai-settings-form');
  if (!form) return;
  form.hidden = !form.hidden;
  if (!form.hidden) aiSettingsPost('get');
}

function aiSettingsSave(): void {
  const key = document.getElementById('ai-settings-key') as HTMLInputElement | null;
  const model = document.getElementById('ai-settings-model') as HTMLSelectElement | null;
  const posted = aiSettingsPost('save', { apiKey: key ? key.value.trim() : '', model: model ? model.value.trim() : '' });
  if (!posted) receiveAiSettings({ configured: false, source: 'none', model: '', error: 'API key can only be saved inside the desktop app.' });
  if (key) key.value = '';
}

function aiSettingsLoadModels(): void {
  const key = document.getElementById('ai-settings-key') as HTMLInputElement | null;
  const status = document.getElementById('ai-settings-status');
  if (status) status.textContent = 'Loading models from Gemini...';
  if (!aiSettingsPost('list', { apiKey: key ? key.value.trim() : '' })) receiveAiModels({ ok: false, models: [], suggested: '', error: 'Model list is only available inside the desktop app.' });
}

function aiSettingsFillModels(models: string[], selected: string): void {
  const select = document.getElementById('ai-settings-model') as HTMLSelectElement | null;
  if (!select) return;
  const names = models.slice();
  if (selected && names.indexOf(selected) < 0) names.unshift(selected);
  select.innerHTML = names.map(function(name) {
    return '<option value="' + aiChatEscape(name) + '">' + aiChatEscape(name) + '</option>';
  }).join('');
  if (selected) select.value = selected;
}

function receiveAiModels(result: { ok: boolean; models: string[]; suggested: string; error?: string }): void {
  const status = document.getElementById('ai-settings-status');
  const select = document.getElementById('ai-settings-model') as HTMLSelectElement | null;
  if (!result.ok) {
    if (status) {
      status.textContent = result.error || 'Could not load models.';
      status.classList.add('warn');
    }
    return;
  }
  const current = select ? select.value : '';
  const keep = current && result.models.indexOf(current) >= 0 ? current : result.suggested;
  aiSettingsFillModels(result.models, keep);
  if (status) {
    status.textContent = result.models.length + ' models available. Pick one and Save.';
    status.classList.remove('warn');
  }
}

function aiSettingsClear(): void {
  aiSettingsPost('clear');
}

function receiveAiSettings(settings: AiSettingsState): void {
  const status = document.getElementById('ai-settings-status');
  const model = document.getElementById('ai-settings-model') as HTMLSelectElement | null;
  if (model && settings.model && document.activeElement !== model) aiSettingsFillModels(Array.from(model.options).map(function(o) { return o.value; }), settings.model);
  AI_PERMISSION_STATE.liveAi = !!settings.configured;
  if (!status) return;
  let text: string;
  if (settings.configured) text = 'Gemini connected (' + (settings.source === 'env' ? 'environment key' : 'saved key') + ') - ' + settings.model;
  else text = 'No API key - using offline mock responses. Click the gear to add a key.';
  if (settings.error) text = settings.error + ' | ' + text;
  status.textContent = text;
  status.classList.toggle('warn', !settings.configured || !!settings.error);
  if (settings.configured && model && model.options.length <= 1 && !settings.error) aiSettingsLoadModels();
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
(window as unknown as { aiSettingsToggle: typeof aiSettingsToggle }).aiSettingsToggle = aiSettingsToggle;
(window as unknown as { aiSettingsSave: typeof aiSettingsSave }).aiSettingsSave = aiSettingsSave;
(window as unknown as { aiSettingsClear: typeof aiSettingsClear }).aiSettingsClear = aiSettingsClear;
(window as unknown as { aiSettingsLoadModels: typeof aiSettingsLoadModels }).aiSettingsLoadModels = aiSettingsLoadModels;
(window as unknown as { receiveAiModels: typeof receiveAiModels }).receiveAiModels = receiveAiModels;
(window as unknown as { aiPermissionDecide: typeof aiPermissionDecide }).aiPermissionDecide = aiPermissionDecide;
(window as unknown as { aiPermissionRevoke: typeof aiPermissionRevoke }).aiPermissionRevoke = aiPermissionRevoke;
(window as unknown as { aiPermissionRequestRobotCodegen: typeof aiPermissionRequestRobotCodegen }).aiPermissionRequestRobotCodegen = aiPermissionRequestRobotCodegen;
(window as unknown as { aiPermissionDecideRobot: typeof aiPermissionDecideRobot }).aiPermissionDecideRobot = aiPermissionDecideRobot;
(window as unknown as { aiPermissionRefreshGrants: typeof aiPermissionRefreshGrants }).aiPermissionRefreshGrants = aiPermissionRefreshGrants;
(window as unknown as { receiveAiGrants: typeof receiveAiGrants }).receiveAiGrants = receiveAiGrants;
(window as unknown as { receiveAiSettings: typeof receiveAiSettings }).receiveAiSettings = receiveAiSettings;
(window as unknown as { receiveAiChunk: typeof receiveAiChunk }).receiveAiChunk = receiveAiChunk;
(window as unknown as { runAiChatUiValidation: typeof runAiChatUiValidation }).runAiChatUiValidation = runAiChatUiValidation;
(window as unknown as { runAiChatStreamingValidation: typeof runAiChatStreamingValidation }).runAiChatStreamingValidation = runAiChatStreamingValidation;

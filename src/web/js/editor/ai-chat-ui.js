"use strict";

const AI_CHAT_STATE = {
  messages: [],
  proposals: {},
  pendingRawText: '',
  pendingRequestId: null
};

function aiChatBridge() {
  return window.GrafcetStudioAI || (window.GrafcetStudio && window.GrafcetStudio.ai) || null;
}

function aiChatEscape(value) {
  return String(value == null ? '' : value).replace(/[&<>"']/g, function(ch) {
    return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[ch];
  });
}

function aiChatText(value, fallback) {
  const text = String(value == null ? '' : value).trim();
  return text || fallback || '';
}

function aiChatClone(value) {
  return JSON.parse(JSON.stringify(value));
}

function aiChatGetBody() {
  return document.getElementById('ai-chat-body');
}

function toggleAiChatPanel(show) {
  const panel = document.getElementById('ai-chat-panel');
  if (!panel) return;
  panel.classList.toggle('show', show !== false);
  if (show !== false && !AI_CHAT_STATE.messages.length) {
    aiChatAddMessage('assistant', 'Create or receive an AI proposal here. Preview and validation are required before Apply is enabled.');
  }
}

function aiChatAddMessage(role, text, options) {
  const message = Object.assign({ id: 'ai-msg-' + Date.now() + '-' + Math.random().toString(36).slice(2, 7), role, text }, options || {});
  AI_CHAT_STATE.messages.push(message);
  aiChatRender();
  return message;
}

function aiChatMakeApplyContext() {
  return {
    getProject: function() { return project; },
    saveProject: typeof saveProject === 'function' ? saveProject : undefined,
    renderTree: typeof renderTree === 'function' ? renderTree : undefined,
    renderGlobalVarTable: typeof renderGlobalVarTable === 'function' ? renderGlobalVarTable : undefined,
    refresh: function() {
      if (typeof syncStructDataFromProjectData === 'function') syncStructDataFromProjectData();
      if (typeof syncVariableSignalAddressesFromDeviceTypes === 'function') syncVariableSignalAddressesFromDeviceTypes();
      if (activeDiagramId === VARS_TAB_ID && typeof renderGlobalVarTable === 'function') renderGlobalVarTable();
    }
  };
}

function aiChatValidatePreview(proposal) {
  const bridge = aiChatBridge();
  if (!bridge || !bridge.contracts || !bridge.applyLayer) {
    return { ok: false, errors: ['AI bridge is not available.'], warnings: [], affectedIds: [] };
  }
  const validation = bridge.contracts.validateAiProposal(proposal);
  if (!validation.ok) return { ok: false, errors: validation.errors, warnings: [], affectedIds: [] };
  if (validation.value && validation.value.status !== 'validated') {
    return { ok: false, errors: ['Proposal must be validated before preview/apply; current status is ' + validation.value.status + '.'], warnings: [], affectedIds: [] };
  }
  return bridge.applyLayer.dryRunProposal(validation.value || proposal, { context: aiChatMakeApplyContext(), dryRun: true });
}

function aiChatAddProposal(proposal, sourceText) {
  const bridge = aiChatBridge();
  const safeProposal = bridge && bridge.proposalParser
    ? bridge.proposalParser.normalizeAiProposal(aiChatClone(proposal))
    : aiChatClone(proposal);
  const preview = aiChatValidatePreview(safeProposal);
  const record = {
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

function aiChatRenderPreview(proposal) {
  const data = proposal && proposal.data ? proposal.data : {};
  if (proposal.intent === 'create-variable' || proposal.intent === 'clone-variable') {
    const variable = data.variable || {};
    const signalAddresses = variable.signalAddresses ? Object.keys(variable.signalAddresses).map(function(key) {
      return key + ': ' + variable.signalAddresses[key];
    }).join(', ') : '';
    const rows = [
      ['Label', variable.label],
      ['Format', variable.format || variable.dataType],
      ['Kind', variable.kind],
      ['Address', variable.address],
      ['Bucket', data.bucket || 'user'],
      ['Source', variable.source],
      ['Signals', signalAddresses],
      ['Comment', variable.comment]
    ];
    return '<table class="ai-preview-table"><tbody>' + rows.filter(function(row) { return aiChatText(row[1], ''); }).map(function(row) {
      return '<tr><td>' + aiChatEscape(row[0]) + '</td><td>' + aiChatEscape(row[1]) + '</td></tr>';
    }).join('') + '</tbody></table>';
  }
  return '<pre class="ai-preview-json">' + aiChatEscape(JSON.stringify(data, null, 2)) + '</pre>';
}

function aiChatRenderList(items, className) {
  const list = Array.isArray(items) ? items.filter(Boolean) : [];
  if (!list.length) return '';
  return '<ul class="ai-message-list ' + className + '">' + list.map(function(item) { return '<li>' + aiChatEscape(item) + '</li>'; }).join('') + '</ul>';
}

function aiChatCanApply(record) {
  return !!record && !!record.preview && record.preview.ok && record.proposal.status === 'validated';
}

function aiChatRenderProposal(record) {
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

function aiChatRender() {
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

function aiChatApplyProposal(proposalId) {
  const record = AI_CHAT_STATE.proposals[proposalId];
  if (!record || record.proposal.status === 'applied' || record.proposal.status === 'discarded') return;
  record.preview = aiChatValidatePreview(record.proposal);
  if (!aiChatCanApply(record)) {
    aiChatRender();
    return;
  }
  try {
    const result = aiChatBridge().applyLayer.applyProposal(record.proposal, { context: aiChatMakeApplyContext() });
    if (!result.ok) record.preview = result;
    else record.preview = Object.assign({}, result, { warnings: result.warnings || [], errors: [] });
  } catch (error) {
    record.preview = { ok: false, proposalId, dryRun: false, affectedIds: [], warnings: [], errors: [error instanceof Error ? error.message : String(error)], changed: false };
  }
  aiChatRender();
}

function aiChatDiscardProposal(proposalId) {
  const record = AI_CHAT_STATE.proposals[proposalId];
  if (!record || record.proposal.status === 'applied' || record.proposal.status === 'discarded') return;
  record.proposal.status = 'discarded';
  record.preview = { ok: false, proposalId, dryRun: true, affectedIds: [], warnings: [], errors: ['Proposal was discarded.'], changed: false };
  aiChatRender();
}

function aiChatEditProposal(proposalId) {
  const record = AI_CHAT_STATE.proposals[proposalId];
  if (!record || record.proposal.status === 'applied' || record.proposal.status === 'discarded') return;
  record.editing = true;
  record.editError = '';
  aiChatRender();
}

function aiChatCancelEdit(proposalId) {
  const record = AI_CHAT_STATE.proposals[proposalId];
  if (!record) return;
  record.editing = false;
  record.editError = '';
  aiChatRender();
}

function aiChatSaveEdit(proposalId) {
  const record = AI_CHAT_STATE.proposals[proposalId];
  const input = document.getElementById('ai-edit-' + proposalId);
  if (!record || !input) return;
  try {
    const edited = JSON.parse(input.value);
    const bridge = aiChatBridge();
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

function aiChatBuildRequest(message, intent) {
  const bridge = aiChatBridge();
  return bridge.contextBuilder.buildAiRequest({
    message,
    selectedIntent: intent,
    rawProject: project,
    selection: { diagramId: activeDiagramId && !isVirtualTab(activeDiagramId) ? activeDiagramId : undefined }
  });
}

function aiChatSend() {
  toggleAiChatPanel(true);
  const input = document.getElementById('ai-chat-input');
  const intentInput = document.getElementById('ai-chat-intent');
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
  AI_CHAT_STATE.pendingRawText = '';
  AI_CHAT_STATE.pendingRequestId = requestResult.request.id;
  if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
    window.chrome.webview.postMessage({ type: 'AI_REQUEST', payload: Object.assign({}, requestResult.request, { prompt: message, mockFixture: intent }) });
  } else {
    const result = bridge.mockService.generateMockResponse(requestResult.request);
    if (result.ok && result.proposal) aiChatAddProposal(result.proposal, result.rawText);
    else aiChatAddMessage('assistant', (result.errors || ['Mock AI response failed.']).join('; '), { error: true });
  }
}

function aiChatInsertMockProposal() {
  toggleAiChatPanel(true);
  const bridge = aiChatBridge();
  const intentInput = document.getElementById('ai-chat-intent');
  const intent = aiChatText(intentInput && intentInput.value, 'create-variable');
  if (!bridge) {
    aiChatAddMessage('assistant', 'AI bridge is not available.', { error: true });
    return;
  }
  const proposal = bridge.mockService.getFixtureProposal(intent, 'ai-req-ui-preview');
  aiChatAddProposal(proposal, JSON.stringify(proposal));
}

function receiveAiChunk(chunk) {
  toggleAiChatPanel(true);
  if (chunk === '__STREAM_END__') {
    const result = aiChatBridge().mockService.receiveHostResponse(AI_CHAT_STATE.pendingRawText);
    if (result.ok && result.proposal) aiChatAddProposal(result.proposal, result.rawText);
    else aiChatAddMessage('assistant', (result.errors || ['AI response failed.']).join('; '), { error: true });
    AI_CHAT_STATE.pendingRawText = '';
    return;
  }
  AI_CHAT_STATE.pendingRawText += String(chunk == null ? '' : chunk);
}

function runAiChatUiValidation() {
  const bridge = aiChatBridge();
  if (!bridge) return { ok: false, errors: ['AI bridge is not available.'] };
  const errors = [];
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
    if (record.proposal.status !== 'applied' || project.variables.user.length !== 1) errors.push('apply should mark proposal applied and add one user variable.');
    aiChatApplyProposal(proposal.id);
    if (project.variables.user.length !== 1) errors.push('double apply guard should prevent another variable.');
    const discardProposal = bridge.mockService.getFixtureProposal('create-variable', 'ai-req-ui-test', 'ai-prop-ui-discard');
    discardProposal.data.variable.label = 'AiUiDiscard';
    discardProposal.data.variable.address = 'MR901';
    const discardRecord = aiChatAddProposal(discardProposal, JSON.stringify(discardProposal));
    aiChatDiscardProposal(discardProposal.id);
    if (discardRecord.proposal.status !== 'discarded' || project.variables.user.length !== 1) errors.push('discard should update status without mutation.');
    const editProposal = bridge.mockService.getFixtureProposal('create-variable', 'ai-req-ui-test', 'ai-prop-ui-edit');
    editProposal.data.variable.label = 'AiUiEdit';
    editProposal.data.variable.address = 'MR902';
    const editRecord = aiChatAddProposal(editProposal, JSON.stringify(editProposal));
    editRecord.proposal.data.variable.label = 'AiUiEdited';
    editRecord.preview = aiChatValidatePreview(editRecord.proposal);
    if (!editRecord.preview.ok || editRecord.proposal.data.variable.label !== 'AiUiEdited') errors.push('edit path should revalidate edited proposal data.');
    const errorProposal = bridge.mockService.getFixtureProposal('create-variable', 'ai-req-ui-test', 'ai-prop-ui-error');
    errorProposal.data.variable.label = 'AiUiEdited';
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

window.toggleAiChatPanel = toggleAiChatPanel;
window.aiChatSend = aiChatSend;
window.aiChatInsertMockProposal = aiChatInsertMockProposal;
window.aiChatApplyProposal = aiChatApplyProposal;
window.aiChatDiscardProposal = aiChatDiscardProposal;
window.aiChatEditProposal = aiChatEditProposal;
window.aiChatCancelEdit = aiChatCancelEdit;
window.aiChatSaveEdit = aiChatSaveEdit;
window.receiveAiChunk = receiveAiChunk;
window.runAiChatUiValidation = runAiChatUiValidation;

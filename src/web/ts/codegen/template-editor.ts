namespace GrafcetStudioTemplateEditor {
  type TemplateDiagnosticSeverity = 'error' | 'warning' | 'info';
  type Preset = { id: string; label: string; path: string; platform?: string; group?: string; description?: string; active?: boolean; note?: string };
  type CatalogEntry = { path: string; platform: string; group: string; description: string; active: boolean; note: string };
  type Suggestion = { label: string; insert: string; cursorToken?: string };
  type TemplateDiagnostic = { line: number; column: number; message: string; severity: TemplateDiagnosticSeverity };
  type TemplateFilePayload = { requestId?: string; relativePath?: string; content?: string; error?: string; files?: CatalogEntry[]; syntaxError?: string | null };
  type PendingRead = { resolve: (payload: TemplateFilePayload) => void; reject: (error: Error) => void };
  type TemplateEditorApi = {
    open(templateId?: string): Promise<void>;
    close(): void;
    save(): Promise<void>;
    reset(): Promise<void>;
  };


  // Fallback used only when the host cannot list the Template Root; canonical names only.
  const FALLBACK_PRESETS: Preset[] = [
    'unitMainOutput.hbs', 'step-body.hbs', 'unitError.hbs', 'unitManual.hbs', 'unitAuto.hbs', 'unitOrigin.hbs', 'output.hbs', 'MapIO.hbs',
    'simple/UnitCode.hbs', 'simple/DeviceManager.hbs', 'simple/SystemControl.hbs', 'simple/MapIO.hbs', 'simple/Error.hbs',
    'packml/UnitCode.hbs', 'packml/DeviceManager.hbs', 'packml/SystemControl.hbs', 'packml/MapIO.hbs', 'packml/Error.hbs',
    'devices/cylinder.hbs', 'devices/servo.hbs', 'devices/motor.hbs', 'devices/generic.hbs'
  ].map(path => ({ id: path, label: path, path }));
  let PRESETS: Preset[] = FALLBACK_PRESETS;

  const SUGGESTIONS: Suggestion[] = [
    { label: '{{#each }}', insert: '{{#each items}}\n  \n{{/each}}', cursorToken: 'items' },
    { label: '{{#if }}', insert: '{{#if condition}}\n  \n{{/if}}', cursorToken: 'condition' },
    { label: '{{else}}', insert: '{{else}}' },
    { label: '{{/each}}', insert: '{{/each}}' },
    { label: '{{/if}}', insert: '{{/if}}' },
    { label: '{{> partial}}', insert: '{{> partial}}', cursorToken: 'partial' },
    { label: '{{! comment }}', insert: '{{! comment }}', cursorToken: 'comment' }
  ];

  let activeId = PRESETS[0].id;
  let currentText = '';
  let loadedText = '';
  let defaultText = '';
  let visibleSuggestions: Suggestion[] = SUGGESTIONS;
  let suggestionVisible = false;
  let suggestionIndex = 0;
  let loadError = '';
  let hostSyntaxError = '';
  let validateTimer = 0;
  let validateToken = 0;
  const pendingTemplateReads: Record<string, PendingRead> = {};

  function el<T extends HTMLElement = HTMLElement>(id: string): T | null {
    return document.getElementById(id) as T | null;
  }


  function isDirty(): boolean {
    return !loadError && currentText !== loadedText;
  }

  function confirmDiscard(): boolean {
    return !isDirty() || window.confirm('You have unsaved changes to this template. Discard them?');
  }

  // execCommand keeps the edit on the browser undo stack (assigning .value would clear Ctrl+Z).
  function replaceTextRange(input: HTMLTextAreaElement, start: number, end: number, text: string): void {
    input.focus();
    input.setSelectionRange(start, end);
    if (!document.execCommand('insertText', false, text)) {
      input.setRangeText(text, start, end, 'end');
    }
  }

  function presetById(id: string): Preset {
    return PRESETS.find(preset => preset.id === id) || PRESETS[0];
  }

  function escapeHtml(text: string): string {
    return String(text || '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  function classForExpression(expression: string): string {
    const inner = expression.slice(2, -2).trim();
    if (inner.startsWith('!')) return 'hbs-comment';
    if (/^#(each|if)\b/.test(inner)) return 'hbs-block';
    if (/^\/(each|if)$/.test(inner)) return 'hbs-block-end';
    if (inner === 'else') return 'hbs-else';
    if (inner.startsWith('>')) return 'hbs-partial';
    return 'hbs-var';
  }

  function renderExpression(expression: string): string {
    const inner = expression.slice(2, -2);
    const trimmed = inner.trim();
    const prefixWhitespace = inner.match(/^\s*/)?.[0] || '';
    const suffixWhitespace = inner.match(/\s*$/)?.[0] || '';
    const body = trimmed || inner;
    return '<span class="hbs-delim">{{</span>'
      + escapeHtml(prefixWhitespace)
      + `<span class="${classForExpression(expression)}">${escapeHtml(body)}</span>`
      + escapeHtml(suffixWhitespace)
      + '<span class="hbs-delim">}}</span>';
  }

  function renderHighlight(text: string): string {
    const pattern = /\{\{[\s\S]*?\}\}/g;
    let html = '';
    let lastIndex = 0;
    let match: RegExpExecArray | null;
    while ((match = pattern.exec(text)) !== null) {
      html += escapeHtml(text.slice(lastIndex, match.index));
      html += renderExpression(match[0]);
      lastIndex = match.index + match[0].length;
    }
    html += escapeHtml(text.slice(lastIndex));
    return html || '&nbsp;';
  }

  function lineColumnAt(text: string, index: number): { line: number; column: number } {
    const before = text.slice(0, Math.max(0, index));
    const lines = before.split('\n');
    return { line: lines.length, column: lines[lines.length - 1].length + 1 };
  }

  function validateTemplate(text: string): TemplateDiagnostic[] {
    const diagnostics: TemplateDiagnostic[] = [];
    const blockStack: { name: string; line: number; column: number }[] = [];
    const pattern = /\{\{([\s\S]*?)(\}\}|$)/g;
    let match: RegExpExecArray | null;

    while ((match = pattern.exec(text)) !== null) {
      const location = lineColumnAt(text, match.index);
      if (match[2] !== '}}') {
        diagnostics.push({ ...location, message: 'Missing closing delimiter }}', severity: 'error' });
        break;
      }

      const inner = match[1].trim();
      const open = inner.match(/^#(each|if|unless|with)\b/);
      const close = inner.match(/^\/(each|if|unless|with)$/);
      if (inner === 'else' || /^else\s/.test(inner)) {
        if (!blockStack.length) diagnostics.push({ ...location, message: '{{else}} is outside any block', severity: 'error' });
      } else if (open) {
        blockStack.push({ name: open[1], ...location });
      } else if (close) {
        const expected = blockStack.pop();
        if (!expected) {
          diagnostics.push({ ...location, message: `Closing block {{/${close[1]}}} has no opening block`, severity: 'error' });
        } else if (expected.name !== close[1]) {
          diagnostics.push({ ...location, message: `Expected {{/${expected.name}}} but found {{/${close[1]}}}`, severity: 'error' });
        }
      } else if (inner.startsWith('>') && !/^>\s*[\w./-]+$/.test(inner)) {
        diagnostics.push({ ...location, message: 'Invalid partial syntax', severity: 'warning' });
      }
    }

    for (const block of blockStack.reverse()) {
      diagnostics.push({ line: block.line, column: block.column, message: `Missing closing block for {{#${block.name}}}`, severity: 'error' });
    }
    return diagnostics;
  }

  function ensureModal(): HTMLElement {
    let root = el('template-editor-modal');
    if (root) return root;

    root = document.createElement('div');
    root.id = 'template-editor-modal';
    root.className = 'modal-bg modal-bg-stretch';
    root.innerHTML = `
      <section class="modal modal-wide template-editor-modal">
        <header class="modal-header template-editor-header">
          <span class="modal-title-accent">&#8862; TEMPLATE EDITOR</span>
          <span id="template-editor-title" class="modal-subtitle"></span>
          <div class="modal-actions">
            <button class="btn" onclick="openTemplateEditorReset()">Reset to default</button>
            <button class="btn" onclick="closeTemplateEditor()">Cancel</button>
            <button id="template-editor-save" class="btn a" onclick="saveTemplateEditor()">Save Template</button>
          </div>
        </header>
        <div class="template-editor-toolbar">
          <div class="template-editor-field">
            <div class="template-editor-label">Template file</div>
            <select id="template-editor-select" class="modal-select" onchange="switchTemplateEditorPreset(this.value)"></select>
          </div>
          <div class="template-editor-field">
            <div class="template-editor-label">Template Root (shared setting)</div>
            <div class="template-editor-root">
              <input id="template-editor-root" class="cg-input" spellcheck="false" onchange="changeTemplateEditorRoot(this.value)">
              <button class="btn" onclick="browseTemplateEditorRoot()">Browse...</button>
            </div>
          </div>
          <div class="template-editor-field template-editor-help">
            <div class="template-editor-label">Shortcuts</div>
            <div>Type <b>{{</b> or press <b>Ctrl+Space</b> for Handlebars snippets</div>
          </div>
          <div class="template-editor-field template-editor-status-field">
            <div class="template-editor-label">Status</div>
            <div id="template-editor-status" class="template-editor-status">Ready</div>
          </div>
        </div>
        <div class="template-editor-body">
          <div class="template-editor-code-wrap">
            <pre id="template-editor-highlight" class="template-editor-highlight" aria-hidden="true"></pre>
            <textarea id="template-editor-textarea" class="template-editor-textarea" spellcheck="false" oninput="onTemplateEditorInput()" onscroll="syncTemplateEditorScroll()" onkeydown="onTemplateEditorKeydown(event)"></textarea>
          </div>
          <div id="template-editor-completions" class="template-editor-completions is-hidden" role="listbox"></div>
        </div>
      </section>`;
    document.body.appendChild(root);
    return root;
  }

  function setPresetOptions(): void {
    const select = el<HTMLSelectElement>('template-editor-select');
    if (!select) return;
    const option = (preset: Preset): string => {
      const suffix = preset.active === false ? ' (not used)' : '';
      const title = [preset.description, preset.note].filter(Boolean).join(' - ');
      return `<option value="${escapeHtml(preset.id)}" title="${escapeHtml(title)}">${escapeHtml(preset.label + suffix)}</option>`;
    };
    const groups: string[] = [];
    PRESETS.forEach(preset => {
      const name = preset.platform ? `${preset.platform} / ${preset.group}` : '';
      if (!groups.includes(name)) groups.push(name);
    });
    select.innerHTML = groups.map(name => {
      const items = PRESETS.filter(preset => (preset.platform ? `${preset.platform} / ${preset.group}` : '') === name).map(option).join('');
      return name ? `<optgroup label="${escapeHtml(name)}">${items}</optgroup>` : items;
    }).join('');
    select.value = activeId;
    const title = el<HTMLElement>('template-editor-title');
    if (title) title.textContent = presetById(activeId).label;
  }

  function templateRootPath(): string {
    const getRoot = (window as Window & { cgGetTemplateRoot?: () => string }).cgGetTemplateRoot;
    return (getRoot ? getRoot() : '').trim();
  }

  function canUseHostBridge(): boolean {
    const candidate = window as Window & { chrome?: { webview?: { postMessage?: (message: unknown) => void } } };
    return typeof candidate.chrome?.webview?.postMessage === 'function';
  }

  function templateHostRequest(type: string, rootPath: string, relativePath: string, content?: string): Promise<TemplateFilePayload> {
    const bridge = (window as Window & { chrome?: { webview?: { postMessage?: (message: unknown) => void } } }).chrome?.webview;
    const postMessage = bridge?.postMessage;
    if (!postMessage) return Promise.reject(new Error('Host bridge is unavailable.'));

    const requestId = 'template-' + Date.now().toString(36) + '-' + Math.random().toString(36).slice(2);
    return new Promise((resolve, reject) => {
      const timeout = window.setTimeout(() => {
        delete pendingTemplateReads[requestId];
        reject(new Error('Timed out waiting for host.'));
      }, 5000);
      pendingTemplateReads[requestId] = {
        resolve: result => {
          window.clearTimeout(timeout);
          delete pendingTemplateReads[requestId];
          resolve(result);
        },
        reject: error => {
          window.clearTimeout(timeout);
          delete pendingTemplateReads[requestId];
          reject(error);
        }
      };
      postMessage.call(bridge, {
        type,
        payload: content === undefined ? { requestId, rootPath, relativePath } : { requestId, rootPath, relativePath, content }
      });
    });
  }

  async function readTemplateViaHost(rootPath: string, relativePath: string): Promise<string> {
    return (await templateHostRequest('READ_TEMPLATE_FILE', rootPath, relativePath)).content || '';
  }

  async function writeTemplateViaHost(rootPath: string, relativePath: string, content: string): Promise<void> {
    await templateHostRequest('WRITE_TEMPLATE_FILE', rootPath, relativePath, content);
  }

  async function refreshPresets(): Promise<void> {
    const rootPath = templateRootPath();
    PRESETS = FALLBACK_PRESETS;
    if (rootPath && canUseHostBridge()) {
      try {
        const result = await templateHostRequest('LIST_TEMPLATE_FILES', rootPath, '');
        const listed = (result.files || []).map<Preset>(file => ({
          id: file.path, label: file.path, path: file.path, platform: file.platform, group: file.group,
          description: file.description, active: file.active, note: file.note
        }));
        if (listed.length) PRESETS = listed;
      } catch (error) {
        updateStatus(`Cannot list templates: ${error instanceof Error ? error.message : String(error)}`);
      }
    }
    if (!PRESETS.some(preset => preset.id === activeId)) activeId = PRESETS[0].id;
  }

  async function loadDefaultText(id: string): Promise<string> {
    const preset = presetById(id);
    const rootPath = templateRootPath();
    if (rootPath && canUseHostBridge()) {
      return await readTemplateViaHost(rootPath, preset.path);
    }

    const url = `https://templates.grafcet.local/${preset.path}`;
    const response = await fetch(url, { cache: 'no-store' });
    if (!response.ok) throw new Error('HTTP ' + response.status);
    return await response.text();
  }

  function setLoadError(message: string): void {
    loadError = message;
    const input = el<HTMLTextAreaElement>('template-editor-textarea');
    if (input) input.readOnly = !!message;
    const save = el<HTMLButtonElement>('template-editor-save');
    if (save) save.disabled = !!message;
  }

  async function loadTemplate(id: string): Promise<void> {
    try {
      defaultText = await loadDefaultText(id);
      setLoadError('');
    } catch (error) {
      defaultText = '';
      setLoadError(`Cannot read ${presetById(id).path}: ${error instanceof Error ? error.message : String(error)}`);
    }
    hostSyntaxError = '';
    currentText = defaultText;
    loadedText = currentText;
    const input = el<HTMLTextAreaElement>('template-editor-textarea');
    if (input) {
      input.value = currentText;
      input.focus();
      input.setSelectionRange(0, 0);
    }
    syncFromTextarea();
  }

  function scheduleHostValidation(): void {
    window.clearTimeout(validateTimer);
    if (loadError || !canUseHostBridge()) return;
    const token = ++validateToken;
    validateTimer = window.setTimeout(async () => {
      let error = '';
      try {
        const result = await templateHostRequest('VALIDATE_TEMPLATE', '', '', currentText);
        error = result.syntaxError || '';
      } catch {
        error = '';
      }
      if (token !== validateToken || error === hostSyntaxError) return;
      hostSyntaxError = error;
      updateStatus();
    }, 400);
  }

  function updateStatus(message?: string): void {
    const status = el<HTMLElement>('template-editor-status');
    if (!status) return;
    if (loadError) {
      status.textContent = loadError;
      status.className = 'template-editor-status has-error';
      return;
    }
    if (message) {
      status.textContent = message;
      status.className = 'template-editor-status';
      return;
    }

    const diagnostics = validateTemplate(currentText);
    const firstError = diagnostics.find(diagnostic => diagnostic.severity === 'error');
    status.className = 'template-editor-status' + (firstError ? ' has-error' : '');
    if (firstError) {
      status.textContent = `Line ${firstError.line}: ${firstError.message}`;
      return;
    }
    if (hostSyntaxError) {
      status.className = 'template-editor-status has-error';
      status.textContent = `Handlebars: ${hostSyntaxError}`;
      return;
    }
    status.textContent = currentText === loadedText ? 'No changes' : 'Edited';
  }

  function syncFromTextarea(): void {
    const input = el<HTMLTextAreaElement>('template-editor-textarea');
    const highlight = el<HTMLElement>('template-editor-highlight');
    if (!input || !highlight) return;
    currentText = input.value;
    highlight.innerHTML = renderHighlight(currentText) + '<br>';
    syncTemplateEditorScroll();
    updateStatus();
    updateCompletions();
    scheduleHostValidation();
  }

  function syncTemplateEditorScroll(): void {
    const input = el<HTMLTextAreaElement>('template-editor-textarea');
    const highlight = el<HTMLElement>('template-editor-highlight');
    if (!input || !highlight) return;
    highlight.scrollTop = input.scrollTop;
    highlight.scrollLeft = input.scrollLeft;
  }

  function showCompletions(visible: boolean): void {
    const box = el<HTMLElement>('template-editor-completions');
    if (!box) return;
    suggestionVisible = visible;
    box.classList.toggle('is-hidden', !visible);
  }

  function completionContext(force: boolean): { typed: string; shouldShow: boolean } {
    const input = el<HTMLTextAreaElement>('template-editor-textarea');
    if (!input) return { typed: '', shouldShow: false };
    const cursor = input.selectionStart || 0;
    const before = input.value.slice(0, cursor);
    const markerIndex = before.lastIndexOf('{{');
    const closeIndex = before.lastIndexOf('}}');
    const insideExpression = markerIndex >= 0 && markerIndex > closeIndex;
    const typed = insideExpression ? before.slice(markerIndex + 2).trim().toLowerCase() : '';
    return {
      typed,
      shouldShow: force || before.endsWith('{{') || before.endsWith('{{#') || before.endsWith('{{/') || before.endsWith('{{>') || (insideExpression && typed.length <= 16)
    };
  }

  function updateCompletions(force = false): void {
    const box = el<HTMLElement>('template-editor-completions');
    if (!box) return;
    const context = completionContext(force);
    if (!context.shouldShow) {
      showCompletions(false);
      return;
    }

    visibleSuggestions = SUGGESTIONS.filter(item => !context.typed || item.label.toLowerCase().includes(context.typed) || item.insert.toLowerCase().includes(context.typed));
    if (!visibleSuggestions.length) {
      showCompletions(false);
      return;
    }
    suggestionIndex = Math.max(0, Math.min(suggestionIndex, visibleSuggestions.length - 1));
    box.innerHTML = visibleSuggestions.map((item, index) => `<button class="template-editor-completion ${index === suggestionIndex ? 'active' : ''}" data-index="${index}" onclick="pickTemplateEditorSuggestion(${index})"><span>${escapeHtml(item.label)}</span></button>`).join('');
    showCompletions(true);
  }

  function replaceRangeForSuggestion(input: HTMLTextAreaElement): { start: number; end: number } {
    const cursor = input.selectionStart || 0;
    const before = input.value.slice(0, cursor);
    const markerIndex = before.lastIndexOf('{{');
    if (markerIndex < 0) return { start: cursor, end: input.selectionEnd || cursor };
    const closingIndex = input.value.indexOf('}}', cursor);
    return { start: markerIndex, end: closingIndex >= 0 ? closingIndex + 2 : input.selectionEnd || cursor };
  }

  function insertSuggestion(index: number): void {
    const input = el<HTMLTextAreaElement>('template-editor-textarea');
    if (!input) return;
    const item = visibleSuggestions[index] || visibleSuggestions[0] || SUGGESTIONS[0];
    const range = replaceRangeForSuggestion(input);
    replaceTextRange(input, range.start, range.end, item.insert);
    const tokenIndex = item.cursorToken ? item.insert.indexOf(item.cursorToken) : -1;
    const nextPos = range.start + (tokenIndex >= 0 ? tokenIndex : item.insert.length);
    input.focus();
    input.setSelectionRange(nextPos, nextPos + (tokenIndex >= 0 && item.cursorToken ? item.cursorToken.length : 0));
    showCompletions(false);
    syncFromTextarea();
  }

  function syncRootInput(): void {
    const input = el<HTMLInputElement>('template-editor-root');
    if (input) input.value = templateRootPath();
  }

  function changeTemplateEditorRoot(path: string): void {
    if (!confirmDiscard()) {
      syncRootInput();
      return;
    }
    loadedText = currentText;
    const setRoot = (window as Window & { cgSetTemplateRoot?: (path: string) => void }).cgSetTemplateRoot;
    if (setRoot) setRoot(path);
  }

  function browseTemplateEditorRoot(): void {
    const bridge = (window as Window & { chrome?: { webview?: { postMessage?: (message: unknown) => void } } }).chrome?.webview;
    if (bridge?.postMessage) bridge.postMessage({ type: 'BROWSE_CODEGEN_PATH', payload: { target: 'templateRoot' } });
  }

  async function onTemplateRootChanged(): Promise<void> {
    const modal = el('template-editor-modal');
    if (!modal || !modal.classList.contains('show')) return;
    syncRootInput();
    await refreshPresets();
    setPresetOptions();
    await loadTemplate(activeId);
  }

  async function openTemplateEditor(id?: string): Promise<void> {
    const modal = ensureModal();
    syncRootInput();
    activeId = id || activeId || PRESETS[0].id;
    await refreshPresets();
    setPresetOptions();
    showCompletions(false);
    modal.classList.add('show');
    await loadTemplate(activeId);
  }

  function closeTemplateEditor(force = false): void {
    if (!force && !confirmDiscard()) return;
    const modal = el('template-editor-modal');
    if (modal) modal.classList.remove('show');
    showCompletions(false);
  }

  async function saveTemplateEditor(): Promise<void> {
    if (loadError) return;
    const preset = presetById(activeId);
    const rootPath = templateRootPath();
    if (rootPath && canUseHostBridge()) {
      try {
        await writeTemplateViaHost(rootPath, preset.path, currentText || '');
        loadedText = currentText;
        updateStatus(`Saved ${preset.path} (previous version kept as ${preset.path}.bak)`);
      } catch (error) {
        updateStatus(`Save failed: ${error instanceof Error ? error.message : String(error)}`);
      }
      return;
    }

    const filename = preset.path.replace(/[\\/]/g, '__');
    const blob = new Blob([currentText || ''], { type: 'text/plain;charset=utf-8' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = filename;
    link.click();
    setTimeout(() => URL.revokeObjectURL(link.href), 1000);
    loadedText = currentText;
    updateStatus('Downloaded template file');
  }

  async function switchTemplateEditorPreset(id: string): Promise<void> {
    if (!confirmDiscard()) {
      setPresetOptions();
      return;
    }
    activeId = id;
    setPresetOptions();
    showCompletions(false);
    await loadTemplate(id);
  }

  async function openTemplateEditorReset(): Promise<void> {
    if (loadError) return;
    currentText = defaultText;
    loadedText = currentText;
    const input = el<HTMLTextAreaElement>('template-editor-textarea');
    if (input) input.value = currentText;
    syncFromTextarea();
    updateStatus('Reset to default');
  }

  function onTemplateEditorInput(): void {
    syncFromTextarea();
  }

  function onTemplateEditorKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      if (suggestionVisible) showCompletions(false);
      else closeTemplateEditor();
      return;
    }

    if (event.key === 'Tab') {
      const input = el<HTMLTextAreaElement>('template-editor-textarea');
      if (!input) return;
      event.preventDefault();
      const start = input.selectionStart || 0;
      const end = input.selectionEnd || start;
      replaceTextRange(input, start, end, '  ');
      syncFromTextarea();
      return;
    }

    if (event.key === ' ' && event.ctrlKey) {
      event.preventDefault();
      updateCompletions(true);
      return;
    }

    if (event.key === 'ArrowDown' && suggestionVisible) {
      event.preventDefault();
      suggestionIndex = Math.min(visibleSuggestions.length - 1, suggestionIndex + 1);
      updateCompletions(true);
      return;
    }

    if (event.key === 'ArrowUp' && suggestionVisible) {
      event.preventDefault();
      suggestionIndex = Math.max(0, suggestionIndex - 1);
      updateCompletions(true);
      return;
    }

    if (event.key === 'Enter' && suggestionVisible) {
      event.preventDefault();
      insertSuggestion(suggestionIndex);
    }
  }

  function pickTemplateEditorSuggestion(index: number): void {
    suggestionIndex = index;
    insertSuggestion(index);
  }

  const api: TemplateEditorApi = {
    open: openTemplateEditor,
    close: closeTemplateEditor,
    save: saveTemplateEditor,
    reset: openTemplateEditorReset
  };

  function receiveTemplateFile(payload: TemplateFilePayload): void {
    const requestId = payload?.requestId || '';
    const pending = pendingTemplateReads[requestId];
    if (!pending) return;
    if (payload?.error) pending.reject(new Error(payload.error));
    else pending.resolve(payload || {});
  }

  (window as unknown as Window & {
    GrafcetTemplateEditor?: TemplateEditorApi;
    receiveTemplateFile?: typeof receiveTemplateFile;
    openTemplateEditor?: typeof openTemplateEditor;
    closeTemplateEditor?: typeof closeTemplateEditor;
    saveTemplateEditor?: typeof saveTemplateEditor;
    switchTemplateEditorPreset?: typeof switchTemplateEditorPreset;
    openTemplateEditorReset?: typeof openTemplateEditorReset;
    onTemplateEditorInput?: typeof onTemplateEditorInput;
    onTemplateEditorKeydown?: typeof onTemplateEditorKeydown;
    syncTemplateEditorScroll?: typeof syncTemplateEditorScroll;
    pickTemplateEditorSuggestion?: typeof pickTemplateEditorSuggestion;
  }).GrafcetTemplateEditor = api;
  (window as Window & any).changeTemplateEditorRoot = changeTemplateEditorRoot;
  (window as Window & any).browseTemplateEditorRoot = browseTemplateEditorRoot;
  (window as Window & any).onTemplateRootChanged = onTemplateRootChanged;
  (window as Window & any).receiveTemplateFile = receiveTemplateFile;
  (window as Window & any).openTemplateEditor = openTemplateEditor;
  (window as Window & any).closeTemplateEditor = closeTemplateEditor;
  (window as Window & any).saveTemplateEditor = saveTemplateEditor;
  (window as Window & any).switchTemplateEditorPreset = switchTemplateEditorPreset;
  (window as Window & any).openTemplateEditorReset = openTemplateEditorReset;
  (window as Window & any).onTemplateEditorInput = onTemplateEditorInput;
  (window as Window & any).onTemplateEditorKeydown = onTemplateEditorKeydown;
  (window as Window & any).syncTemplateEditorScroll = syncTemplateEditorScroll;
  (window as Window & any).pickTemplateEditorSuggestion = pickTemplateEditorSuggestion;
}

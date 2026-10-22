namespace GrafcetStudioTemplateEditor {
  type TemplateDiagnosticSeverity = 'error' | 'warning' | 'info';
  type Preset = { id: string; label: string; path: string; platform?: string; group?: string; description?: string; active?: boolean; note?: string };
  type CatalogEntry = { path: string; platform: string; group: string; description: string; active: boolean; note: string };
  type Suggestion = { label: string; insert: string; cursorToken?: string; detail?: string; range?: { start: number; end: number } };
  type VarNode = { name: string; kind: string; sample?: string | null; children: VarNode[] };
  type TemplateDiagnostic = { line: number; column: number; message: string; severity: TemplateDiagnosticSeverity };
  type TemplateFilePayload = { requestId?: string; relativePath?: string; content?: string; error?: string; files?: CatalogEntry[]; syntaxError?: string | null; preview?: PreviewResult; variables?: VarNode };
  type PreviewResult = { files: { path: string; content: string }[]; warnings: string[] };
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

  const HELPERS: Suggestion[] = ['pad', 'padStart2', 'eq', 'renderDevice'].map(name => ({ label: name, insert: name + ' ', detail: 'helper' }));

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
  let previewOn = false;
  let previewTimer = 0;
  let previewToken = 0;
  let previewPayload: unknown = null;
  let previewPayloadError = '';
  let previewFiles: { path: string; content: string }[] = [];
  let previewSelected = '';
  let variableRoot: VarNode | null = null;
  let variablesToken = 0;
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
            <button id="template-editor-preview-toggle" class="btn" onclick="toggleTemplateEditorPreview()">Preview</button>
            <button class="btn" onclick="openTemplateEditorReset()">Reset to default</button>
            <button class="btn" onclick="closeTemplateEditor()">Cancel</button>
            <button id="template-editor-save" class="btn a" onclick="saveTemplateEditor()">Save Template</button>
          </div>
        </header>
        <div class="template-editor-toolbar">
          <div class="template-editor-field">
            <div class="template-editor-label">Template file</div>
            <div class="template-editor-pickers">
              <select id="template-editor-platform" class="modal-select" title="Platform" onchange="switchTemplateEditorPlatform(this.value)"></select>
              <select id="template-editor-select" class="modal-select" onchange="switchTemplateEditorPreset(this.value)"></select>
            </div>
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
          <div id="template-editor-preview" class="template-editor-preview is-hidden">
            <div class="template-editor-preview-bar">
              <select id="template-editor-preview-file" class="modal-select" onchange="selectTemplateEditorPreviewFile(this.value)"></select>
              <button class="btn" title="Re-read the current project data" onclick="refreshTemplateEditorPreviewData()">Refresh data</button>
            </div>
            <div id="template-editor-preview-note" class="template-editor-preview-note is-hidden"></div>
            <pre id="template-editor-preview-out" class="template-editor-preview-out"></pre>
          </div>
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
    const platforms: string[] = [];
    PRESETS.forEach(preset => {
      if (preset.platform && !platforms.includes(preset.platform)) platforms.push(preset.platform);
    });
    const platformSelect = el<HTMLSelectElement>('template-editor-platform');
    const activePlatform = presetById(activeId).platform || '';
    if (platformSelect) {
      platformSelect.innerHTML = platforms.map(name => `<option value="${escapeHtml(name)}">${escapeHtml(name)}</option>`).join('');
      platformSelect.value = activePlatform;
      platformSelect.style.display = platforms.length > 1 ? '' : 'none';
    }

    const visible = platforms.length > 1 ? PRESETS.filter(preset => (preset.platform || '') === activePlatform) : PRESETS;
    const groups: string[] = [];
    visible.forEach(preset => {
      const name = preset.group || '';
      if (!groups.includes(name)) groups.push(name);
    });
    select.innerHTML = groups.map(name => {
      const items = visible.filter(preset => (preset.group || '') === name).map(option).join('');
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

  function templateHostRequest(type: string, rootPath: string, relativePath: string, content?: string, extra?: Record<string, unknown>, timeoutMs = 5000): Promise<TemplateFilePayload> {
    const bridge = (window as Window & { chrome?: { webview?: { postMessage?: (message: unknown) => void } } }).chrome?.webview;
    const postMessage = bridge?.postMessage;
    if (!postMessage) return Promise.reject(new Error('Host bridge is unavailable.'));

    const requestId = 'template-' + Date.now().toString(36) + '-' + Math.random().toString(36).slice(2);
    return new Promise((resolve, reject) => {
      const timeout = window.setTimeout(() => {
        delete pendingTemplateReads[requestId];
        reject(new Error('Timed out waiting for host.'));
      }, timeoutMs);
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
        payload: { requestId, rootPath, relativePath, ...(content === undefined ? {} : { content }), ...(extra || {}) }
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

  function buildPreviewPayload(): void {
    previewPayload = null;
    previewPayloadError = '';
    const build = (window as Window & { cgBuildCSharpPayload?: (platform: string) => unknown }).cgBuildCSharpPayload;
    if (!build) {
      previewPayloadError = 'Project data is not available.';
      return;
    }
    try {
      previewPayload = build('Keyence');
    } catch (error) {
      previewPayloadError = 'Cannot build project data: ' + (error instanceof Error ? error.message : String(error));
    }
  }

  function defaultPreviewFile(files: { path: string }[]): string {
    const path = presetById(activeId).path.toLowerCase();
    const wanted = path.includes('error') && !path.startsWith('unit') ? 'error.mnm'
      : path.includes('systemcontrol') || path.includes('system') ? 'system.mnm'
      : path.includes('devicemanager') ? 'devicemanager.mnm'
      : path.includes('mapio') ? 'iomapping.mnm'
      : 'units/';
    const hit = files.find(file => file.path.toLowerCase().includes(wanted));
    return (hit || files[0] || { path: '' }).path;
  }

  function showPreviewNote(text: string, isError: boolean): void {
    const note = el<HTMLElement>('template-editor-preview-note');
    if (!note) return;
    note.textContent = text;
    note.classList.toggle('is-hidden', !text);
    note.classList.toggle('has-error', isError);
  }

  function renderPreviewFile(): void {
    const out = el<HTMLElement>('template-editor-preview-out');
    const select = el<HTMLSelectElement>('template-editor-preview-file');
    if (!out || !select) return;
    select.innerHTML = previewFiles.map(file => `<option value="${escapeHtml(file.path)}">${escapeHtml(file.path)}</option>`).join('');
    if (!previewFiles.some(file => file.path === previewSelected)) previewSelected = defaultPreviewFile(previewFiles);
    select.value = previewSelected;
    const file = previewFiles.find(item => item.path === previewSelected);
    out.textContent = file ? file.content : '';
  }

  function schedulePreview(): void {
    window.clearTimeout(previewTimer);
    if (!previewOn || loadError) return;
    const token = ++previewToken;
    previewTimer = window.setTimeout(async () => {
      if (presetById(activeId).platform === 'Siemens LAD') {
        showPreviewNote('Preview is not available for Siemens LAD templates yet.', false);
        return;
      }
      if (!canUseHostBridge()) {
        showPreviewNote('Preview needs the desktop host.', true);
        return;
      }
      if (!previewPayload) {
        showPreviewNote(previewPayloadError || 'Project data is not available.', true);
        return;
      }
      try {
        const result = await templateHostRequest('PREVIEW_TEMPLATE', templateRootPath(), presetById(activeId).path, currentText, { codegenPayload: previewPayload }, 30000);
        if (token !== previewToken) return;
        previewFiles = result.preview?.files || [];
        showPreviewNote((result.preview?.warnings || []).join(' '), false);
        renderPreviewFile();
      } catch (error) {
        if (token !== previewToken) return;
        showPreviewNote('Render failed: ' + (error instanceof Error ? error.message : String(error)), true);
      }
    }, 500);
  }

  function toggleTemplateEditorPreview(): void {
    previewOn = !previewOn;
    el('template-editor-preview')?.classList.toggle('is-hidden', !previewOn);
    el('template-editor-modal')?.querySelector('.template-editor-body')?.classList.toggle('has-preview', previewOn);
    el('template-editor-preview-toggle')?.classList.toggle('a', previewOn);
    if (previewOn) {
      buildPreviewPayload();
      schedulePreview();
    }
  }

  async function loadVariables(): Promise<void> {
    variableRoot = null;
    if (!previewPayload || !canUseHostBridge()) return;
    const token = ++variablesToken;
    try {
      const result = await templateHostRequest('LIST_TEMPLATE_VARIABLES', '', presetById(activeId).path, undefined, { codegenPayload: previewPayload }, 30000);
      if (token === variablesToken) variableRoot = result.variables || null;
    } catch {
      if (token === variablesToken) variableRoot = null;
    }
  }

  function refreshTemplateEditorPreviewData(): void {
    buildPreviewPayload();
    schedulePreview();
    void loadVariables();
  }

  function selectTemplateEditorPreviewFile(path: string): void {
    previewSelected = path;
    renderPreviewFile();
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
    schedulePreview();
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

  function findChild(node: VarNode, name: string): VarNode | null {
    const lower = name.toLowerCase();
    return node.children.find(child => child.name.toLowerCase() === lower) || null;
  }

  function descend(base: VarNode | null, parts: string[]): VarNode | null {
    let node = base;
    for (const part of parts) {
      if (!node) return null;
      if (!part || part === 'this') continue;
      node = findChild(node, part);
    }
    return node;
  }

  // Resolves a Handlebars path (this., ../, @root.) against the scope stack; innermost scope is last.
  function resolveVarPath(path: string, scopes: VarNode[]): VarNode | null {
    let rest = path;
    let up = 0;
    while (rest.startsWith('../')) {
      up++;
      rest = rest.slice(3);
    }
    let base: VarNode | undefined = scopes[scopes.length - 1 - up];
    if (rest === '@root' || rest.startsWith('@root.')) {
      base = scopes[0];
      rest = rest.slice(6);
    }
    if (!base) return null;
    return descend(base, rest.split('.').filter(Boolean));
  }

  // Scopes opened by #each/#with blocks that are still open at the end of `text`.
  function scopesAt(text: string): { scopes: VarNode[]; inEach: boolean } | null {
    if (!variableRoot) return null;
    const scopes: VarNode[] = [variableRoot];
    const blocks: { name: string; scoped: boolean }[] = [];
    const pattern = /\{\{~?([\s\S]*?)~?\}\}/g;
    let match: RegExpExecArray | null;
    while ((match = pattern.exec(text)) !== null) {
      const inner = match[1].trim();
      const open = inner.match(/^#(each|with|if|unless)\s*(.*)$/);
      const close = inner.match(/^\/(each|with|if|unless)$/);
      if (open) {
        const scoped = open[1] === 'each' || open[1] === 'with';
        if (scoped) {
          const target = resolveVarPath(open[2].trim().split(/\s+/)[0] || '', scopes);
          scopes.push(target || { name: '', kind: 'null', children: [] });
        }
        blocks.push({ name: open[1], scoped });
      } else if (close) {
        const block = blocks.pop();
        if (block && block.scoped && scopes.length > 1) scopes.pop();
      }
    }
    return { scopes, inEach: blocks.some(block => block.name === 'each') };
  }

  function kindDetail(node: VarNode): string {
    if (node.kind === 'list') return 'list';
    if (node.kind === 'object') return 'object';
    return node.sample ? `${node.kind}: ${node.sample}` : node.kind;
  }

  // Variable suggestions for the identifier being typed inside an unclosed {{ ... }}.
  function variableSuggestions(input: HTMLTextAreaElement): Suggestion[] {
    if (!variableRoot) return [];
    const cursor = input.selectionStart || 0;
    const before = input.value.slice(0, cursor);
    const markerIndex = before.lastIndexOf('{{');
    if (markerIndex < 0 || markerIndex < before.lastIndexOf('}}')) return [];

    let expression = before.slice(markerIndex + 2).replace(/^[~{&^]+/, '');
    if (/^[/!>]/.test(expression)) return [];
    const isBlock = expression.startsWith('#');
    expression = expression.replace(/^#/, '');
    const words = expression.split(/[\s(]+/);
    const token = words[words.length - 1];
    const isFirstWord = words.length === 1;
    if (isBlock && isFirstWord) return [];

    const scope = scopesAt(before.slice(0, markerIndex));
    if (!scope) return [];

    let afterUp = token;
    let up = 0;
    while (afterUp.startsWith('../')) {
      up++;
      afterUp = afterUp.slice(3);
    }
    const dot = afterUp.lastIndexOf('.');
    const prefix = afterUp.slice(dot + 1);
    const parentPath = dot >= 0 ? afterUp.slice(0, dot) : '';
    const base: VarNode | null = parentPath
      ? resolveVarPath('../'.repeat(up) + parentPath, scope.scopes)
      : scope.scopes[scope.scopes.length - 1 - up] || null;
    if (!base) return [];

    const lowerPrefix = prefix.toLowerCase();
    const matches = (name: string): number => {
      const lower = name.toLowerCase();
      return !lowerPrefix || lower.startsWith(lowerPrefix) ? 0 : lower.includes(lowerPrefix) ? 1 : -1;
    };
    const range = { start: cursor - prefix.length, end: cursor };
    const trailing = /^[\w$]*/.exec(input.value.slice(cursor));
    if (trailing) range.end += trailing[0].length;

    const found: { rank: number; suggestion: Suggestion }[] = [];
    for (const child of base.children) {
      const rank = matches(child.name);
      if (rank < 0) continue;
      found.push({ rank, suggestion: { label: child.name, insert: child.name + (child.kind === 'object' ? '.' : ''), detail: kindDetail(child), range } });
    }
    if (!parentPath && up === 0) {
      const specials = ['this'];
      if (scope.scopes.length > 1) specials.push('../');
      specials.push('@root');
      if (scope.inEach) specials.push('@index', '@first', '@last', '@key');
      for (const name of specials) {
        const rank = matches(name);
        if (rank >= 0) found.push({ rank, suggestion: { label: name, insert: name, detail: 'scope', range } });
      }
      if (isFirstWord && !isBlock) {
        for (const helper of HELPERS) {
          const rank = matches(helper.label);
          if (rank >= 0) found.push({ rank, suggestion: { ...helper, range } });
        }
      }
    }
    return found.sort((a, b) => a.rank - b.rank).slice(0, 80).map(item => item.suggestion);
  }

  function updateCompletions(force = false): void {
    const box = el<HTMLElement>('template-editor-completions');
    const input = el<HTMLTextAreaElement>('template-editor-textarea');
    if (!box || !input) return;
    const context = completionContext(force);
    const variables = variableSuggestions(input);
    if (!context.shouldShow && !variables.length) {
      showCompletions(false);
      return;
    }

    // Snippets only make sense for the first word of an expression; later words are arguments.
    const expressionStart = input.value.lastIndexOf('{{', input.selectionStart || 0);
    const inArgument = variables.length > 0 && expressionStart >= 0 && /\s/.test(input.value.slice(expressionStart + 2, input.selectionStart || 0).trimStart());
    const snippets = inArgument ? [] : SUGGESTIONS.filter(item => !context.typed || item.label.toLowerCase().includes(context.typed) || item.insert.toLowerCase().includes(context.typed));
    visibleSuggestions = context.shouldShow ? [...variables, ...snippets] : variables;
    if (!visibleSuggestions.length) {
      showCompletions(false);
      return;
    }
    suggestionIndex = Math.max(0, Math.min(suggestionIndex, visibleSuggestions.length - 1));
    box.innerHTML = visibleSuggestions.map((item, index) => `<button class="template-editor-completion ${index === suggestionIndex ? 'active' : ''}" data-index="${index}" onclick="pickTemplateEditorSuggestion(${index})"><span>${escapeHtml(item.label)}</span>${item.detail ? `<small>${escapeHtml(item.detail)}</small>` : ''}</button>`).join('');
    showCompletions(true);
    box.querySelector('.active')?.scrollIntoView({ block: 'nearest' });
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
    const range = item.range || replaceRangeForSuggestion(input);
    replaceTextRange(input, range.start, range.end, item.insert);
    const tokenIndex = item.cursorToken ? item.insert.indexOf(item.cursorToken) : -1;
    const nextPos = range.start + (tokenIndex >= 0 ? tokenIndex : item.insert.length);
    input.focus();
    input.setSelectionRange(nextPos, nextPos + (tokenIndex >= 0 && item.cursorToken ? item.cursorToken.length : 0));
    showCompletions(false);
    syncFromTextarea();
    if (item.range && item.insert.endsWith('.')) updateCompletions(true);
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
    buildPreviewPayload();
    void loadVariables();
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

  async function switchTemplateEditorPlatform(platform: string): Promise<void> {
    const inPlatform = PRESETS.filter(preset => preset.platform === platform);
    const first = inPlatform.find(preset => preset.active !== false) || inPlatform[0];
    if (!first) {
      setPresetOptions();
      return;
    }
    await switchTemplateEditorPreset(first.id);
  }

  async function switchTemplateEditorPreset(id: string): Promise<void> {
    if (!confirmDiscard()) {
      setPresetOptions();
      return;
    }
    activeId = id;
    previewSelected = '';
    setPresetOptions();
    showCompletions(false);
    void loadVariables();
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
  (window as Window & any).switchTemplateEditorPlatform = switchTemplateEditorPlatform;
  (window as Window & any).toggleTemplateEditorPreview = toggleTemplateEditorPreview;
  (window as Window & any).refreshTemplateEditorPreviewData = refreshTemplateEditorPreviewData;
  (window as Window & any).selectTemplateEditorPreviewFile = selectTemplateEditorPreviewFile;
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

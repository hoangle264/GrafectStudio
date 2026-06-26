"use strict";
var GrafcetStudioTemplateEditor;
(function (GrafcetStudioTemplateEditor) {
    const PRESETS = [
        { id: 'main-output', label: 'main-output.hbs', path: 'main-output.hbs' },
        { id: 'step-body', label: 'step-body.hbs', path: 'step-body.hbs' },
        { id: 'step-body-1', label: 'step-body-1.hbs', path: 'step-body-1.hbs' },
        { id: 'error', label: 'error.hbs', path: 'error.hbs' },
        { id: 'manual', label: 'manual.hbs', path: 'manual.hbs' },
        { id: 'auto', label: 'auto.hbs', path: 'auto.hbs' },
        { id: 'origin', label: 'origin.hbs', path: 'origin.hbs' },
        { id: 'output', label: 'output.hbs', path: 'output.hbs' },
        { id: 'simple-UnitCode', label: 'simple/UnitCode.hbs', path: 'simple/UnitCode.hbs' },
        { id: 'simple-DeviceManager', label: 'simple/DeviceManager.hbs', path: 'simple/DeviceManager.hbs' },
        { id: 'simple-SystemControl', label: 'simple/SystemControl.hbs', path: 'simple/SystemControl.hbs' },
        { id: 'simple-MapIO', label: 'simple/MapIO.hbs', path: 'simple/MapIO.hbs' },
        { id: 'simple-Error', label: 'simple/Error.hbs', path: 'simple/Error.hbs' },
        { id: 'packml-UnitCode', label: 'packml/UnitCode.hbs', path: 'packml/UnitCode.hbs' },
        { id: 'packml-DeviceManager', label: 'packml/DeviceManager.hbs', path: 'packml/DeviceManager.hbs' },
        { id: 'packml-SystemControl', label: 'packml/SystemControl.hbs', path: 'packml/SystemControl.hbs' },
        { id: 'packml-MapIO', label: 'packml/MapIO.hbs', path: 'packml/MapIO.hbs' },
        { id: 'packml-Error', label: 'packml/Error.hbs', path: 'packml/Error.hbs' },
        { id: 'devices-cylinder', label: 'devices/cylinder.hbs', path: 'devices/cylinder.hbs' },
        { id: 'devices-servo', label: 'devices/servo.hbs', path: 'devices/servo.hbs' },
        { id: 'devices-motor', label: 'devices/motor.hbs', path: 'devices/motor.hbs' },
        { id: 'devices-generic', label: 'devices/generic.hbs', path: 'devices/generic.hbs' },
        { id: 'devices-robot', label: 'devices/robot.hbs', path: 'devices/robot.hbs' },
        { id: 'devices-device_robot', label: 'devices/device_robot.hbs', path: 'devices/device_robot.hbs' },
        { id: 'devices-device_starter', label: 'devices/device_starter.hbs', path: 'devices/device_starter.hbs' }
    ];
    const SUGGESTIONS = [
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
    let visibleSuggestions = SUGGESTIONS;
    let suggestionVisible = false;
    let suggestionIndex = 0;
    const pendingTemplateReads = {};
    function el(id) {
        return document.getElementById(id);
    }
    function presetById(id) {
        return PRESETS.find(preset => preset.id === id) || PRESETS[0];
    }
    function escapeHtml(text) {
        return String(text || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }
    function classForExpression(expression) {
        const inner = expression.slice(2, -2).trim();
        if (inner.startsWith('!'))
            return 'hbs-comment';
        if (/^#(each|if)\b/.test(inner))
            return 'hbs-block';
        if (/^\/(each|if)$/.test(inner))
            return 'hbs-block-end';
        if (inner === 'else')
            return 'hbs-else';
        if (inner.startsWith('>'))
            return 'hbs-partial';
        return 'hbs-var';
    }
    function renderExpression(expression) {
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
    function renderHighlight(text) {
        const pattern = /\{\{[\s\S]*?\}\}/g;
        let html = '';
        let lastIndex = 0;
        let match;
        while ((match = pattern.exec(text)) !== null) {
            html += escapeHtml(text.slice(lastIndex, match.index));
            html += renderExpression(match[0]);
            lastIndex = match.index + match[0].length;
        }
        html += escapeHtml(text.slice(lastIndex));
        return html || '&nbsp;';
    }
    function lineColumnAt(text, index) {
        const before = text.slice(0, Math.max(0, index));
        const lines = before.split('\n');
        return { line: lines.length, column: lines[lines.length - 1].length + 1 };
    }
    function validateTemplate(text) {
        const diagnostics = [];
        const blockStack = [];
        const pattern = /\{\{([\s\S]*?)(\}\}|$)/g;
        let match;
        while ((match = pattern.exec(text)) !== null) {
            const location = lineColumnAt(text, match.index);
            if (match[2] !== '}}') {
                diagnostics.push({ ...location, message: 'Missing closing delimiter }}', severity: 'error' });
                break;
            }
            const inner = match[1].trim();
            const open = inner.match(/^#(each|if)\b/);
            const close = inner.match(/^\/(each|if)$/);
            if (open) {
                blockStack.push({ name: open[1], ...location });
            }
            else if (close) {
                const expected = blockStack.pop();
                if (!expected) {
                    diagnostics.push({ ...location, message: `Closing block {{/${close[1]}}} has no opening block`, severity: 'error' });
                }
                else if (expected.name !== close[1]) {
                    diagnostics.push({ ...location, message: `Expected {{/${expected.name}}} but found {{/${close[1]}}}`, severity: 'error' });
                }
            }
            else if (inner.startsWith('>') && !/^>\s*[\w./-]+$/.test(inner)) {
                diagnostics.push({ ...location, message: 'Invalid partial syntax', severity: 'warning' });
            }
        }
        for (const block of blockStack.reverse()) {
            diagnostics.push({ line: block.line, column: block.column, message: `Missing closing block for {{#${block.name}}}`, severity: 'error' });
        }
        return diagnostics;
    }
    function ensureModal() {
        let root = el('template-editor-modal');
        if (root)
            return root;
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
            <button class="btn a" onclick="saveTemplateEditor()">Save Template</button>
          </div>
        </header>
        <div class="template-editor-toolbar">
          <div class="template-editor-field">
            <div class="template-editor-label">Template file</div>
            <select id="template-editor-select" class="modal-select" onchange="switchTemplateEditorPreset(this.value)"></select>
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
    function setPresetOptions() {
        const select = el('template-editor-select');
        if (!select)
            return;
        select.innerHTML = PRESETS.map(preset => `<option value="${escapeHtml(preset.id)}">${escapeHtml(preset.label)}</option>`).join('');
        select.value = activeId;
        const title = el('template-editor-title');
        if (title)
            title.textContent = presetById(activeId).label;
    }
    function templateRootPath() {
        const input = document.getElementById('cg-template-root-path');
        return (input?.value || '').trim();
    }
    function canUseHostBridge() {
        const candidate = window;
        return typeof candidate.chrome?.webview?.postMessage === 'function';
    }
    function readTemplateViaHost(rootPath, relativePath) {
        const bridge = window.chrome?.webview;
        const postMessage = bridge?.postMessage;
        if (!postMessage)
            return Promise.reject(new Error('Host bridge is unavailable.'));
        const requestId = 'template-' + Date.now().toString(36) + '-' + Math.random().toString(36).slice(2);
        return new Promise((resolve, reject) => {
            const timeout = window.setTimeout(() => {
                delete pendingTemplateReads[requestId];
                reject(new Error('Timed out reading template from host.'));
            }, 5000);
            pendingTemplateReads[requestId] = content => {
                window.clearTimeout(timeout);
                delete pendingTemplateReads[requestId];
                resolve(content);
            };
            postMessage.call(bridge, {
                type: 'READ_TEMPLATE_FILE',
                payload: { requestId, rootPath, relativePath }
            });
        });
    }
    async function loadDefaultText(id) {
        const preset = presetById(id);
        const rootPath = templateRootPath();
        if (rootPath && canUseHostBridge()) {
            try {
                return await readTemplateViaHost(rootPath, preset.path);
            }
            catch {
            }
        }
        const url = `https://templates.grafcet.local/${preset.path}`;
        try {
            const response = await fetch(url, { cache: 'no-store' });
            if (!response.ok)
                throw new Error('HTTP ' + response.status);
            return await response.text();
        }
        catch {
            return '{{! Template file unavailable }}\n';
        }
    }
    async function loadTemplate(id) {
        defaultText = await loadDefaultText(id);
        currentText = defaultText;
        loadedText = currentText;
        const input = el('template-editor-textarea');
        if (input) {
            input.value = currentText;
            input.focus();
            input.setSelectionRange(0, 0);
        }
        syncFromTextarea();
    }
    function updateStatus(message) {
        const status = el('template-editor-status');
        if (!status)
            return;
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
        status.textContent = currentText === loadedText ? 'No changes' : 'Edited';
    }
    function syncFromTextarea() {
        const input = el('template-editor-textarea');
        const highlight = el('template-editor-highlight');
        if (!input || !highlight)
            return;
        currentText = input.value;
        highlight.innerHTML = renderHighlight(currentText) + '<br>';
        syncTemplateEditorScroll();
        updateStatus();
        updateCompletions();
    }
    function syncTemplateEditorScroll() {
        const input = el('template-editor-textarea');
        const highlight = el('template-editor-highlight');
        if (!input || !highlight)
            return;
        highlight.scrollTop = input.scrollTop;
        highlight.scrollLeft = input.scrollLeft;
    }
    function showCompletions(visible) {
        const box = el('template-editor-completions');
        if (!box)
            return;
        suggestionVisible = visible;
        box.classList.toggle('is-hidden', !visible);
    }
    function completionContext(force) {
        const input = el('template-editor-textarea');
        if (!input)
            return { typed: '', shouldShow: false };
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
    function updateCompletions(force = false) {
        const box = el('template-editor-completions');
        if (!box)
            return;
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
    function replaceRangeForSuggestion(input) {
        const cursor = input.selectionStart || 0;
        const before = input.value.slice(0, cursor);
        const markerIndex = before.lastIndexOf('{{');
        if (markerIndex < 0)
            return { start: cursor, end: input.selectionEnd || cursor };
        const closingIndex = input.value.indexOf('}}', cursor);
        return { start: markerIndex, end: closingIndex >= 0 ? closingIndex + 2 : input.selectionEnd || cursor };
    }
    function insertSuggestion(index) {
        const input = el('template-editor-textarea');
        if (!input)
            return;
        const item = visibleSuggestions[index] || visibleSuggestions[0] || SUGGESTIONS[0];
        const range = replaceRangeForSuggestion(input);
        input.value = input.value.slice(0, range.start) + item.insert + input.value.slice(range.end);
        const tokenIndex = item.cursorToken ? item.insert.indexOf(item.cursorToken) : -1;
        const nextPos = range.start + (tokenIndex >= 0 ? tokenIndex : item.insert.length);
        input.focus();
        input.setSelectionRange(nextPos, nextPos + (tokenIndex >= 0 && item.cursorToken ? item.cursorToken.length : 0));
        showCompletions(false);
        syncFromTextarea();
    }
    async function openTemplateEditor(id) {
        const modal = ensureModal();
        activeId = id || activeId || PRESETS[0].id;
        setPresetOptions();
        showCompletions(false);
        modal.classList.add('show');
        await loadTemplate(activeId);
    }
    function closeTemplateEditor() {
        const modal = el('template-editor-modal');
        if (modal)
            modal.classList.remove('show');
        showCompletions(false);
    }
    async function saveTemplateEditor() {
        const preset = presetById(activeId);
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
    async function switchTemplateEditorPreset(id) {
        activeId = id;
        setPresetOptions();
        showCompletions(false);
        await loadTemplate(id);
    }
    async function openTemplateEditorReset() {
        currentText = defaultText || await loadDefaultText(activeId);
        loadedText = currentText;
        const input = el('template-editor-textarea');
        if (input)
            input.value = currentText;
        syncFromTextarea();
        updateStatus('Reset to default');
    }
    function onTemplateEditorInput() {
        syncFromTextarea();
    }
    function onTemplateEditorKeydown(event) {
        if (event.key === 'Escape') {
            event.preventDefault();
            if (suggestionVisible)
                showCompletions(false);
            else
                closeTemplateEditor();
            return;
        }
        if (event.key === 'Tab') {
            const input = el('template-editor-textarea');
            if (!input)
                return;
            event.preventDefault();
            const start = input.selectionStart || 0;
            const end = input.selectionEnd || start;
            input.value = input.value.slice(0, start) + '  ' + input.value.slice(end);
            input.setSelectionRange(start + 2, start + 2);
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
    function pickTemplateEditorSuggestion(index) {
        suggestionIndex = index;
        insertSuggestion(index);
    }
    const api = {
        open: openTemplateEditor,
        close: closeTemplateEditor,
        save: saveTemplateEditor,
        reset: openTemplateEditorReset
    };
    function receiveTemplateFile(payload) {
        const requestId = payload?.requestId || '';
        const resolve = pendingTemplateReads[requestId];
        if (resolve)
            resolve(payload?.content || '');
    }
    window.GrafcetTemplateEditor = api;
    window.receiveTemplateFile = receiveTemplateFile;
    window.openTemplateEditor = openTemplateEditor;
    window.closeTemplateEditor = closeTemplateEditor;
    window.saveTemplateEditor = saveTemplateEditor;
    window.switchTemplateEditorPreset = switchTemplateEditorPreset;
    window.openTemplateEditorReset = openTemplateEditorReset;
    window.onTemplateEditorInput = onTemplateEditorInput;
    window.onTemplateEditorKeydown = onTemplateEditorKeydown;
    window.syncTemplateEditorScroll = syncTemplateEditorScroll;
    window.pickTemplateEditorSuggestion = pickTemplateEditorSuggestion;
})(GrafcetStudioTemplateEditor || (GrafcetStudioTemplateEditor = {}));

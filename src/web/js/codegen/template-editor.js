"use strict";
var GrafcetStudioTemplateEditor;
(function (GrafcetStudioTemplateEditor) {
    const PRESETS = [
        { id: 'main-output', label: 'main-output.hbs', path: 'main-output.hbs' },
        { id: 'step-body', label: 'step-body.hbs', path: 'step-body.hbs' },
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
        { id: 'devices-device_robot', label: 'devices/device_robot.hbs', path: 'devices/device_robot.hbs' }
    ];
    const SUGGESTIONS = [
        { label: '{{#each }}', insert: '{{#each items}}\n  \n{{/each}}' },
        { label: '{{#if }}', insert: '{{#if condition}}\n  \n{{/if}}' },
        { label: '{{/each}}', insert: '{{/each}}' },
        { label: '{{/if}}', insert: '{{/if}}' },
        { label: '{{else}}', insert: '{{else}}' },
        { label: '{{> partial}}', insert: '{{> partial}}' },
        { label: '{{! comment }}', insert: '{{! comment }}' }
    ];
    let activeId = PRESETS[0].id;
    let currentText = '';
    let loadedText = '';
    let suggestionVisible = false;
    let suggestionIndex = 0;
    function host() {
        return window;
    }
    function el(id) {
        return document.getElementById(id);
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
          <span class="modal-title-accent">? TEMPLATE EDITOR</span>
          <span id="template-editor-title" class="modal-subtitle"></span>
          <div class="modal-actions">
            <button class="btn" onclick="openTemplateEditorReset()">Reset to default</button>
            <button class="btn" onclick="closeTemplateEditor()">Cancel</button>
            <button class="btn a" onclick="saveTemplateEditor()">Save</button>
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
    function presetById(id) {
        return PRESETS.find(p => p.id === id) || PRESETS[0];
    }
    function escapeHtml(text) {
        return String(text || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }
    function renderHighlight(text) {
        const escaped = escapeHtml(text);
        return escaped
            .replace(/\{\{!([\s\S]*?)\}\}/g, '<span class="hbs-comment">{{!$1}}</span>')
            .replace(/\{\{#(each|if)\s*([^}]*)\}\}/g, '<span class="hbs-delim">{{</span><span class="hbs-block">#$1</span><span class="hbs-var"> $2</span><span class="hbs-delim">}}</span>')
            .replace(/\{\{\/(each|if)\}\}/g, '<span class="hbs-delim">{{</span><span class="hbs-block-end">/$1</span><span class="hbs-delim">}}</span>')
            .replace(/\{\{else\}\}/g, '<span class="hbs-delim">{{</span><span class="hbs-else">else</span><span class="hbs-delim">}}</span>')
            .replace(/\{\{>\s*([^}]+?)\s*\}\}/g, '<span class="hbs-delim">{{</span><span class="hbs-partial">&gt; $1</span><span class="hbs-delim">}}</span>')
            .replace(/\{\{\s*([a-zA-Z0-9_@.]+)\s*\}\}/g, '<span class="hbs-delim">{{</span><span class="hbs-var">$1</span><span class="hbs-delim">}}</span>');
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
    function updateStatus(message) {
        const status = el('template-editor-status');
        if (!status)
            return;
        status.textContent = message || (currentText === loadedText ? 'No changes' : 'Edited');
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
    function updateCompletions(force = false) {
        const input = el('template-editor-textarea');
        const box = el('template-editor-completions');
        if (!input || !box)
            return;
        const cursor = input.selectionStart || 0;
        const before = input.value.slice(0, cursor);
        const markerIndex = before.lastIndexOf('{{');
        const typed = markerIndex >= 0 ? before.slice(markerIndex + 2).trim().toLowerCase() : '';
        const shouldShow = force || before.endsWith('{{') || before.endsWith('{{#') || before.endsWith('{{/') || before.endsWith('{{>') || (markerIndex >= 0 && typed.length <= 12);
        if (!shouldShow) {
            showCompletions(false);
            return;
        }
        const items = SUGGESTIONS.filter(item => !typed || item.label.toLowerCase().includes(typed) || item.insert.toLowerCase().includes(typed));
        box.innerHTML = items.map((item, idx) => `<button class="template-editor-completion ${idx === suggestionIndex ? 'active' : ''}" data-index="${idx}" onclick="pickTemplateEditorSuggestion(${idx})"><span>${escapeHtml(item.label)}</span></button>`).join('');
        showCompletions(true);
    }
    function insertSuggestion(index) {
        const input = el('template-editor-textarea');
        if (!input)
            return;
        const cursor = input.selectionStart || 0;
        const before = input.value.slice(0, cursor);
        const after = input.value.slice(input.selectionEnd || cursor);
        const start = Math.max(0, before.lastIndexOf('{{'));
        const item = SUGGESTIONS[index] || SUGGESTIONS[0];
        input.value = before.slice(0, start) + item.insert + after;
        const nextPos = start + item.insert.length;
        input.focus();
        input.setSelectionRange(nextPos, nextPos);
        syncFromTextarea();
    }
    function setPresetOptions() {
        const select = el('template-editor-select');
        if (!select)
            return;
        select.innerHTML = PRESETS.map(p => `<option value="${escapeHtml(p.id)}">${escapeHtml(p.label)}</option>`).join('');
        select.value = activeId;
        const title = el('template-editor-title');
        if (title)
            title.textContent = presetById(activeId).label;
    }
    async function loadPresetText(id) {
        const preset = presetById(id);
        const url = `https://templates.grafcet.local/${preset.path}`;
        try {
            const response = await fetch(url, { cache: 'no-store' });
            if (!response.ok)
                throw new Error('HTTP ' + response.status);
            return await response.text();
        }
        catch {
            return '';
        }
    }
    function saveDownloadedFile() {
        const preset = presetById(activeId);
        const filename = preset.path.replace(/[\\/]/g, '__');
        const blob = new Blob([currentText || ''], { type: 'text/plain;charset=utf-8' });
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = filename;
        link.click();
        setTimeout(() => URL.revokeObjectURL(link.href), 1000);
        closeTemplateEditor();
    }
    async function openTemplateEditor(id) {
        const modal = ensureModal();
        modal.classList.add('show');
        activeId = id || activeId || PRESETS[0].id;
        setPresetOptions();
        showCompletions(false);
        const text = await loadPresetText(activeId);
        currentText = text;
        loadedText = text;
        const input = el('template-editor-textarea');
        if (input) {
            input.value = text;
            input.focus();
            input.setSelectionRange(0, 0);
        }
        syncFromTextarea();
    }
    function closeTemplateEditor() {
        const modal = el('template-editor-modal');
        if (modal)
            modal.classList.remove('show');
        showCompletions(false);
    }
    async function switchTemplateEditorPreset(id) {
        activeId = id;
        setPresetOptions();
        const text = await loadPresetText(id);
        currentText = text;
        loadedText = text;
        const input = el('template-editor-textarea');
        if (input)
            input.value = text;
        syncFromTextarea();
    }
    function openTemplateEditorReset() {
        switchTemplateEditorPreset(activeId);
    }
    function onTemplateEditorInput() {
        syncFromTextarea();
    }
    function onTemplateEditorKeydown(event) {
        if (event.key === 'Escape') {
            event.preventDefault();
            closeTemplateEditor();
            return;
        }
        if (event.key === ' ' && event.ctrlKey) {
            event.preventDefault();
            updateCompletions(true);
            return;
        }
        if (event.key === 'ArrowDown' && suggestionVisible) {
            event.preventDefault();
            suggestionIndex = Math.min(SUGGESTIONS.length - 1, suggestionIndex + 1);
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
    window.openTemplateEditor = openTemplateEditor;
    window.closeTemplateEditor = closeTemplateEditor;
    window.saveTemplateEditor = saveDownloadedFile;
    window.switchTemplateEditorPreset = switchTemplateEditorPreset;
    window.openTemplateEditorReset = openTemplateEditorReset;
    window.onTemplateEditorInput = onTemplateEditorInput;
    window.onTemplateEditorKeydown = onTemplateEditorKeydown;
    window.syncTemplateEditorScroll = syncTemplateEditorScroll;
    window.pickTemplateEditorSuggestion = pickTemplateEditorSuggestion;
})(GrafcetStudioTemplateEditor || (GrafcetStudioTemplateEditor = {}));

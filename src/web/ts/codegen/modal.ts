"use strict";

type MdCodegenFile = GrafcetStudioProject.CodegenFile;

declare function esc2(s: unknown): string;
declare function flushState(): void;
declare function cgBuildUnitList(): void;
declare function cgUpdatePreview(): void;
declare function cgGenerateSelectedUnit(): boolean;
declare function cgGenerateAllUnits(): boolean;
declare function cgResolveHostPlatform(target: string): string;
declare function openTemplateEditor(): void;

let cgSavedPaths: { deviceLibraryPath: string; templatePath: string; outputPath: string } = { deviceLibraryPath: '', templatePath: '', outputPath: '' };
let cgSavedSiemensTiaConfig: { projectPath: string; deviceName: string; plcName: string; targetFolderPath: string; overwriteMode: string } = { projectPath: '', deviceName: '', plcName: '', targetFolderPath: 'Program blocks', overwriteMode: 'FailIfExists' };
let cgPendingSiemensTiaPush = false;

let cgStep = 1;

function showGenerateCodeModal(): void {
  if (activeDiagramId && typeof flushState === 'function') flushState();
  const aiWindow = window as unknown as { aiPermissionRefreshGrants?: () => void; chrome?: { webview?: { postMessage?: (message: unknown) => void } } };
  if (aiWindow.aiPermissionRefreshGrants) aiWindow.aiPermissionRefreshGrants();
  if (aiWindow.chrome && aiWindow.chrome.webview && aiWindow.chrome.webview.postMessage) aiWindow.chrome.webview.postMessage({ type: 'AI_SETTINGS', payload: { action: 'get' } });
  let el = document.getElementById('modal-codegen');
  if (el) el.remove();
  el = document.createElement('div');
  el.id = 'modal-codegen';
  el.className = 'modal-bg show';

  el.innerHTML = `
    <div class="modal cg-modal" role="dialog" aria-label="Generate code">
      <div class="cg-head">
        <span class="cg-title">GENERATE CODE</span>
        <div class="cg-stepper" id="cg-stepper">
          <span class="cg-stepper-item" data-step="1" onclick="cgGoStep(1)"><span class="cg-stepper-num">1</span>Target</span>
          <span class="cg-stepper-item" data-step="2" onclick="cgGoStep(2)"><span class="cg-stepper-num">2</span>Scope</span>
          <span class="cg-stepper-item" data-step="3"><span class="cg-stepper-num">3</span>Result</span>
        </div>
        <button class="btn cg-close" onclick="closeModal('modal-codegen')" title="Close">X</button>
      </div>

      <!-- Step 1: Target -->
      <div class="cg-step" data-step="1">
        <p class="cg-step-intro">Choose what to generate code for.</p>
        <div class="cg-field">
          <div class="cg-field-label">Target PLC</div>
          <select id="cg-target" class="cg-select cg-select-wide" onchange="cgOnTargetChanged()">
            <option value="Keyence">Keyence Mnemonic List</option>
            <option value="siemens-lad">Siemens LAD XML</option>
            <option value="siemens-db">Siemens DB/UDT XML</option>
            <option value="abb-rapid">ABB Robot (RAPID)</option>
          </select>
        </div>

        <div id="cg-base-mr-wrap" class="is-hidden">
          <input id="cg-base-mr" type="number" min="0" max="9999" value="100" step="2" oninput="cgUpdatePreview()">
        </div>

        <div id="cg-siemens-lad-panel" class="cg-panel is-hidden">
          <div class="cg-mode-row">
            <span class="cg-field-label">Siemens TIA mode</span>
            <label><input type="radio" name="cg-siemens-lad-mode" value="preview" checked onchange="cgOnSiemensLadConfigChanged()"> Preview XML</label>
            <label><input type="radio" name="cg-siemens-lad-mode" value="push" onchange="cgOnSiemensLadConfigChanged()"> Push to TIA</label>
            <span id="cg-siemens-lad-status" class="cg-mode-status">Preview XML mode</span>
          </div>
          <div id="cg-siemens-tia-config" class="cg-tia-grid">
            <label>TIA Project Path</label>
            <input id="cg-tia-project-path" class="cg-input" oninput="cgOnSiemensLadConfigChanged()" placeholder="Optional if project is already open">
            <label>Overwrite</label>
            <select id="cg-tia-overwrite-mode" class="cg-select" onchange="cgOnSiemensLadConfigChanged()"><option value="FailIfExists">Fail if exists</option><option value="Overwrite">Overwrite</option><option value="Rename">Rename</option></select>
            <label>Device / Station</label>
            <input id="cg-tia-device-name" class="cg-input" oninput="cgOnSiemensLadConfigChanged()" placeholder="TIA device name">
            <label>PLC Name</label>
            <input id="cg-tia-plc-name" class="cg-input" oninput="cgOnSiemensLadConfigChanged()" placeholder="PLC/device item name">
            <label>Block Folder</label>
            <input id="cg-tia-target-folder" class="cg-input" oninput="cgOnSiemensLadConfigChanged()" value="Program blocks" placeholder="Program blocks/Generated">
            <div class="cg-tia-note">Generating XML first, then importing it through the host bridge.</div>
          </div>
        </div>

        <div id="cg-robot-safety-panel" class="cg-safety">
          <span class="cg-safety-icon">⚠️</span>
          <div>
            <strong>SAFETY WARNING:</strong>
            <span class="cg-safety-text">Review all motion instructions &amp; positions carefully before exporting. Incorrect positions or speed settings can cause physical equipment damage or personal harm. Always teach/verify positions in low speed manual mode (T1).</span>
          </div>
        </div>

        <div class="cg-advanced">
          <div class="cg-advanced-toggle" onclick="cgToggleAssetPaths()">
            <span id="asset-paths-chevron">&gt;</span>Advanced: file paths<span id="asset-paths-summary" class="cg-count"></span>
          </div>
          <div id="asset-paths-body" class="cg-advanced-body">
            <div class="cg-paths-grid">
              <label>Device Library</label><input id="cg-device-library-path" class="cg-input" value="config/Devices.json" oninput="cgUpdateAssetPathStatus(); cgUpdatePreview()"><button class="btn" onclick="cgBrowseCodegenPath('deviceLibrary')">Browse...</button>
              <label>Template Root</label><input id="cg-template-root-path" class="cg-input" value="templates" oninput="cgUpdateAssetPathStatus(); cgUpdatePreview()" onchange="cgSetTemplateRoot(this.value)"><button class="btn" onclick="cgBrowseCodegenPath('templateRoot')">Browse...</button>
              <label>Output Folder</label><input id="cg-output-root-path" class="cg-input" value="" oninput="cgUpdateAssetPathStatus(); cgUpdatePreview()"><button class="btn" onclick="cgBrowseCodegenPath('outputRoot')">Browse...</button>
            </div>
            <div id="asset-paths-status" class="cg-paths-status"></div>
          </div>
        </div>
      </div>

      <!-- Step 2: Scope -->
      <div class="cg-step" data-step="2">
        <p class="cg-step-intro">Choose the unit to generate, and which of its diagrams to include.</p>
        <div id="cg-unit-wrap" class="cg-field">
          <div class="cg-field-label">Unit</div>
          <div id="cg-unit-list" class="cg-chip-row"></div>
        </div>
        <div id="cg-unit-diag-section" class="cg-unit-diag-section">
          <div class="cg-field-label">Diagrams
            <button class="cg-link-btn" onclick="cgSelectAll(true)">all</button>
            <button class="cg-link-btn muted" onclick="cgSelectAll(false)">none</button>
          </div>
          <div id="cg-diag-list" class="cg-chip-row"></div>
        </div>
      </div>

      <!-- Step 3: Result -->
      <div class="cg-step cg-step-result" data-step="3">
        <div id="cg-files"></div>
      </div>

      <div class="cg-foot" data-step="1">
        <span class="cg-foot-spacer"></span>
        <button class="btn a" onclick="cgGoStep(2)">Next: Scope</button>
      </div>
      <div class="cg-foot" data-step="2">
        <button class="btn" onclick="cgGoStep(1)">Back</button>
        <span class="cg-foot-spacer"></span>
        <button class="btn" onclick="cgGenerateAllUnits(); cgGoStep(3)">Generate all units</button>
        <button class="btn a" onclick="cgGenerateSelectedUnit(); cgGoStep(3)">Generate selected unit</button>
      </div>
      <div class="cg-foot" data-step="3">
        <button class="btn" onclick="cgGoStep(2)">Back</button>
        <span id="cg-stat" class="cg-stat"></span>
        <button class="btn" onclick="openTemplateEditor()">Template Editor</button>
        <button class="btn" onclick="cgCopyAllFiles()">Copy all</button>
        <button class="btn a" onclick="cgDownloadAllFiles()">Download ZIP</button>
      </div>
    </div>`;

  document.body.appendChild(el);
  cgApplySavedPaths();
  cgApplySavedSiemensTiaConfig();
  cgBuildUnitList();
  cgUpdateAssetPathStatus();
  cgUpdateSiemensLadPanel();
  cgGoStep(1);
  cgUpdatePreview();
}

function cgGoStep(step: number): void {
  const root = document.getElementById('modal-codegen');
  if (!root) return;
  cgStep = Math.max(1, Math.min(3, step));
  root.querySelectorAll<HTMLElement>('.cg-step').forEach(el => el.classList.toggle('active', Number(el.dataset.step) === cgStep));
  root.querySelectorAll<HTMLElement>('.cg-foot').forEach(el => el.classList.toggle('active', Number(el.dataset.step) === cgStep));
  root.querySelectorAll<HTMLElement>('.cg-stepper-item').forEach(el => {
    const n = Number(el.dataset.step);
    el.classList.toggle('active', n === cgStep);
    el.classList.toggle('done', n < cgStep);
  });
}

function cgRenderGeneratedFiles(files: MdCodegenFile[]): void {
  const root = document.getElementById('cg-files');
  if (!root) return;
  if (!window.GrafcetStudio) window.GrafcetStudio = {};
  (window.GrafcetStudio as unknown as { codegenLastFiles?: MdCodegenFile[] }).codegenLastFiles = Array.isArray(files) ? files : [];
  (window.GrafcetStudio as unknown as { codegenSelectedFileIndex?: number }).codegenSelectedFileIndex = 0;

  if (!files || !files.length) {
    root.innerHTML = '<div class="cg-empty">No files generated yet.</div>';
    return;
  }

  const options = files.map((file, index) => `<option value="${index}">${esc2(file && file.path ? String(file.path) : 'file.st')}</option>`).join('');
  root.innerHTML = `<div class="cg-file-bar"><label>FILE</label><select id="cg-file-select" class="cg-select cg-file-select" onchange="cgShowGeneratedFile(this.value)">${options}</select><span class="cg-foot-spacer"></span><button class="btn" onclick="cgCopySelectedFile()">Copy file</button><button class="btn" onclick="cgDownloadSelectedFile()">Download file</button></div><div id="cg-file-meta" class="cg-file-meta"></div><pre id="cg-file-preview" class="cg-file-preview"></pre>`;
  cgShowGeneratedFile(0);
}

function cgShowGeneratedFile(index: number | string): void {
  const files = (window.GrafcetStudio && (window.GrafcetStudio as unknown as { codegenLastFiles?: MdCodegenFile[] }).codegenLastFiles) || [];
  const safeIndex = Math.max(0, Math.min(files.length - 1, Number(index) || 0));
  const file = files[safeIndex];
  const preview = document.getElementById('cg-file-preview');
  const meta = document.getElementById('cg-file-meta');
  const select = document.getElementById('cg-file-select') as HTMLSelectElement | null;
  if (!file || !preview) return;
  if (window.GrafcetStudio) (window.GrafcetStudio as unknown as { codegenSelectedFileIndex?: number }).codegenSelectedFileIndex = safeIndex;
  if (select) select.value = String(safeIndex);
  preview.textContent = file.content || '';
  if (meta) meta.textContent = (file.path || 'file.st') + ' - ' + String((file.content || '').length) + ' chars';
}

function cgCopySelectedFile(): void {
  const files = (window.GrafcetStudio && (window.GrafcetStudio as unknown as { codegenLastFiles?: MdCodegenFile[] }).codegenLastFiles) || [];
  const index = (window.GrafcetStudio && (window.GrafcetStudio as unknown as { codegenSelectedFileIndex?: number }).codegenSelectedFileIndex) || 0;
  const file = files[index];
  if (!file) return;
  cgCopyText(file.content || '');
}

function cgDownloadSelectedFile(): void {
  const files = (window.GrafcetStudio && (window.GrafcetStudio as unknown as { codegenLastFiles?: MdCodegenFile[] }).codegenLastFiles) || [];
  const index = (window.GrafcetStudio && (window.GrafcetStudio as unknown as { codegenSelectedFileIndex?: number }).codegenSelectedFileIndex) || 0;
  const file = files[index];
  if (!file) return;
  cgDownloadTextFile(cgSafeFilename(file.path || 'export.mnm'), file.content || '');
}

function cgCopyAllFiles(): void {
  const files = (window.GrafcetStudio && (window.GrafcetStudio as unknown as { codegenLastFiles?: MdCodegenFile[] }).codegenLastFiles) || [];
  const text = files.map(file => `// FILE: ${file.path || 'file.st'}\n${file.content || ''}`).join('\n\n');
  if (text) cgCopyText(text);
}

function cgDownloadAllFiles(): void {
  const files = (window.GrafcetStudio && (window.GrafcetStudio as unknown as { codegenLastFiles?: MdCodegenFile[] }).codegenLastFiles) || [];
  const platform = cgResolveHostPlatform((document.getElementById('cg-target') as HTMLSelectElement | null)?.value || 'Keyence');
  if (!files.length) return;
  if (!cgExportViaHost(files, platform)) toast('Host export is unavailable');
}

function cgDownloadTextFile(filename: string, content: string): void {
  const blob = new Blob([content || ''], { type: 'text/plain;charset=utf-8' });
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = filename || 'export.mnm';
  a.click();
  setTimeout(() => URL.revokeObjectURL(a.href), 1000);
}

function cgSafeFilename(path: string): string {
  return String(path || 'export.mnm').replace(/[\\/:*?"<>|]+/g, '_').trim() || 'export.mnm';
}

function cgCopyText(text: string): void {
  navigator.clipboard.writeText(text || '').then(() => toast('Copied to clipboard'));
}

function cgOnTargetChanged(): void {
  const target = (document.getElementById('cg-target') as HTMLSelectElement | null)?.value || 'Keyence';
  const isRobot = target === 'abb-rapid' || target === 'ABB Robot';
  const safetyPanel = document.getElementById('cg-robot-safety-panel');
  if (safetyPanel) safetyPanel.style.display = isRobot ? 'flex' : 'none';
  cgUpdatePreview();
  cgUpdateSiemensLadPanel();
}

function cgGetSiemensLadMode(): string {
  return document.querySelector<HTMLInputElement>('input[name="cg-siemens-lad-mode"]:checked')?.value || 'preview';
}

function cgGetSiemensTiaConfig(): { projectPath: string; deviceName: string; plcName: string; targetFolderPath: string; overwriteMode: string } {
  return {
    projectPath: ((document.getElementById('cg-tia-project-path') as HTMLInputElement | null)?.value || '').trim(),
    deviceName: ((document.getElementById('cg-tia-device-name') as HTMLInputElement | null)?.value || '').trim(),
    plcName: ((document.getElementById('cg-tia-plc-name') as HTMLInputElement | null)?.value || '').trim(),
    targetFolderPath: ((document.getElementById('cg-tia-target-folder') as HTMLInputElement | null)?.value || 'Program blocks').trim(),
    overwriteMode: (document.getElementById('cg-tia-overwrite-mode') as HTMLSelectElement | null)?.value || 'FailIfExists'
  };
}

function cgApplySavedSiemensTiaConfig(): void {
  const config = cgSavedSiemensTiaConfig || {} as typeof cgSavedSiemensTiaConfig;
  const values: Record<string, string> = {
    'cg-tia-project-path': config.projectPath || '',
    'cg-tia-device-name': config.deviceName || '',
    'cg-tia-plc-name': config.plcName || '',
    'cg-tia-target-folder': config.targetFolderPath || 'Program blocks',
    'cg-tia-overwrite-mode': config.overwriteMode || 'FailIfExists'
  };
  Object.keys(values).forEach(id => {
    const input = document.getElementById(id) as HTMLInputElement | HTMLSelectElement | null;
    if (input) input.value = values[id];
  });
  cgUpdateSiemensLadPanel();
}

function cgOnSiemensLadConfigChanged(): void {
  cgSavedSiemensTiaConfig = cgGetSiemensTiaConfig();
  cgUpdateSiemensLadPanel();
}

function cgUpdateSiemensLadPanel(): void {
  const target = (document.getElementById('cg-target') as HTMLSelectElement | null)?.value || '';
  const panel = document.getElementById('cg-siemens-lad-panel');
  const config = document.getElementById('cg-siemens-tia-config');
  const status = document.getElementById('cg-siemens-lad-status');
  const isSiemensTarget = target === 'siemens-lad' || target === 'siemens-db';
  if (panel) panel.classList.toggle('is-hidden', !isSiemensTarget);
  if (!isSiemensTarget) return;
  const isPush = cgGetSiemensLadMode() === 'push';
  if (config) (config as HTMLElement).style.display = isPush ? 'grid' : 'none';
  if (status && !cgPendingSiemensTiaPush) {
    status.textContent = isPush ? (target === 'siemens-db' ? 'Push mode: DB XML only; UDT files remain manual import' : 'Push mode: generate XML then import to TIA') : 'Preview XML mode';
    (status as HTMLElement).style.color = isPush ? 'var(--amber)' : 'var(--text3)';
  }
}

function cgSetSiemensLadStatus(message: string, ok: boolean | null): void {
  const status = document.getElementById('cg-siemens-lad-status');
  if (!status) return;
  status.textContent = message || '';
  (status as HTMLElement).style.color = ok === true ? 'var(--cyan)' : ok === false ? 'var(--red)' : 'var(--amber)';
}

function cgShouldPushSiemensTia(): boolean {
  const target = (document.getElementById('cg-target') as HTMLSelectElement | null)?.value || '';
  return (target === 'siemens-lad' || target === 'siemens-db') && cgGetSiemensLadMode() === 'push';
}

function receiveSiemensTiaPushResult(payload: { kind?: string; config?: typeof cgSavedSiemensTiaConfig; result?: { ok?: boolean; message?: string }; ok?: boolean; message?: string } | null): void {
  if (!payload) return;
  if (payload.kind === 'config') {
    cgSavedSiemensTiaConfig = payload.config || cgSavedSiemensTiaConfig;
    cgApplySavedSiemensTiaConfig();
    return;
  }
  const result = payload.result || payload;
  const ok = !!result.ok;
  const message = result.message || (ok ? 'TIA push completed' : 'TIA push failed');
  cgPendingSiemensTiaPush = false;
  cgSetSiemensLadStatus((ok ? 'Success: ' : 'Failure: ') + message, ok);
  const stat = document.getElementById('cg-stat');
  if (stat) stat.textContent = ok ? 'Siemens TIA push succeeded' : 'Siemens TIA push failed';
  if (typeof toast === 'function') toast((ok ? 'Success: ' : 'Warning: ') + message);
}

// Status badge cho file load
function cgExportViaHost(code: MdCodegenFile[], platform: string): boolean {
  const chromeHost = (window as Window & { chrome?: { webview?: { postMessage?: (message: unknown) => void } } }).chrome;
  if (!(chromeHost && chromeHost.webview && typeof chromeHost.webview.postMessage === 'function')) {
    return false;
  }
  chromeHost.webview.postMessage({
    type: 'EXPORT_CODE',
    payload: { files: code, platform }
  });
  toast('Export dialog opened');
  return true;
}

// JSON Files panel toggle

// --- Codegen asset paths ----------------------------------------------------
// Template Root is app-wide: the modal input mirrors it, but it is readable (and persisted) without the modal.
function cgGetTemplateRoot(): string {
  const input = document.getElementById('cg-template-root-path') as HTMLInputElement | null;
  if (input) return input.value.trim();
  return (cgSavedPaths.templatePath || '').trim() || 'templates';
}

function cgSetTemplateRoot(path: string): void {
  const value = (path || '').trim();
  const input = document.getElementById('cg-template-root-path') as HTMLInputElement | null;
  if (input) input.value = value;
  cgSavedPaths.templatePath = value;
  cgUpdateAssetPathStatus();
  cgUpdatePreview();
  const host = (window as Window & { chrome?: { webview?: { postMessage?: (message: unknown) => void } } }).chrome;
  if (host?.webview?.postMessage) host.webview.postMessage({ type: 'SET_TEMPLATE_ROOT', payload: { path: value } });
  (window as Window & { onTemplateRootChanged?: () => void }).onTemplateRootChanged?.();
}

function cgGetCodegenAssets(): { deviceLibraryPath: string; templateRootPath: string; outputPath: string } {
  const devPath = (document.getElementById('cg-device-library-path') as HTMLInputElement | null)?.value || 'config/Devices.json';
  const tplRoot = cgGetTemplateRoot() || 'templates';
  const outRoot = (document.getElementById('cg-output-root-path') as HTMLInputElement | null)?.value || '';
  return {
    deviceLibraryPath: devPath.trim(),
    templateRootPath: tplRoot.trim(),
    outputPath: outRoot.trim()
  };
}

function cgUpdateAssetPathStatus(): void {
  const assets = cgGetCodegenAssets();
  const status = document.getElementById('asset-paths-status');
  const summary = document.getElementById('asset-paths-summary');
  const ok = !!assets.deviceLibraryPath && !!assets.templateRootPath && !!assets.outputPath;
  const text = ok
    ? 'C# host will resolve: ' + assets.deviceLibraryPath + ' | ' + assets.templateRootPath + ' | ' + assets.outputPath
    : 'Missing device library, template root, or output path';
  if (status) {
    status.textContent = text;
    (status as HTMLElement).style.color = ok ? 'var(--cyan)' : 'var(--amber)';
  }
  if (summary) {
    summary.textContent = ok ? '✓ paths ready' : 'missing path';
    (summary as HTMLElement).style.color = ok ? 'var(--cyan)' : 'var(--amber)';
  }
}

function cgBrowseCodegenPath(target: string): boolean {
  const chromeHost = (window as Window & { chrome?: { webview?: { postMessage?: (message: unknown) => void } } }).chrome;
  if (!(chromeHost && chromeHost.webview && typeof chromeHost.webview.postMessage === 'function')) {
    toast('Browse is available only inside the WPF host');
    return false;
  }

  chromeHost.webview.postMessage({
    type: 'BROWSE_CODEGEN_PATH',
    payload: { target }
  });
  return true;
}

function receiveCodegenPath(payload: string | { target?: string; path?: string } | null): void {
  const data = typeof payload === 'string' ? { target: 'templateRoot', path: payload } : (payload || {});
  if ((!data.target || data.target === 'templateRoot') && data.path) {
    cgSetTemplateRoot(data.path);
    return;
  }
  const inputId = data.target === 'deviceLibrary'
    ? 'cg-device-library-path'
    : data.target === 'outputRoot'
      ? 'cg-output-root-path'
      : 'cg-template-root-path';
  const input = document.getElementById(inputId) as HTMLInputElement | null;
  if (!input || !data.path) return;
  input.value = data.path;
  cgUpdateAssetPathStatus();
  cgUpdatePreview();
}

function receiveSavedPaths(payload: { deviceLibraryPath?: string; templatePath?: string; templateRootPath?: string; outputPath?: string } | null): void {
  cgSavedPaths = (payload || {}) as typeof cgSavedPaths;
  cgApplySavedPaths();
}

function cgApplySavedPaths(): void {
  const data = cgSavedPaths || {} as typeof cgSavedPaths;
  const deviceInput = document.getElementById('cg-device-library-path') as HTMLInputElement | null;
  const templateInput = document.getElementById('cg-template-root-path') as HTMLInputElement | null;
  const outputInput = document.getElementById('cg-output-root-path') as HTMLInputElement | null;
  if (deviceInput && data.deviceLibraryPath) deviceInput.value = data.deviceLibraryPath;
  if (templateInput && ((data as unknown as { templateRootPath?: string }).templateRootPath || data.templatePath)) templateInput.value = (data as unknown as { templateRootPath?: string }).templateRootPath || data.templatePath;
  if (outputInput && data.outputPath) outputInput.value = data.outputPath;
  cgUpdateAssetPathStatus();
  cgUpdatePreview();
}

function cgToggleAssetPaths(): void {
  const body = document.getElementById('asset-paths-body');
  const chevron = document.getElementById('asset-paths-chevron');
  if (!body) return;
  const open = getComputedStyle(body as HTMLElement).display !== 'none';
  (body as HTMLElement).style.display = open ? 'none' : 'block';
  if (chevron) chevron.textContent = open ? '>' : 'v';
  if (!open) cgUpdateAssetPathStatus();
}

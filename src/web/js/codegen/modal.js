"use strict";

let cgSavedPaths = { deviceLibraryPath: '', templatePath: '', outputPath: '' };

//  GRAFCET Code Generator - grafcet-codegen.js
//  Target: Keyence KV Mnemonic IL (.mnm)
//  Planned: IEC 61131-3 ST (.st) demo/stub only
//  Reads from global: project, loadDiagramData(), flushState(),
//  resolveStepsThrough(), toast(), esc2()
//  Show Generate Code Modal 
function showGenerateCodeModal() {
  if (activeDiagramId && typeof flushState === 'function') flushState();
  let el = document.getElementById('modal-codegen');
  if (el) el.remove();
  el = document.createElement('div');
  el.id = 'modal-codegen';
  el.className = 'modal-bg show';

  el.innerHTML = `
    <div class="modal" style="min-width:720px;max-width:96vw;max-height:92vh;
      display:flex;flex-direction:column;padding:0;overflow:hidden;">

      <!-- Header -->
      <div style="padding:12px 20px;background:var(--s3);border-bottom:1px solid var(--border);
        display:flex;align-items:center;gap:10px;flex-shrink:0;">
        <span style="font-size:12px;letter-spacing:2px;font-family:'Orbitron',monospace;">GENERATE CODE</span>
        <span style="flex:1;"></span>
        <button class="btn" onclick="closeModal('modal-codegen')" style="padding:2px 10px;">X</button>
      </div>

      <!-- Options row -->
      <div style="padding:10px 20px;border-bottom:1px solid var(--border);display:flex;
        gap:20px;align-items:center;flex-wrap:wrap;flex-shrink:0;background:var(--s2);">

        <!-- Target PLC -->
        <div>
          <div style="font-size:9px;color:var(--text3);letter-spacing:1px;margin-bottom:5px;">TARGET PLC</div>
          <select id="cg-target" onchange="cgUpdatePreview()"
            style="background:var(--bg);border:1px solid var(--border);color:var(--cyan);
            font-family:'JetBrains Mono',monospace;font-size:11px;padding:4px 8px;
            border-radius:3px;outline:none;">
            <option value="unit-config">Unit Config JSON</option>
            <option value="runtime-plan">Runtime Plan [debug]</option>
            <option value="kv-5500">Keyence KV-5500</option>
            <option value="kv-8000">Keyence KV-8000</option>
            <option value="melsec">Mitsubishi MELSEC</option>
            <option value="omron">OMRON</option>
            <option value="siemens">Siemens</option>
            <option value="twincat-st">TwinCAT ST</option>
            <option value="csharp-kv-5500">C# Keyence KV demo</option>
            <option value="csharp-twincat-st">C# TwinCAT ST demo</option>
          </select>
        </div>

        <!-- Base MR address hidden for unit-config -->
        <div id="cg-base-mr-wrap">
          <div style="font-size:9px;color:var(--text3);letter-spacing:1px;margin-bottom:5px;">
            BASE ADDRESS <span style="color:var(--cyan);">@MR</span>
          </div>
          <input id="cg-base-mr" type="number" min="0" max="9999" value="100" step="2"
            style="width:80px;background:var(--bg);border:1px solid var(--border);
            color:var(--cyan);font-family:'JetBrains Mono',monospace;font-size:12px;
            padding:4px 8px;border-radius:3px;outline:none;"
            oninput="cgUpdatePreview()">
        </div>

        <!-- Unit + Diagram selector hidden for unit-config -->
        <div id="cg-unit-wrap" style="flex:1;min-width:220px;">
          <div style="font-size:9px;color:var(--text3);letter-spacing:1px;margin-bottom:5px;display:flex;align-items:center;gap:8px;">UNIT <button class="btn" onclick="cgGenerateSelectedUnit()" style="padding:2px 8px;font-size:9px;">Send selected</button></div>
          <div id="cg-unit-list" style="display:flex;flex-wrap:wrap;gap:5px;margin-bottom:8px;"></div>
          <div id="cg-unit-diag-section" style="display:none;">
            <div style="font-size:9px;color:var(--text3);letter-spacing:1px;margin-bottom:5px;">
              DIAGRAMS
              <button onclick="cgSelectAll(true)"
                style="margin-left:8px;background:none;border:none;color:var(--cyan);
                font-size:9px;cursor:pointer;padding:0;">all</button>
              <button onclick="cgSelectAll(false)"
                style="background:none;border:none;color:var(--text3);
                font-size:9px;cursor:pointer;padding:0;">none</button>
            </div>
            <div id="cg-diag-list" style="display:flex;flex-wrap:wrap;gap:5px;"></div>
          </div>
        </div>
      </div>

        <!-- Codegen asset paths (C# reads these files) -->
        <div style="border-top:1px solid var(--border);background:var(--s2);flex-shrink:0;">
          <div style="padding:6px 20px;display:flex;align-items:center;gap:8px;cursor:pointer;user-select:none;"
            onclick="cgToggleAssetPaths()">
            <span style="font-size:9px;letter-spacing:1px;color:var(--text3);">CODEGEN PATHS</span>
            <span id="asset-paths-chevron" style="font-size:9px;color:var(--text3);">&gt;</span>
            <span id="asset-paths-summary" style="font-size:9px;color:var(--text3);margin-left:4px;"></span>
          </div>
          <div id="asset-paths-body" style="display:none;padding:8px 20px 10px 20px;">
            <div style="display:grid;grid-template-columns:120px 1fr auto;gap:6px 10px;align-items:center;">
              <label style="font-size:9px;color:var(--text3);">Device Library</label>
              <input id="cg-device-library-path" value="config/Devices.json" oninput="cgUpdateAssetPathStatus(); cgUpdatePreview()"
                style="background:var(--bg);border:1px solid var(--border);color:var(--cyan);font-family:'JetBrains Mono',monospace;font-size:10px;padding:4px 6px;border-radius:3px;outline:none;">
              <button class="btn" onclick="cgBrowseCodegenPath('deviceLibrary')" style="padding:3px 8px;font-size:9px;">Browse...</button>
              <label style="font-size:9px;color:var(--text3);">Template Root</label>
              <input id="cg-template-root-path" value="templates" oninput="cgUpdateAssetPathStatus(); cgUpdatePreview()"
                style="background:var(--bg);border:1px solid var(--border);color:var(--cyan);font-family:'JetBrains Mono',monospace;font-size:10px;padding:4px 6px;border-radius:3px;outline:none;">
              <button class="btn" onclick="cgBrowseCodegenPath('templateRoot')" style="padding:3px 8px;font-size:9px;">Browse...</button>
              <label style="font-size:9px;color:var(--text3);">Output Folder</label>
              <input id="cg-output-root-path" value="" oninput="cgUpdateAssetPathStatus(); cgUpdatePreview()"
                style="background:var(--bg);border:1px solid var(--border);color:var(--cyan);font-family:'JetBrains Mono',monospace;font-size:10px;padding:4px 6px;border-radius:3px;outline:none;">
              <button class="btn" onclick="cgBrowseCodegenPath('outputRoot')" style="padding:3px 8px;font-size:9px;">Browse...</button>
            </div>
            <div id="asset-paths-status" style="font-size:9px;color:var(--text3);margin-top:6px;"></div>
          </div>
        </div>

        <!-- Multi-file export preview -->
        <div id="cg-files" style="flex:1;overflow:auto;padding:12px 14px;background:var(--bg);"></div>

        <!-- Footer actions -->
        <div style="padding:10px 20px;border-top:1px solid var(--border);
          display:flex;gap:8px;justify-content:flex-end;flex-shrink:0;background:var(--s3);">
          <span id="cg-stat" style="flex:1;font-size:9px;color:var(--text3);align-self:center;"></span>
          <button class="btn" onclick="cgCopyAllFiles()">Copy all</button>
          <button class="btn a" onclick="cgDownloadAllFiles()">Download ZIP</button>
        </div>
      </div>`;

  document.body.appendChild(el);
  cgApplySavedPaths();
  cgBuildUnitList();
  cgUpdateAssetPathStatus();
  cgUpdatePreview();
}


function cgRenderGeneratedFiles(files) {
  const root = document.getElementById('cg-files');
  if (!root) return;
  if (!window.GrafcetStudio) window.GrafcetStudio = {};
  window.GrafcetStudio.codegenLastFiles = Array.isArray(files) ? files : [];
  window.GrafcetStudio.codegenSelectedFileIndex = 0;

  if (!files || !files.length) {
    root.innerHTML = '<div style="padding:12px;color:var(--text3);font-size:12px;">No files generated yet.</div>';
    return;
  }

  const options = files.map((file, index) => {
    const path = file && file.path ? String(file.path) : 'file.st';
    return `<option value="${index}">${esc2(path)}</option>`;
  }).join('');

  root.innerHTML = `
    <div style="display:flex;flex-direction:column;height:100%;gap:10px;">
      <div style="display:flex;align-items:center;gap:8px;flex-wrap:wrap;">
        <label style="font-size:10px;color:var(--text3);letter-spacing:1px;">FILE</label>
        <select id="cg-file-select"
          onchange="cgShowGeneratedFile(this.value)"
          style="min-width:260px;background:var(--s1);border:1px solid var(--border);color:var(--cyan);font-family:'JetBrains Mono',monospace;font-size:11px;padding:6px 8px;border-radius:3px;outline:none;">
          ${options}
        </select>
        <span style="flex:1;"></span>
        <button class="btn" onclick="cgCopySelectedFile()">Copy file</button>
        <button class="btn" onclick="cgDownloadSelectedFile()">Download file</button>
      </div>
      <div id="cg-file-meta" style="font-size:10px;color:var(--text3);"></div>
      <pre id="cg-file-preview" style="margin:0;flex:1;padding:12px;border:1px solid var(--border);background:var(--s1);font-family:'JetBrains Mono',monospace;font-size:11px;line-height:1.6;color:var(--text2);white-space:pre;overflow:auto;tab-size:4;"></pre>
    </div>`;

  cgShowGeneratedFile(0);
}

function cgShowGeneratedFile(index) {
  const files = (window.GrafcetStudio && window.GrafcetStudio.codegenLastFiles) || [];
  const safeIndex = Math.max(0, Math.min(files.length - 1, Number(index) || 0));
  const file = files[safeIndex];
  const preview = document.getElementById('cg-file-preview');
  const meta = document.getElementById('cg-file-meta');
  const select = document.getElementById('cg-file-select');
  if (!file || !preview) return;
  if (window.GrafcetStudio) window.GrafcetStudio.codegenSelectedFileIndex = safeIndex;
  if (select) select.value = String(safeIndex);
  preview.textContent = file.content || '';
  if (meta) meta.textContent = (file.path || 'file.st') + ' - ' + String((file.content || '').length) + ' chars';
}

function cgCopySelectedFile() {
  const files = (window.GrafcetStudio && window.GrafcetStudio.codegenLastFiles) || [];
  const index = (window.GrafcetStudio && window.GrafcetStudio.codegenSelectedFileIndex) || 0;
  const file = files[index];
  if (!file) return;
  cgCopyCode(file.content || '');
}

function cgDownloadSelectedFile() {
  const files = (window.GrafcetStudio && window.GrafcetStudio.codegenLastFiles) || [];
  const index = (window.GrafcetStudio && window.GrafcetStudio.codegenSelectedFileIndex) || 0;
  const file = files[index];
  if (!file) return;
  cgDownloadTextFile(cgSafeFilename(file.path || 'export.st'), file.content || '');
}

function cgCopyAllFiles() {
  const files = (window.GrafcetStudio && window.GrafcetStudio.codegenLastFiles) || [];
  const text = files.map(file => `// FILE: ${file.path || 'file.st'}\n${file.content || ''}`).join('\\n\\n');
  if (!text) return;
  cgCopyCode(text);
}

function cgDownloadAllFiles() {
  const files = (window.GrafcetStudio && window.GrafcetStudio.codegenLastFiles) || [];
  const platform = cgResolveHostPlatform(document.getElementById('cg-target')?.value || 'unit-config');
  console.log(files);
  console.log(platform);
  if (!files.length) return;
  if (!cgExportViaHost(files, platform)) {
    toast('Host export is unavailable');
  }
}

function cgDownloadTextFile(filename, content) {
  const blob = new Blob([content || ''], { type: 'text/plain;charset=utf-8' });
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = filename || 'export.st';
  a.click();
  setTimeout(() => URL.revokeObjectURL(a.href), 1000);
}

function cgSafeFilename(path) {
  const safe = String(path || 'export.st').replace(/[\\\\/:*?"<>|]+/g, '_').trim();
  return safe || 'export.st';
}


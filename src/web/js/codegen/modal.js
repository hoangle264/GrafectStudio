"use strict";

let cgSavedPaths = { deviceLibraryPath: '', templatePath: '', outputPath: '' };
let cgSavedSiemensTiaConfig = { projectPath: '', deviceName: '', plcName: '', targetFolderPath: 'Program blocks', overwriteMode: 'FailIfExists' };
let cgPendingSiemensTiaPush = false;

function showGenerateCodeModal() {
  if (activeDiagramId && typeof flushState === 'function') flushState();
  let el = document.getElementById('modal-codegen');
  if (el) el.remove();
  el = document.createElement('div');
  el.id = 'modal-codegen';
  el.className = 'modal-bg show';

  el.innerHTML = `
    <div class="modal" style="min-width:720px;max-width:96vw;max-height:92vh;display:flex;flex-direction:column;padding:0;overflow:hidden;">
      <div style="padding:12px 20px;background:var(--s3);border-bottom:1px solid var(--border);display:flex;align-items:center;gap:10px;flex-shrink:0;">
        <span style="font-size:12px;letter-spacing:2px;font-family:'Orbitron',monospace;">GENERATE CODE</span>
        <span style="flex:1;"></span>
        <button class="btn" onclick="closeModal('modal-codegen')" style="padding:2px 10px;">X</button>
      </div>

      <div style="padding:10px 20px;border-bottom:1px solid var(--border);display:flex;gap:20px;align-items:center;flex-wrap:wrap;flex-shrink:0;background:var(--s2);">
        <div>
          <div style="font-size:9px;color:var(--text3);letter-spacing:1px;margin-bottom:5px;">TARGET PLC</div>
          <select id="cg-target" onchange="cgOnTargetChanged()" style="background:var(--bg);border:1px solid var(--border);color:var(--cyan);font-family:'JetBrains Mono',monospace;font-size:11px;padding:4px 8px;border-radius:3px;outline:none;">
            <option value="unit-config">Unit Config JSON</option>
            <option value="runtime-plan">Runtime Plan [debug]</option>
            <option value="kv-5500">Keyence KV-5500</option>
            <option value="kv-8000">Keyence KV-8000</option>
            <option value="melsec">Mitsubishi MELSEC</option>
            <option value="omron">OMRON</option>
            <option value="siemens">Siemens AWL</option>
            <option value="siemens-lad">Siemens LAD XML</option>
            <option value="twincat-st">TwinCAT ST</option>
            <option value="csharp-kv-5500">C# Keyence KV demo</option>
            <option value="csharp-twincat-st">C# TwinCAT ST demo</option>
          </select>
        </div>

        <div id="cg-base-mr-wrap">
          <div style="font-size:9px;color:var(--text3);letter-spacing:1px;margin-bottom:5px;">BASE ADDRESS <span style="color:var(--cyan);">@MR</span></div>
          <input id="cg-base-mr" type="number" min="0" max="9999" value="100" step="2" style="width:80px;background:var(--bg);border:1px solid var(--border);color:var(--cyan);font-family:'JetBrains Mono',monospace;font-size:12px;padding:4px 8px;border-radius:3px;outline:none;" oninput="cgUpdatePreview()">
        </div>

        <div id="cg-unit-wrap" style="flex:1;min-width:220px;">
          <div style="font-size:9px;color:var(--text3);letter-spacing:1px;margin-bottom:5px;display:flex;align-items:center;gap:8px;">UNIT <button class="btn" onclick="cgGenerateSelectedUnit()" style="padding:2px 8px;font-size:9px;">Send selected</button><button class="btn" onclick="cgGenerateAllUnits()" style="padding:2px 8px;font-size:9px;">Send all</button></div>
          <div id="cg-unit-list" style="display:flex;flex-wrap:wrap;gap:5px;margin-bottom:8px;"></div>
          <div id="cg-unit-diag-section" style="display:none;">
            <div style="font-size:9px;color:var(--text3);letter-spacing:1px;margin-bottom:5px;">DIAGRAMS <button onclick="cgSelectAll(true)" style="margin-left:8px;background:none;border:none;color:var(--cyan);font-size:9px;cursor:pointer;padding:0;">all</button> <button onclick="cgSelectAll(false)" style="background:none;border:none;color:var(--text3);font-size:9px;cursor:pointer;padding:0;">none</button></div>
            <div id="cg-diag-list" style="display:flex;flex-wrap:wrap;gap:5px;"></div>
          </div>
        </div>
      </div>

      <div id="cg-siemens-lad-panel" style="display:none;border-top:1px solid var(--border);background:var(--s2);padding:10px 20px;flex-shrink:0;">
        <div style="display:flex;align-items:center;gap:12px;flex-wrap:wrap;margin-bottom:8px;">
          <span style="font-size:9px;letter-spacing:1px;color:var(--text3);">SIEMENS LAD MODE</span>
          <label style="font-size:10px;color:var(--text2);"><input type="radio" name="cg-siemens-lad-mode" value="preview" checked onchange="cgOnSiemensLadConfigChanged()"> Preview XML</label>
          <label style="font-size:10px;color:var(--text2);"><input type="radio" name="cg-siemens-lad-mode" value="push" onchange="cgOnSiemensLadConfigChanged()"> Push to TIA</label>
          <span id="cg-siemens-lad-status" style="font-size:10px;color:var(--text3);margin-left:auto;">Preview XML mode</span>
        </div>
        <div id="cg-siemens-tia-config" style="display:none;grid-template-columns:130px 1fr 110px 1fr;gap:6px 10px;align-items:center;">
          <label style="font-size:9px;color:var(--text3);">TIA Project Path</label>
          <input id="cg-tia-project-path" oninput="cgOnSiemensLadConfigChanged()" placeholder="Optional if project is already open" style="background:var(--bg);border:1px solid var(--border);color:var(--cyan);font-family:'JetBrains Mono',monospace;font-size:10px;padding:4px 6px;border-radius:3px;outline:none;">
          <label style="font-size:9px;color:var(--text3);">Overwrite</label>
          <select id="cg-tia-overwrite-mode" onchange="cgOnSiemensLadConfigChanged()" style="background:var(--bg);border:1px solid var(--border);color:var(--cyan);font-family:'JetBrains Mono',monospace;font-size:10px;padding:4px 6px;border-radius:3px;outline:none;"><option value="FailIfExists">Fail if exists</option><option value="Overwrite">Overwrite</option><option value="Rename">Rename</option></select>
          <label style="font-size:9px;color:var(--text3);">Device / Station</label>
          <input id="cg-tia-device-name" oninput="cgOnSiemensLadConfigChanged()" placeholder="TIA device name" style="background:var(--bg);border:1px solid var(--border);color:var(--cyan);font-family:'JetBrains Mono',monospace;font-size:10px;padding:4px 6px;border-radius:3px;outline:none;">
          <label style="font-size:9px;color:var(--text3);">PLC Name</label>
          <input id="cg-tia-plc-name" oninput="cgOnSiemensLadConfigChanged()" placeholder="PLC/device item name" style="background:var(--bg);border:1px solid var(--border);color:var(--cyan);font-family:'JetBrains Mono',monospace;font-size:10px;padding:4px 6px;border-radius:3px;outline:none;">
          <label style="font-size:9px;color:var(--text3);">Block Folder</label>
          <input id="cg-tia-target-folder" oninput="cgOnSiemensLadConfigChanged()" value="Program blocks" placeholder="Program blocks/Generated" style="background:var(--bg);border:1px solid var(--border);color:var(--cyan);font-family:'JetBrains Mono',monospace;font-size:10px;padding:4px 6px;border-radius:3px;outline:none;">
          <div style="font-size:9px;color:var(--text3);grid-column:3 / span 2;">Send selected/all generates XML first, then imports it through the host bridge.</div>
        </div>
      </div>

      <div style="border-top:1px solid var(--border);background:var(--s2);flex-shrink:0;">
        <div style="padding:6px 20px;display:flex;align-items:center;gap:8px;cursor:pointer;user-select:none;" onclick="cgToggleAssetPaths()">
          <span style="font-size:9px;letter-spacing:1px;color:var(--text3);">CODEGEN PATHS</span><span id="asset-paths-chevron" style="font-size:9px;color:var(--text3);">&gt;</span><span id="asset-paths-summary" style="font-size:9px;color:var(--text3);margin-left:4px;"></span>
        </div>
        <div id="asset-paths-body" style="display:none;padding:8px 20px 10px 20px;">
          <div style="display:grid;grid-template-columns:120px 1fr auto;gap:6px 10px;align-items:center;">
            <label style="font-size:9px;color:var(--text3);">Device Library</label><input id="cg-device-library-path" value="config/Devices.json" oninput="cgUpdateAssetPathStatus(); cgUpdatePreview()" style="background:var(--bg);border:1px solid var(--border);color:var(--cyan);font-family:'JetBrains Mono',monospace;font-size:10px;padding:4px 6px;border-radius:3px;outline:none;"><button class="btn" onclick="cgBrowseCodegenPath('deviceLibrary')" style="padding:3px 8px;font-size:9px;">Browse...</button>
            <label style="font-size:9px;color:var(--text3);">Template Root</label><input id="cg-template-root-path" value="templates" oninput="cgUpdateAssetPathStatus(); cgUpdatePreview()" style="background:var(--bg);border:1px solid var(--border);color:var(--cyan);font-family:'JetBrains Mono',monospace;font-size:10px;padding:4px 6px;border-radius:3px;outline:none;"><button class="btn" onclick="cgBrowseCodegenPath('templateRoot')" style="padding:3px 8px;font-size:9px;">Browse...</button>
            <label style="font-size:9px;color:var(--text3);">Output Folder</label><input id="cg-output-root-path" value="" oninput="cgUpdateAssetPathStatus(); cgUpdatePreview()" style="background:var(--bg);border:1px solid var(--border);color:var(--cyan);font-family:'JetBrains Mono',monospace;font-size:10px;padding:4px 6px;border-radius:3px;outline:none;"><button class="btn" onclick="cgBrowseCodegenPath('outputRoot')" style="padding:3px 8px;font-size:9px;">Browse...</button>
          </div>
          <div id="asset-paths-status" style="font-size:9px;color:var(--text3);margin-top:6px;"></div>
        </div>
      </div>

      <div id="cg-files" style="flex:1;overflow:auto;padding:12px 14px;background:var(--bg);"></div>
      <div style="padding:10px 20px;border-top:1px solid var(--border);display:flex;gap:8px;justify-content:flex-end;flex-shrink:0;background:var(--s3);">
        <span id="cg-stat" style="flex:1;font-size:9px;color:var(--text3);align-self:center;"></span>
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
  cgUpdatePreview();
  cgUpdateSiemensLadPanel();
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

  const options = files.map((file, index) => `<option value="${index}">${esc2(file && file.path ? String(file.path) : 'file.st')}</option>`).join('');
  root.innerHTML = `<div style="display:flex;flex-direction:column;height:100%;gap:10px;"><div style="display:flex;align-items:center;gap:8px;flex-wrap:wrap;"><label style="font-size:10px;color:var(--text3);letter-spacing:1px;">FILE</label><select id="cg-file-select" onchange="cgShowGeneratedFile(this.value)" style="min-width:260px;background:var(--s1);border:1px solid var(--border);color:var(--cyan);font-family:'JetBrains Mono',monospace;font-size:11px;padding:6px 8px;border-radius:3px;outline:none;">${options}</select><span style="flex:1;"></span><button class="btn" onclick="cgCopySelectedFile()">Copy file</button><button class="btn" onclick="cgDownloadSelectedFile()">Download file</button></div><div id="cg-file-meta" style="font-size:10px;color:var(--text3);"></div><pre id="cg-file-preview" style="margin:0;flex:1;padding:12px;border:1px solid var(--border);background:var(--s1);font-family:'JetBrains Mono',monospace;font-size:11px;line-height:1.6;color:var(--text2);white-space:pre;overflow:auto;tab-size:4;"></pre></div>`;
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
  cgCopyText(file.content || '');
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
  const text = files.map(file => `// FILE: ${file.path || 'file.st'}\n${file.content || ''}`).join('\n\n');
  if (text) cgCopyText(text);
}

function cgDownloadAllFiles() {
  const files = (window.GrafcetStudio && window.GrafcetStudio.codegenLastFiles) || [];
  const platform = cgResolveHostPlatform(document.getElementById('cg-target')?.value || 'unit-config');
  if (!files.length) return;
  if (!cgExportViaHost(files, platform)) toast('Host export is unavailable');
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
  return String(path || 'export.st').replace(/[\\/:*?"<>|]+/g, '_').trim() || 'export.st';
}

function cgCopyText(text) {
  navigator.clipboard.writeText(text || '').then(() => toast('Copied to clipboard'));
}

function cgOnTargetChanged() {
  cgUpdatePreview();
  cgUpdateSiemensLadPanel();
}

function cgGetSiemensLadMode() {
  return document.querySelector('input[name="cg-siemens-lad-mode"]:checked')?.value || 'preview';
}

function cgGetSiemensTiaConfig() {
  return {
    projectPath: (document.getElementById('cg-tia-project-path')?.value || '').trim(),
    deviceName: (document.getElementById('cg-tia-device-name')?.value || '').trim(),
    plcName: (document.getElementById('cg-tia-plc-name')?.value || '').trim(),
    targetFolderPath: (document.getElementById('cg-tia-target-folder')?.value || 'Program blocks').trim(),
    overwriteMode: document.getElementById('cg-tia-overwrite-mode')?.value || 'FailIfExists'
  };
}

function cgApplySavedSiemensTiaConfig() {
  const config = cgSavedSiemensTiaConfig || {};
  const values = {
    'cg-tia-project-path': config.projectPath || '',
    'cg-tia-device-name': config.deviceName || '',
    'cg-tia-plc-name': config.plcName || '',
    'cg-tia-target-folder': config.targetFolderPath || 'Program blocks',
    'cg-tia-overwrite-mode': config.overwriteMode || 'FailIfExists'
  };
  Object.keys(values).forEach(id => {
    const input = document.getElementById(id);
    if (input) input.value = values[id];
  });
  cgUpdateSiemensLadPanel();
}

function cgOnSiemensLadConfigChanged() {
  cgSavedSiemensTiaConfig = cgGetSiemensTiaConfig();
  cgUpdateSiemensLadPanel();
}

function cgUpdateSiemensLadPanel() {
  const target = document.getElementById('cg-target')?.value || '';
  const panel = document.getElementById('cg-siemens-lad-panel');
  const config = document.getElementById('cg-siemens-tia-config');
  const status = document.getElementById('cg-siemens-lad-status');
  const isSiemensLad = target === 'siemens-lad';
  if (panel) panel.style.display = isSiemensLad ? '' : 'none';
  if (!isSiemensLad) return;
  const isPush = cgGetSiemensLadMode() === 'push';
  if (config) config.style.display = isPush ? 'grid' : 'none';
  if (status && !cgPendingSiemensTiaPush) {
    status.textContent = isPush ? 'Push mode: generate XML then import to TIA' : 'Preview XML mode';
    status.style.color = isPush ? 'var(--amber)' : 'var(--text3)';
  }
}

function cgSetSiemensLadStatus(message, ok) {
  const status = document.getElementById('cg-siemens-lad-status');
  if (!status) return;
  status.textContent = message || '';
  status.style.color = ok === true ? 'var(--cyan)' : ok === false ? 'var(--red)' : 'var(--amber)';
}

function cgShouldPushSiemensTia() {
  return document.getElementById('cg-target')?.value === 'siemens-lad' && cgGetSiemensLadMode() === 'push';
}

function receiveSiemensTiaPushResult(payload) {
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

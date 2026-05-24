"use strict";

let cgSavedPaths = { deviceLibraryPath: '', templatePath: '', outputPath: '' };

//  GRAFCET Code Generator - grafcet-codegen.js
//  Target: Keyence KV Mnemonic IL (.mnm)
//  Planned: IEC 61131-3 ST (.st) â€” demo/stub only
//
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
          <div style="font-size:9px;color:var(--text3);letter-spacing:1px;margin-bottom:5px;">UNIT</div>
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

        <!-- Code preview -->
        <div style="flex:1;overflow:auto;padding:0;">
          <pre id="cg-preview"
            style="margin:0;padding:14px 18px;font-family:'JetBrains Mono',monospace;
            font-size:11px;line-height:1.7;color:var(--text2);background:var(--bg);
            min-height:300px;white-space:pre;tab-size:4;"></pre>
        </div>

        <!-- Footer actions -->
        <div style="padding:10px 20px;border-top:1px solid var(--border);
          display:flex;gap:8px;justify-content:flex-end;flex-shrink:0;background:var(--s3);">
          <span id="cg-stat" style="flex:1;font-size:9px;color:var(--text3);align-self:center;"></span>
          <button class="btn" onclick="cgCopyCode()">Copy</button>
          <button class="btn a" onclick="cgDownloadCode()">Download</button>
        </div>
      </div>`;

  document.body.appendChild(el);
  cgApplySavedPaths();
  cgBuildUnitList();
  cgUpdateAssetPathStatus();
  cgUpdatePreview();
}



//  Build unit selector cho Unit Config mode

// Build unit radio list 
function cgBuildUnitList() {
  const wrap = document.getElementById('cg-unit-list');
  if (!wrap) return;
  wrap.innerHTML = '';

  const units = project.units || [];
  const allDiags = project.diagrams || [];
  const hasOrphans = allDiags.some(d => !d.unitId);

  if (!units.length && !hasOrphans) {
    wrap.innerHTML = '<span style="font-size:10px;color:var(--text3)">No units found</span>';
    return;
  }

  let firstId = null;

  units.forEach((u, i) => {
    const count = allDiags.filter(d => d.unitId === u.id).length;
    const lbl = document.createElement('label');
    lbl.className = 'cg-radio-lbl';
    lbl.innerHTML = `<input type="radio" name="cg-unit-radio" value="${u.id}"
      onchange="cgOnUnitSelect('${u.id}')">${esc2(u.name || u.id)}
      <span style="color:var(--text3);font-size:8px;margin-left:2px;">(${count})</span>`;
    wrap.appendChild(lbl);
    if (i === 0) firstId = u.id;
  });

  if (hasOrphans) {
    const count = allDiags.filter(d => !d.unitId).length;
    const lbl = document.createElement('label');
    lbl.className = 'cg-radio-lbl';
    lbl.innerHTML = `<input type="radio" name="cg-unit-radio" value="__none__"
      onchange="cgOnUnitSelect('__none__')">(No unit)
      <span style="color:var(--text3);font-size:8px;margin-left:2px;">(${count})</span>`;
    wrap.appendChild(lbl);
    if (!firstId) firstId = '__none__';
  }

  // Pre-select first unit and render its diagrams
  if (firstId) {
    const radio = wrap.querySelector(`input[value="${firstId}"]`);
    if (radio) radio.checked = true;
    cgBuildDiagForUnit(firstId);
  }
}

//  Called when user selects a unit radio button 
function cgOnUnitSelect(unitId) {
  cgBuildDiagForUnit(unitId);
  cgUpdatePreview();
}

// Build diagram checkboxes for the selected unit
function cgBuildDiagForUnit(unitId) {
  const listWrap = document.getElementById('cg-diag-list');
  const section  = document.getElementById('cg-unit-diag-section');
  if (!listWrap) return;
  listWrap.innerHTML = '';

  const diags = (project.diagrams || []).filter(d =>
    unitId === '__none__' ? !d.unitId : d.unitId === unitId
  );

  if (section) section.style.display = diags.length ? '' : 'none';

  diags.forEach(d => {
    const modeName = d.mode || 'Auto';
    const lbl = document.createElement('label');
    lbl.className = 'cg-diag-chip';
    lbl.dataset.diagId = d.id;
    lbl.innerHTML = `<input type="checkbox" value="${d.id}" checked
      onchange="cgUpdatePreview()" style="margin-right:4px;">
      <span>${esc2(d.name || d.id)}</span>
      <span style="color:var(--text3);font-size:8px;margin-left:2px;">[${modeName}]</span>`;
    listWrap.appendChild(lbl);
  });
}

function cgSelectAll(val) {
  document.querySelectorAll('#cg-diag-list input[type=checkbox]')
    .forEach(c => { c.checked = val; });
  cgUpdatePreview();
}


//  Live preview 
function cgUpdatePreview() {
  const target = document.getElementById('cg-target')?.value || 'kv-5500';
  const platform = cgResolveHostPlatform(target);
  const isUC = (platform === 'unit-config');

  // Show/hide panels
  const baseMRWrap  = document.getElementById('cg-base-mr-wrap');
  const unitWrap    = document.getElementById('cg-unit-wrap');
  if (baseMRWrap) baseMRWrap.style.display = isUC ? 'none' : '';
  if (unitWrap)   unitWrap.style.display   = isUC ? 'none' : '';

  const pre  = document.getElementById('cg-preview');
  const stat = document.getElementById('cg-stat');
  if (!pre) return;

  if (!isUC) {
    const selected = cgGetSelectedDiagramIds();
    if (!selected.length) {
      pre.textContent = '; No diagrams selected.';
      if (stat) stat.textContent = '';
      return;
    }

    pre.textContent = '; Waiting for C# generator...';
    if (stat) stat.textContent = 'C# generator request queued';
    cgGenerateViaHost(platform, selected[0]);
    return;
  }

  // Unit Config JSON engine
  if (isUC) {
    // Route Unit Config generation to C# host; JSON file inputs were removed from the modal.
    pre.textContent = '; Waiting for C# generator...';
    if (stat) stat.textContent = 'C# generator request queued';
    const selectedUnit = cgGetDefaultUnitId();
    cgGenerateViaHost('unit-config', selectedUnit || '');
    return;
  }

}

// Syntax highlight cho Unit Config output 
function cgResolveHostPlatform(target) {
  return {
    'csharp-kv-5500': 'kv-5500',
    'csharp-twincat-st': 'twincat-st'
  }[target] || target || 'kv-5500';
}
function cgGetDefaultUnitId() {
  const unitRadio = document.querySelector('#cg-unit-list input[name="cg-unit-radio"]:checked');
  if (unitRadio) return unitRadio.value;
  const firstUnit = (project.units || [])[0];
  if (firstUnit) return firstUnit.id || '';
  return (project.diagrams || []).some(d => !d.unitId) ? '__none__' : '';
}

function cgGetSelectedDiagramIds() {
  return Array.from(
    document.querySelectorAll('#cg-diag-list input[type=checkbox]:checked')
  ).map(c => c.value);
}

function cgBuildCSharpPayload(platform, diagId) {
  if (activeDiagramId && typeof flushState === 'function') flushState();
  console.debug('gen')
  const diag = (project.diagrams || []).find(d => d.id === diagId) || {};
  const data = loadDiagramData(diagId);
  const s = (data && data.state) || { steps: [], transitions: [], connections: [], vars: [] };
  const steps = (s.steps || []).map(step => ({
    id: step.id || '',
    number: Number(step.number || 0),
    label: step.label || '',
    initial: !!step.initial,
    actions: (step.actions || []).map(action => ({
      variable: action.variable || '',
      address: action.address || null,
      qualifier: action.qualifier || 'N',
      timeMs: Number(action.timeMs || action.time || 0)
    }))
  }));
  const stepIds = new Set(steps.map(step => step.id));
  const transitions = (s.transitions || []).map(trans => ({
    id: trans.id || '',
    label: trans.label || '',
    condition: trans.condition || '',
    fromStepIds: (s.connections || [])
      .filter(conn => conn.to === trans.id && stepIds.has(conn.from))
      .map(conn => conn.from),
    toStepIds: (s.connections || [])
      .filter(conn => conn.from === trans.id && stepIds.has(conn.to))
      .map(conn => conn.to)
  }));

  console.log('[Codegen] Diagram payload counts', {
    diagramId: diag.id || diagId,
    steps: steps.length,
    transitions: transitions.length
  });

  return {
    platform,
    codegenAssets: cgGetCodegenAssets(),
    deviceLibraryPath: cgGetCodegenAssets().deviceLibraryPath,
    templateRootPath: cgGetCodegenAssets().templateRootPath,
    templatePath: cgGetCodegenAssets().templateRootPath,
    outputPath: cgGetCodegenAssets().outputPath,
    project: {
      id: project.id || '',
      name: project.name || '',
      machineName: project.machineName || ''
    },
    diagram: {
      id: diag.id || diagId,
      name: diag.name || diagId,
      mode: diag.mode || '',
      unitId: diag.unitId || '',
      unit: diag.unit || ''
    },
    steps,
    transitions,
    variables: cgGetCSharpVariables(s),
    deviceTypes: (project && project.devices) || []
  };
}

function cgGetCSharpVariables(diagramState) {
  const vars = [];
  const seen = new Set();
  const add = function(v) {
    if (!v || !v.label || seen.has(v.label)) return;
    seen.add(v.label);
    vars.push({
      label: v.label,
      format: v.format || v.dataType || '',
      address: v.address || null,
      signalAddresses: v.signalAddresses || {}
    });
  };

  (diagramState.vars || []).forEach(add);
  if (typeof ensureProjectVariables === 'function') {
    const grouped = ensureProjectVariables();
    (grouped.imported || []).forEach(add);
    (grouped.user || []).forEach(add);
  }
  (project.excelVars || []).forEach(add);
  return vars;
}

function cgGenerateViaHost(platform, diagId) {
  if (!(window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function')) {
    const pre = document.getElementById('cg-preview');
    const stat = document.getElementById('cg-stat');
    if (pre) pre.textContent = '; C# generator is available only inside the WPF host.';
    if (stat) stat.textContent = 'Host bridge unavailable';
    return false;
  }

  window.chrome.webview.postMessage({
    type: 'GENERATE_CODE',
    payload: cgBuildCSharpPayload(platform, diagId)
  });
  return true;
}

function receiveGeneratedCode(code) {
  const pre = document.getElementById('cg-preview');
  const stat = document.getElementById('cg-stat');
  if (pre) pre.textContent = code || '';
  if (stat) stat.textContent = 'Generated by C# host';
}

function receiveError(payload) {
  const pre = document.getElementById('cg-preview');
  const stat = document.getElementById('cg-stat');
  const source = payload && payload.source ? payload.source : 'host';
  const message = payload && payload.message ? payload.message : String(payload || 'Unknown error');
  if (typeof toast === 'function' && (source === 'template-loader' || source === 'template-validation')) {
    toast('? ' + source + ': ' + message);
  }
  if (pre) pre.textContent = '; ' + source + ' error: ' + message;
  if (stat) stat.textContent = 'C# host error';
}

function cgUCHighlight(pre, profile) {
  const commentPfx = profile ? profile.comment : ';';
  const commentRe = commentPfx === '//' ? /^(\/\/.*)$/gm : /^(;.*)$/gm;
  const escaped = pre.textContent.replace(/[&<>]/g, c =>
    c === '&' ? '&amp;' : c === '<' ? '&lt;' : '&gt;');
  pre.innerHTML = escaped
    .replace(/^(;&lt;h1\/&gt;.*)$/gm, '<span style="color:var(--amber);font-weight:bold">$1</span>')
    .replace(commentRe, '<span style="color:var(--text3)">$1</span>')
    .replace(/\b(LDP|LDB|ANP|ANF|ALT|DIFU|ZRES|CON|MPS|MRD|MPP|LD|SET|RES|RST|OUT|AND|ANB|OR|ORL|ANL|ONDL|MOV)\b/g,
      '<span style="color:var(--cyan)">$1</span>')
    .replace(/@MR\d+/g, '<span style="color:var(--amber)">$&</span>')
    .replace(/\bMR\d+\b/g, '<span style="color:#4ade80">$&</span>')
    .replace(/\bLR\d+\b/g, '<span style="color:#f472b6">$&</span>');
}

// Status badge cho file load 

// â”€â”€â”€ Download / Copy â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
function cgDownloadCode() {
  const target = document.getElementById('cg-target')?.value || 'kv-5500';
  const platform = cgResolveHostPlatform(target);

  if (platform === 'unit-config') {
    cgGenerateViaHost(platform, cgGetDefaultUnitId() || '');
    return;
  }

  const selected = cgGetSelectedDiagramIds();
  if (!selected.length) { toast('No diagrams selected'); return; }

  cgGenerateViaHost(platform, selected[0] || '');
}
function cgExportViaHost(code, platform) {
  if (!(window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function')) {
    return false;
  }
  window.chrome.webview.postMessage({
    type: 'EXPORT_CODE',
    payload: { code, platform }
  });
  toast('Export dialog opened');
  return true;
}

function cgCopyCode() {
  const pre = document.getElementById('cg-preview');
  if (!pre) return;
  navigator.clipboard.writeText(pre.textContent).then(() => toast('âœ“ Copied to clipboard'));
}

// JSON Files panel toggle 

// --- Codegen asset paths ----------------------------------------------------
function cgGetCodegenAssets() {
  const devPath = document.getElementById('cg-device-library-path')?.value || 'config/Devices.json';
  const tplRoot = document.getElementById('cg-template-root-path')?.value || 'templates';
  const outRoot = document.getElementById('cg-output-root-path')?.value || '';
  return {
    deviceLibraryPath: devPath.trim(),
    templateRootPath: tplRoot.trim(),
    outputPath: outRoot.trim()
  };
}

function cgUpdateAssetPathStatus() {
  const assets = cgGetCodegenAssets();
  const status = document.getElementById('asset-paths-status');
  const summary = document.getElementById('asset-paths-summary');
  const ok = !!assets.deviceLibraryPath && !!assets.templateRootPath && !!assets.outputPath;
  const text = ok
    ? 'C# host will resolve: ' + assets.deviceLibraryPath + ' | ' + assets.templateRootPath + ' | ' + assets.outputPath
    : 'Missing device library, template root, or output path';
  if (status) {
    status.textContent = text;
    status.style.color = ok ? 'var(--cyan)' : 'var(--amber)';
  }
  if (summary) {
    summary.textContent = ok ? '? paths ready' : 'missing path';
    summary.style.color = ok ? 'var(--cyan)' : 'var(--amber)';
  }
}

function cgBrowseCodegenPath(target) {
  if (!(window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function')) {
    toast('Browse is available only inside the WPF host');
    return false;
  }

  window.chrome.webview.postMessage({
    type: 'BROWSE_CODEGEN_PATH',
    payload: { target }
  });
  return true;
}

function receiveCodegenPath(payload) {
  const data = typeof payload === 'string' ? { target: 'templateRoot', path: payload } : (payload || {});
  const inputId = data.target === 'deviceLibrary'
    ? 'cg-device-library-path'
    : data.target === 'outputRoot'
      ? 'cg-output-root-path'
      : 'cg-template-root-path';
  const input = document.getElementById(inputId);
  if (!input || !data.path) return;
  input.value = data.path;
  cgUpdateAssetPathStatus();
  cgUpdatePreview();
}

function receiveSavedPaths(payload) {
  cgSavedPaths = payload || {};
  cgApplySavedPaths();
}

function cgApplySavedPaths() {
  const data = cgSavedPaths || {};
  const deviceInput = document.getElementById('cg-device-library-path');
  const templateInput = document.getElementById('cg-template-root-path');
  const outputInput = document.getElementById('cg-output-root-path');
  if (deviceInput && data.deviceLibraryPath) deviceInput.value = data.deviceLibraryPath;
  if (templateInput && (data.templateRootPath || data.templatePath)) templateInput.value = data.templateRootPath || data.templatePath;
  if (outputInput && data.outputPath) outputInput.value = data.outputPath;
  cgUpdateAssetPathStatus();
  cgUpdatePreview();
}

function cgToggleAssetPaths() {
  const body = document.getElementById('asset-paths-body');
  const chevron = document.getElementById('asset-paths-chevron');
  if (!body) return;
  const open = body.style.display !== 'none';
  body.style.display = open ? 'none' : '';
  if (chevron) chevron.textContent = open ? '>' : 'v';
  if (!open) cgUpdateAssetPathStatus();
}




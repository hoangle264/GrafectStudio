"use strict";

type VuiProjectVariable = GrafcetStudioProject.ProjectVariable;
type VuiUnitConfig = GrafcetStudioProject.UnitConfig;
type VuiDeviceSignal = GrafcetStudioProject.DeviceSignal;
type VuiVarEntry = GrafcetStudioVars.VarEntry;
type VuiPlcBlock = GrafcetStudioProject.PlcBlock;

declare function updateVarDatalist(): void;
declare function init(): void;
declare function fitView(): void;
declare function confirmRename(): void;

function varsGetApi(): GrafcetStudioVars.VarsApi {
  const api = window.GrafcetStudio && window.GrafcetStudio.vars as GrafcetStudioVars.VarsApi | undefined;
  if (!api) throw new Error('GrafcetStudio vars bridge is not loaded');
  return api;
}

function ioGetApi(): GrafcetStudioIOMapping.IOMappingApi {
  const api = window.GrafcetStudio && window.GrafcetStudio.ioMapping as GrafcetStudioIOMapping.IOMappingApi | undefined;
  if (!api) throw new Error('GrafcetStudio IO mapping bridge is not loaded');
  return api;
}

function varsGetContext(): GrafcetStudioVars.VarsContext {
  return {
    project,
    ensureProjectVariables: typeof ensureProjectVariables === 'function' ? ensureProjectVariables : undefined
  };
}

function ioGetContext(): GrafcetStudioIOMapping.IOMappingContext {
  return {
    project,
    ensureProjectIOMapping: typeof ensureProjectIOMapping === 'function' ? ensureProjectIOMapping : undefined,
    varsApi: varsGetApi(),
    varsContext: varsGetContext()
  };
}
// ─────────────────────────────────────────────
//  GLOBAL VARIABLE TABLE (sidebar panel)
// ─────────────────────────────────────────────
const GVT_CYL_SIGNALS: VuiDeviceSignal[] = [
  {id:'cyl_coilA',  name:'CoilA',    dataType:'Bool', varType:'Output', comment:'Output coil A (extend)'},
  {id:'cyl_coilB',  name:'CoilB',    dataType:'Bool', varType:'Output', comment:'Output coil B (retract)'},
  {id:'cyl_lsh',    name:'LSH',      dataType:'Bool', varType:'Input',  comment:'Limit switch high (extended)'},
  {id:'cyl_lsl',    name:'LSL',      dataType:'Bool', varType:'Input',  comment:'Limit switch low (retracted)'},
  {id:'cyl_lockA',  name:'LockA',    dataType:'Bool', varType:'Var',    comment:'Interlock coil A'},
  {id:'cyl_lockB',  name:'LockB',    dataType:'Bool', varType:'Var',    comment:'Interlock coil B'},
  {id:'cyl_disSnsH',name:'DisSnsH',  dataType:'Bool', varType:'Var',    comment:'Disable sensor LSH'},
  {id:'cyl_disSnsL',name:'DisSnsL',  dataType:'Bool', varType:'Var',    comment:'Disable sensor LSL'},
  {id:'cyl_errA',   name:'ErrorA',   dataType:'Bool', varType:'Var',    comment:'Error flag dir A'},
  {id:'cyl_errB',   name:'ErrorB',   dataType:'Bool', varType:'Var',    comment:'Error flag dir B'},
  {id:'cyl_state',  name:'State',    dataType:'Bool', varType:'Var',    comment:'Cylinder state'},
  {id:'cyl_hmiMan', name:'HmiManBtn',dataType:'Bool', varType:'Var',    comment:'HMI manual button'},
];

const GVT_UNIT_SIGNALS: VuiDeviceSignal[] = [
  {id:'originBaseAddr', name:'originBaseAddr', dataType:'Word', varType:'Var', path:'originBaseAddr'},
  {id:'autoBaseAddr',   name:'autoBaseAddr',   dataType:'Word', varType:'Var', path:'autoBaseAddr'},
  {id:'flagOrigin',     name:'flagOrigin',     dataType:'Bool', varType:'Var', path:'flags.flagOrigin'},
  {id:'flagAuto',       name:'flagAuto',       dataType:'Bool', varType:'Var', path:'flags.flagAuto'},
  {id:'flagManual',     name:'flagManual',     dataType:'Bool', varType:'Var', path:'flags.flagManual'},
  {id:'flagError',      name:'flagError',      dataType:'Bool', varType:'Var', path:'flags.flagError'},
  {id:'btnStart',       name:'btnStart',       dataType:'Bool', varType:'Input', path:'io.btnStart'},
  {id:'hmiStop',        name:'hmiStop',        dataType:'Bool', varType:'Input', path:'io.hmiStop'},
  {id:'btnReset',       name:'btnReset',       dataType:'Bool', varType:'Input', path:'io.btnReset'},
  {id:'eStop',          name:'eStop',          dataType:'Bool', varType:'Input', path:'io.eStop'},
  {id:'outHomed',       name:'outHomed',       dataType:'Bool', varType:'Output', path:'io.outHomed'},
];

// Danh sch kiu primitive ph thng
const GVT_PRIMITIVE_TYPES = ['BOOL','BOOL','INT','DINT','UINT','UDINT','WORD','DWORD','BYTE','REAL','LREAL','STRING','TIME'].filter(function(value, index, list) { return list.indexOf(value) === index; });

function gvtGetEntries(): VuiVarEntry[] {
  return varsGetApi().gvtGetEntries(varsGetContext());
}

function gvtGetUnitAddr(cfg: VuiUnitConfig | null | undefined, path: string): string {
  return varsGetApi().gvtGetUnitAddr(cfg, path);
}

function gvtSetUnitAddr(cfg: VuiUnitConfig, path: string, value: string): void {
  return varsGetApi().gvtSetUnitAddr(cfg, path, value);
}

function gvtGetUnitSigList(): VuiDeviceSignal[] {
  return varsGetApi().gvtGetUnitSigList(varsGetContext());
}

function gvtGetSigList(v: VuiProjectVariable): VuiDeviceSignal[] {
  return varsGetApi().gvtGetSigList(varsGetContext(), v);
}

function gvtGetExcelSignalAddress(v: VuiProjectVariable | null | undefined, sig: VuiDeviceSignal | null | undefined): string {
  return varsGetApi().gvtGetExcelSignalAddress(v, sig);
}

//  Compatibility: getVars() dng bi actions.js / updateVarDatalist 

function gvtSetSignalAddress(entry: VuiVarEntry, sig: VuiDeviceSignal, value: string): boolean {
  if (!entry || !sig) return false;
  if((entry.source === 'imported' || entry.source === 'user') && project.variables && entry.bucket && project.variables[entry.bucket] && (project.variables[entry.bucket] as VuiProjectVariable[])[entry.key as number]){
    const rec = (project.variables[entry.bucket] as VuiProjectVariable[])[entry.key as number];
    if(!rec.signalAddresses) rec.signalAddresses={};
    rec.signalAddresses[sig.id]=value;
  } else if(entry.source === 'excel' && project.excelVars[entry.key as number]){
    if(!project.excelVars[entry.key as number].signalAddresses) project.excelVars[entry.key as number].signalAddresses={};
    project.excelVars[entry.key as number].signalAddresses![sig.id]=value;
  } else if(entry.source === 'unit' && project.unitConfig && project.unitConfig[entry.key as string]) {
    const cfg = project.unitConfig[entry.key as string];
    const isKnownPath = GVT_UNIT_SIGNALS.some(function(unitSig) { return unitSig.path === sig.path; });
    if (isKnownPath) {
      gvtSetUnitAddr(cfg, sig.path!, value);
    } else {
      if (!cfg.signalAddresses) cfg.signalAddresses = {};
      cfg.signalAddresses[sig.id] = value;
    }
  } else {
    return false;
  }
  return true;
}

function gvtFlushFocusedAddressInput(): void {
  const active = document.activeElement as (HTMLElement & { dispatchEvent(e: Event): boolean }) | null;
  if (!active || typeof active.dispatchEvent !== 'function') return;
  if (active.classList && (active.classList.contains('vt-sig-addr') || active.classList.contains('addr'))) {
    active.dispatchEvent(new Event('input', { bubbles: true }));
  }
}

function getVars(): VuiProjectVariable[] {
  return varsGetApi().getVars(varsGetContext());
}

//  To <select> cho ct Type (primitive + struct/device) 
function gvtMakeTypeSelect(entry: VuiVarEntry, v: VuiProjectVariable): HTMLSelectElement {
  const currentFormat = v.format || v.dataType || 'BOOL';
  const structDevices = (project.devices || []).map(function(d) { return d.name; });
  const isPrimitive   = GVT_PRIMITIVE_TYPES.includes(currentFormat.toUpperCase());

  const sel = document.createElement('select');
  sel.className = 'vt-cell';
  sel.style.cssText = 'color:var(--cyan);background:var(--s1);border:none;width:100%;cursor:pointer;';
  sel.title = currentFormat;

  // Group 1: Primitive
  const grpPrim = document.createElement('optgroup');
  grpPrim.label = 'Primitive';
  GVT_PRIMITIVE_TYPES.forEach(function(t) {
    const opt = document.createElement('option');
    opt.value = t;
    opt.textContent = t;
    if (t === currentFormat.toUpperCase()) opt.selected = true;
    grpPrim.appendChild(opt);
  });
  sel.appendChild(grpPrim);

  // Group 2: Struct / Device
  const grpStruct = document.createElement('optgroup');
  grpStruct.label = 'Struct / Device';
  const structOptions = structDevices.length
    ? structDevices
    : ['Cylinder', 'Unit Station', 'Struct Data'];

  structOptions.forEach(function(t) {
    const opt = document.createElement('option');
    opt.value = t;
    opt.textContent = t;
    if (t === currentFormat) opt.selected = true;
    grpStruct.appendChild(opt);
  });

  // Nu currentFormat l struct nhng khng nm trong danh sch  thm vo
  if (!isPrimitive && !structOptions.includes(currentFormat)) {
    const opt = document.createElement('option');
    opt.value = currentFormat;
    opt.textContent = currentFormat;
    opt.selected = true;
    grpStruct.appendChild(opt);
  }
  sel.appendChild(grpStruct);

  sel.addEventListener('change', function() {
    gvtEditVar(entry.source, String(entry.key), 'format', this.value);
  });

  return sel;
}


function gvtEnsurePlcBlocks(): VuiPlcBlock[] {
  if (!Array.isArray(project.blocks)) project.blocks = [];
  return project.blocks;
}

function gvtBlockIdFromName(name: string): string {
  const base = String(name || 'DB_Block').trim().toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '') || 'block';
  let id = 'plc-block-' + base;
  const blocks = gvtEnsurePlcBlocks();
  let index = 2;
  while (blocks.some(function(block) { return block.id === id; })) id = 'plc-block-' + base + '-' + (index++);
  return id;
}

function gvtVariableEntriesForBlocks(): VuiVarEntry[] {
  return gvtGetEntries().filter(function(entry) { return entry.source === 'imported' || entry.source === 'user' || entry.source === 'excel'; });
}

function gvtSyncPlcBlockMemberIds(): void {
  const blocks = gvtEnsurePlcBlocks();
  blocks.forEach(function(block) { block.memberVarIds = []; });
  gvtVariableEntriesForBlocks().forEach(function(entry) {
    const variable = entry.data as VuiProjectVariable;
    if (!variable || !variable.blockId) return;
    const block = blocks.find(function(item) { return item.id === variable.blockId; });
    if (!block) return;
    if (!block.memberVarIds) block.memberVarIds = [];
    const variableId = variable.id || (entry.source + '-' + entry.key);
    if (block.memberVarIds.indexOf(variableId) < 0) block.memberVarIds.push(variableId);
  });
}

function gvtBlockOptionsHtml(selectedBlockId: string): string {
  const options = ['<option value="">(Address mapped / no block)</option>'];
  gvtEnsurePlcBlocks().forEach(function(block) {
    options.push('<option value="' + esc2(block.id) + '" ' + (block.id === selectedBlockId ? 'selected' : '') + '>' + esc2(block.kind || 'DB') + ' ? ' + esc2(block.name || block.id) + '</option>');
  });
  return options.join('');
}

function gvtAddPlcBlockFromModal(): void {
  const name = ((document.getElementById('gvt-block-name') as HTMLInputElement | null)?.value || '').trim();
  const kind = ((document.getElementById('gvt-block-kind') as HTMLSelectElement | null)?.value || 'DB').trim() || 'DB';
  const comment = ((document.getElementById('gvt-block-comment') as HTMLInputElement | null)?.value || '').trim();
  if (!name) { toast('PLC block name is required'); return; }
  const blocks = gvtEnsurePlcBlocks();
  if (blocks.some(function(block) { return String(block.name || '').toLowerCase() === name.toLowerCase(); })) { toast('PLC block name already exists'); return; }
  blocks.push({ id: gvtBlockIdFromName(name), name, kind, comment, memberVarIds: [] });
  gvtSyncPlcBlockMemberIds();
  saveProject();
  gvtRenderPlcBlockManager();
}

function gvtUpdatePlcBlock(blockId: string, field: string, value: string): void {
  const block = gvtEnsurePlcBlocks().find(function(item) { return item.id === blockId; });
  if (!block) return;
  if (field === 'name') block.name = value;
  else if (field === 'kind') block.kind = value;
  else if (field === 'comment') block.comment = value;
  gvtSyncPlcBlockMemberIds();
  saveProject();
}

function gvtDeletePlcBlock(blockId: string): void {
  const blocks = gvtEnsurePlcBlocks();
  const idx = blocks.findIndex(function(block) { return block.id === blockId; });
  if (idx < 0) return;
  const block = blocks[idx];
  if (!confirm('Delete PLC block "' + (block.name || block.id) + '"? Variables assigned to it will become address-mapped.')) return;
  blocks.splice(idx, 1);
  gvtVariableEntriesForBlocks().forEach(function(entry) {
    const variable = entry.data as VuiProjectVariable;
    if (variable && variable.blockId === blockId) {
      variable.blockId = '';
      variable.declarationMode = 'AddressMapped';
    }
  });
  saveProject();
  renderGlobalVarTable();
  gvtRenderPlcBlockManager();
}

function gvtAssignVariableBlock(source: string, key: string, blockId: string): void {
  const hit = gvtResolveEntry(source, key);
  if (!hit.item) return;
  hit.item.blockId = blockId || '';
  hit.item.declarationMode = blockId ? 'SymbolicBlock' : 'AddressMapped';
  gvtSyncPlcBlockMemberIds();
  saveProject();
  renderGlobalVarTable();
  gvtRenderPlcBlockManager();
}

function gvtRenderPlcBlockManager(): void {
  const body = document.getElementById('gvt-block-manager-body');
  if (!body) return;
  const blocks = gvtEnsurePlcBlocks();
  gvtSyncPlcBlockMemberIds();
  const blockRows = blocks.length ? blocks.map(function(block) {
    const count = gvtVariableEntriesForBlocks().filter(function(entry) { return (entry.data as VuiProjectVariable).blockId === block.id; }).length;
    const udtNote = String(block.kind || '').toUpperCase() === 'UDT' ? '<span style="color:var(--amber);font-size:9px;">manual import only</span>' : '<span style="color:var(--cyan);font-size:9px;">pushable DB</span>';
    return '<tr>'
      + '<td><input value="' + esc2(block.name || '') + '" onchange="gvtUpdatePlcBlock(\'' + esc2(block.id) + '\',\'name\',this.value)" style="width:100%;background:var(--bg);border:1px solid var(--border);color:var(--cyan);padding:4px 6px;"></td>'
      + '<td><select onchange="gvtUpdatePlcBlock(\'' + esc2(block.id) + '\',\'kind\',this.value)" style="background:var(--bg);border:1px solid var(--border);color:var(--cyan);padding:4px 6px;"><option value="DB" ' + (String(block.kind).toUpperCase()==='DB'?'selected':'') + '>DB</option><option value="UDT" ' + (String(block.kind).toUpperCase()==='UDT'?'selected':'') + '>UDT</option></select></td>'
      + '<td style="font-size:10px;color:var(--text2);">' + count + ' vars<br>' + udtNote + '</td>'
      + '<td><input value="' + esc2(block.comment || '') + '" onchange="gvtUpdatePlcBlock(\'' + esc2(block.id) + '\',\'comment\',this.value)" style="width:100%;background:var(--bg);border:1px solid var(--border);color:var(--text2);padding:4px 6px;"></td>'
      + '<td><button class="btn" onclick="gvtDeletePlcBlock(\'' + esc2(block.id) + '\')" style="padding:2px 7px;color:#f87171;">Delete</button></td>'
      + '</tr>';
  }).join('') : '<tr><td colspan="5" style="color:var(--text3);padding:8px;">No PLC blocks yet.</td></tr>';

  const variableRows = gvtVariableEntriesForBlocks().map(function(entry) {
    const variable = entry.data as VuiProjectVariable;
    const key = String(entry.key).replace(/'/g, '&#39;');
    return '<tr>'
      + '<td style="color:var(--text2);">' + esc2(variable.label || '') + '</td>'
      + '<td style="color:var(--text3);">' + esc2(variable.format || variable.dataType || '') + '</td>'
      + '<td><select onchange="gvtAssignVariableBlock(\'' + esc2(entry.source) + '\',\'' + key + '\',this.value)" style="width:100%;background:var(--bg);border:1px solid var(--border);color:var(--cyan);padding:4px 6px;">' + gvtBlockOptionsHtml(variable.blockId || '') + '</select></td>'
      + '<td style="font-size:10px;color:' + (variable.declarationMode === 'SymbolicBlock' ? 'var(--cyan)' : 'var(--text3)') + ';">' + esc2(variable.declarationMode || 'AddressMapped') + '</td>'
      + '</tr>';
  }).join('');

  body.innerHTML = '<div style="display:grid;grid-template-columns:1fr 1fr;gap:12px;min-height:0;">'
    + '<div style="min-width:0;"><div style="font-size:10px;color:var(--text3);letter-spacing:1px;margin-bottom:6px;">BLOCKS</div>'
    + '<div style="display:grid;grid-template-columns:1fr 80px 1fr auto;gap:6px;margin-bottom:8px;"><input id="gvt-block-name" placeholder="DB_Motor" style="background:var(--bg);border:1px solid var(--border);color:var(--cyan);padding:5px 7px;"><select id="gvt-block-kind" style="background:var(--bg);border:1px solid var(--border);color:var(--cyan);padding:5px 7px;"><option value="DB">DB</option><option value="UDT">UDT</option></select><input id="gvt-block-comment" placeholder="Comment" style="background:var(--bg);border:1px solid var(--border);color:var(--text2);padding:5px 7px;"><button class="btn" onclick="gvtAddPlcBlockFromModal()" style="padding:4px 8px;">Add</button></div>'
    + '<div style="max-height:360px;overflow:auto;"><table class="vt-table table-full"><thead><tr><th>Name</th><th>Kind</th><th>Status</th><th>Comment</th><th></th></tr></thead><tbody>' + blockRows + '</tbody></table></div></div>'
    + '<div style="min-width:0;"><div style="font-size:10px;color:var(--text3);letter-spacing:1px;margin-bottom:6px;">VARIABLE ASSIGNMENT</div>'
    + '<div style="font-size:9px;color:var(--text3);margin-bottom:8px;">Assigning a variable to a block sets declarationMode=SymbolicBlock and blockId. Clearing it restores AddressMapped.</div>'
    + '<div style="max-height:410px;overflow:auto;"><table class="vt-table table-full"><thead><tr><th>Variable</th><th>Type</th><th>Block</th><th>Mode</th></tr></thead><tbody>' + (variableRows || '<tr><td colspan="4" style="color:var(--text3);padding:8px;">No variables.</td></tr>') + '</tbody></table></div></div>'
    + '</div>';
}

function gvtOpenPlcBlockManager(): void {
  if (typeof ensureProjectVariables === 'function') ensureProjectVariables();
  gvtEnsurePlcBlocks();
  const existing = document.getElementById('modal-plc-blocks');
  if (existing) existing.remove();
  const el = document.createElement('div');
  el.id = 'modal-plc-blocks';
  el.className = 'modal-bg show';
  el.innerHTML = '<div class="modal" style="min-width:900px;max-width:96vw;max-height:90vh;display:flex;flex-direction:column;padding:0;overflow:hidden;">'
    + '<div style="padding:12px 20px;background:var(--s3);border-bottom:1px solid var(--border);display:flex;align-items:center;gap:10px;"><span style="font-size:12px;letter-spacing:2px;font-family:\'Orbitron\',monospace;">PLC BLOCK MANAGER</span><span style="font-size:10px;color:var(--text3);">DB can be pushed via bridge; UDT generates XML for manual import.</span><span style="flex:1;"></span><button class="btn" onclick="closeModal(\'modal-plc-blocks\')" style="padding:2px 10px;">X</button></div>'
    + '<div id="gvt-block-manager-body" style="padding:14px 16px;overflow:auto;background:var(--bg);"></div>'
    + '<div style="padding:10px 16px;border-top:1px solid var(--border);display:flex;justify-content:flex-end;background:var(--s3);"><button class="btn a" onclick="gvtSyncPlcBlockMemberIds(); saveProject(); renderGlobalVarTable(); closeModal(\'modal-plc-blocks\')">Done</button></div>'
    + '</div>';
  document.body.appendChild(el);
  gvtRenderPlcBlockManager();
}

function renderGlobalVarTable(): void {
  const tbody = document.getElementById('gvt-tbody');
  if(!tbody) return;
  tbody.innerHTML = '';
  const entries = gvtGetEntries();
  const filter = ((document.getElementById('gvt-search') as HTMLInputElement | null)?.value||'').toLowerCase();
  const filtered = entries.filter(v=>
    !filter ||
    (v.label||'').toLowerCase().includes(filter) ||
    (v.format||'').toLowerCase().includes(filter)
  );

  const cnt = document.getElementById('gvt-count');
  if(cnt) cnt.textContent = entries.length+' item'+(entries.length!==1?'s':'');

  if(filtered.length===0){
    const tr=document.createElement('tr');
    tr.innerHTML=`<td colspan="4" class="vt-empty">${entries.length===0
      ?'Cha c  import t  CSV/Excel'
      :'No match for filter'}</td>`;
    tbody.appendChild(tr);
    return;
  }

  let lastGroup = '';
  filtered.forEach(function(entry){
    const v = entry.data as VuiProjectVariable;
    const sigList = entry.source === 'unit' ? gvtGetUnitSigList() : gvtGetSigList(v);
    const isExpanded = v._sigExpanded !== false;
    const groupName = (entry.bucket || entry.source) === 'user' ? 'User Variables' : 'Imported / Unit Devices';
    if(groupName !== lastGroup) {
      const groupTr = document.createElement('tr');
      groupTr.innerHTML = `<td colspan="4" style="padding:8px 12px;background:var(--s2);color:var(--amber);font-size:9px;letter-spacing:1.5px;font-family:'Orbitron',monospace;border-top:1px solid var(--border);">${groupName}</td>`;
      tbody.appendChild(groupTr);
      lastGroup = groupName;
    }

    //  Device header row 
    const tr=document.createElement('tr');
    tr.className = sigList.length ? 'vt-dev-instance' : '';

    // col 1: Delete button
    const tdDel=document.createElement('td');
    tdDel.className='vt-rownum';
    tdDel.innerHTML=`<button onclick="gvtDeleteVar('${entry.source}', '${String(entry.key).replace(/'/g, '\\&#39;')}')" title="Xa" style="background:none;border:none;color:#f87171;cursor:pointer;font-size:11px;padding:0 3px;line-height:1;">X</button>`;
    tr.appendChild(tdDel);

    // col 2: Label input
    const tdL=document.createElement('td');
    tdL.innerHTML=`<input class="vt-cell lbl" value="${esc2(v.label||'')}" onchange="gvtEditVar('${entry.source}','${entry.key}','label',this.value)">`;
    tr.appendChild(tdL);

    // col 3: Type — select box (primitive + struct/device)
    const tdF = document.createElement('td');
    tdF.appendChild(gvtMakeTypeSelect(entry, v));
    tr.appendChild(tdF);

    // col 4: Toggle expand / Address input (primitive)
    const tdTog=document.createElement('td');
    tdTog.style.cssText='padding:0 8px;font-size:9px;color:var(--cyan);cursor:pointer;user-select:none;';

    if(sigList.length) {
      // Có signals → nút toggle expand
      tdTog.innerHTML=`<span style="display:inline-flex;align-items:center;gap:4px;">
        <span>${isExpanded?'▼':'▶'}</span>
        <span style="opacity:.7;">${sigList.length} address${sigList.length!==1?'es':''}</span>
      </span>`;
      tdTog.addEventListener('click',function(){
        if((entry.source === 'imported' || entry.source === 'user') && project.variables && entry.bucket && project.variables[entry.bucket] && (project.variables[entry.bucket] as VuiProjectVariable[])[entry.key as number]) {
          (project.variables[entry.bucket] as VuiProjectVariable[])[entry.key as number]._sigExpanded = !isExpanded;
        } else if(entry.source === 'excel' && project.excelVars[entry.key as number]) {
          project.excelVars[entry.key as number]._sigExpanded = !isExpanded;
        } else if(entry.source === 'unit' && project.unitConfig && project.unitConfig[entry.key as string]) {
          project.unitConfig[entry.key as string]._sigExpanded = !isExpanded;
        }
        saveProject(); renderGlobalVarTable();
      });
    } else {
      // Khng c signals   nhp a ch trc tip
      // Dùng createElement để tránh re-render mất focus khi click
      tdTog.style.cssText = '';
      tdTog.onclick = null;

      const addrInput = document.createElement('input');
      addrInput.type = 'text';
      addrInput.className = 'vt-cell addr';
      addrInput.value = v.address || '';
      addrInput.placeholder = '%MX0.0';

      // Ch lu khi blur / Enter  khng re-render gia chng
      addrInput.addEventListener('change', function() {
        gvtEditVar(entry.source, String(entry.key), 'address', this.value);
      });
      // Ngn propagation khi ang g (trnh trigger event ngoi)
      addrInput.addEventListener('input', function(e) {
        e.stopPropagation();
        const hit = gvtResolveEntry(entry.source, String(entry.key));
        if(hit.item) {
          hit.item.address = this.value;
          saveProject();
        }
      });

      tdTog.appendChild(addrInput);
    }

    tr.appendChild(tdTog);
    tbody.appendChild(tr);

    //  Signal sub-rows (editable) 
    if(isExpanded && sigList.length>0){
      sigList.forEach(function(sig){
        const subTr=document.createElement('tr');
        subTr.className='vt-dev-signal-row';
        const vc=({Input:'vt-input',Output:'vt-output',Var:'vt-var'} as Record<string,string>)[sig.varType||'']||'vt-var';
        const vs=({Input:'IN',Output:'OUT',Var:'VAR'} as Record<string,string>)[sig.varType||'']||'VAR';
        const tc=({Bool:'sig-bool',Int:'sig-int',DInt:'sig-int',UInt:'sig-int',UDInt:'sig-int',Real:'sig-real',LReal:'sig-real',Word:'sig-word',DWord:'sig-word',Byte:'sig-word',String:'sig-word',Time:'sig-word'} as Record<string,string>)[sig.dataType||'Bool']||'sig-bool';

        // col 1: indent marker
        const tdSN=document.createElement('td');
        tdSN.innerHTML='<div class="vt-sig-num"></div>';
        subTr.appendChild(tdSN);

        // col 2: Label.SignalName
        const tdSLabel=document.createElement('td');
        tdSLabel.innerHTML=`<div class="vt-sig-label">
          <span class="vt-sig-indent"></span>
          <span class="vt-sig-name">${esc2(v.label||'?')}.${esc2(sig.name)}</span>
        </div>`;
        subTr.appendChild(tdSLabel);

        // col 3: Type badges
        const tdSType=document.createElement('td');
        tdSType.innerHTML=`<span class="sdcol-type ${tc}">${esc2(sig.dataType||'Bool')}</span>
          <span class="sdcol-io ${vc}" style="margin-left:3px;">${vs}</span>`;
        subTr.appendChild(tdSType);

        // col 4: Address input (signal sub-row)
        const tdSAddr=document.createElement('td');
        const addrInp=document.createElement('input');
        addrInp.type='text';
        addrInp.className='vt-cell addr vt-sig-addr';
        addrInp.value=entry.source === 'unit' ? gvtGetUnitAddr(v as unknown as VuiUnitConfig, sig.path!) : gvtGetExcelSignalAddress(v, sig);
        addrInp.placeholder=sig.varType==='Input'?'MR':sig.varType==='Output'?'LR':'MR';
        function commitSignalAddress(): void {
          if (!gvtSetSignalAddress(entry, sig, addrInp.value)) return;
          saveProject();
          if(typeof updateVarDatalist==='function') updateVarDatalist();
        }
        addrInp.addEventListener('input', function(e) {
          e.stopPropagation();
          commitSignalAddress();
        });
        addrInp.addEventListener('change', commitSignalAddress);
        tdSAddr.appendChild(addrInp);
        subTr.appendChild(tdSAddr);
        tbody.appendChild(subTr);
      });
    }
  });
}

function gvtResolveEntry(source: string, key: string | number): GrafcetStudioVars.ResolveEntryResult {
  return varsGetApi().gvtResolveEntry(varsGetContext(), source, key);
}

function gvtEditVar(source: string, key: string | number, field: string, value: unknown): void {
  const hit = varsGetApi().gvtEditVar(varsGetContext(), source, key, field, value);
  if(!hit.item) return;
  saveProject();
  renderGlobalVarTable();
  if(typeof updateVarDatalist==='function') updateVarDatalist();
}
function gvtAddUserVar(): void {
  if (typeof ensureProjectVariables === 'function') ensureProjectVariables();
  const label = 'UserVar_' + String(project.variables.user.length + 1).padStart(2, '0');
  const base: Partial<VuiProjectVariable> = { label: label, format: 'BOOL', dataType: 'BOOL', address: '', comment: '', source: 'manual' };
  project.variables.user.push(typeof normalizeVariableRecord === 'function' ? normalizeVariableRecord(base, 'user') : base as VuiProjectVariable);
  saveProject();
  renderGlobalVarTable();
  toast('Added user variable');
}

function gvtDeleteVar(source: string, key: string | number): void {
  if(source === 'imported' || source === 'user') {
    const hit = gvtResolveEntry(source, key);
    if(!hit.list || !hit.item) return;
    if(!confirm('Delete "'+(hit.item.label||'variable')+'"?')) return;
    hit.list.splice(hit.idx,1);
    saveProject();
    renderGlobalVarTable();
    if(typeof updateVarDatalist==='function') updateVarDatalist();
    return;
  }
  if(source === 'excel') {
    const idx = parseInt(String(key), 10);
    if(!project.excelVars||idx<0||idx>=project.excelVars.length) return;
    if(!confirm('Xa "'+project.excelVars[idx].label+'" khi Global Vars?')) return;
    project.excelVars.splice(idx,1);
  } else if(source === 'unit') {
    if(!project.unitConfig || !project.unitConfig[key as string]) return;
    if(!confirm('Xa unit "'+(project.unitConfig[key as string].label||key)+'" khi Global Vars?')) return;
    delete project.unitConfig[key as string];
  } else {
    return;
  }
  saveProject();
  renderGlobalVarTable();
}


// ─────────────────────────────────────────────
//  BOOT
// ─────────────────────────────────────────────
window.addEventListener('load', ()=>{
  document.body.classList.add('unified-vars');
  init();
  setTimeout(fitView, 200);
});
document.getElementById('modal-input')!.addEventListener('keydown', e=>{ if((e as KeyboardEvent).key==='Enter') confirmRename(); });

// Unit modal enter

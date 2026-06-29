function makeDiagItem(d) {
  const item = document.createElement('div');
  item.className = 'tree-item' + (d.id===activeDiagramId?' active':'');
  item.dataset.id = d.id; item.dataset.type = 'diagram';

  const MODE_COLORS = {Auto:'#39d353',Origin:'#f5a623',Manual:'#4fa3e3',Error:'#e35a4f',Drivers:'#a78bfa'};
  const modeColor = MODE_COLORS[d.mode]||'var(--text3)';
  const typeLbl = d.diagramType==='MacroStep'?'MS':'M';
  const typeColor = d.diagramType==='MacroStep'?'var(--blue)':'var(--amber)';

  item.innerHTML = `
    <span class="tree-item-mode-dot" style="background:${modeColor};box-shadow:0 0 5px ${modeColor};" title="${esc2(d.mode||'Program')}"></span>
    <span class="tree-item-name">${esc2(d.name)}</span>
    <span class="tree-item-type-badge" style="border-color:${typeColor};color:${typeColor};">${typeLbl}</span>
    <div class="tree-item-actions">
      <button class="tree-item-btn" onclick="openDiagPropsPanel('${d.id}');event.stopPropagation()" title="Properties">P</button>
      <button class="tree-item-btn del" onclick="removeDiagram('${d.id}',event)" title="Delete">-</button>
    </div>`;
  item.addEventListener('click', e=>{ if(!e.target.closest('.tree-item-btn')) openTab(d.id); });
  item.addEventListener('dblclick', ()=>openDiagPropsPanel(d.id));
  item.addEventListener('contextmenu', e=>{ e.preventDefault(); showTreeCtx(e, d.id, 'diagram'); });
  return item;
}

// -- Unit management --
let unitModalMode = null; // 'add' | 'rename:{id}'

function addUnit() {
  unitModalMode = 'add';
  document.getElementById('modal-unit-title').textContent = 'ADD UNIT';
  document.getElementById('modal-unit-name').value = treeGetApi().getNextUnitName(project);
  showModal('modal-unit');
  setTimeout(()=>document.getElementById('modal-unit-name').select(),60);
}
function renameUnit(id) {
  unitModalMode = 'rename:'+id;
  const u = (project.units||[]).find(x=>x.id===id);
  document.getElementById('modal-unit-title').textContent = 'RENAME UNIT';
  document.getElementById('modal-unit-name').value = u?.name||'';
  showModal('modal-unit');
}
function confirmUnit() {
  const val = document.getElementById('modal-unit-name').value.trim();
  if(!val) return;
  let result = null;
  if(unitModalMode==='add'){
    result = treeGetApi().addUnit(treeGetContext(), val);
    if (!result.ok) { alert(result.message || 'Unable to add unit.'); return; }
    saveProject(); renderTree();
    toast('Unit added: '+val);
  } else if(unitModalMode?.startsWith('rename:')){
    const id=unitModalMode.split(':')[1];
    result = treeGetApi().renameUnit(treeGetContext(), id, val);
    if (!result.ok) { alert(result.message || 'Unable to rename unit.'); return; }
    saveProject(); renderTree();
  }
  closeModal('modal-unit');
}
function removeUnit(id, e) {
  if(e) e.stopPropagation();
  const u=(project.units||[]).find(x=>x.id===id);
  const diagsIn=(project.diagrams||[]).filter(d=>d.unitId===id);
  if(!confirm(`Delete unit "${u?.name}"? ${diagsIn.length>0?diagsIn.length+' diagram(s) will be unassigned.':''}`)) return;
  const result = treeGetApi().removeUnit(treeGetContext(), id);
  if (!result.ok) { alert(result.message || 'Unable to delete unit.'); return; }
  saveProject(); renderTree();
}
function toggleUnitOpen(id) {
  const result = treeGetApi().toggleUnitOpen(treeGetContext(), id);
  if(result.ok){ saveProject(); renderTree(); }
}
function addDiagramInUnit(unitId, mode) {
  const result = treeGetApi().addDiagramInUnit(treeGetContext(), unitId, mode);
  saveDiagramData(result.diagram.id, result.emptyState, result.nextId, result.nextStepNum, result.viewX, result.viewY, result.viewScale);
  saveProject(); renderTree(); openTab(result.diagram.id);
}
function addDriverDiagram() {
  const id='diag-'+Date.now();
  project.diagrams.push({
    id, name:'Driver_Device', unitId:null,
    mode:'Drivers', diagramType:'Macro',
    machine:project.machineName||project.name, unit:'', description:''
  });
  saveDiagramData(id, {steps:[],transitions:[],parallels:[],connections:[],vars:[]}, 1, 1, 100, 80, 1);
  saveProject(); renderTree(); openTab(id);
}

// -- Inline Diagram Properties Panel --
let diagPropsId = null;
const MODE_CFG = {
  Auto:   {color:'#39d353', bg:'rgba(57,211,83,.12)'},
  Origin: {color:'#f5a623', bg:'rgba(245,166,35,.12)'},
  Manual: {color:'#4fa3e3', bg:'rgba(79,163,227,.12)'},
  Error:  {color:'#e35a4f', bg:'rgba(227,90,79,.12)'} 
};

function openDiagPropsPanel(id) {
  diagPropsId = id;
  const d = project.diagrams.find(x=>x.id===id);
  if(!d) return;

  // Populate unit dropdown
  const sel = document.getElementById('dp-unit');
  sel.innerHTML = '<option value="">- unassigned -</option>';
  (project.units||[]).forEach(u=>{
    const o = document.createElement('option');
    o.value = u.id; o.textContent = u.name;
    if(u.id===d.unitId) o.selected=true;
    sel.appendChild(o);
  });

  // Fill fields
  document.getElementById('dp-name').value = d.name||'';
  document.getElementById('dp-desc').value = d.description||'';
  document.getElementById('dp-machine').value = d.machine||project.machineName||'';

  if (typeof ensureFlowAddressConfig === 'function') ensureFlowAddressConfig(d, true);

  dpSetControlState(d.controlState || d.mode || 'Auto');
  dpSetMode(d.mode||'Auto');
  dpSetOrchestrator(!!(d.category === 'orchestrator'));
  dpRenderOrchestratorElements(d.orchestratorConfig);
  // Type chips
  dpSetType(d.diagramType||'Macro');
  dpSetAddressMode(d.addressMode||'bool');
  dpSetBoolAddressMode(d.boolAddressMode||'linear');
  document.getElementById('dp-base-mr').value = d.baseMr || 'MR100';
  document.getElementById('dp-active-word').value = d.activeWord || 'DM0';
  document.getElementById('dp-complete-word').value = d.completeWord || 'DM100';
  dpUpdateAddressFields();
  // Header badge
  dpUpdateBadge(d.mode||'Auto');
  document.getElementById('dp-title').textContent = d.name;
  // Code preview
  dpUpdateCodePreview(d);

  // Show panel, hide element props
  document.getElementById('dp-panel').classList.add('show');
  document.getElementById('props-area').style.display='none';
}

function closeDiagPropsPanel() {
  diagPropsId = null;
  document.getElementById('dp-panel').classList.remove('show');
  document.getElementById('props-area').style.display='block';
}

function dpSetMode(mode) {
  document.querySelectorAll('#dp-mode-chips .dp-chip').forEach(c=>{
    c.classList.toggle('active', c.dataset.mode===mode);
  });
  dpUpdateBadge(mode);
  dpLiveUpdate();
}
function dpSetType(type) {
  const mBtn = document.getElementById('dp-type-main');
  const sBtn = document.getElementById('dp-type-sub');
  mBtn.className = 'dp-type-chip' + (type==='Macro'||type==='Main'?' active-main':'');
  sBtn.className = 'dp-type-chip' + (type==='MacroStep'||type==='SubRoutine'?' active-sub':'');
  dpLiveUpdate();
}

function dpSetAddressMode(mode) {
  const normalized = String(mode || 'bool').toLowerCase() === 'word' ? 'word' : 'bool';
  document.querySelectorAll('#dp-address-mode-chips .dp-chip').forEach(c=>{
    c.classList.toggle('active', c.dataset.addressMode===normalized);
  });
  dpUpdateAddressFields();
  dpLiveUpdate();
}
function dpSetBoolAddressMode(mode) {
  const normalized = typeof normalizeBoolAddressMode === 'function' ? normalizeBoolAddressMode(mode) : (String(mode).toLowerCase()==='block'?'block':'linear');
  document.querySelectorAll('#dp-bool-mode-chips .dp-chip').forEach(c=>{
    c.classList.toggle('active', c.dataset.boolMode===normalized);
  });
  dpLiveUpdate();
}
function dpGetCurrentAddressMode() {
  const active = document.querySelector('#dp-address-mode-chips .dp-chip.active');
  return active ? active.dataset.addressMode : 'bool';
}
function dpGetCurrentBoolAddressMode() {
  const active = document.querySelector('#dp-bool-mode-chips .dp-chip.active');
  return active ? active.dataset.boolMode : 'linear';
}
function dpUpdateAddressFields() {
  const isWord = dpGetCurrentAddressMode() === 'word';
  const boolFields = document.getElementById('dp-bool-address-fields');
  const wordFields = document.getElementById('dp-word-address-fields');
  if (boolFields) boolFields.style.display = isWord ? 'none' : 'block';
  if (wordFields) wordFields.style.display = isWord ? 'block' : 'none';
}
function dpReadAddressConfig() {
  const mode = dpGetCurrentAddressMode();
  const baseMr = (document.getElementById('dp-base-mr')?.value || 'MR100').trim() || 'MR100';
  return {
    addressMode: mode,
    boolAddressMode: dpGetCurrentBoolAddressMode(),
    baseMr,
    activeWord: (document.getElementById('dp-active-word')?.value || 'DM0').trim() || 'DM0',
    completeWord: (document.getElementById('dp-complete-word')?.value || 'DM100').trim() || 'DM100'
  };
}
function dpValidateAddressConfig(config) {
  if (config.addressMode === 'bool') {
    const boolRe = /^[A-Za-z]+\d+$/;
    if (!boolRe.test(String(config.baseMr || '').trim())) {
      toast('Warning: Base address must look like MR100 or LR0');
      return false;
    }
  }
  if (config.addressMode === 'word') {
    const wordRe = /^[A-Za-z]+\d+$/;
    if (!wordRe.test(config.activeWord) || !wordRe.test(config.completeWord)) {
      toast('Warning: Word addresses must look like DM0 or DM100');
      return false;
    }
  }
  return true;
}

function dpSetControlState(value) {
  const select = document.getElementById('dp-control-state');
  if (select) select.value = value || 'Auto';
}
function dpGetCurrentControlState() {
  const select = document.getElementById('dp-control-state');
  return select ? (select.value || 'Auto') : 'Auto';
}
function dpSetOrchestrator(enabled) {
  const toggle = document.getElementById('dp-orchestrator-toggle');
  if (toggle) toggle.checked = !!enabled;
  dpUpdateOrchestratorUi();
}
function dpIsOrchestrator() {
  const toggle = document.getElementById('dp-orchestrator-toggle');
  return !!(toggle && toggle.checked);
}
function dpUpdateOrchestratorUi() {
  const area = document.getElementById('dp-orchestrator-area');
  if (area) area.style.display = dpIsOrchestrator() ? 'block' : 'none';
}
function dpBuildOrchestratorConfig() {
  const text = document.getElementById('dp-orchestrator-elements')?.value || '';
  const elements = text.split(/\r?\n/).map(line => line.trim()).filter(Boolean).map(type => ({ type, config: {} }));
  return { elements };
}
function dpRenderOrchestratorElements(config) {
  const area = document.getElementById('dp-orchestrator-elements');
  if (!area) return;
  const elements = (config && Array.isArray(config.elements)) ? config.elements : [];
  area.value = elements.map(el => el && el.type ? el.type : '').filter(Boolean).join('\n');
}
function dpUpdateBadge(mode) {
  const cfg = MODE_CFG[mode]||{color:'var(--text2)',bg:'var(--s3)'};
  const badge = document.getElementById('dp-mode-badge');
  badge.textContent = mode||'-';
  badge.style.color = cfg.color;
  badge.style.borderColor = cfg.color;
  badge.style.background = cfg.bg;
}
function dpGetCurrentMode() {
  const active = document.querySelector('#dp-mode-chips .dp-chip.active');
  return active ? active.dataset.mode : 'Auto';
}
function dpGetCurrentType() {
  return document.getElementById('dp-type-main').classList.contains('active-main') ? 'Macro' : 'MacroStep';
}

function dpLiveUpdate() {
  const mode = dpGetCurrentMode();
  dpUpdateBadge(mode);
  dpUpdateOrchestratorUi();
  // Build preview
  const machine = document.getElementById('dp-machine').value||'Machine';
  const unitSel = document.getElementById('dp-unit');
  const unitName = unitSel.selectedIndex>0 ? unitSel.options[unitSel.selectedIndex].text : (document.getElementById('dp-desc').value||'Unit');
  const type = dpGetCurrentType();
  const name = document.getElementById('dp-name').value||'GRAFCET';
  const desc = document.getElementById('dp-desc').value;
  const address = dpReadAddressConfig();
  const fake = {machine, unit:unitName, mode, controlState: dpGetCurrentControlState(), category: dpIsOrchestrator() ? 'orchestrator' : 'normal', orchestratorConfig: dpBuildOrchestratorConfig(), diagramType:type, name, description:desc, ...address};
  dpUpdateCodePreview(fake);
}
function dpUpdateCodePreview(d) {
  const el = document.getElementById('dp-codeprev');
  if(!el) return;
  const unit = d.unit || ((project.units||[]).find(u=>u.id===d.unitId)?.name)||'-';
  el.innerHTML = [
    ['machine', d.machine||project.machineName||'Machine'],
    ['unit',    unit],
    ['mode',    d.mode||'Auto'],
    ['type',    d.diagramType||'Macro'],
    ['address', d.addressMode === 'word'
      ? `${d.activeWord||'DM0'} / ${d.completeWord||'DM100'}`
      : `${d.baseMr || 'MR100'} (${d.boolAddressMode||'linear'})`],
    ['name',    d.name||'GRAFCET'],
  ].map(([k,v])=>`<span class="k">${k}</span>: <span class="v">${esc(v)}</span>`).join('\n');
}

function saveDiagPropsPanel() {
  if(!diagPropsId) return;
  const d = project.diagrams.find(x=>x.id===diagPropsId);
  if(!d) return;
  d.name = document.getElementById('dp-name').value.trim()||d.name;
  d.description = document.getElementById('dp-desc').value.trim();
  d.machine = document.getElementById('dp-machine').value.trim()||project.machineName;
  const unitSel = document.getElementById('dp-unit');
  d.unitId = unitSel.value||null;
  d.unit = unitSel.value ? (project.units.find(u=>u.id===unitSel.value)?.name||'') : '';
  d.mode = dpGetCurrentMode();
  d.controlState = dpGetCurrentControlState();
  d.category = dpIsOrchestrator() ? 'orchestrator' : 'normal';
  d.orchestratorConfig = dpIsOrchestrator() ? dpBuildOrchestratorConfig() : undefined;
  d.diagramType = dpGetCurrentType();
  const address = dpReadAddressConfig();
  if (!dpValidateAddressConfig(address)) return;
  d.addressMode = address.addressMode;
  d.boolAddressMode = address.boolAddressMode;
  d.baseMr = address.baseMr;
  d.activeWord = address.activeWord;
  d.completeWord = address.completeWord;
  saveProject(); renderTree(); renderTabs();
  document.getElementById('dp-title').textContent = d.name;
  dpUpdateCodePreview(d);
  toast('Properties saved');
}

function removeDiagram(id, e) {
  if (e) e.stopPropagation();
  const allDiags = project.diagrams.length;
  if (allDiags <= 1) { toast('Warning: Cannot delete last diagram'); return; }
  if (!confirm('Delete diagram "'+project.diagrams.find(d=>d.id===id)?.name+'"?')) return;
  deleteDiagramData(id);
  project.diagrams = project.diagrams.filter(d=>d.id!==id);
  openTabs = openTabs.filter(t=>t.id!==id);
  saveProject();
  if (activeDiagramId===id) {
    activeDiagramId=null;
    if (openTabs.length>0) openTab(openTabs[0].id);
    else if(project.diagrams.length>0) openTab(project.diagrams[0].id);
    else { renderTree(); renderTabs(); }
  } else { renderTree(); renderTabs(); }
}

// --- Tree context menu ---
let treeCtxTarget = null;
function showTreeCtx(e, id, type) {
  e.stopPropagation();
  treeCtxTarget = {id, type};
  const m = document.getElementById('tree-ctx');
  const isDiag = type==='diagram';
  document.getElementById('tctx-open').style.display = isDiag?'flex':'none';
  document.getElementById('tctx-dup').style.display  = isDiag?'flex':'none';
  m.style.display='block';
  const vw=window.innerWidth, vh=window.innerHeight;
  m.style.left=e.clientX+'px'; m.style.top=e.clientY+'px';
  requestAnimationFrame(()=>{
    const r=m.getBoundingClientRect();
    if(r.right>vw) m.style.left=(e.clientX-r.width)+'px';
    if(r.bottom>vh) m.style.top=(e.clientY-r.height)+'px';
  });
}
function hideTreeCtx(){
  document.getElementById('tree-ctx').style.display='none';
}
document.addEventListener('click', e=>{
  if(!e.target.closest('#tree-ctx')) hideTreeCtx();
});
function tctxOpen(){ if(treeCtxTarget?.type==='diagram') openTab(treeCtxTarget.id); hideTreeCtx(); }
function tctxRename(){ if(!treeCtxTarget) return; hideTreeCtx(); if(treeCtxTarget.type==='diagram') renameCurrentDiagram(treeCtxTarget.id); }
function tctxDup(){
  hideTreeCtx();
  if(!treeCtxTarget||treeCtxTarget.type!=='diagram') return;
  const d=project.diagrams.find(x=>x.id===treeCtxTarget.id); if(!d) return;
  const newId='diag-'+Date.now();
  const srcData=loadDiagramData(d.id);
  project.diagrams.push({id:newId, name:d.name+' Copy'});
  if(srcData) saveDiagramData(newId, JSON.parse(JSON.stringify(srcData.state)), srcData.nextId, srcData.nextStepNum, srcData.viewX, srcData.viewY, srcData.viewScale);
  saveProject(); renderTree();
  toast('Duplicated');
}
function tctxDel(){ hideTreeCtx(); if(!treeCtxTarget) return; if(treeCtxTarget.type==='diagram') removeDiagram(treeCtxTarget.id); }


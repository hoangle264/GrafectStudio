"use strict";

type TdDiagramMeta = GrafcetStudioProject.DiagramMeta;
type TdOrchestratorConfig = GrafcetStudioProject.OrchestratorConfig;
type TdAddressMode = GrafcetStudioProject.AddressMode;
type TdBoolAddressMode = GrafcetStudioProject.BoolAddressMode;

declare function normalizeBoolAddressMode(mode: unknown): string;

interface TdAddressConfig {
  addressMode: string;
  boolAddressMode: string;
  baseMr: string;
  activeWord: string;
  completeWord: string;
  activeWordTag: string;
  completeWordTag: string;
}

interface TdCodePreviewSource {
  machine?: string;
  unit?: string;
  unitId?: string;
  mode?: string;
  controlState?: string;
  diagramType?: string;
  name?: string;
  addressMode?: string;
  activeWordTag?: string;
  activeWord?: string;
  completeWordTag?: string;
  completeWord?: string;
  baseMr?: string | number;
  boolAddressMode?: string;
}

function makeDiagItem(d: TdDiagramMeta): HTMLElement {
  const item = document.createElement('div');
  item.className = 'tree-item' + (d.id===activeDiagramId?' active':'');
  item.dataset.id = d.id; item.dataset.type = 'diagram';

  const MODE_COLORS: Record<string, string> = {Auto:'#39d353',Origin:'#f5a623',Manual:'#4fa3e3',Error:'#e35a4f',Drivers:'#a78bfa'};
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
  item.addEventListener('click', e=>{ if(!(e.target as HTMLElement).closest('.tree-item-btn')) openTab(d.id); });
  item.addEventListener('dblclick', ()=>openDiagPropsPanel(d.id));
  item.addEventListener('contextmenu', e=>{ e.preventDefault(); showTreeCtx(e, d.id, 'diagram'); });
  return item;
}

// -- Unit management --
let unitModalMode: string | null = null; // 'add' | 'rename:{id}'

function addUnit(): void {
  unitModalMode = 'add';
  document.getElementById('modal-unit-title')!.textContent = 'ADD UNIT';
  (document.getElementById('modal-unit-name') as HTMLInputElement).value = treeGetApi().getNextUnitName(project);
  showModal('modal-unit');
  setTimeout(()=>(document.getElementById('modal-unit-name') as HTMLInputElement).select(),60);
}
function renameUnit(id: string): void {
  unitModalMode = 'rename:'+id;
  const u = (project.units||[]).find(x=>x.id===id);
  document.getElementById('modal-unit-title')!.textContent = 'RENAME UNIT';
  (document.getElementById('modal-unit-name') as HTMLInputElement).value = u?.name||'';
  showModal('modal-unit');
}
function confirmUnit(): void {
  const val = (document.getElementById('modal-unit-name') as HTMLInputElement).value.trim();
  if(!val) return;
  let result: GrafcetStudioTree.MutationResult<GrafcetStudioProject.Unit> | null = null;
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
function removeUnit(id: string, e?: Event): void {
  if(e) e.stopPropagation();
  const u=(project.units||[]).find(x=>x.id===id);
  const diagsIn=(project.diagrams||[]).filter(d=>d.unitId===id);
  if(!confirm(`Delete unit "${u?.name}"? ${diagsIn.length>0?diagsIn.length+' diagram(s) will be unassigned.':''}`)) return;
  const result = treeGetApi().removeUnit(treeGetContext(), id);
  if (!result.ok) { alert(result.message || 'Unable to delete unit.'); return; }
  saveProject(); renderTree();
}
function toggleUnitOpen(id: string): void {
  const result = treeGetApi().toggleUnitOpen(treeGetContext(), id);
  if(result.ok){ saveProject(); renderTree(); }
}
function addDiagramInUnit(unitId: string | null | undefined, mode: string): void {
  const result = treeGetApi().addDiagramInUnit(treeGetContext(), unitId, mode);
  saveDiagramData(result.diagram.id, result.emptyState, result.nextId, result.nextStepNum, result.viewX, result.viewY, result.viewScale);
  saveProject(); renderTree(); openTab(result.diagram.id);
}
function addDriverDiagram(): void {
  const id='diag-'+Date.now();
  project.diagrams.push({
    id, name:'Driver_Device', unitId:undefined,
    mode:'Drivers', diagramType:'Macro',
    machine:project.machineName||project.name, unit:'', description:''
  });
  saveDiagramData(id, {steps:[],transitions:[],connections:[]} as unknown as GrafcetStudioProject.DiagramState, 1, 1, 100, 80, 1);
  saveProject(); renderTree(); openTab(id);
}

// -- Inline Diagram Properties Panel --
let diagPropsId: string | null = null;
const MODE_CFG: Record<string, { color: string; bg: string }> = {
  Auto:   {color:'#39d353', bg:'rgba(57,211,83,.12)'},
  Origin: {color:'#f5a623', bg:'rgba(245,166,35,.12)'},
  Manual: {color:'#4fa3e3', bg:'rgba(79,163,227,.12)'},
  Error:  {color:'#e35a4f', bg:'rgba(227,90,79,.12)'}
};

function openDiagPropsPanel(id: string): void {
  diagPropsId = id;
  const d = project.diagrams.find(x=>x.id===id);
  if(!d) return;

  // Populate unit dropdown
  const sel = document.getElementById('dp-unit') as HTMLSelectElement;
  sel.innerHTML = '<option value="">- unassigned -</option>';
  (project.units||[]).forEach(u=>{
    const o = document.createElement('option');
    o.value = u.id; o.textContent = u.name;
    if(u.id===d.unitId) o.selected=true;
    sel.appendChild(o);
  });

  // Fill fields
  (document.getElementById('dp-name') as HTMLInputElement).value = d.name||'';
  (document.getElementById('dp-desc') as HTMLInputElement).value = d.description||'';
  (document.getElementById('dp-machine') as HTMLInputElement).value = d.machine||project.machineName||'';

  if (typeof ensureFlowAddressConfig === 'function') ensureFlowAddressConfig(d, true);

  dpSetControlState(d.controlState || d.mode || 'Auto');
  dpSetMode(d.mode||'Auto');
  dpSetOrchestrator(!!(d.category === 'orchestrator'));
  dpRenderOrchestratorElements(d.orchestratorConfig);
  // Type chips
  dpSetType(d.diagramType||'Macro');
  dpSetAddressMode(d.addressMode||'bool');
  dpSetBoolAddressMode(d.boolAddressMode||'linear');
  (document.getElementById('dp-base-mr') as HTMLInputElement).value = String(d.baseMr || 'MR100');
  (document.getElementById('dp-active-word') as HTMLInputElement).value = d.activeWordTag || d.activeWord || 'DM0';
  (document.getElementById('dp-complete-word') as HTMLInputElement).value = d.completeWordTag || d.completeWord || 'DM100';
  dpUpdateAddressFields();
  // Header badge
  dpUpdateBadge(d.mode||'Auto');
  document.getElementById('dp-title')!.textContent = d.name;
  // Code preview
  dpUpdateCodePreview(d);

  // Show panel, hide element props
  document.getElementById('dp-panel')!.classList.add('show');
  (document.getElementById('props-area') as HTMLElement).style.display='none';
}

function closeDiagPropsPanel(): void {
  diagPropsId = null;
  document.getElementById('dp-panel')!.classList.remove('show');
  (document.getElementById('props-area') as HTMLElement).style.display='block';
}

function dpSetMode(mode: string): void {
  document.querySelectorAll<HTMLElement>('#dp-mode-chips .dp-chip').forEach(c=>{
    c.classList.toggle('active', c.dataset.mode===mode);
  });
  dpUpdateBadge(mode);
  dpLiveUpdate();
}
function dpSetType(type: string): void {
  const mBtn = document.getElementById('dp-type-main')!;
  const sBtn = document.getElementById('dp-type-sub')!;
  mBtn.className = 'dp-type-chip' + (type==='Macro'||type==='Main'?' active-main':'');
  sBtn.className = 'dp-type-chip' + (type==='MacroStep'||type==='SubRoutine'?' active-sub':'');
  dpLiveUpdate();
}

function dpSetAddressMode(mode: string): void {
  const normalized = String(mode || 'bool').toLowerCase() === 'word' ? 'word' : 'bool';
  document.querySelectorAll<HTMLElement>('#dp-address-mode-chips .dp-chip').forEach(c=>{
    c.classList.toggle('active', c.dataset.addressMode===normalized);
  });
  dpUpdateAddressFields();
  dpLiveUpdate();
}
function dpSetBoolAddressMode(mode: string): void {
  const normalized = typeof normalizeBoolAddressMode === 'function' ? normalizeBoolAddressMode(mode) : (String(mode).toLowerCase()==='block'?'block':'linear');
  document.querySelectorAll<HTMLElement>('#dp-bool-mode-chips .dp-chip').forEach(c=>{
    c.classList.toggle('active', c.dataset.boolMode===normalized);
  });
  dpLiveUpdate();
}
function dpGetCurrentAddressMode(): string {
  const active = document.querySelector<HTMLElement>('#dp-address-mode-chips .dp-chip.active');
  return active ? active.dataset.addressMode || 'bool' : 'bool';
}
function dpGetCurrentBoolAddressMode(): string {
  const active = document.querySelector<HTMLElement>('#dp-bool-mode-chips .dp-chip.active');
  return active ? active.dataset.boolMode || 'linear' : 'linear';
}
function dpUpdateAddressFields(): void {
  const isWord = dpGetCurrentAddressMode() === 'word';
  const boolFields = document.getElementById('dp-bool-address-fields');
  const wordFields = document.getElementById('dp-word-address-fields');
  if (boolFields) boolFields.style.display = isWord ? 'none' : 'block';
  if (wordFields) wordFields.style.display = isWord ? 'block' : 'none';
}
function dpReadAddressConfig(): TdAddressConfig {
  const mode = dpGetCurrentAddressMode();
  const baseMr = ((document.getElementById('dp-base-mr') as HTMLInputElement | null)?.value || 'MR100').trim() || 'MR100';
  const activeWordInput = ((document.getElementById('dp-active-word') as HTMLInputElement | null)?.value || 'DM0').trim() || 'DM0';
  const completeWordInput = ((document.getElementById('dp-complete-word') as HTMLInputElement | null)?.value || 'DM100').trim() || 'DM100';
  const wordAddressRe = /^[A-Za-z]+\d+$/;
  return {
    addressMode: mode,
    boolAddressMode: dpGetCurrentBoolAddressMode(),
    baseMr,
    activeWord: wordAddressRe.test(activeWordInput) ? activeWordInput : 'DM0',
    completeWord: wordAddressRe.test(completeWordInput) ? completeWordInput : 'DM100',
    activeWordTag: wordAddressRe.test(activeWordInput) ? '' : activeWordInput,
    completeWordTag: wordAddressRe.test(completeWordInput) ? '' : completeWordInput
  };
}
function dpValidateAddressConfig(config: TdAddressConfig): boolean {
  if (config.addressMode === 'bool') {
    const boolRe = /^[A-Za-z]+\d+$/;
    if (!boolRe.test(String(config.baseMr || '').trim())) {
      toast('Warning: Base address must look like MR100 or LR0');
      return false;
    }
  }
  if (config.addressMode === 'word') {
    const hasActive = !!String(config.activeWordTag || config.activeWord || '').trim();
    const hasComplete = !!String(config.completeWordTag || config.completeWord || '').trim();
    if (!hasActive || !hasComplete) {
      toast('Warning: Enter an active and complete word source, for example DM0 or StepWord');
      return false;
    }
  }
  return true;
}

function dpSetControlState(value: string): void {
  const select = document.getElementById('dp-control-state') as HTMLSelectElement | null;
  if (select) select.value = value || 'Auto';
}
function dpGetCurrentControlState(): string {
  const select = document.getElementById('dp-control-state') as HTMLSelectElement | null;
  return select ? (select.value || 'Auto') : 'Auto';
}
function dpSetOrchestrator(enabled: boolean): void {
  const toggle = document.getElementById('dp-orchestrator-toggle') as HTMLInputElement | null;
  if (toggle) toggle.checked = !!enabled;
  dpUpdateOrchestratorUi();
}
function dpIsOrchestrator(): boolean {
  const toggle = document.getElementById('dp-orchestrator-toggle') as HTMLInputElement | null;
  return !!(toggle && toggle.checked);
}
function dpUpdateOrchestratorUi(): void {
  const area = document.getElementById('dp-orchestrator-area');
  if (area) area.style.display = dpIsOrchestrator() ? 'block' : 'none';
}
function dpBuildOrchestratorConfig(): TdOrchestratorConfig {
  const text = (document.getElementById('dp-orchestrator-elements') as HTMLTextAreaElement | null)?.value || '';
  const elements = text.split(/\r?\n/).map(line => line.trim()).filter(Boolean).map(type => ({ type, config: {} }));
  return { elements };
}
function dpRenderOrchestratorElements(config: TdOrchestratorConfig | null | undefined): void {
  const area = document.getElementById('dp-orchestrator-elements') as HTMLTextAreaElement | null;
  if (!area) return;
  const elements = (config && Array.isArray(config.elements)) ? config.elements : [];
  area.value = elements.map(el => el && el.type ? el.type : '').filter(Boolean).join('\n');
}
function dpUpdateBadge(mode: string): void {
  const cfg = MODE_CFG[mode]||{color:'var(--text2)',bg:'var(--s3)'};
  const badge = document.getElementById('dp-mode-badge') as HTMLElement;
  badge.textContent = mode||'-';
  badge.style.color = cfg.color;
  badge.style.borderColor = cfg.color;
  badge.style.background = cfg.bg;
}
function dpGetCurrentMode(): string {
  const active = document.querySelector<HTMLElement>('#dp-mode-chips .dp-chip.active');
  return active ? active.dataset.mode || 'Auto' : 'Auto';
}
function dpGetCurrentType(): string {
  return document.getElementById('dp-type-main')!.classList.contains('active-main') ? 'Macro' : 'MacroStep';
}

function dpLiveUpdate(): void {
  const mode = dpGetCurrentMode();
  dpUpdateBadge(mode);
  dpUpdateOrchestratorUi();
  // Build preview
  const machine = (document.getElementById('dp-machine') as HTMLInputElement).value||'Machine';
  const unitSel = document.getElementById('dp-unit') as HTMLSelectElement;
  const unitName = unitSel.selectedIndex>0 ? unitSel.options[unitSel.selectedIndex].text : ((document.getElementById('dp-desc') as HTMLInputElement).value||'Unit');
  const type = dpGetCurrentType();
  const name = (document.getElementById('dp-name') as HTMLInputElement).value||'GRAFCET';
  const desc = (document.getElementById('dp-desc') as HTMLInputElement).value;
  const address = dpReadAddressConfig();
  const fake: TdCodePreviewSource = {machine, unit:unitName, mode, controlState: dpGetCurrentControlState(), diagramType:type, name, ...address};
  dpUpdateCodePreview(fake);
}
function dpUpdateCodePreview(d: TdDiagramMeta | TdCodePreviewSource): void {
  const el = document.getElementById('dp-codeprev');
  if(!el) return;
  const unit = d.unit || ((project.units||[]).find(u=>u.id===d.unitId)?.name)||'-';
  el.innerHTML = [
    ['machine', d.machine||project.machineName||'Machine'],
    ['unit',    unit],
    ['mode',    d.mode||'Auto'],
    ['type',    d.diagramType||'Macro'],
    ['address', d.addressMode === 'word'
      ? `${d.activeWordTag || d.activeWord || 'DM0'} / ${d.completeWordTag || d.completeWord || 'DM100'}`
      : `${d.baseMr || 'MR100'} (${d.boolAddressMode||'linear'})`],
    ['name',    d.name||'GRAFCET'],
  ].map(([k,v])=>`<span class="k">${k}</span>: <span class="v">${esc(v as string)}</span>`).join('\n');
}

function saveDiagPropsPanel(): void {
  if(!diagPropsId) return;
  const d = project.diagrams.find(x=>x.id===diagPropsId);
  if(!d) return;
  d.name = (document.getElementById('dp-name') as HTMLInputElement).value.trim()||d.name;
  d.description = (document.getElementById('dp-desc') as HTMLInputElement).value.trim();
  d.machine = (document.getElementById('dp-machine') as HTMLInputElement).value.trim()||project.machineName;
  const unitSel = document.getElementById('dp-unit') as HTMLSelectElement;
  d.unitId = unitSel.value||undefined;
  d.unit = unitSel.value ? (project.units.find(u=>u.id===unitSel.value)?.name||'') : '';
  d.mode = dpGetCurrentMode();
  d.controlState = dpGetCurrentControlState();
  d.category = dpIsOrchestrator() ? 'orchestrator' : 'normal';
  d.orchestratorConfig = dpIsOrchestrator() ? dpBuildOrchestratorConfig() : undefined;
  d.diagramType = dpGetCurrentType();
  const address = dpReadAddressConfig();
  if (!dpValidateAddressConfig(address)) return;
  d.addressMode = address.addressMode as TdAddressMode;
  d.boolAddressMode = address.boolAddressMode as TdBoolAddressMode;
  d.baseMr = address.baseMr;
  d.activeWord = address.activeWord;
  d.completeWord = address.completeWord;
  d.activeWordTag = address.activeWordTag;
  d.completeWordTag = address.completeWordTag;
  saveProject(); renderTree(); renderTabs();
  document.getElementById('dp-title')!.textContent = d.name;
  dpUpdateCodePreview(d);
  toast('Properties saved');
}

function removeDiagram(id: string, e?: Event): void {
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
let treeCtxTarget: { id: string; type: string } | null = null;
function showTreeCtx(e: Event, id: string, type: string): void {
  e.stopPropagation();
  treeCtxTarget = {id, type};
  const m = document.getElementById('tree-ctx') as HTMLElement;
  const isDiag = type==='diagram';
  (document.getElementById('tctx-open') as HTMLElement).style.display = isDiag?'flex':'none';
  (document.getElementById('tctx-dup') as HTMLElement).style.display  = isDiag?'flex':'none';
  m.style.display='block';
  const vw=window.innerWidth, vh=window.innerHeight;
  const mouseEvent = e as MouseEvent;
  m.style.left=mouseEvent.clientX+'px'; m.style.top=mouseEvent.clientY+'px';
  requestAnimationFrame(()=>{
    const r=m.getBoundingClientRect();
    if(r.right>vw) m.style.left=(mouseEvent.clientX-r.width)+'px';
    if(r.bottom>vh) m.style.top=(mouseEvent.clientY-r.height)+'px';
  });
}
function hideTreeCtx(): void {
  (document.getElementById('tree-ctx') as HTMLElement).style.display='none';
}
document.addEventListener('click', e=>{
  if(!(e.target as HTMLElement).closest('#tree-ctx')) hideTreeCtx();
});
function tctxOpen(): void { if(treeCtxTarget?.type==='diagram') openTab(treeCtxTarget.id); hideTreeCtx(); }
function tctxRename(): void { if(!treeCtxTarget) return; hideTreeCtx(); if(treeCtxTarget.type==='diagram') renameCurrentDiagram(treeCtxTarget.id); }
function tctxDup(): void {
  hideTreeCtx();
  if(!treeCtxTarget||treeCtxTarget.type!=='diagram') return;
  const d=project.diagrams.find(x=>x.id===treeCtxTarget!.id); if(!d) return;
  const newId='diag-'+Date.now();
  const srcData=loadDiagramData(d.id);
  project.diagrams.push({id:newId, name:d.name+' Copy'} as TdDiagramMeta);
  if(srcData) saveDiagramData(newId, JSON.parse(JSON.stringify(srcData.state)), srcData.nextId, srcData.nextStepNum, srcData.viewX, srcData.viewY, srcData.viewScale);
  saveProject(); renderTree();
  toast('Duplicated');
}
function tctxDel(): void { hideTreeCtx(); if(!treeCtxTarget) return; if(treeCtxTarget.type==='diagram') removeDiagram(treeCtxTarget.id); }

"use strict";

// ===========================================================
//  RENDER TABS & TREE
// ===========================================================
function treeGetApi() {
  const api = window.GrafcetStudio && window.GrafcetStudio.tree;
  if (!api) throw new Error('GrafcetStudio tree bridge is not loaded');
  return api;
}

function treeGetContext() {
  return {
    project,
    now: () => Date.now(),
    findNextAvailableBaseMr: typeof findNextAvailableBaseMr === 'function' ? findNextAvailableBaseMr : undefined,
    syncVariableSignalAddressesFromDeviceTypes: typeof syncVariableSignalAddressesFromDeviceTypes === 'function' ? syncVariableSignalAddressesFromDeviceTypes : undefined
  };
}
function renderTabs() {
  const bar = document.getElementById('tabs-bar');
  bar.innerHTML = '';
  openTabs.forEach(t => {
    if(String(t.id).startsWith(STRUCT_TAB_PREFIX)) {
      const devId = String(t.id).slice(STRUCT_TAB_PREFIX.length);
      const dev = (project.devices||[]).find(d=>d.id===devId);
      const tab = document.createElement('div');
      tab.className = 'tab' + (activeDiagramId===t.id?' active':'');
      tab.dataset.id = t.id;
      tab.innerHTML = `<span class="tab-name">Structure: ${esc2(dev?.name||'Unknown')}</span><button class="tab-close" onclick="closeTab('${t.id}',event)">x</button>`;
      tab.addEventListener('click', e=>{ if(!e.target.classList.contains('tab-close')) openStructTab(devId); });
      bar.appendChild(tab);
      return;
    }
    if(t.id === IO_MAPPING_TAB_ID) {
      const tab = document.createElement('div');
      tab.className = 'tab' + (activeDiagramId===IO_MAPPING_TAB_ID?' active':'');
      tab.dataset.id = IO_MAPPING_TAB_ID;
      tab.innerHTML = `<span class="tab-name">IO Mapping</span><button class="tab-close" onclick="closeTab('${IO_MAPPING_TAB_ID}',event)">x</button>`;
      tab.addEventListener('click', e=>{ if(!e.target.classList.contains('tab-close')) openIOMappingTab(); });
      bar.appendChild(tab);
      return;
    }
    const diag = project.diagrams.find(d=>d.id===t.id);
    if (!diag) return;
    const tab = document.createElement('div');
    tab.className = 'tab' + (t.id===activeDiagramId?' active':'');
    tab.dataset.id = t.id;
    tab.innerHTML = `<span class="tab-name">${diag.name}</span>${diag.mode?`<span style="font-size:8px;color:var(--text3);margin-left:2px;">[${diag.mode}]</span>`:''}<button class="tab-close" onclick="closeTab('${t.id}',event)">x</button>`;
    tab.addEventListener('click', e=>{ if(!e.target.classList.contains('tab-close')) openTab(t.id); });
    tab.addEventListener('dblclick', e=>{ if(!e.target.classList.contains('tab-close')) renameCurrentDiagram(t.id); });
    bar.appendChild(tab);
  });
  const addBtn = document.createElement('button');
  addBtn.className = 'tab-new'; addBtn.textContent = '+'; addBtn.title = 'New Diagram';
  addBtn.onclick = ()=>addDiagram();
  bar.appendChild(addBtn);
}

function treeIcon(name, cls='') {
  const icons = {
    chevron: '<svg viewBox="0 0 24 24"><path d="M9 5l7 7-7 7"/></svg>',
    folder: '<svg viewBox="0 0 24 24"><path d="M5 19a2 2 0 01-2-2V7a2 2 0 012-2h4l2 2h6a2 2 0 012 2v8a2 2 0 01-2 2H5z"/></svg>',
    plc: '<svg viewBox="0 0 24 24"><path d="M9 3v2m6-2v2M9 19v2m6-2v2M5 9H3m2 6H3m18-6h-2m2 6h-2M7 19h10a2 2 0 002-2V7a2 2 0 00-2-2H7a2 2 0 00-2 2v10a2 2 0 002 2zM9 9h6v6H9V9z"/></svg>',
    variables: '<svg viewBox="0 0 24 24"><path d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414A1 1 0 0119 9.414V19a2 2 0 01-2 2z"/></svg>',
    io: '<svg viewBox="0 0 24 24"><path d="M4 7h16M4 17h16M7 4v6m10-6v6M9 14v6m6-6v6"/></svg>',
    structure: '<svg viewBox="0 0 24 24"><path d="M4 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2V6zM14 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2V6zM4 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2v-2zM14 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2v-2z"/></svg>',
    program: '<svg viewBox="0 0 24 24"><path d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z"/><path d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/></svg>'
  };
  return `<span class="tree-svg ${cls}">${icons[name] || icons.folder}</span>`;
}

function treeSectionOpen(key) {
  return localStorage.getItem('gf2-tree-section-open-' + key) !== '0';
}

function bindTreeSectionToggle(section, key) {
  const head = section.querySelector('.tree-section-head');
  const body = section.querySelector('.tree-section-body');
  const toggle = section.querySelector('.tree-unit-toggle');
  if (!head || !body || !toggle) return;
  head.addEventListener('click', e => {
    if (e.target.closest('button')) return;
    const hidden = body.classList.toggle('hidden');
    toggle.classList.toggle('closed', hidden);
    toggle.classList.toggle('open', !hidden);
    localStorage.setItem('gf2-tree-section-open-' + key, hidden ? '0' : '1');
  });
}

function renderTree() {
  const body = document.getElementById('tree-body');
  body.innerHTML = '';
  if (!project.units) project.units = [];
  if (!project.devices) project.devices = [];

  // Project root.
  const editProjectRow = document.createElement('div');
  editProjectRow.className = 'tree-machine';
  editProjectRow.innerHTML = `
    ${treeIcon('folder','root')}
    <span class="tree-machine-name">${esc2(project.name)}</span>
    <button class="tree-machine-edit" onclick="renameProject()" title="Edit project">+</button>`;
  body.appendChild(editProjectRow);

  // PLC branch.
  const plcWrap = document.createElement('div');
  plcWrap.className = 'tree-section tree-plc-section';
  const plcOpen = treeSectionOpen('plc');
  plcWrap.innerHTML = `
    <div class="tree-section-head">
      <span class="tree-unit-toggle ${plcOpen?'open':'closed'}">${treeIcon('chevron')}</span>
      ${treeIcon('folder','folder')}
      <span class="tree-section-name">PLC</span>
    </div>
    <div class="tree-section-body${plcOpen?'':' hidden'}">
      <div class="tree-leaf" onclick="openPlcConfigModal()">
        ${treeIcon('plc','plc')}
        <span class="tree-leaf-name">${esc2(project.plcConfig?.name || project.plcName || project.machineName || 'Model PLC')}</span>
      </div>
    </div>`;
  bindTreeSectionToggle(plcWrap, 'plc');
  body.appendChild(plcWrap);

  // Machine branch.
  const machineWrap = document.createElement('div');
  machineWrap.className = 'tree-section tree-machine-section';
  const machineOpen = treeSectionOpen('machine');
  machineWrap.innerHTML = `
    <div class="tree-section-head">
      <span class="tree-unit-toggle ${machineOpen?'open':'closed'}">${treeIcon('chevron')}</span>
      ${treeIcon('folder','folder')}
      <span class="tree-section-name">Machine</span>
    </div>`;
  const machineBody = document.createElement('div');
  machineBody.className = 'tree-section-body' + (machineOpen?'':' hidden');

  const varGroups = typeof ensureProjectVariables === 'function' ? ensureProjectVariables() : (project.variables || {imported:[], user:[]});
  const varsCount = ((varGroups.imported||[]).length) + ((varGroups.user||[]).length) + Object.keys(project.unitConfig||{}).length;
  const varsItem = document.createElement('div');
  varsItem.className = 'tree-leaf';
  varsItem.innerHTML = `${treeIcon('variables','vars')}<span class="tree-leaf-name">Global Variables</span><span class="tree-count-badge">${varsCount}</span>`;
  varsItem.addEventListener('click', ()=>openVarsTab());
  machineBody.appendChild(varsItem);

  const ioItem = document.createElement('div');
  ioItem.className = 'tree-leaf';
  ioItem.innerHTML = `${treeIcon('io','io')}<span class="tree-leaf-name">IO Mapping</span>`;
  ioItem.addEventListener('click', ()=>openIOMappingTab());
  machineBody.appendChild(ioItem);

  const devSection = makeDevicesSection();
  devSection.classList.add('tree-section-nested');
  machineBody.appendChild(devSection);

  machineWrap.appendChild(machineBody);
  bindTreeSectionToggle(machineWrap, 'machine');
  body.appendChild(machineWrap);

  // Units branch.
  const unitsWrap = document.createElement('div');
  unitsWrap.className = 'tree-section tree-units-section';
  const unitsOpen = treeSectionOpen('units');
  unitsWrap.innerHTML = `
    <div class="tree-section-head">
      <span class="tree-unit-toggle ${unitsOpen?'open':'closed'}">${treeIcon('chevron')}</span>
      ${treeIcon('folder','folder')}
      <span class="tree-section-name">Units</span>
      <button class="tree-mode-add" onclick="addUnit();event.stopPropagation()" title="Add unit">+</button>
    </div>`;
  const unitsBody = document.createElement('div');
  unitsBody.className = 'tree-section-body' + (unitsOpen?'':' hidden');
  project.units.forEach(u=>unitsBody.appendChild(makeUnitItem(u)));
  unitsWrap.appendChild(unitsBody);
  bindTreeSectionToggle(unitsWrap, 'units');
  body.appendChild(unitsWrap);

  const orphans = project.diagrams.filter(d=>!d.unitId && d.mode!=='Drivers');
  if(orphans.length){
    const orphanHead = document.createElement('div');
    orphanHead.style.cssText='padding:4px 8px;font-size:8px;color:var(--text3);letter-spacing:1px;border-top:1px solid var(--border);';
    orphanHead.textContent='- UNASSIGNED';
    body.appendChild(orphanHead);
    orphans.forEach(d=>body.appendChild(makeDiagItem(d)));
  }

  const projectNameEl = document.getElementById('project-name-display');
  if (projectNameEl) projectNameEl.textContent = project.name;
  updateAlignBtns();
}

function makeUnitItem(u) {
  const wrap = document.createElement('div');
  wrap.className = 'tree-unit'; wrap.dataset.unitId = u.id;
  const isOpen = u.open !== false;
  const diagsInUnit = project.diagrams.filter(d=>d.unitId===u.id);

  const head = document.createElement('div');
  head.className = 'tree-unit-head';
  head.innerHTML = `
    <span class="tree-unit-toggle ${isOpen?'open':'closed'}">${treeIcon('chevron')}</span>
    ${treeIcon('folder','unit')}
    <span class="tree-unit-name">${esc2(u.name)}</span>
    <div class="tree-unit-actions">
      <button class="tree-unit-btn" onclick="addDiagramInUnit('${u.id}','Auto');event.stopPropagation()" title="Add diagram">+</button>
      <button class="tree-unit-btn" onclick="renameUnit('${u.id}');event.stopPropagation()" title="Rename">+</button>
      <button class="tree-unit-btn del" onclick="removeUnit('${u.id}',event)" title="Delete">-</button>
    </div>`;
  head.addEventListener('click', ()=>toggleUnitOpen(u.id));

  const children = document.createElement('div');
  children.className = 'tree-unit-children' + (isOpen?'':' hidden');

  // New tree keeps programs directly under the unit; mode stays in Properties.
  if(!diagsInUnit.length){
    const empty = document.createElement('div');
    empty.style.cssText='padding:3px 8px 3px 34px;font-size:9px;color:var(--text3);font-style:italic;';
    empty.textContent='no programs';
    children.appendChild(empty);
  } else {
    diagsInUnit.forEach(d=>children.appendChild(makeDiagItem(d)));
  }

  wrap.appendChild(head); wrap.appendChild(children);
  return wrap;
}

function makeDriversSection() {
  const wrap = document.createElement('div');
  wrap.className = 'tree-drivers';
  const driverDiags = project.diagrams.filter(d=>d.mode==='Drivers');
  const isOpen = localStorage.getItem('gf2-drivers-open') !== '0';

  const head = document.createElement('div');
  head.className = 'tree-drivers-head mode-drivers';
  head.innerHTML = `
    <span class="tree-unit-toggle ${isOpen?'open':'closed'}">v</span>
    <span style="margin:0 4px;font-size:11px;">*</span>
    <span style="flex:1;font-size:9px;letter-spacing:1px;">ActiveDevices</span>
    <button class="tree-mode-add" onclick="addDriverDiagram();event.stopPropagation()" title="Add active device diagram">+</button>`;
  head.addEventListener('click', ()=>{
    const c=wrap.querySelector('.tree-drivers-body');
    const hidden=c.classList.toggle('hidden');
    localStorage.setItem('gf2-drivers-open', hidden?'0':'1');
  });

  const body2 = document.createElement('div');
  body2.className = 'tree-mode-children' + (isOpen?'':' hidden');
  body2.style.cssText='border-left:2px solid rgba(167,139,250,.3);margin-left:12px;';
  if(!driverDiags.length){
    const empty=document.createElement('div');
    empty.style.cssText='padding:3px 8px;font-size:9px;color:var(--text3);font-style:italic;';
    empty.textContent='no driver diagrams';
    body2.appendChild(empty);
  } else {
    driverDiags.forEach(d=>body2.appendChild(makeDiagItem(d)));
  }
  body2.className = 'tree-drivers-body' + (isOpen?'':' hidden');

  wrap.appendChild(head); wrap.appendChild(body2);
  return wrap;
}

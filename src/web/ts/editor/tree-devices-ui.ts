"use strict";
// ===========================================================
//  DEVICES - Class-based device type library (flat list)
//  Each device has: name, categoryId (type tag), signals[]
//  Signals: name | dataType | variableType (Input/Output/Var) | comment
//  NO address here - address assigned in Variable Table (instance)
// ===========================================================

type DevDeviceType = GrafcetStudioProject.DeviceType;
type DevDeviceSignal = GrafcetStudioProject.DeviceSignal;
type DevPlcConfig = GrafcetStudioProject.PlcConfig;

interface DevCategory {
  id: string;
  name: string;
  icon: string;
}

declare function treeIcon(name: string, cls?: string): string;
declare function showModal(id: string): void;

const DEV_BUILTIN_CATS: DevCategory[] = [
  { id:'cat-cylinder', name:'Cylinder',  icon:'O' },
  { id:'cat-motor',    name:'Motor',     icon:'*'  },
  { id:'cat-inverter', name:'Inverter',  icon:'I' },
  { id:'cat-servo',    name:'Servo',     icon:'S' },
  { id:'cat-step',     name:'Step Motor',icon:'R' },
  { id:'cat-other',    name:'Other',     icon:'+' },
];

function getDevCatById(catId: string | undefined): DevCategory {
  return DEV_BUILTIN_CATS.find(c=>c.id===catId) || {id:catId||'',name:catId||'',icon:'+'};
}

// -- Tree section ------------------------------------------
function makeDevicesSection(): HTMLElement {
  if(!project.devices) project.devices = [];
  const isOpen = localStorage.getItem('gf2-devices-open') !== '0';
  const wrap = document.createElement('div');
  wrap.className = 'tree-devices-section';

  const head = document.createElement('div');
  head.className = 'tree-devices-head';
  const totalTypes = (project.devices||[]).length;
  head.innerHTML = `
    <span class="tree-dev-toggle ${isOpen?'':'closed'}">${treeIcon('chevron')}</span>
    ${treeIcon('structure','structure')}
    <span class="tree-dev-title">Structure</span>
    <span class="tree-dev-count">${totalTypes}</span>
    <button class="tree-dev-add-btn" onclick="openDeviceTypeModal(null);event.stopPropagation()" title="Add Struct Data">+</button>`;

  const body = document.createElement('div');
  body.className = 'tree-devices-body' + (isOpen?'':' hidden');
  body.id = 'devices-body';

  head.addEventListener('click', ()=>{
    const h = body.classList.toggle('hidden');
    head.querySelector('.tree-dev-toggle')?.classList.toggle('closed', h);
    localStorage.setItem('gf2-devices-open', h?'0':'1');
  });

  renderDevicesList(body);
  wrap.appendChild(head);
  wrap.appendChild(body);
  return wrap;
}

function renderDevicesList(container?: HTMLElement | null): void {
  if(!container) container = document.getElementById('devices-body');
  if(!container) return;
  container.innerHTML = '';
  const devs = project.devices||[];
  if(!devs.length){
    const e=document.createElement('div');
    e.className='tree-dev-empty';
    e.style.cssText='padding:6px 12px;font-size:9px;color:var(--text3);font-style:italic;';
    e.textContent='no struct data defined';
    container.appendChild(e);
    return;
  }
  devs.forEach(dev => container!.appendChild(makeDevTypeRow(dev)));
}

function makeDevTypeRow(dev: DevDeviceType): HTMLElement {
  const isOpen = false;
  const cat = getDevCatById(dev.categoryId||'cat-other');
  const wrap = document.createElement('div');
  wrap.className = 'tree-dev-type';

  const head = document.createElement('div');
  head.className = 'tree-dev-type-head';
  head.innerHTML = `
    <span class="tree-dev-toggle ${isOpen?'':'closed'}">${treeIcon('chevron')}</span>
    <span class="tree-dev-dot"></span>
    <span class="tree-dev-type-name">${esc2(dev.name)}</span>
    <span class="tree-dev-type-tag" title="${esc2(cat.name)}">${esc2(cat.name)}</span>
    <span class="tree-dev-type-meta">${(dev.signals||[]).length} sig</span>
    <div class="tree-dev-type-acts">
      <button class="tree-dev-btn" onclick="openDeviceTypeModal('${dev.id}');event.stopPropagation()" title="Edit">+</button>
      <button class="tree-dev-btn del" onclick="removeDeviceType('${dev.id}',event)">-</button>
    </div>`;

  const children = document.createElement('div');
  children.className = 'tree-dev-sig-list' + (isOpen?'':' hidden');

  if(!(dev.signals||[]).length){
    const e=document.createElement('div');e.className='tree-dev-empty';e.textContent='no signals';
    children.appendChild(e);
  } else {
    const hdr=document.createElement('div');
    hdr.className='tree-dev-sig-hdr';
    hdr.innerHTML='<span class="sdcol-name">SIGNAL</span><span class="sdcol-type">TYPE</span><span class="sdcol-io">VAR</span><span class="sdcol-cmt">COMMENT</span>';
    children.appendChild(hdr);
    (dev.signals||[]).forEach(sig=>{
      const row=document.createElement('div');
      row.className='tree-dev-sig-row';
      const tc=({Bool:'sig-bool',Int:'sig-int',DInt:'sig-int',UInt:'sig-int',UDInt:'sig-int',Real:'sig-real',LReal:'sig-real',Word:'sig-word',DWord:'sig-word',Byte:'sig-word',String:'sig-word',Time:'sig-word'} as Record<string,string>)[sig.dataType]||'sig-bool';
      const vc=({Input:'vt-input',Output:'vt-output',Var:'vt-var'} as Record<string,string>)[sig.varType||'']||'vt-var';
      const vs=({Input:'IN',Output:'OUT',Var:'VAR'} as Record<string,string>)[sig.varType||'']||'VAR';
      row.innerHTML=`
        <span class="sdcol-name" title="${esc2(sig.name)}">${esc2(sig.name)}</span>
        <span class="sdcol-type ${tc}">${esc2(sig.dataType||'Bool')}</span>
        <span class="sdcol-io ${vc}">${vs}</span>
        <span class="sdcol-cmt" title="${esc2(sig.comment||'')}">${esc2(sig.comment||'')}</span>
        <button class="tree-dev-sig-del" onclick="removeDeviceSignal('${dev.id}','${sig.id}',event)" title="Remove">-</button>`;
      children.appendChild(row);
    });
  }

  head.addEventListener('click',()=>{
    if(typeof openStructTab === 'function') openStructTab(dev.id);
    else openDeviceTypeModal(dev.id);
  });

  // show/hide action buttons on hover
  const acts = head.querySelector<HTMLElement>('.tree-dev-type-acts');
  if(acts){ acts.style.opacity='0'; head.addEventListener('mouseenter',()=>acts.style.opacity='1'); head.addEventListener('mouseleave',()=>acts.style.opacity='0'); }

  wrap.appendChild(head); wrap.appendChild(children);
  return wrap;
}

// -- PLC configuration modal ---------------------------------
function openPlcConfigModal(): void {
  const cfg: DevPlcConfig = project.plcConfig || {};
  let el = document.getElementById('modal-plc-config');
  if (el) el.remove();
  el = document.createElement('div');
  el.id = 'modal-plc-config';
  el.className = 'modal-bg';
  el.style.cssText = 'align-items:center;justify-content:center;';

  const plcTypes = ['Siemens S7-1200', 'Siemens S7-1500', 'Modbus TCP', 'Omron', 'Mitsubishi'];
  const currentType = cfg.type || 'Siemens S7-1200';
  const plcOptions = plcTypes.map(t => `<option value="${esc2(t)}" ${currentType===t?'selected':''}>${esc2(t)}</option>`).join('');

  el.innerHTML = `
    <div class="modal" style="width:520px;min-width:360px;max-width:92vw;display:flex;flex-direction:column;padding:0;overflow:hidden;">
      <div style="padding:12px 20px 10px;background:var(--s3);border-bottom:1px solid var(--border);display:flex;align-items:center;gap:10px;flex-shrink:0;">
        <span style="font-size:15px;">${treeIcon('plc','plc')}</span>
        <span style="font-size:12px;letter-spacing:.3px;font-family:'Segoe UI',sans-serif;font-weight:600;">PLC Configuration</span>
      </div>
      <div style="padding:14px 20px;display:grid;grid-template-columns:1fr 1fr;gap:12px 14px;">
        <div style="grid-column:1 / -1;">
          <div class="dev-field-lbl">Name / Model PLC</div>
          <input id="plc-modal-name" type="text" placeholder="Model PLC" value="${esc2(cfg.name || project.plcName || project.machineName || 'Model PLC')}"
            style="width:100%;background:var(--bg);border:1px solid var(--border);color:var(--text);font-family:'Segoe UI',sans-serif;font-size:12px;padding:6px 8px;border-radius:3px;outline:none;margin-top:5px;">
        </div>
        <div style="grid-column:1 / -1;">
          <div class="dev-field-lbl">PLC</div>
          <select id="plc-modal-type" style="width:100%;background:var(--bg);border:1px solid var(--border);color:var(--text);font-family:'Segoe UI',sans-serif;font-size:12px;padding:6px 8px;border-radius:3px;outline:none;margin-top:5px;">
            ${plcOptions}
          </select>
        </div>
        <div>
          <div class="dev-field-lbl">IP Address</div>
          <input id="plc-modal-ip" type="text" placeholder="192.168.0.1" value="${esc2(cfg.ip || '')}"
            style="width:100%;background:var(--bg);border:1px solid var(--border);color:var(--text);font-family:'Segoe UI',sans-serif;font-size:12px;padding:6px 8px;border-radius:3px;outline:none;margin-top:5px;">
        </div>
        <div>
          <div class="dev-field-lbl">Port</div>
          <input id="plc-modal-port" type="number" min="0" placeholder="102" value="${esc2(cfg.port ?? '102')}"
            style="width:100%;background:var(--bg);border:1px solid var(--border);color:var(--text);font-family:'Segoe UI',sans-serif;font-size:12px;padding:6px 8px;border-radius:3px;outline:none;margin-top:5px;">
        </div>
        <div>
          <div class="dev-field-lbl">Rack</div>
          <input id="plc-modal-rack" type="number" min="0" placeholder="0" value="${esc2(cfg.rack ?? '0')}"
            style="width:100%;background:var(--bg);border:1px solid var(--border);color:var(--text);font-family:'Segoe UI',sans-serif;font-size:12px;padding:6px 8px;border-radius:3px;outline:none;margin-top:5px;">
        </div>
        <div>
          <div class="dev-field-lbl">Slot</div>
          <input id="plc-modal-slot" type="number" min="0" placeholder="1" value="${esc2(cfg.slot ?? '1')}"
            style="width:100%;background:var(--bg);border:1px solid var(--border);color:var(--text);font-family:'Segoe UI',sans-serif;font-size:12px;padding:6px 8px;border-radius:3px;outline:none;margin-top:5px;">
        </div>
        <div style="grid-column:1 / -1;">
          <div class="dev-field-lbl">Timeout</div>
          <input id="plc-modal-timeout" type="number" min="0" placeholder="3000" value="${esc2(cfg.timeout ?? '3000')}"
            style="width:100%;background:var(--bg);border:1px solid var(--border);color:var(--text);font-family:'Segoe UI',sans-serif;font-size:12px;padding:6px 8px;border-radius:3px;outline:none;margin-top:5px;">
        </div>
      </div>
      <div style="padding:0 20px 14px;display:flex;gap:8px;flex-wrap:wrap;">
        <button class="btn" onclick="plcConnect()">Connect</button>
        <button class="btn" onclick="plcDisconnect()">Disconnect</button>
        <button class="btn" onclick="plcPingTest()">Ping Test</button>
      </div>
      <div style="padding:10px 20px;border-top:1px solid var(--border);display:flex;gap:8px;justify-content:flex-end;flex-shrink:0;background:var(--s3);">
        <button class="btn" onclick="closeModal('modal-plc-config')">Cancel</button>
        <button class="btn a" onclick="confirmPlcConfig()">Save</button>
      </div>
    </div>`;
  document.body.appendChild(el);
  showModal('modal-plc-config');
  setTimeout(()=>document.getElementById('plc-modal-name')?.focus(),80);
}

function confirmPlcConfig(): void {
  const name = ((document.getElementById('plc-modal-name') as HTMLInputElement).value || '').trim() || 'Model PLC';
  project.plcConfig = {
    name,
    type: (document.getElementById('plc-modal-type') as HTMLSelectElement).value,
    ip: ((document.getElementById('plc-modal-ip') as HTMLInputElement).value || '').trim(),
    port: (document.getElementById('plc-modal-port') as HTMLInputElement).value,
    rack: (document.getElementById('plc-modal-rack') as HTMLInputElement).value,
    slot: (document.getElementById('plc-modal-slot') as HTMLInputElement).value,
    timeout: (document.getElementById('plc-modal-timeout') as HTMLInputElement).value
  };
  project.plcName = name;
  saveProject(); renderTree();
  closeModal('modal-plc-config');
  toast('PLC configuration saved');
}

function plcConnect(): void {
  toast('PLC connect command is ready');
}

function plcDisconnect(): void {
  toast('PLC disconnect command is ready');
}

function plcPingTest(): void {
  const ip = ((document.getElementById('plc-modal-ip') as HTMLInputElement | null)?.value || '').trim();
  toast(ip ? 'Ping Test: ' + ip : 'Ping Test: enter IP Address');
}

// -- Device type modal -------------------------------------
let _devModalDevId: string | null = null;

function openDeviceTypeModal(devId: string | null): void {
  _devModalDevId=devId;
  const dev=devId?(project.devices||[]).find(d=>d.id===devId):null;

  let el=document.getElementById('modal-device-type');
  if(el) el.remove();
  el=document.createElement('div');
  el.id='modal-device-type';
  el.className='modal-bg';
  el.style.cssText='align-items:center;justify-content:center;';

  const selCatId = dev?.categoryId || 'cat-cylinder';
  const typeOptions = DEV_BUILTIN_CATS.map(c=>
    `<option value="${c.id}" ${selCatId===c.id?'selected':''}>${c.icon} ${esc2(c.name)}</option>`
  ).join('');

  el.innerHTML=`
    <div class="modal" style="width:800px;min-width:430px;max-width:92vw;max-height:88vh;display:flex;flex-direction:column;padding:0;overflow:hidden;">
      <div style="padding:12px 20px 10px;background:var(--s3);border-bottom:1px solid var(--border);display:flex;align-items:center;gap:10px;flex-shrink:0;">
        <span style="font-size:15px;">Struct</span>
        <span style="font-size:12px;letter-spacing:.3px;font-family:'Segoe UI',sans-serif;font-weight:600;">${devId?'Edit':'New'} Struct Data</span>
      </div>
      <div style="padding:12px 20px 4px;display:flex;gap:20px;flex-shrink:0;flex-wrap:wrap;">
        <div style="flex:1;min-width:180px;">
          <div class="dev-field-lbl">Struct data name</div>
          <input id="dev-modal-name" type="text" placeholder="e.g. CylA, MotorConv..."
            style="width:100%;background:var(--bg);border:1px solid var(--border);color:var(--text);font-family:'Segoe UI',sans-serif;font-size:12px;padding:5px 8px;border-radius:3px;outline:none;margin-top:5px;">
        </div>
        <div style="flex:0 0 180px;">
          <div class="dev-field-lbl">Type</div>
          <select id="dev-modal-cat" style="width:100%;background:var(--bg);border:1px solid var(--border);color:var(--text);font-family:'Segoe UI',sans-serif;font-size:12px;padding:5px 8px;border-radius:3px;outline:none;margin-top:5px;">
            ${typeOptions}
          </select>
        </div>
      </div>
      <div style="padding:4px 20px;flex-shrink:0;display:flex;align-items:center;justify-content:space-between;">
        <span style="font-size:11px;letter-spacing:.3px;color:var(--cyan);font-family:'Segoe UI',sans-serif;font-weight:600;">Signals</span>
        <button class="btn" style="border-color:var(--cyan);color:var(--cyan);font-size:9px;" onclick="devModalAddRow()">+ Add Signal</button>
      </div>
      <div style="flex:1;overflow:auto;padding:0 20px 8px;">
        <table class="dev-sig-table">
          <thead><tr>
            <th style="width:140px;">Signal name</th>
            <th style="width:67px;">Data type</th>
            <th style="width:80px;">Variable type</th>
            <th style="width:110px;">Address</th>
            <th>Comment</th>
            <th style="width:22px;"></th>
          </tr></thead>
          <tbody id="dev-modal-tbody"></tbody>
        </table>
      </div>
      <div style="padding:10px 20px;border-top:1px solid var(--border);display:flex;gap:8px;justify-content:flex-end;flex-shrink:0;background:var(--s3);">
        <button class="btn" onclick="closeModal('modal-device-type')">Cancel</button>
        <button class="btn a" onclick="confirmDeviceType()">Save</button>
      </div>
    </div>`;
  document.body.appendChild(el);

  (document.getElementById('dev-modal-name') as HTMLInputElement).value = dev?.name||'';

  const sigs = dev?.signals||[];
  if(sigs.length) sigs.forEach(s=>devModalAddRow(s));
  else { devModalAddRow(); devModalAddRow(); }

  showModal('modal-device-type');
  setTimeout(()=>document.getElementById('dev-modal-name')?.focus(),80);
}

function devModalAddRow(sig?: DevDeviceSignal): void {
  const tbody=document.getElementById('dev-modal-tbody');
  if(!tbody) return;
  const sid=sig?.id||'';
  const tr=document.createElement('tr');
  tr.dataset.sigId=sid;
  tr.innerHTML=`
    <td><input class="dev-sig-input" placeholder="LSL" value="${esc2(sig?.name||'')}" data-f="name"></td>
    <td>
      <select class="dev-sig-select" data-f="dataType">
        ${['Bool','Int','DInt','UInt','UDInt','Real','LReal','Word','DWord','Byte','String','Time'].map(t=>`<option value="${t}" ${(sig?.dataType||'Bool')===t?'selected':''}>${t}</option>`).join('')}
      </select>
    </td>
    <td>
      <select class="dev-sig-select" data-f="varType" style="color:var(--text);">
        <option value="Input"  ${(sig?.varType||'Input')==='Input' ?'selected':''}>Input</option>
        <option value="Output" ${(sig?.varType||'')==='Output'?'selected':''}>Output</option>
        <option value="Var"    ${(sig?.varType||'')==='Var'   ?'selected':''}>Var</option>
      </select>
    </td>
    <td><input class="dev-sig-input" placeholder="%IX0.0 / %QX0.0" value="${esc2(sig?.address||'')}" data-f="address" style="color:var(--amber);"></td>
    <td><input class="dev-sig-input" placeholder="e.g. Lower limit sensor" value="${esc2(sig?.comment||'')}" data-f="comment" style="color:var(--text2);"></td>
    <td><button class="dev-del-row" onclick="this.closest('tr').remove()">-</button></td>`;
  tbody.appendChild(tr);
}

function confirmDeviceType(): void {
  const name=((document.getElementById('dev-modal-name') as HTMLInputElement).value||'').trim();
  if(!name){alert('Please enter a struct data name.');return;}
  const catId=(document.getElementById('dev-modal-cat') as HTMLSelectElement).value;
  const signals=Array.from(document.getElementById('dev-modal-tbody')!.querySelectorAll('tr')).map(tr=>({
    id: String(tr.dataset.sigId || '').trim(),
    name: (tr.querySelector('[data-f="name"]') as HTMLInputElement).value.trim(),
    dataType: (tr.querySelector('[data-f="dataType"]') as HTMLSelectElement).value,
    varType: (tr.querySelector('[data-f="varType"]') as HTMLSelectElement).value,
    address: (tr.querySelector('[data-f="address"]') as HTMLInputElement | null)?.value.trim() || '',
    comment: (tr.querySelector('[data-f="comment"]') as HTMLInputElement).value.trim()
  }));
  const result = treeGetApi().confirmDeviceType(treeGetContext(), {
    modalDeviceId: _devModalDevId,
    name,
    categoryId: catId,
    signals
  });
  if (!result.ok) { alert(result.message || 'Unable to save struct data.'); return; }
  saveProject(); renderTree();
  closeModal('modal-device-type');
  toast('Struct Data: '+name);
}
function removeDeviceType(devId: string, e?: Event): void {
  if(e)e.stopPropagation();
  const d=(project.devices||[]).find(x=>x.id===devId);
  if(!confirm(`Delete struct data "${d?.name}"?`)) return;
  const result = treeGetApi().removeDeviceType(treeGetContext(), devId);
  if (!result.ok) { alert(result.message || 'Unable to delete struct data.'); return; }
  saveProject(); renderTree();
}
function removeDeviceSignal(devId: string, sigId: string, e?: Event): void {
  if(e)e.stopPropagation();
  const d=(project.devices||[]).find(x=>x.id===devId);
  if(!d)return;
  d.signals=(d.signals||[]).filter(s=>s.id!==sigId);
  saveProject(); renderTree();
}

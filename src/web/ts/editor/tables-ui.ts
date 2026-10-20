"use strict";

type TuiDiagramState = GrafcetStudioProject.DiagramState;
type TuiStepAction = GrafcetStudioProject.StepAction;

declare function flushState(): void;

interface TuiParallelBar {
  id: string;
  type?: string;
  ports?: number;
  width?: number;
  x: number;
  [key: string]: unknown;
}

function tablesGetApi(): GrafcetStudioTables.TablesApi {
  const api = window.GrafcetStudio && window.GrafcetStudio.tables as GrafcetStudioTables.TablesApi | undefined;
  if (!api) throw new Error('GrafcetStudio tables bridge is not loaded');
  return api;
}

function tablesGetContext(): GrafcetStudioTables.TablesContext {
  return {
    project,
    resolveStepsThrough: resolveStepsThrough as unknown as GrafcetStudioTables.TablesContext['resolveStepsThrough'],
    getStepActionsStatic
  };
}
// ─────────────────────────────────────────────
//  EXPORT TABLES
// ─────────────────────────────────────────────
let etCurrentTab = 'steps';

function showExportTablesModal(): void {
  if(!activeDiagramId){ toast('No active diagram'); return; }
  flushState(); // ensure vars and all state are written to localStorage first
  const diagName = project.diagrams.find(d=>d.id===activeDiagramId)?.name||'Diagram';
  document.getElementById('et-diag-name')!.textContent = project.name + ' > ' + diagName;
  document.getElementById('modal-tables')!.classList.add('show');
  etShowTab(etCurrentTab);
}

function etShowTab(tab: string): void {
  etCurrentTab = tab;
  document.querySelectorAll('.et-tab').forEach(t=>t.classList.remove('active'));
  const btn = document.getElementById('et-tab-'+tab);
  if(btn) btn.classList.add('active');
  const content = document.getElementById('et-content')!;
  // Use global state directly (already flushed) — no localStorage roundtrip needed
  const s = state;
  if(tab==='steps')       content.innerHTML = buildStepTable(s);
  if(tab==='transitions') content.innerHTML = buildTransTable(s);
  if(tab==='branches')    content.innerHTML = buildBranchTable(s);
  if(tab==='vars')        content.innerHTML = buildVarsTable(s);
}

// ── Helpers ──
const ET_QUALIFIERS = ['N','S','R','P','P0','L','D','SD','DS','SL'];
/** CSS class for a qualifier badge (colors live in et-q-* rules). */
function etQualClass(q: string): string {
  return 'et-q-' + (ET_QUALIFIERS.indexOf(q) >= 0 ? q : 'x');
}
const ET_DASH = '<span class="et-c-text3">-</span>';
// ── Step Table ──
function buildStepTable(s: TuiDiagramState): string {
  const data = tablesGetApi().buildStepTableData(tablesGetContext(), s);
  if(!data.rows.length) return '<div class="et-empty">No steps in this diagram</div>';
  const stats = `
    <span class="et-stat"><b class="et-c-blue">${data.stats.steps}</b> Steps</span>
    <span class="et-stat"><b class="et-c-amber">${data.stats.initial}</b> Initial</span>
    <span class="et-stat"><b class="et-c-green">${data.stats.actions}</b> Actions total</span>`;
  const rows = data.rows.map(row=>{
    const acts = row.actions.length ? row.actions.map(a=>{
      const q=a.qualifier||'N';
      const vdisp = a.variable||(a.address?a.address:'');
      const addrPart = a.address&&a.address!==a.variable ? `<span class="et-c-text3 et-sm"> @${esc(a.address)}</span>` : '';
      const timePart = a.time ? `<span class="et-c-cyan et-sm"> ${esc(a.time)}</span>` : '';
      return `<div class="et-act-row"><span class="et-badge ${etQualClass(q)}">${esc(q)}</span><span class="et-c-text">${esc(vdisp)}</span>${addrPart}${timePart}</div>`;
    }).join('') : '<span class="et-c-text3 et-sm">-</span>';
    const initBadge = row.initial
      ? '<span class="et-badge et-badge-initial">INITIAL</span>'
      : ET_DASH;
    return `<tr><td class="et-c-blue et-b et-nw">S${row.numberText}</td><td class="et-nw">${esc(row.id)}</td><td class="et-c-text">${esc(row.label||'-')}</td><td>${initBadge}</td><td>${acts}</td></tr>`;
  }).join('');
  return `<div class="et-table-wrap"><div class="et-section-title">STEP TABLE - IEC 60848</div><div>${stats}</div><table class="et-table"><thead><tr><th>STEP NO</th><th>ID</th><th>NAME / LABEL</th><th>INITIAL</th><th>ACTIONS (Qualifier - Variable)</th></tr></thead><tbody>${rows}</tbody></table></div>`;
}
// ── Transition Table ──
function buildTransTable(s: TuiDiagramState): string {
  const data = tablesGetApi().buildTransitionTableData(tablesGetContext(), s);
  if(!data.stats.transitions) return '<div class="et-empty">No transitions in this diagram</div>';
  const stepLabel = (st: GrafcetStudioProject.Step | null) => st ? `<span class="et-c-blue et-b">S${String(st.number).padStart(2,'0')}</span>${st.label ? ` <span class="et-c-text2">${esc(st.label)}</span>` : ''}` : ET_DASH;
  let html = '';
  let prevTid: string | null = null;
  let pendingRows: GrafcetStudioTables.TransitionRow[] = [];
  function flushPending(): void {
    if(!pendingRows.length) return;
    pendingRows.forEach((r,i)=>{
      const condCell = r.condition ? `<span class="et-c-green et-mono">${esc(r.condition)}</span>` : '<span class="et-c-text3">- (always true)</span>';
      const labelCell = r.label ? esc(r.label) : ET_DASH;
      if(i===0) html+=`<tr><td rowspan="${pendingRows.length}" class="et-c-green et-b et-nw et-top">${esc(r.tid)}</td><td rowspan="${pendingRows.length}" class="et-nw et-top">${labelCell}</td><td>${stepLabel(r.fromStep)}</td><td>${stepLabel(r.toStep)}</td><td rowspan="${pendingRows.length}" class="et-top">${condCell}</td></tr>`;
      else html+=`<tr><td>${stepLabel(r.fromStep)}</td><td>${stepLabel(r.toStep)}</td></tr>`;
    });
    pendingRows=[];
  }
  data.rows.forEach(r=>{ if(r.tid!==prevTid){ flushPending(); prevTid=r.tid; } pendingRows.push(r); });
  flushPending();
  const stats = `<span class="et-stat"><b class="et-c-green">${data.stats.transitions}</b> Transitions</span><span class="et-stat"><b class="et-c-green">${data.stats.withCondition}</b> With condition</span><span class="et-stat"><b class="et-c-green">${data.stats.stepPairs}</b> Step pairs</span>`;
  return `<div class="et-table-wrap"><div class="et-section-title">TRANSITION TABLE - IEC 60848</div><div>${stats}</div><table class="et-table"><thead><tr><th>TRANS ID</th><th>LABEL</th><th>FROM STEP</th><th>TO STEP</th><th>CONDITION / RECEPTIVITY</th></tr></thead><tbody>${html}</tbody></table></div>`;
}
// ── Branch Table (Parallel) ──
function buildBranchTable(s: TuiDiagramState): string {
  const data = tablesGetApi().buildBranchTableData(tablesGetContext(), s);
  if(!data.rows.length) return '<div class="et-empty">No parallel branches in this diagram</div>';
  const seeBranches = '<span class="et-c-text3">see branches</span>';
  const rows = data.rows.map(row=>{
    const typeBadge = `<span class="et-badge et-badge-purple">${row.isSplit?'AND-SPLIT':'AND-JOIN'}</span>`;
    const singCell = row.singleTransitions.length
      ? row.singleTransitions.map(t=>`<span class="et-c-green">${esc(t.id)}</span>${t.condition?' <span class="et-c-text2 et-sm">['+esc(t.condition)+']</span>':''}`).join(', ')
      : ET_DASH;
    const branchCell = row.branchSteps.map((steps,i)=>{
      const label = steps.length ? steps.map(st=>`<b class="et-c-blue">S${String(st.number).padStart(2,'0')}</b>${st.label?' '+esc(st.label):''}`).join(', ') : ET_DASH;
      return `<div class="et-mb3"><span class="et-c-purple et-sm">B${i+1}</span> ${label}</div>`;
    }).join('');
    return `<tr><td class="et-c-purple et-b et-nw">${esc(row.id)}</td><td>${typeBadge}</td><td>${row.isSplit?singCell:seeBranches}</td><td>${branchCell}</td><td>${!row.isSplit?singCell:seeBranches}</td><td class="et-c-text2 et-sm">${row.descriptionParts.map(p=>esc(p)).join(' - ')}</td></tr>`;
  });
  const stats = `<span class="et-stat"><b class="et-c-purple">${data.stats.split}</b> AND-Split</span><span class="et-stat"><b class="et-c-purple">${data.stats.join}</b> AND-Join</span>`;
  return `<div class="et-table-wrap"><div class="et-section-title">PARALLEL BRANCH TABLE - IEC 60848</div><div>${stats}</div><table class="et-table"><thead><tr><th>BRANCH ID</th><th>TYPE</th><th>SPLIT TRANSITION</th><th>STEP BRANCHES (per branch)</th><th>JOIN TRANSITION</th><th>DESCRIPTION</th></tr></thead><tbody>${rows.join('')}</tbody></table></div>`;
}
// ── Variables Table ──
function buildVarsTable(s: TuiDiagramState): string {
  const data = tablesGetApi().buildVariablesTableData(tablesGetContext(), s);
  if(!data.rows.length) return '<div class="et-empty">No variables defined - use the Variable Table panel at the bottom</div>';
  let rows = '';
  data.rows.forEach(row=>{
    if(row.deviceType){
      rows += `<tr class="et-dev-instance"><td class="et-c-text2">${row.index}</td><td class="et-c-cyan et-b">${esc(row.label||'-')}</td><td class="et-c-cyan">${esc(row.format||'-')}</td><td class="et-c-text3 et-it et-sm">${row.signalRows.length} signal${row.signalRows.length!==1?'s':''}</td><td class="et-c-text2">${esc(row.comment||'-')}</td></tr>`;
      row.signalRows.forEach(sig=>{
        const vc = ({Input:'et-sig-in',Output:'et-sig-out',Var:'et-sig-var'} as Record<string,string>)[sig.varType]||'et-sig-var';
        const vs = ({Input:'IN',Output:'OUT',Var:'VAR'} as Record<string,string>)[sig.varType]||'VAR';
        rows += `<tr class="et-dev-signal"><td class="et-c-text3 et-sm et-center">-</td><td class="et-sig-name"><span class="et-sig-dash">-</span>${esc(sig.label)}<span class="${vc} et-sig-tag">${vs}</span></td><td class="et-c-text3 et-sm">${esc(sig.dataFormat||'Bool')}</td><td class="et-c-amber et-mono">${esc(sig.address||'-')}</td><td class="et-c-text3 et-sm">${esc(sig.comment||'-')}</td></tr>`;
      });
    } else {
      rows += `<tr><td class="et-c-text2">${row.index}</td><td class="et-c-text et-b">${esc(row.label||'-')}</td><td class="et-c-cyan">${esc(row.format||'-')}</td><td class="et-c-amber et-mono">${esc(row.address||'-')}</td><td class="et-c-text2">${esc(row.comment||'-')}</td></tr>`;
    }
  });
  const stats = `<span class="et-stat"><b class="et-c-cyan">${data.stats.variables}</b> Variables</span>` + (data.stats.deviceInstances ? `<span class="et-stat"><b class="et-c-cyan">${data.stats.deviceInstances}</b> Device instances</span>` : '');
  return `<div class="et-table-wrap"><div class="et-section-title">VARIABLE TABLE</div><div class="et-mb8">${stats}</div><table class="et-table"><thead><tr><th>#</th><th>LABEL / SIGNAL</th><th>DATA FORMAT</th><th>ADDRESS</th><th>COMMENT</th></tr></thead><tbody>${rows}</tbody></table></div>`;
}
// ── Export as HTML ──
function etExportHTML(): void {
  const s = state;  // use global state directly
  const diagName = project.diagrams.find(d=>d.id===activeDiagramId)?.name||'Diagram';
  const stepsHTML = buildStepTable(s);
  const transHTML = buildTransTable(s);
  const branchHTML = buildBranchTable(s);
  const varsHTML = buildVarsTable(s);
  const html = `<!DOCTYPE html>
<html lang="vi"><head><meta charset="UTF-8">
<title>GRAFCET Tables - ${diagName}</title>
<style>
*{margin:0;padding:0;box-sizing:border-box;}
body{background:#0b0d11;color:#c8d0e0;font-family:'Courier New',monospace;padding:30px;font-size:12px;}
h1{font-size:14px;letter-spacing:4px;color:#f5a623;margin-bottom:4px;}
.sub{font-size:10px;color:#3a4a6a;margin-bottom:30px;letter-spacing:2px;}
.section{margin-bottom:40px;}
.et-section-title{font-size:10px;letter-spacing:2px;color:#7a8aaa;margin-bottom:10px;padding-bottom:6px;border-bottom:1px solid #222d44;text-transform:uppercase;}
.et-stat{display:inline-flex;align-items:center;gap:6px;padding:3px 10px;background:#171d2c;border:1px solid #222d44;border-radius:3px;font-size:10px;color:#7a8aaa;margin-right:8px;margin-bottom:10px;}
table{width:100%;border-collapse:collapse;font-size:11px;}
thead th{background:#1d2438;color:#7a8aaa;font-size:9px;letter-spacing:1.5px;padding:7px 12px;text-align:left;border-bottom:2px solid #2d3d5a;border-right:1px solid #222d44;}
thead th:last-child{border-right:none;}
tbody td{padding:7px 12px;border-bottom:1px solid #1d2438;border-right:1px solid #1d2438;vertical-align:top;}
tbody td:last-child{border-right:none;}
tbody tr:nth-child(even) td{background:rgba(255,255,255,.02);}
tbody tr:hover td{background:rgba(79,163,227,.04);}
.et-badge{display:inline-block;padding:1px 6px;border-radius:3px;font-size:9px;font-weight:bold;margin-right:3px;border:1px solid;}
.et-act-row{display:flex;align-items:center;gap:6px;margin-bottom:3px;}
.et-act-row:last-child{margin-bottom:0;}
b.blue{color:#4fa3e3;} b.green{color:#39d353;} b.amber{color:#f5a623;} b.purple{color:#a78bfa;} b.cyan{color:#22d3ee;}
.et-dev-instance td{background:rgba(34,211,238,.06);border-left:2px solid #22d3ee;}
.et-dev-signal td{background:rgba(34,211,238,.02);font-size:10px;}
.et-sig-in{background:rgba(74,222,128,.12);color:#4ade80;border:1px solid rgba(74,222,128,.3);border-radius:2px;padding:1px 5px;font-size:9px;}
.et-sig-out{background:rgba(251,146,60,.12);color:#fb923c;border:1px solid rgba(251,146,60,.3);border-radius:2px;padding:1px 5px;font-size:9px;}
.et-sig-var{background:rgba(167,139,250,.12);color:#a78bfa;border:1px solid rgba(167,139,250,.3);border-radius:2px;padding:1px 5px;font-size:9px;}
.et-c-blue{color:#4fa3e3;}
.et-c-green{color:#39d353;}
.et-c-amber{color:#f5a623;}
.et-c-purple{color:#a78bfa;}
.et-c-cyan{color:#22d3ee;}
.et-c-text{color:#c8d0e0;}
.et-c-text2{color:#7a8aaa;}
.et-c-text3{color:#5a6a8a;}
.et-b{font-weight:bold;}
.et-nw{white-space:nowrap;}
.et-top{vertical-align:top;}
.et-mono{font-family:monospace;}
.et-sm{font-size:9px;}
.et-it{font-style:italic;}
.et-center{text-align:center;}
.et-mb3{margin-bottom:3px;}
.et-mb8{margin-bottom:8px;}
.et-badge-initial{color:#f5a623;border-color:#f5a623;background:rgba(245,166,35,.1);}
.et-badge-purple{color:#a78bfa;border-color:#a78bfa;background:rgba(167,139,250,.1);}
.et-q-N{color:#4fa3e3;border-color:#4fa3e3;background:#4fa3e318;}
.et-q-S{color:#39d353;border-color:#39d353;background:#39d35318;}
.et-q-R{color:#e35a4f;border-color:#e35a4f;background:#e35a4f18;}
.et-q-P{color:#f5a623;border-color:#f5a623;background:#f5a62318;}
.et-q-P0{color:#f5a623;border-color:#f5a623;background:#f5a62318;}
.et-q-L{color:#22d3ee;border-color:#22d3ee;background:#22d3ee18;}
.et-q-D{color:#a78bfa;border-color:#a78bfa;background:#a78bfa18;}
.et-q-SD{color:#39d353;border-color:#39d353;background:#39d35318;}
.et-q-DS{color:#a78bfa;border-color:#a78bfa;background:#a78bfa18;}
.et-q-SL{color:#22d3ee;border-color:#22d3ee;background:#22d3ee18;}
.et-q-x{color:#888;border-color:#888;background:#88818;}
.et-sig-name{padding-left:18px;color:#7a8aaa;}
.et-sig-dash{color:rgba(34,211,238,.4);margin-right:4px;}
.et-sig-tag{margin-left:6px;}
.foot{margin-top:20px;font-size:9px;color:#222d44;letter-spacing:2px;}
</style></head>
<body>
<h1>GRAFCET TABLES - ${diagName.toUpperCase()}</h1>
<div class="sub">IEC 60848 - ${project.name} - Generated ${new Date().toLocaleString('vi-VN')}</div>
<div class="section">${stepsHTML}</div>
<div class="section">${transHTML}</div>
<div class="section">${branchHTML}</div>
<div class="section">${varsHTML}</div>
<div class="foot">GENERATED BY GRAFCET STUDIO v2</div>
</body></html>`;
  const safeProj=project.name.replace(/\s+/g,'_');
  const safeDiag=diagName.replace(/\s+/g,'_');
  const blob=new Blob([html],{type:'text/html'});
  const a=document.createElement('a');a.href=URL.createObjectURL(blob);
  a.download=safeProj+'_'+safeDiag+'_tables.html';a.click();
  toast('Tables exported as HTML');
}

// ── Export as CSV (multi-sheet in one zip-like approach: separate files) ──
function etExportCSV(): void {
  const s = state;  // use global state directly
  const diagName = project.diagrams.find(d=>d.id===activeDiagramId)?.name||'Diagram';
  const safe = project.name.replace(/\s+/g,'_')+'_'+diagName.replace(/\s+/g,'_');

  // Steps CSV
  const stepsRows: (string | number)[][] = [['Step No','ID','Name/Label','Initial','Qualifier','Variable','Address','Time']];
  (s.steps||[]).slice().sort((a,b)=>(a.number||0)-(b.number||0)).forEach(step=>{
    const acts = getStepActionsStatic(step);
    if(!acts.length) stepsRows.push([String(step.number).padStart(2,'0'),step.id,step.label||'',step.initial?'YES':'NO','','','','']);
    else acts.forEach((act,ai)=>{
      stepsRows.push(ai===0?[String(step.number).padStart(2,'0'),step.id,step.label||'',step.initial?'YES':'NO',act.qualifier||'N',act.variable||'',act.address||'',act.time||'']
                            :['','','','',act.qualifier||'N',act.variable||'',act.address||'',act.time||'']);
    });
  });
  downloadCSV(stepsRows, safe+'_steps.csv');

  // Transitions CSV — one row per (transition × fromStep × toStep) pair
  const transRows: (string | number)[][] = [['Trans ID','Label','From Step ID','From Step Name','To Step ID','To Step Name','Condition']];
  (s.transitions||[]).forEach(t=>{
    const fromSteps = resolveStepsThrough(t.id, 'upstream',   s.connections||[], s.steps||[], (s as unknown as { parallels?: TuiParallelBar[] }).parallels||[]);
    const toSteps   = resolveStepsThrough(t.id, 'downstream', s.connections||[], s.steps||[], (s as unknown as { parallels?: TuiParallelBar[] }).parallels||[]);
    const pairs: Array<{ fs: GrafcetStudioProject.Step | null; ts: GrafcetStudioProject.Step | null }> = [];
    if(!fromSteps.length && !toSteps.length){
      pairs.push({fs:null,ts:null});
    } else if(!fromSteps.length){
      toSteps.forEach(ts=>pairs.push({fs:null,ts}));
    } else if(!toSteps.length){
      fromSteps.forEach(fs=>pairs.push({fs,ts:null}));
    } else {
      fromSteps.forEach(fs=>toSteps.forEach(ts=>pairs.push({fs,ts})));
    }
    pairs.forEach(({fs,ts})=>{
      transRows.push([
        t.id, t.label||'',
        fs?fs.id:'', fs?`S${String(fs.number).padStart(2,'0')}${fs.label?' '+fs.label:''}` :'',
        ts?ts.id:'', ts?`S${String(ts.number).padStart(2,'0')}${ts.label?' '+ts.label:''}` :'',
        t.condition||''
      ]);
    });
  });
  setTimeout(()=>downloadCSV(transRows, safe+'_transitions.csv'), 200);

  // Branches CSV
  const branchRows: (string | number)[][] = [['Branch ID','Type','Nr Branches','Width','Single Trans ID','Branch 1 Steps','Branch 2 Steps','Branch 3+']];
  ((s as unknown as { parallels?: TuiParallelBar[] }).parallels||[]).forEach(p=>{
    const ports=p.ports||3;
    const isSplit=p.type==='split';
    const singT=(s.connections||[]).filter(c=>isSplit?c.to===p.id:c.from===p.id).map(c=>isSplit?c.from:c.to).join('; ');
    const branches: string[] = [];
    for(let i=0;i<ports;i++){
      const bPort=isSplit?`bottom-${i}`:`top-${i}`;
      const bIds=(s.connections||[]).filter(c=>isSplit?c.from===p.id&&c.fromPort===bPort:c.to===p.id&&c.toPort===bPort).map(c=>isSplit?c.to:c.from).join(', ');
      branches.push(bIds||'-');
    }
    branchRows.push([p.id,String(p.type||'').toUpperCase(),ports,p.width||0,singT,branches[0]||'',branches[1]||'',branches.slice(2).join(' | ')||'']);
  });
  setTimeout(()=>downloadCSV(branchRows, safe+'_branches.csv'), 400);

  // Variables CSV — expand device type instances into per-signal rows
  const varRows: (string | number)[][] = [['#','Label','Data Format','Address','Variable Type','Comment']];
  let vRowNum=0;
  ((s as unknown as { vars?: GrafcetStudioProject.ProjectVariable[] }).vars||[]).forEach(v=>{
    vRowNum++;
    const devType=(project.devices||[]).find(d=>d.name===(v.format||''));
    if(devType){
      varRows.push([vRowNum, v.label||'', v.format||'', '', 'DEVICE', v.comment||'']);
      (devType.signals||[]).forEach(sig=>{
        const addr=(v.signalAddresses||{})[sig.id]||'';
        varRows.push(['-', (v.label||'?')+'.'+sig.name, sig.dataType||'Bool', addr, sig.varType||'', sig.comment||'']);
      });
    } else {
      varRows.push([vRowNum, v.label||'', v.format||'', v.address||'', '', v.comment||'']);
    }
  });
  setTimeout(()=>downloadCSV(varRows, safe+'_variables.csv'), 600);

  toast('4 CSV files downloading...');
}

function downloadCSV(rows: (string | number)[][], filename: string): void {
  const csv=rows.map(r=>r.map(c=>'"'+String(c).replace(/"/g,'""')+'"').join(',')).join('\r\n');
  const blob=new Blob(['﻿'+csv],{type:'text/csv;charset=utf-8'});
  const a=document.createElement('a');a.href=URL.createObjectURL(blob);a.download=filename;a.click();
}

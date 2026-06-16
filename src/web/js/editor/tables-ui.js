"use strict";

function tablesGetApi() {
  const api = window.GrafcetStudio && window.GrafcetStudio.tables;
  if (!api) throw new Error('GrafcetStudio tables bridge is not loaded');
  return api;
}

function tablesGetContext() {
  return {
    project,
    resolveStepsThrough,
    getStepActionsStatic
  };
}
// 
//  EXPORT TABLES
// 
let etCurrentTab = 'steps';

function showExportTablesModal() {
  if(!activeDiagramId){ toast('No active diagram'); return; }
  flushState(); // ensure vars and all state are written to localStorage first
  const diagName = project.diagrams.find(d=>d.id===activeDiagramId)?.name||'Diagram';
  document.getElementById('et-diag-name').textContent = project.name + ' > ' + diagName;
  document.getElementById('modal-tables').classList.add('show');
  etShowTab(etCurrentTab);
}

function etShowTab(tab) {
  etCurrentTab = tab;
  document.querySelectorAll('.et-tab').forEach(t=>t.classList.remove('active'));
  const btn = document.getElementById('et-tab-'+tab);
  if(btn) btn.classList.add('active');
  const content = document.getElementById('et-content');
  // Use global state directly (already flushed)  no localStorage roundtrip needed
  const s = state;
  if(tab==='steps')       content.innerHTML = buildStepTable(s);
  if(tab==='transitions') content.innerHTML = buildTransTable(s);
  if(tab==='branches')    content.innerHTML = buildBranchTable(s);
  if(tab==='vars')        content.innerHTML = buildVarsTable(s);
}

//  Helpers 
function etQualColor(q) {
  const map={N:'#4fa3e3',S:'#39d353',R:'#e35a4f',P:'#f5a623',P0:'#f5a623',L:'#22d3ee',D:'#a78bfa',SD:'#39d353',DS:'#a78bfa',SL:'#22d3ee'};
  return map[q]||'#888';
}
//  Step Table 
function buildStepTable(s) {
  const data = tablesGetApi().buildStepTableData(tablesGetContext(), s);
  if(!data.rows.length) return '<div class="et-empty">No steps in this diagram</div>';
  const stats = `
    <span class="et-stat"><b style="color:var(--blue)">${data.stats.steps}</b> Steps</span>
    <span class="et-stat"><b style="color:var(--amber)">${data.stats.initial}</b> Initial</span>
    <span class="et-stat"><b style="color:var(--green)">${data.stats.actions}</b> Actions total</span>`;
  const rows = data.rows.map(row=>{
    const acts = row.actions.length ? row.actions.map(a=>{
      const qc=etQualColor(a.qualifier||'N');
      const vdisp = a.variable||(a.address?a.address:'');
      const addrPart = a.address&&a.address!==a.variable ? `<span style="color:var(--text3);font-size:9px;"> @${esc(a.address)}</span>` : '';
      const timePart = a.time ? `<span style="color:#22d3ee;font-size:9px;"> ${esc(a.time)}</span>` : '';
      return `<div class="et-act-row"><span class="et-badge" style="color:${qc};border-color:${qc};background:${qc}18">${esc(a.qualifier||'N')}</span><span style="color:var(--text)">${esc(vdisp)}</span>${addrPart}${timePart}</div>`;
    }).join('') : '<span style="color:var(--text3);font-size:9px;">-</span>';
    const initBadge = row.initial
      ? '<span class="et-badge" style="color:var(--amber);border-color:var(--amber);background:rgba(245,166,35,.1)">INITIAL</span>'
      : '<span style="color:var(--text3)">-</span>';
    return `<tr><td style="color:var(--blue);font-weight:bold;white-space:nowrap;">S${row.numberText}</td><td style="white-space:nowrap;">${esc(row.id)}</td><td style="color:var(--text)">${esc(row.label||'-')}</td><td>${initBadge}</td><td>${acts}</td></tr>`;
  }).join('');
  return `<div class="et-table-wrap"><div class="et-section-title">STEP TABLE - IEC 60848</div><div>${stats}</div><table class="et-table"><thead><tr><th>STEP NO</th><th>ID</th><th>NAME / LABEL</th><th>INITIAL</th><th>ACTIONS (Qualifier - Variable)</th></tr></thead><tbody>${rows}</tbody></table></div>`;
}
// Transition Table 
function buildTransTable(s) {
  const data = tablesGetApi().buildTransitionTableData(tablesGetContext(), s);
  if(!data.stats.transitions) return '<div class="et-empty">No transitions in this diagram</div>';
  const stepLabel = st => st ? `<span style="color:var(--blue);font-weight:bold;">S${String(st.number).padStart(2,'0')}</span>${st.label ? ` <span style="color:var(--text2)">${esc(st.label)}</span>` : ''}` : '<span style="color:var(--text3)">-</span>';
  let html = '';
  let prevTid = null, pendingRows = [];
  function flushPending() {
    if(!pendingRows.length) return;
    pendingRows.forEach((r,i)=>{
      const condCell = r.condition ? `<span style="color:var(--green);font-family:monospace;">${esc(r.condition)}</span>` : '<span style="color:var(--text3)">- (always true)</span>';
      const labelCell = r.label ? esc(r.label) : '<span style="color:var(--text3)">-</span>';
      if(i===0) html+=`<tr><td rowspan="${pendingRows.length}" style="color:var(--green);font-weight:bold;white-space:nowrap;vertical-align:top;">${esc(r.tid)}</td><td rowspan="${pendingRows.length}" style="white-space:nowrap;vertical-align:top;">${labelCell}</td><td>${stepLabel(r.fromStep)}</td><td>${stepLabel(r.toStep)}</td><td rowspan="${pendingRows.length}" style="vertical-align:top;">${condCell}</td></tr>`;
      else html+=`<tr><td>${stepLabel(r.fromStep)}</td><td>${stepLabel(r.toStep)}</td></tr>`;
    });
    pendingRows=[];
  }
  data.rows.forEach(r=>{ if(r.tid!==prevTid){ flushPending(); prevTid=r.tid; } pendingRows.push(r); });
  flushPending();
  const stats = `<span class="et-stat"><b style="color:var(--green)">${data.stats.transitions}</b> Transitions</span><span class="et-stat"><b style="color:var(--green)">${data.stats.withCondition}</b> With condition</span><span class="et-stat"><b style="color:var(--green)">${data.stats.stepPairs}</b> Step pairs</span>`;
  return `<div class="et-table-wrap"><div class="et-section-title">TRANSITION TABLE - IEC 60848</div><div>${stats}</div><table class="et-table"><thead><tr><th>TRANS ID</th><th>LABEL</th><th>FROM STEP</th><th>TO STEP</th><th>CONDITION / RECEPTIVITY</th></tr></thead><tbody>${html}</tbody></table></div>`;
}
// Branch Table (Parallel) 
function buildBranchTable(s) {
  const data = tablesGetApi().buildBranchTableData(tablesGetContext(), s);
  if(!data.rows.length) return '<div class="et-empty">No parallel branches in this diagram</div>';
  const rows = data.rows.map(row=>{
    const typeBadge = `<span class="et-badge" style="color:var(--purple);border-color:var(--purple);background:rgba(167,139,250,.1)">${row.isSplit?'AND-SPLIT':'AND-JOIN'}</span>`;
    const singCell = row.singleTransitions.length
      ? row.singleTransitions.map(t=>`<span style="color:var(--green);">${esc(t.id)}</span>${t.condition?' <span style="color:var(--text2);font-size:9px;">['+esc(t.condition)+']</span>':''}`).join(', ')
      : '<span style="color:var(--text3)">-</span>';
    const branchCell = row.branchSteps.map((steps,i)=>{
      const label = steps.length ? steps.map(st=>`<b style="color:var(--blue)">S${String(st.number).padStart(2,'0')}</b>${st.label?' '+esc(st.label):''}`).join(', ') : '<span style="color:var(--text3)">-</span>';
      return `<div style="margin-bottom:3px;"><span style="color:var(--purple);font-size:9px;">B${i+1}</span> ${label}</div>`;
    }).join('');
    return `<tr><td style="color:var(--purple);font-weight:bold;white-space:nowrap;">${esc(row.id)}</td><td>${typeBadge}</td><td>${row.isSplit?singCell:'<span style="color:var(--text3)">see branches</span>'}</td><td>${branchCell}</td><td>${!row.isSplit?singCell:'<span style="color:var(--text3)">see branches</span>'}</td><td style="color:var(--text2);font-size:10px;">${row.descriptionParts.map(esc).join(' - ')}</td></tr>`;
  });
  const stats = `<span class="et-stat"><b style="color:var(--purple)">${data.stats.split}</b> AND-Split</span><span class="et-stat"><b style="color:var(--purple)">${data.stats.join}</b> AND-Join</span>`;
  return `<div class="et-table-wrap"><div class="et-section-title">PARALLEL BRANCH TABLE - IEC 60848</div><div>${stats}</div><table class="et-table"><thead><tr><th>BRANCH ID</th><th>TYPE</th><th>SPLIT TRANSITION</th><th>STEP BRANCHES (per branch)</th><th>JOIN TRANSITION</th><th>DESCRIPTION</th></tr></thead><tbody>${rows.join('')}</tbody></table></div>`;
}
// Variables Table 
function buildVarsTable(s) {
  const data = tablesGetApi().buildVariablesTableData(tablesGetContext(), s);
  if(!data.rows.length) return '<div class="et-empty">No variables defined - use the Variable Table panel at the bottom</div>';
  let rows = '';
  data.rows.forEach(row=>{
    if(row.deviceType){
      rows += `<tr class="et-dev-instance"><td style="color:var(--text2);">${row.index}</td><td style="color:var(--cyan);font-weight:bold;">${esc(row.label||'-')}</td><td style="color:var(--cyan);">${esc(row.format||'-')}</td><td style="color:var(--text3);font-style:italic;font-size:10px;">${row.signalRows.length} signal${row.signalRows.length!==1?'s':''}</td><td style="color:var(--text2);">${esc(row.comment||'-')}</td></tr>`;
      row.signalRows.forEach(sig=>{
        const vc = {Input:'et-sig-in',Output:'et-sig-out',Var:'et-sig-var'}[sig.varType]||'et-sig-var';
        const vs = {Input:'IN',Output:'OUT',Var:'VAR'}[sig.varType]||'VAR';
        rows += `<tr class="et-dev-signal"><td style="color:var(--text3);font-size:9px;text-align:center;">-</td><td style="padding-left:18px;color:var(--text2);"><span style="color:rgba(34,211,238,.4);margin-right:4px;">-</span>${esc(sig.label)}<span class="${vc}" style="margin-left:6px;font-size:9px;padding:1px 5px;border-radius:2px;">${vs}</span></td><td style="color:var(--text3);font-size:10px;">${esc(sig.dataFormat||'Bool')}</td><td style="color:var(--amber);font-family:monospace;">${esc(sig.address||'-')}</td><td style="color:var(--text3);font-size:10px;">${esc(sig.comment||'-')}</td></tr>`;
      });
    } else {
      rows += `<tr><td style="color:var(--text2);">${row.index}</td><td style="color:var(--text);font-weight:bold;">${esc(row.label||'-')}</td><td style="color:var(--cyan);">${esc(row.format||'-')}</td><td style="color:var(--amber);font-family:monospace;">${esc(row.address||'-')}</td><td style="color:var(--text2);">${esc(row.comment||'-')}</td></tr>`;
    }
  });
  const stats = `<span class="et-stat"><b style="color:var(--cyan)">${data.stats.variables}</b> Variables</span>` + (data.stats.deviceInstances ? `<span class="et-stat"><b style="color:var(--cyan)">${data.stats.deviceInstances}</b> Device instances</span>` : '');
  return `<div class="et-table-wrap"><div class="et-section-title">VARIABLE TABLE</div><div style="margin-bottom:8px;">${stats}</div><style>.et-dev-instance td{background:rgba(34,211,238,.05);}.et-dev-signal td{background:rgba(34,211,238,.02);font-size:10px;}.et-sig-in{background:rgba(74,222,128,.12);color:#4ade80;border:1px solid rgba(74,222,128,.3);}.et-sig-out{background:rgba(251,146,60,.12);color:#fb923c;border:1px solid rgba(251,146,60,.3);}.et-sig-var{background:rgba(167,139,250,.12);color:#a78bfa;border:1px solid rgba(167,139,250,.3);}</style><table class="et-table"><thead><tr><th>#</th><th>LABEL / SIGNAL</th><th>DATA FORMAT</th><th>ADDRESS</th><th>COMMENT</th></tr></thead><tbody>${rows}</tbody></table></div>`;
}
// Export as HTML 
function etExportHTML() {
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
</style></head>
<body>
<h1>GRAFCET TABLES - ${diagName.toUpperCase()}</h1>
<div class="sub">IEC 60848 - ${project.name} - Generated ${new Date().toLocaleString('vi-VN')}</div>
<div class="section">${stepsHTML}</div>
<div class="section">${transHTML}</div>
<div class="section">${branchHTML}</div>
<div class="section">${varsHTML}</div>
<div style="margin-top:20px;font-size:9px;color:#222d44;letter-spacing:2px;">GENERATED BY GRAFCET STUDIO v2</div>
</body></html>`;
  const safeProj=project.name.replace(/\s+/g,'_');
  const safeDiag=diagName.replace(/\s+/g,'_');
  const blob=new Blob([html],{type:'text/html'});
  const a=document.createElement('a');a.href=URL.createObjectURL(blob);
  a.download=safeProj+'_'+safeDiag+'_tables.html';a.click();
  toast('Tables exported as HTML');
}

//  Export as CSV (multi-sheet in one zip-like approach: separate files) 
function etExportCSV() {
  const s = state;  // use global state directly
  const diagName = project.diagrams.find(d=>d.id===activeDiagramId)?.name||'Diagram';
  const safe = project.name.replace(/\s+/g,'_')+'_'+diagName.replace(/\s+/g,'_');

  // Steps CSV
  const stepsRows = [['Step No','ID','Name/Label','Initial','Qualifier','Variable','Address','Time']];
  (s.steps||[]).slice().sort((a,b)=>a.number-b.number).forEach(step=>{
    const acts = getStepActionsStatic(step);
    if(!acts.length) stepsRows.push([String(step.number).padStart(2,'0'),step.id,step.label||'',step.initial?'YES':'NO','','','','']);
    else acts.forEach((act,ai)=>{
      stepsRows.push(ai===0?[String(step.number).padStart(2,'0'),step.id,step.label||'',step.initial?'YES':'NO',act.qualifier||'N',act.variable||'',act.address||'',act.time||'']
                            :['','','','',act.qualifier||'N',act.variable||'',act.address||'',act.time||'']);
    });
  });
  downloadCSV(stepsRows, safe+'_steps.csv');

  // Transitions CSV  one row per (transition  fromStep  toStep) pair
  const transRows = [['Trans ID','Label','From Step ID','From Step Name','To Step ID','To Step Name','Condition']];
  (s.transitions||[]).forEach(t=>{
    const fromSteps = resolveStepsThrough(t.id, 'upstream',   s.connections||[], s.steps||[], s.parallels||[]);
    const toSteps   = resolveStepsThrough(t.id, 'downstream', s.connections||[], s.steps||[], s.parallels||[]);
    const pairs = [];
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
  const branchRows = [['Branch ID','Type','Nr Branches','Width','Single Trans ID','Branch 1 Steps','Branch 2 Steps','Branch 3+']];
  (s.parallels||[]).forEach(p=>{
    const ports=p.ports||3;
    const isSplit=p.type==='split';
    const singT=(s.connections||[]).filter(c=>isSplit?c.to===p.id:c.from===p.id).map(c=>isSplit?c.from:c.to).join('; ');
    const branches=[];
    for(let i=0;i<ports;i++){
      const bPort=isSplit?`bottom-${i}`:`top-${i}`;
      const bIds=(s.connections||[]).filter(c=>isSplit?c.from===p.id&&c.fromPort===bPort:c.to===p.id&&c.toPort===bPort).map(c=>isSplit?c.to:c.from).join(', ');
      branches.push(bIds||'-');
    }
    branchRows.push([p.id,p.type.toUpperCase(),ports,p.width,singT,branches[0]||'',branches[1]||'',branches.slice(2).join(' | ')||'']);
  });
  setTimeout(()=>downloadCSV(branchRows, safe+'_branches.csv'), 400);

  // Variables CSV  expand device type instances into per-signal rows
  const varRows=[['#','Label','Data Format','Address','Variable Type','Comment']];
  let vRowNum=0;
  (s.vars||[]).forEach(v=>{
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

function downloadCSV(rows, filename) {
  const csv=rows.map(r=>r.map(c=>'"'+String(c).replace(/"/g,'""')+'"').join(',')).join('\r\n');
  const blob=new Blob(['\uFEFF'+csv],{type:'text/csv;charset=utf-8'});
  const a=document.createElement('a');a.href=URL.createObjectURL(blob);a.download=filename;a.click();
}





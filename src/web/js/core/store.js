"use strict";
// ═══════════════════════════════════════════════════════════
//  store.js — Grafcet Studio
//  Project state singleton + localStorage persistence.
//  Must be loaded BEFORE grafcet-studio-v2.js and grafcet-codegen.js.
//
//  NOTE: saveDiagramData / flushState reference runtime globals
//  (state, nextId, nextStepNum, viewX, viewY, viewScale) that are
//  declared in grafcet-studio-v2.js. This is intentional — those are
//  diagram-render globals and belong with the canvas layer. They are
//  only accessed at call-time (not parse-time), so load order is safe.
// ═══════════════════════════════════════════════════════════

// ── Project state ────────────────────────────────────────────
let project = {
  id:'proj-1',
  name:'My Project',
  machineName:'Machine',   // top-level machine name
  units:[],                // [{id, name, open}]
  diagrams:[],             // [{id, name, unitId, mode, diagramType, machine, unit}]
  devices:[],              // [{id, name, open, signals:[{id,name,dataType,ioType,address}]}]
  variables:{ imported:[], user:[] },
  excelVars:[],            // [{label, format, signalAddresses:{...}, comment, _source:'excel'}]
  unitConfig:{},           // {[unitLabel]: {label, unitIndex, originBaseAddr, autoBaseAddr, flags, io}}
  ioMapping:{ physicalIOs:[], entries:[] }
};
let openTabs = [];         // [{diagramId}]
let activeDiagramId = null;


// ── Flow address configuration ────────────────────────────────
const GF_ADDRESS_DEFAULT_BOOL_SPAN = 200;

function normalizeBoolAddressMode(mode) {
  const value = String(mode || '').trim().toLowerCase();
  return value === 'block' ? 'block' : 'linear';
}

function getFlowStepMaxNumber(diagId) {
  const data = loadDiagramData(diagId);
  const steps = (data && data.state && data.state.steps) || [];
  return steps.reduce((max, step) => Math.max(max, Number(step && step.number) || 0), 0);
}

function getBoolAddressRange(flow) {
  if (!flow || flow.addressMode !== 'bool') return null;
  const maxStepNumber = Math.max(1, getFlowStepMaxNumber(flow.id));
  const start = Number(flow.baseMr || 0);
  return { start, end: start + maxStepNumber * 2 - 1 };
}

function boolAddressRangesOverlap(a, b) {
  return !!a && !!b && a.start <= b.end && b.start <= a.end;
}

function findNextAvailableBaseMr(unitId, excludeDiagId) {
  let base = 100;
  while (base < 100000) {
    const candidate = { start: base, end: base + GF_ADDRESS_DEFAULT_BOOL_SPAN - 1 };
    const overlaps = (project.diagrams || []).some(function(diag) {
      if (!diag || diag.id === excludeDiagId) return false;
      if ((diag.unitId || null) !== (unitId || null)) return false;
      if ((diag.addressMode || 'bool') !== 'bool' || diag.baseMr === undefined || diag.baseMr === null || diag.baseMr === '') return false;
      return boolAddressRangesOverlap(candidate, getBoolAddressRange(diag));
    });
    if (!overlaps) return base;
    base += GF_ADDRESS_DEFAULT_BOOL_SPAN;
  }
  return base;
}

function ensureFlowAddressConfig(diag, assignUniqueBase) {
  if (!diag) return false;
  let changed = false;
  if (!diag.addressMode) {
    diag.addressMode = 'bool';
    changed = true;
  }
  if (diag.addressMode === 'bool') {
    if (!diag.boolAddressMode) {
      diag.boolAddressMode = 'linear';
      changed = true;
    } else {
      const normalized = normalizeBoolAddressMode(diag.boolAddressMode);
      if (diag.boolAddressMode !== normalized) {
        diag.boolAddressMode = normalized;
        changed = true;
      }
    }
    if (diag.baseMr === undefined || diag.baseMr === null || diag.baseMr === '') {
      diag.baseMr = assignUniqueBase ? findNextAvailableBaseMr(diag.unitId || null, diag.id) : 100;
      changed = true;
    } else {
      const n = Number(diag.baseMr);
      if (Number.isFinite(n) && diag.baseMr !== n) {
        diag.baseMr = n;
        changed = true;
      }
    }
  }
  if (diag.addressMode === 'word') {
    if (!diag.activeWord) {
      diag.activeWord = 'DM0';
      changed = true;
    }
    if (!diag.completeWord) {
      diag.completeWord = 'DM100';
      changed = true;
    }
  }
  return changed;
}

function migrateFlowAddressConfigs() {
  let changed = false;
  (project.diagrams || []).forEach(function(diag) {
    changed = ensureFlowAddressConfig(diag, true) || changed;
  });
  return changed;
}

// ── Persistence ───────────────────────────────────────────────
function saveProject() {
  try { localStorage.setItem('gf2-project', JSON.stringify(project)); } catch(e){}
}

function saveDiagramData(id, s, nid, nsn, vx, vy, vs) {
  try {
    localStorage.setItem('gf2-diag-'+id, JSON.stringify({
      state:       s   || state,
      nextId:      nid ?? nextId,
      nextStepNum: nsn ?? nextStepNum,
      viewX:       vx  ?? viewX,
      viewY:       vy  ?? viewY,
      viewScale:   vs  ?? viewScale
    }));
  } catch(e){}
}

function loadDiagramData(id) {
  try {
    const raw = localStorage.getItem('gf2-diag-'+id);
    if (raw) return JSON.parse(raw);
  } catch(e){}
  return null;
}

function deleteDiagramData(id) {
  try { localStorage.removeItem('gf2-diag-'+id); } catch(e){}
}

const PROJECT_UNIT_STRUCT_SIGNALS = [
  { id:'originBaseAddr', name:'originBaseAddr', dataType:'Word', varType:'Var',    comment:'Origin base address' },
  { id:'autoBaseAddr',   name:'autoBaseAddr',   dataType:'Word', varType:'Var',    comment:'Auto base address' },
  { id:'flagOrigin',     name:'flagOrigin',     dataType:'Bool', varType:'Var',    comment:'Origin mode flag' },
  { id:'flagAuto',       name:'flagAuto',       dataType:'Bool', varType:'Var',    comment:'Auto mode flag' },
  { id:'flagManual',     name:'flagManual',     dataType:'Bool', varType:'Var',    comment:'Manual mode flag' },
  { id:'flagError',      name:'flagError',      dataType:'Bool', varType:'Var',    comment:'Error flag' },
  { id:'btnStart',       name:'btnStart',       dataType:'Bool', varType:'Input',  comment:'Start input' },
  { id:'hmiStop',        name:'hmiStop',        dataType:'Bool', varType:'Input',  comment:'Stop input' },
  { id:'btnReset',       name:'btnReset',       dataType:'Bool', varType:'Input',  comment:'Reset input' },
  { id:'eStop',          name:'eStop',          dataType:'Bool', varType:'Input',  comment:'Emergency stop input' },
  { id:'outHomed',       name:'outHomed',       dataType:'Bool', varType:'Output', comment:'Homed output' }
];

function ensureStructDataType(name, signals, categoryId) {
  if (!name) return false;
  if (!project.devices) project.devices = [];
  if (project.devices.some(function(d) { return d && d.name === name; })) return false;
  project.devices.push({
    id: 'dev-sync-' + name.toLowerCase().replace(/[^a-z0-9]+/g, '-') + '-' + Date.now(),
    name: name,
    categoryId: categoryId || 'cat-other',
    open: true,
    signals: (signals || []).map(function(sig, idx) {
      return {
        id: sig.id || ('sig-sync-' + idx),
        name: sig.name || sig.id || ('Signal' + (idx + 1)),
        dataType: sig.dataType || 'Bool',
        varType: sig.varType || 'Var',
        comment: sig.comment || 'Auto-synced from Global Variables'
      };
    })
  });
  return true;
}

function syncStructDataFromProjectData() {
  let changed = false;
  const excelVars = project.excelVars || [];
  const unitConfigs = project.unitConfig || {};

  if (
    excelVars.some(function(v) { return v && v.format === 'Cylinder'; }) &&
    !(project.devices || []).some(function(d) { return d && d.name === 'Cylinder'; }) &&
    typeof ucEnsureCylinderDeviceType === 'function'
  ) {
    ucEnsureCylinderDeviceType();
    changed = true;
  }

  if (Object.keys(unitConfigs).length || excelVars.some(function(v) { return v && v.format === 'Unit Station'; })) {
    changed = ensureStructDataType('Unit Station', PROJECT_UNIT_STRUCT_SIGNALS, 'cat-other') || changed;
  }

  excelVars.forEach(function(v) {
    const formatName = (v && v.format || '').trim();
    if (!formatName || formatName === 'Cylinder') return;
    if ((project.devices || []).some(function(d) { return d && d.name === formatName; })) return;

    const signalIds = Object.keys((v && v.signalAddresses) || {});
    const genericSignals = signalIds.map(function(sigId) {
      return {
        id: sigId,
        name: sigId,
        dataType: 'Bool',
        varType: 'Var',
        comment: 'Auto-synced from Global Variables'
      };
    });
    changed = ensureStructDataType(formatName, genericSignals, 'cat-other') || changed;
  });

  return changed;
}

function ensureProjectVariables() {
  if (!project.variables || Array.isArray(project.variables)) {
    project.variables = { imported:[], user:[] };
  }
  if (!Array.isArray(project.variables.imported)) project.variables.imported = [];
  if (!Array.isArray(project.variables.user)) project.variables.user = [];
  return project.variables;
}

function syncVariableSignalAddressesFromDeviceTypes() {
  const devicesByName = new Map((project.devices || [])
    .filter(function(device) { return device && device.name && Array.isArray(device.signals); })
    .map(function(device) { return [device.name, device]; }));
  const groups = [];
  const vars = ensureProjectVariables();
  groups.push(vars.imported, vars.user, project.excelVars || []);
  let changed = false;

  groups.forEach(function(list) {
    (list || []).forEach(function(v) {
      const format = v && (v.format || v.dataType || '');
      const device = devicesByName.get(format);
      if (!device) return;
      if (!v.signalAddresses || typeof v.signalAddresses !== 'object') {
        v.signalAddresses = {};
        changed = true;
      }
      (device.signals || []).forEach(function(sig) {
        const id = sig && (sig.id || sig.name);
        if (!id || Object.prototype.hasOwnProperty.call(v.signalAddresses, id)) return;
        v.signalAddresses[id] = '';
        changed = true;
      });
    });
  });

  return changed;
}

function normalizeIOMappingDirection(v) {
  const raw = String(v || '').trim().toLowerCase();
  if (raw === 'input' || raw === 'in') return 'Input';
  if (raw === 'output' || raw === 'out' || raw === 'ouput') return 'Output';
  return '';
}

function ensureProjectIOMapping() {
  if (!project.ioMapping || typeof project.ioMapping !== 'object') {
    project.ioMapping = { physicalIOs: [], entries: [] };
  }
  if (!Array.isArray(project.ioMapping.physicalIOs)) project.ioMapping.physicalIOs = [];
  if (!Array.isArray(project.ioMapping.entries)) project.ioMapping.entries = [];
  project.ioMapping.physicalIOs = project.ioMapping.physicalIOs.map(function (item, idx) {
    const rec = Object.assign({}, item || {});
    if (!rec.id) rec.id = 'pio-' + idx + '-' + Date.now();
    rec.direction = normalizeIOMappingDirection(rec.direction || rec.Direction);
    return rec;
  });
  return project.ioMapping;
}

function normalizeVariableRecord(v, bucket) {
  const out = Object.assign({}, v || {});
  if (!out.id) out.id = 'var-' + bucket + '-' + Date.now() + '-' + Math.random().toString(36).slice(2, 7);
  if (!out.kind) out.kind = out.signalAddresses ? 'struct' : 'primitive';
  if (!out.dataType) out.dataType = out.format || 'BOOL';
  out.format = out.dataType;
  if (!out.source) out.source = bucket === 'imported' ? 'csv' : 'manual';
  return out;
}

function upsertProjectVariable(bucket, variableDef) {
  const vars = ensureProjectVariables();
  const list = bucket === 'user' ? vars.user : vars.imported;
  const next = normalizeVariableRecord(variableDef, bucket === 'user' ? 'user' : 'imported');
  const idx = list.findIndex(function(item) {
    return item.id === next.id || (item.label && item.label === next.label);
  });
  if (idx >= 0) list[idx] = next;
  else list.push(next);
  return next;
}

// ── Project load ──────────────────────────────────────────────
function loadProject() {
  try {
    const raw = localStorage.getItem('gf2-project');
    if (raw) {
      project = JSON.parse(raw);
      let projectChanged = false;
      ensureProjectVariables();
      ensureProjectIOMapping();
      projectChanged = syncStructDataFromProjectData() || projectChanged;
      projectChanged = syncVariableSignalAddressesFromDeviceTypes() || projectChanged;
      projectChanged = migrateFlowAddressConfigs() || projectChanged;
      if (projectChanged) saveProject();
      const lastId = localStorage.getItem('gf2-active');
      if (lastId && project.diagrams.find(d=>d.id===lastId)) {
        openTab(lastId);
      } else if (project.diagrams.length > 0) {
        openTab(project.diagrams[0].id);
      } else {
        addDiagram(true);
      }
    } else {
      addDiagram(true);
      // Auto-seed standard device templates cho project mới
      addStandardDeviceTemplates();
    }
  } catch(e) { addDiagram(true); }
}

// ── Flush active diagram to localStorage ──────────────────────
function flushState() {
  if (!activeDiagramId || activeDiagramId === '__vars__' || activeDiagramId === '__io_mapping__' || String(activeDiagramId).startsWith('__struct__:')) return;
  saveDiagramData(activeDiagramId);
  markModified(activeDiagramId, false);
}

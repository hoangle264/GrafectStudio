// -------------------------------------------------------------------
//  store.js Ã¢â‚¬â€ Grafcet Studio
//  Project state singleton + localStorage persistence.
//  Must be loaded BEFORE grafcet-studio-v2.js and grafcet-codegen.js.
//
//  NOTE: saveDiagramData / flushState reference runtime globals
//  (state, nextId, nextStepNum, viewX, viewY, viewScale) that are
//  declared in grafcet-studio-v2.js. This is intentional Ã¢â‚¬â€ those are
//  diagram-render globals and belong with the canvas layer. They are
//  only accessed at call-time (not parse-time), so load order is safe.
// -------------------------------------------------------------------

type StoreProject = GrafcetStudioProject.Project;
type StoreDiagramMeta = GrafcetStudioProject.DiagramMeta;
type StoreDiagramState = GrafcetStudioProject.DiagramState;
type StoreStoredDiagramData = GrafcetStudioProject.StoredDiagramData;
type StoreDeviceSignal = GrafcetStudioProject.DeviceSignal;
type StoreDeviceType = GrafcetStudioProject.DeviceType;
type StoreProjectVariable = GrafcetStudioProject.ProjectVariable;
type StoreProjectVariables = GrafcetStudioProject.ProjectVariables;
type StoreIOMapping = GrafcetStudioProject.IOMapping;

declare let state: StoreDiagramState;
declare let nextId: number;
declare let nextStepNum: number;
declare let viewX: number;
declare let viewY: number;
declare let viewScale: number;
declare function ucEnsureCylinderDeviceType(): void;
declare function openTab(id: string): void;
declare function addDiagram(skipOpen?: boolean): void;
declare function addStandardDeviceTemplates(): void;
declare function markModified(id: string, modified: boolean): void;

// -- Project state -----------------------------------------------
let project: StoreProject = {
  id: 'proj-1',
  name: 'My Project',
  machineName: 'Machine',
  units: [],
  diagrams: [],
  devices: [],
  variables: { imported: [], user: [] },
  excelVars: [],
  unitConfig: {},
  ioMapping: { physicalIOs: [], entries: [] }
};
let openTabs: Array<{ id: string; diagramId?: string }> = [];
let activeDiagramId: string | null = null;

const PROJECT_UNIT_STRUCT_SIGNALS: StoreDeviceSignal[] = [
  { id: 'originBaseAddr', name: 'originBaseAddr', dataType: 'Word', varType: 'Var', comment: 'Origin base address' },
  { id: 'autoBaseAddr', name: 'autoBaseAddr', dataType: 'Word', varType: 'Var', comment: 'Auto base address' },
  { id: 'flagOrigin', name: 'flagOrigin', dataType: 'Bool', varType: 'Var', comment: 'Origin mode flag' },
  { id: 'flagAuto', name: 'flagAuto', dataType: 'Bool', varType: 'Var', comment: 'Auto mode flag' },
  { id: 'flagManual', name: 'flagManual', dataType: 'Bool', varType: 'Var', comment: 'Manual mode flag' },
  { id: 'flagError', name: 'flagError', dataType: 'Bool', varType: 'Var', comment: 'Error flag' },
  { id: 'btnStart', name: 'btnStart', dataType: 'Bool', varType: 'Input', comment: 'Start input' },
  { id: 'hmiStop', name: 'hmiStop', dataType: 'Bool', varType: 'Input', comment: 'Stop input' },
  { id: 'btnReset', name: 'btnReset', dataType: 'Bool', varType: 'Input', comment: 'Reset input' },
  { id: 'eStop', name: 'eStop', dataType: 'Bool', varType: 'Input', comment: 'Emergency stop input' },
  { id: 'outHomed', name: 'outHomed', dataType: 'Bool', varType: 'Output', comment: 'Homed output' }
];

namespace GrafcetStudioStoreHelpers {
  type Project = StoreProject;
  type DiagramMeta = StoreDiagramMeta;
  type StoredDiagramData = StoreStoredDiagramData;
  type DeviceSignal = StoreDeviceSignal;
  type DeviceType = StoreDeviceType;
  type ProjectVariable = StoreProjectVariable;
  type ProjectVariables = StoreProjectVariables;
  type IOMapping = StoreIOMapping;

  export interface StoreContext {
    getProject(): Project;
    loadDiagramData(diagramId: string): StoredDiagramData | null;
    ensureCylinderDeviceType?: () => void;
    unitStructSignals: DeviceSignal[];
  }

  export const GF_ADDRESS_DEFAULT_BOOL_SPAN = 200;
  export const MAX_NESTING_DEPTH = 3;

  function normalizeBoolAddressMode(mode: unknown): string {
    const value = String(mode || '').trim().toLowerCase();
    return value === 'block' ? 'block' : 'linear';
  }

  function getFlowStepMaxNumber(context: StoreContext, diagId: string): number {
    const data = context.loadDiagramData(diagId);
    const steps = (data && data.state && data.state.steps) || [];
    return steps.reduce(function(max, step) { return Math.max(max, Number(step && step.number) || 0); }, 0);
  }

  interface ParsedAddressBase {
    prefix: string;
    number: number;
    width: number;
  }

  function parseAddressBase(value: unknown, fallback: string): ParsedAddressBase {
    const source = String(value == null || value === '' ? fallback : value).trim();
    const match = source.match(/^([A-Za-z]+)(\d+)$/);
    if (!match) {
      const fallbackMatch = fallback.match(/^([A-Za-z]+)(\d+)$/)!;
      return { prefix: fallbackMatch[1].toUpperCase(), number: Number(fallbackMatch[2]), width: fallbackMatch[2].length };
    }
    return { prefix: match[1].toUpperCase(), number: Number(match[2]), width: match[2].length };
  }

  function formatAddressBase(prefix: string, number: number, width: number): string {
    const numberText = width > 1 ? String(number).padStart(width, '0') : String(number);
    return prefix + numberText;
  }

  function getBoolAddressRange(context: StoreContext, flow: DiagramMeta | null | undefined): { start: number; end: number } | null {
    if (!flow || flow.addressMode !== 'bool') return null;
    const maxStepNumber = Math.max(1, getFlowStepMaxNumber(context, flow.id));
    const start = parseAddressBase(flow.baseMr, 'MR100').number;
    return { start, end: start + maxStepNumber * 2 - 1 };
  }

  function boolAddressRangesOverlap(a: { start: number; end: number } | null, b: { start: number; end: number } | null): boolean {
    return !!a && !!b && a.start <= b.end && b.start <= a.end;
  }

  export function findNextAvailableBaseMr(context: StoreContext, unitId?: string | null, excludeDiagId?: string): string {
    const currentProject = context.getProject();
    let base = 100;
    while (base < 100000) {
      const candidate = { start: base, end: base + GF_ADDRESS_DEFAULT_BOOL_SPAN - 1 };
      const overlaps = (currentProject.diagrams || []).some(function(diag) {
        if (!diag || diag.id === excludeDiagId) return false;
        if ((diag.unitId || null) !== (unitId || null)) return false;
        if ((diag.addressMode || 'bool') !== 'bool' || diag.baseMr === undefined || diag.baseMr === null || diag.baseMr === '') return false;
        return boolAddressRangesOverlap(candidate, getBoolAddressRange(context, diag));
      });
      if (!overlaps) return formatAddressBase('MR', base, 0);
      base += GF_ADDRESS_DEFAULT_BOOL_SPAN;
    }
    return 'MR100';
  }

  export function ensureFlowAddressConfig(context: StoreContext, diag: DiagramMeta | null | undefined, assignUniqueBase: boolean): boolean {
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
        diag.baseMr = assignUniqueBase ? findNextAvailableBaseMr(context, diag.unitId || null, diag.id) : 'MR100';
        changed = true;
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

  export function migrateFlowAddressConfigs(context: StoreContext): boolean {
    let changed = false;
    (context.getProject().diagrams || []).forEach(function(diag) {
      changed = ensureFlowAddressConfig(context, diag, true) || changed;
    });
    return changed;
  }

  export function migrateFlowControlState(context: StoreContext): boolean {
    let changed = false;
    (context.getProject().diagrams || []).forEach(function(diag) {
      if (!diag) return;
      if (!diag.controlState) {
        diag.controlState = diag.mode || 'Auto';
        changed = true;
      }
      if (!diag.category) {
        diag.category = 'normal';
        changed = true;
      }
    });
    return changed;
  }

  function ensureStructDataType(context: StoreContext, name: string, signals: DeviceSignal[], categoryId?: string): boolean {
    const currentProject = context.getProject();
    if (!name) return false;
    if (!currentProject.devices) currentProject.devices = [];
    if (currentProject.devices.some(function(device) { return device && device.name === name; })) return false;
    currentProject.devices.push({
      id: 'dev-sync-' + name.toLowerCase().replace(/[^a-z0-9]+/g, '-') + '-' + Date.now(),
      name,
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

  export function syncStructData(context: StoreContext): boolean {
    const currentProject = context.getProject();
    let changed = false;
    const excelVars = currentProject.excelVars || [];
    const unitConfigs = currentProject.unitConfig || {};

    if (
      excelVars.some(function(v) { return v && v.format === 'Cylinder'; }) &&
      !(currentProject.devices || []).some(function(device) { return device && device.name === 'Cylinder'; }) &&
      context.ensureCylinderDeviceType
    ) {
      context.ensureCylinderDeviceType();
      changed = true;
    }

    if (Object.keys(unitConfigs).length || excelVars.some(function(v) { return v && v.format === 'Unit Station'; })) {
      changed = ensureStructDataType(context, 'Unit Station', context.unitStructSignals, 'cat-other') || changed;
    }

    excelVars.forEach(function(v) {
      const formatName = (v && v.format || '').trim();
      if (!formatName || formatName === 'Cylinder') return;
      if ((currentProject.devices || []).some(function(device) { return device && device.name === formatName; })) return;

      const signalIds = Object.keys((v && v.signalAddresses) || {});
      const genericSignals = signalIds.map(function(sigId) {
        return {
          id: sigId,
          name: sigId,
          dataType: 'Bool',
          varType: 'Var',
          comment: 'Auto-synced from Global Variables'
        } as DeviceSignal;
      });
      changed = ensureStructDataType(context, formatName, genericSignals, 'cat-other') || changed;
    });

    return changed;
  }

  export function ensureProjectVariables(context: StoreContext): ProjectVariables {
    const currentProject = context.getProject();
    if (!currentProject.variables || Array.isArray(currentProject.variables)) {
      currentProject.variables = { imported: [], user: [] };
    }
    if (!Array.isArray(currentProject.variables.imported)) currentProject.variables.imported = [];
    if (!Array.isArray(currentProject.variables.user)) currentProject.variables.user = [];
    return currentProject.variables;
  }

  export function syncVariableSignalAddressesFromDeviceTypes(context: StoreContext): boolean {
    const currentProject = context.getProject();
    const devicesByName = new Map<string, DeviceType>((currentProject.devices || [])
      .filter(function(device): device is DeviceType { return !!device && !!device.name && Array.isArray(device.signals); })
      .map(function(device) { return [device.name, device]; }));
    const groups: ProjectVariable[][] = [];
    const vars = ensureProjectVariables(context);
    groups.push(vars.imported, vars.user, currentProject.excelVars || []);
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
        const signalAddresses = v.signalAddresses;
        (device.signals || []).forEach(function(sig) {
          const id = sig && (sig.id || sig.name);
          if (!id || Object.prototype.hasOwnProperty.call(signalAddresses, id)) return;
          signalAddresses[id] = '';
          changed = true;
        });
      });
    });

    return changed;
  }

  export function normalizeIOMappingDirection(v: unknown): string {
    const raw = String(v || '').trim().toLowerCase();
    if (raw === 'input' || raw === 'in') return 'Input';
    if (raw === 'output' || raw === 'out' || raw === 'ouput') return 'Output';
    return '';
  }

  export function ensureProjectIOMapping(context: StoreContext): IOMapping {
    const currentProject = context.getProject();
    if (!currentProject.ioMapping || typeof currentProject.ioMapping !== 'object') {
      currentProject.ioMapping = { physicalIOs: [], entries: [] };
    }
    if (!Array.isArray(currentProject.ioMapping.physicalIOs)) currentProject.ioMapping.physicalIOs = [];
    if (!Array.isArray(currentProject.ioMapping.entries)) currentProject.ioMapping.entries = [];
    currentProject.ioMapping.physicalIOs = currentProject.ioMapping.physicalIOs.map(function(item, idx) {
      const rec = Object.assign({}, item || {});
      if (!rec.id) rec.id = 'pio-' + idx + '-' + Date.now();
      rec.direction = normalizeIOMappingDirection(rec.direction || rec.Direction);
      return rec;
    });
    return currentProject.ioMapping;
  }

  export function normalizeVariableRecord(v: Partial<ProjectVariable> | null | undefined, bucket: string): ProjectVariable {
    const out = Object.assign({}, v || {}) as ProjectVariable;
    if (!out.id) out.id = 'var-' + bucket + '-' + Date.now() + '-' + Math.random().toString(36).slice(2, 7);
    if (!out.kind) out.kind = out.signalAddresses ? 'struct' : 'primitive';
    if (!out.dataType) out.dataType = out.format || 'BOOL';
    out.format = out.dataType;
    if (!out.source) out.source = bucket === 'imported' ? 'csv' : 'manual';
    return out;
  }

  export function upsertProjectVariable(context: StoreContext, bucket: string, variableDef: Partial<ProjectVariable>): ProjectVariable {
    const vars = ensureProjectVariables(context);
    const list = bucket === 'user' ? vars.user : vars.imported;
    const next = normalizeVariableRecord(variableDef, bucket === 'user' ? 'user' : 'imported');
    const idx = list.findIndex(function(item) {
      return item.id === next.id || (!!item.label && item.label === next.label);
    });
    if (idx >= 0) list[idx] = next;
    else list.push(next);
    return next;
  }

  export interface StoreHelperApi {
    ensureProjectVariables(context: StoreContext): ProjectVariables;
    upsertProjectVariable(context: StoreContext, bucket: string, variableDef: Partial<ProjectVariable>): ProjectVariable;
    normalizeVariableRecord(v: Partial<ProjectVariable> | null | undefined, bucket: string): ProjectVariable;
    syncStructData(context: StoreContext): boolean;
    syncVariableSignalAddressesFromDeviceTypes(context: StoreContext): boolean;
    normalizeIOMappingDirection(v: unknown): string;
    ensureProjectIOMapping(context: StoreContext): IOMapping;
    findNextAvailableBaseMr(context: StoreContext, unitId?: string | null, excludeDiagId?: string): string;
    ensureFlowAddressConfig(context: StoreContext, diag: DiagramMeta | null | undefined, assignUniqueBase: boolean): boolean;
    migrateFlowAddressConfigs(context: StoreContext): boolean;
    migrateFlowControlState(context: StoreContext): boolean;
  }

  export const api: StoreHelperApi = {
    ensureProjectVariables,
    upsertProjectVariable,
    normalizeVariableRecord,
    syncStructData,
    syncVariableSignalAddressesFromDeviceTypes,
    normalizeIOMappingDirection,
    ensureProjectIOMapping,
    findNextAvailableBaseMr,
    ensureFlowAddressConfig,
    migrateFlowAddressConfigs,
    migrateFlowControlState,
  };
}

GrafcetStudioInterop.registerBridge('store', GrafcetStudioStoreHelpers.api);

function getStoreContext(): GrafcetStudioStoreHelpers.StoreContext {
  return {
    getProject: function() { return project; },
    loadDiagramData,
    ensureCylinderDeviceType: typeof ucEnsureCylinderDeviceType === 'function' ? ucEnsureCylinderDeviceType : undefined,
    unitStructSignals: PROJECT_UNIT_STRUCT_SIGNALS
  };
}

// -- Flow address configuration wrappers -------------------------
function findNextAvailableBaseMr(unitId?: string | null, excludeDiagId?: string): string {
  return GrafcetStudioStoreHelpers.findNextAvailableBaseMr(getStoreContext(), unitId, excludeDiagId);
}

function ensureFlowAddressConfig(diag: StoreDiagramMeta | null | undefined, assignUniqueBase: boolean): boolean {
  return GrafcetStudioStoreHelpers.ensureFlowAddressConfig(getStoreContext(), diag, assignUniqueBase);
}

function migrateFlowAddressConfigs(): boolean {
  return GrafcetStudioStoreHelpers.migrateFlowAddressConfigs(getStoreContext());
}

function migrateFlowControlState(): boolean {
  return GrafcetStudioStoreHelpers.migrateFlowControlState(getStoreContext());
}

// -- Persistence wrappers ----------------------------------------
function saveProject(): void {
  GrafcetStudioStorePersistence.saveProject(project);
}

function saveDiagramData(id: string, s?: StoreDiagramState, nid?: number, nsn?: number, vx?: number, vy?: number, vs?: number): void {
  GrafcetStudioStorePersistence.saveDiagramData(id, {
    state: s || state,
    nextId: nid ?? nextId,
    nextStepNum: nsn ?? nextStepNum,
    viewX: vx ?? viewX,
    viewY: vy ?? viewY,
    viewScale: vs ?? viewScale
  });
}

function loadDiagramData(id: string): StoreStoredDiagramData | null {
  return GrafcetStudioStorePersistence.loadDiagramData(id);
}

function deleteDiagramData(id: string): void {
  GrafcetStudioStorePersistence.deleteDiagramData(id);
}

// -- Store helper wrappers ---------------------------------------
function syncStructDataFromProjectData(): boolean {
  return GrafcetStudioStoreHelpers.syncStructData(getStoreContext());
}

function syncStructData(): boolean {
  return syncStructDataFromProjectData();
}

function ensureProjectVariables(): StoreProjectVariables {
  return GrafcetStudioStoreHelpers.ensureProjectVariables(getStoreContext());
}

function syncVariableSignalAddressesFromDeviceTypes(): boolean {
  return GrafcetStudioStoreHelpers.syncVariableSignalAddressesFromDeviceTypes(getStoreContext());
}

function normalizeIOMappingDirection(v: unknown): string {
  return GrafcetStudioStoreHelpers.normalizeIOMappingDirection(v);
}

function ensureProjectIOMapping(): StoreIOMapping {
  return GrafcetStudioStoreHelpers.ensureProjectIOMapping(getStoreContext());
}

function normalizeVariableRecord(v: Partial<StoreProjectVariable> | null | undefined, bucket: string): StoreProjectVariable {
  return GrafcetStudioStoreHelpers.normalizeVariableRecord(v, bucket);
}

function upsertProjectVariable(bucket: string, variableDef: Partial<StoreProjectVariable>): StoreProjectVariable {
  return GrafcetStudioStoreHelpers.upsertProjectVariable(getStoreContext(), bucket, variableDef);
}

// -- Project load ------------------------------------------------
function loadProject(): void {
  try {
    const raw = localStorage.getItem('gf2-project');
    if (raw) {
      project = JSON.parse(raw) as StoreProject;
      let projectChanged = false;
      ensureProjectVariables();
      ensureProjectIOMapping();
      projectChanged = syncStructDataFromProjectData() || projectChanged;
      projectChanged = syncVariableSignalAddressesFromDeviceTypes() || projectChanged;
      projectChanged = migrateFlowAddressConfigs() || projectChanged;
      projectChanged = migrateFlowControlState() || projectChanged;
      if (projectChanged) saveProject();
      const lastId = localStorage.getItem('gf2-active');
      if (lastId && project.diagrams.find(function(diagram) { return diagram.id === lastId; })) {
        openTab(lastId);
      } else if (project.diagrams.length > 0) {
        openTab(project.diagrams[0].id);
      } else {
        addDiagram(true);
      }
    } else {
      addDiagram(true);
      // Auto-seed standard device templates cho project m?i
      addStandardDeviceTemplates();
    }
  } catch (e) { addDiagram(true); }
}

// -- Flush active diagram to localStorage ------------------------
function flushState(): void {
  if (!activeDiagramId || activeDiagramId === '__vars__' || activeDiagramId === '__io_mapping__' || String(activeDiagramId).startsWith('__struct__:')) return;
  saveDiagramData(activeDiagramId);
  markModified(activeDiagramId, false);
}



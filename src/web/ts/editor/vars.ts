namespace GrafcetStudioVars {
  type Project = GrafcetStudioProject.Project;
  type ProjectVariable = GrafcetStudioProject.ProjectVariable;
  type DeviceSignal = GrafcetStudioProject.DeviceSignal;
  type UnitConfig = GrafcetStudioProject.UnitConfig;

  export interface VarsContext {
    project: Project;
    ensureProjectVariables?: () => GrafcetStudioProject.ProjectVariables;
  }

  export interface VarEntry {
    source: string;
    bucket?: string;
    key: number | string;
    label: string;
    format: string;
    data: ProjectVariable | UnitConfig;
  }

  export interface ResolveEntryResult {
    list: ProjectVariable[] | null;
    item: ProjectVariable | null;
    idx: number;
    bucket: string;
  }

  export const CYL_SIGNALS: DeviceSignal[] = [
    { id: 'cyl_coilA', name: 'CoilA', dataType: 'Bool', varType: 'Output', comment: 'Output coil A (extend)' },
    { id: 'cyl_coilB', name: 'CoilB', dataType: 'Bool', varType: 'Output', comment: 'Output coil B (retract)' },
    { id: 'cyl_lsh', name: 'LSH', dataType: 'Bool', varType: 'Input', comment: 'Limit switch high (extended)' },
    { id: 'cyl_lsl', name: 'LSL', dataType: 'Bool', varType: 'Input', comment: 'Limit switch low (retracted)' },
    { id: 'cyl_lockA', name: 'LockA', dataType: 'Bool', varType: 'Var', comment: 'Interlock coil A' },
    { id: 'cyl_lockB', name: 'LockB', dataType: 'Bool', varType: 'Var', comment: 'Interlock coil B' },
    { id: 'cyl_disSnsH', name: 'DisSnsH', dataType: 'Bool', varType: 'Var', comment: 'Disable sensor LSH' },
    { id: 'cyl_disSnsL', name: 'DisSnsL', dataType: 'Bool', varType: 'Var', comment: 'Disable sensor LSL' },
    { id: 'cyl_errA', name: 'ErrorA', dataType: 'Bool', varType: 'Var', comment: 'Error flag dir A' },
    { id: 'cyl_errB', name: 'ErrorB', dataType: 'Bool', varType: 'Var', comment: 'Error flag dir B' },
    { id: 'cyl_state', name: 'State', dataType: 'Bool', varType: 'Var', comment: 'Cylinder state' },
    { id: 'cyl_hmiMan', name: 'HmiManBtn', dataType: 'Bool', varType: 'Var', comment: 'HMI manual button' }
  ];

  export const UNIT_SIGNALS: DeviceSignal[] = [
    { id: 'originBaseAddr', name: 'originBaseAddr', dataType: 'Word', varType: 'Var', path: 'originBaseAddr' },
    { id: 'autoBaseAddr', name: 'autoBaseAddr', dataType: 'Word', varType: 'Var', path: 'autoBaseAddr' },
    { id: 'flagOrigin', name: 'flagOrigin', dataType: 'Bool', varType: 'Var', path: 'flags.flagOrigin' },
    { id: 'flagAuto', name: 'flagAuto', dataType: 'Bool', varType: 'Var', path: 'flags.flagAuto' },
    { id: 'flagManual', name: 'flagManual', dataType: 'Bool', varType: 'Var', path: 'flags.flagManual' },
    { id: 'flagError', name: 'flagError', dataType: 'Bool', varType: 'Var', path: 'flags.flagError' },
    { id: 'btnStart', name: 'btnStart', dataType: 'Bool', varType: 'Input', path: 'io.btnStart' },
    { id: 'hmiStop', name: 'hmiStop', dataType: 'Bool', varType: 'Input', path: 'io.hmiStop' },
    { id: 'btnReset', name: 'btnReset', dataType: 'Bool', varType: 'Input', path: 'io.btnReset' },
    { id: 'eStop', name: 'eStop', dataType: 'Bool', varType: 'Input', path: 'io.eStop' },
    { id: 'outHomed', name: 'outHomed', dataType: 'Bool', varType: 'Output', path: 'io.outHomed' }
  ];

  export function ensureVars(context: VarsContext): void {
    if (context.ensureProjectVariables) context.ensureProjectVariables();
  }

  export function gvtGetEntries(context: VarsContext): VarEntry[] {
    ensureVars(context);
    const project = context.project;
    const imported = ((project.variables && project.variables.imported) || []).map(function(v, idx) {
      return { source: 'imported', bucket: 'imported', key: idx, label: v.label || '', format: v.format || v.dataType || 'BOOL', data: v } as VarEntry;
    });
    const user = ((project.variables && project.variables.user) || []).map(function(v, idx) {
      return { source: 'user', bucket: 'user', key: idx, label: v.label || '', format: v.format || v.dataType || 'BOOL', data: v } as VarEntry;
    });
    const unitImported = Object.keys(project.unitConfig || {}).map(function(key) {
      const cfg = project.unitConfig[key] || { label: key };
      return { source: 'unit', bucket: 'imported', key, label: cfg.label || key, format: 'Unit Station', data: cfg } as VarEntry;
    });
    if (imported.length || user.length || unitImported.length) return imported.concat(unitImported, user);

    const unitConfig = project.unitConfig || {};
    const hasExcelUnitStation = (project.excelVars || []).some(function(v) { return v && v.format === 'Unit Station'; });
    const excelEntries = (project.excelVars || []).map(function(v, idx) {
      return { source: 'excel', key: idx, label: v.label || '', format: v.format || 'Struct Data', data: v } as VarEntry;
    });
    const unitEntries = hasExcelUnitStation ? [] : Object.keys(unitConfig).map(function(key) {
      const cfg = unitConfig[key] || { label: key };
      return { source: 'unit', key, label: cfg.label || key, format: 'Unit Station', data: cfg } as VarEntry;
    });
    return excelEntries.concat(unitEntries);
  }

  export function gvtGetUnitAddr(cfg: UnitConfig | null | undefined, path: string): string {
    if (!cfg) return '';
    if (cfg.signalAddresses && Object.prototype.hasOwnProperty.call(cfg.signalAddresses, path)) return cfg.signalAddresses[path] || '';
    return path.split('.').reduce<unknown>(function(cur, part) {
      return cur && typeof cur === 'object' && (cur as Record<string, unknown>)[part] != null ? (cur as Record<string, unknown>)[part] : '';
    }, cfg) as string || '';
  }

  export function gvtSetUnitAddr(cfg: UnitConfig, path: string, value: string): void {
    const parts = path.split('.');
    let cur = cfg as Record<string, unknown>;
    for (let i = 0; i < parts.length - 1; i++) {
      if (!cur[parts[i]] || typeof cur[parts[i]] !== 'object') cur[parts[i]] = {};
      cur = cur[parts[i]] as Record<string, unknown>;
    }
    cur[parts[parts.length - 1]] = value;
  }

  export function gvtGetUnitSigList(context: VarsContext): DeviceSignal[] {
    const devType = (context.project.devices || []).find(device => device.name === 'Unit Station');
    const devSigs = devType ? (devType.signals || []) : [];
    if (!devSigs.length) return UNIT_SIGNALS;
    const unitPaths = UNIT_SIGNALS.reduce<Record<string, string | undefined>>(function(map, sig) {
      map[sig.id] = sig.path;
      return map;
    }, {});
    return devSigs.map(function(sig) { return Object.assign({}, sig, { path: unitPaths[sig.id] || sig.path || sig.id }); });
  }

  function findNestedDeviceType(context: VarsContext, sig: DeviceSignal): GrafcetStudioProject.DeviceType | undefined {
    const nestedTypeId = (sig.nestedTypeId || '').trim();
    const dataType = (sig.dataType || '').trim();
    return (context.project.devices || []).find(function(device) {
      if (!device) return false;
      if (nestedTypeId && device.id === nestedTypeId) return true;
      return !!dataType && device.name === dataType;
    });
  }

  function expandDeviceSignals(
    context: VarsContext,
    signals: DeviceSignal[],
    depth: number,
    parentPath: string,
    visitedTypeIds: Record<string, boolean>
  ): DeviceSignal[] {
    const out: DeviceSignal[] = [];
    (signals || []).forEach(function(sig) {
      if (!sig) return;
      const signalName = sig.name || sig.id || '';
      const currentPath = parentPath && signalName ? parentPath + '.' + signalName : (parentPath || sig.path || signalName);
      const nestedType = findNestedDeviceType(context, sig);
      if (!nestedType) {
        out.push(parentPath ? Object.assign({}, sig, { path: currentPath }) : sig);
        return;
      }
      if (depth >= GrafcetStudioStoreHelpers.MAX_NESTING_DEPTH) {
        console.warn('Struct nesting depth limit reached for signal:', currentPath, 'max:', GrafcetStudioStoreHelpers.MAX_NESTING_DEPTH);
        out.push(parentPath ? Object.assign({}, sig, { path: currentPath }) : sig);
        return;
      }
      if (nestedType.id && visitedTypeIds[nestedType.id]) {
        console.warn('Struct nesting cycle skipped for signal:', currentPath, 'type:', nestedType.name || nestedType.id);
        out.push(parentPath ? Object.assign({}, sig, { path: currentPath }) : sig);
        return;
      }
      const nextVisited = Object.assign({}, visitedTypeIds);
      if (nestedType.id) nextVisited[nestedType.id] = true;
      out.push.apply(out, expandDeviceSignals(context, nestedType.signals || [], depth + 1, currentPath, nextVisited));
    });
    return out;
  }

  export function gvtGetSigList(context: VarsContext, variable: ProjectVariable): DeviceSignal[] {
    const devType = (context.project.devices || []).find(device => device.name === (variable.format || ''));
    const devSigs = devType ? (devType.signals || []) : [];
    if (variable.format === 'Cylinder') return devSigs.length ? devSigs : CYL_SIGNALS;
    if (!devSigs.some(function(sig) { return !!findNestedDeviceType(context, sig); })) return devSigs;
    const visitedTypeIds: Record<string, boolean> = {};
    if (devType && devType.id) visitedTypeIds[devType.id] = true;
    return expandDeviceSignals(context, devSigs, 1, '', visitedTypeIds);
  }

  export function gvtGetExcelSignalAddress(variable: ProjectVariable | null | undefined, sig: DeviceSignal | null | undefined): string {
    const sAddr = (variable && variable.signalAddresses) || {};
    if (!sig) return '';
    if (sig.name && sAddr[sig.name]) return sAddr[sig.name];
    if (sig.id && sAddr[sig.id]) return sAddr[sig.id];
    return '';
  }

  export function getVars(context: VarsContext): ProjectVariable[] {
    ensureVars(context);
    const imported = (context.project.variables && context.project.variables.imported) || [];
    const user = (context.project.variables && context.project.variables.user) || [];
    return imported.concat(user);
  }

  export function gvtResolveEntry(context: VarsContext, source: string, key: string | number): ResolveEntryResult {
    ensureVars(context);
    const idx = parseInt(String(key), 10);
    const variables = context.project.variables || { imported: [], user: [] };
    if (source === 'imported') return { list: variables.imported, item: variables.imported[idx] || null, idx, bucket: 'imported' };
    if (source === 'user') return { list: variables.user, item: variables.user[idx] || null, idx, bucket: 'user' };
    if (source === 'excel') return { list: context.project.excelVars || [], item: (context.project.excelVars || [])[idx] || null, idx, bucket: 'excel' };
    return { list: null, item: null, idx, bucket: '' };
  }

  export function gvtEditVar(context: VarsContext, source: string, key: string | number, field: string, value: unknown): ResolveEntryResult {
    const hit = gvtResolveEntry(context, source, key);
    if (!hit.item) return hit;
    (hit.item as unknown as Record<string, unknown>)[field] = value;
    if (field === 'format') {
      hit.item.dataType = String(value || '');
      const devType = (context.project.devices || []).find(device => device.name === value);
      if (devType) {
        hit.item.kind = 'struct';
        if (!hit.item.signalAddresses) hit.item.signalAddresses = {};
      } else {
        hit.item.kind = 'primitive';
        delete hit.item.signalAddresses;
      }
    }
    return hit;
  }

  export interface VarsApi {
    CYL_SIGNALS: DeviceSignal[];
    UNIT_SIGNALS: DeviceSignal[];
    gvtGetEntries(context: VarsContext): VarEntry[];
    gvtGetUnitAddr(cfg: UnitConfig | null | undefined, path: string): string;
    gvtSetUnitAddr(cfg: UnitConfig, path: string, value: string): void;
    gvtGetUnitSigList(context: VarsContext): DeviceSignal[];
    gvtGetSigList(context: VarsContext, variable: ProjectVariable): DeviceSignal[];
    gvtGetExcelSignalAddress(variable: ProjectVariable | null | undefined, sig: DeviceSignal | null | undefined): string;
    getVars(context: VarsContext): ProjectVariable[];
    gvtResolveEntry(context: VarsContext, source: string, key: string | number): ResolveEntryResult;
    gvtEditVar(context: VarsContext, source: string, key: string | number, field: string, value: unknown): ResolveEntryResult;
  }

  export const api: VarsApi = {
    CYL_SIGNALS,
    UNIT_SIGNALS,
    gvtGetEntries,
    gvtGetUnitAddr,
    gvtSetUnitAddr,
    gvtGetUnitSigList,
    gvtGetSigList,
    gvtGetExcelSignalAddress,
    getVars,
    gvtResolveEntry,
    gvtEditVar
  };
}

GrafcetStudioInterop.registerBridge('vars', GrafcetStudioVars.api);

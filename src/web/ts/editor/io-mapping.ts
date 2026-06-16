namespace GrafcetStudioIOMapping {
  type Project = GrafcetStudioProject.Project;
  type IOMappingEntry = GrafcetStudioProject.IOMappingEntry;
  type DeviceSignal = GrafcetStudioProject.DeviceSignal;
  type UnitConfig = GrafcetStudioProject.UnitConfig;
  type ProjectVariable = GrafcetStudioProject.ProjectVariable;
  type VarEntry = GrafcetStudioVars.VarEntry;

  export interface IOMappingContext {
    project: Project;
    ensureProjectIOMapping?: () => GrafcetStudioProject.IOMapping;
    varsApi: GrafcetStudioVars.VarsApi;
    varsContext: GrafcetStudioVars.VarsContext;
  }

  export interface CandidateVariable {
    appVariable: string;
    direction: string;
    norm: string;
  }

  export interface AddressTarget {
    get(): string;
    set(value: string): void;
  }

  function ensureIO(context: IOMappingContext): GrafcetStudioProject.IOMapping {
    if (context.ensureProjectIOMapping) return context.ensureProjectIOMapping();
    if (!context.project.ioMapping) context.project.ioMapping = { physicalIOs: [], entries: [] };
    if (!Array.isArray(context.project.ioMapping.physicalIOs)) context.project.ioMapping.physicalIOs = [];
    if (!Array.isArray(context.project.ioMapping.entries)) context.project.ioMapping.entries = [];
    return context.project.ioMapping;
  }

  export function ioNormalizeTag(value: unknown): string {
    return String(value || '').trim().toUpperCase().replace(/[-_\s]+/g, '.').replace(/\.+/g, '.');
  }

  export function ioCollectCandidateVariables(context: IOMappingContext): CandidateVariable[] {
    const out: CandidateVariable[] = [];
    context.varsApi.gvtGetEntries(context.varsContext).forEach(function(entry) {
      const variable = entry.data || {};
      const sigs = entry.source === 'unit'
        ? context.varsApi.gvtGetUnitSigList(context.varsContext)
        : context.varsApi.gvtGetSigList(context.varsContext, variable as ProjectVariable);
      const base = String((variable as ProjectVariable | UnitConfig).label || '').trim();
      sigs.forEach(function(sig) {
        const dir = sig.varType === 'Input' ? 'Input' : (sig.varType === 'Output' ? 'Output' : '');
        if (!dir) return;
        const appVariable = base ? (base + '.' + sig.name) : sig.name;
        out.push({ appVariable, direction: dir, norm: ioNormalizeTag(appVariable) });
      });
    });
    return out;
  }

  export function ioAutoMatchEntries(context: IOMappingContext): IOMappingEntry[] {
    const io = ensureIO(context);
    const candidates = ioCollectCandidateVariables(context);
    io.entries = io.physicalIOs.map(function(physical) {
      const norm = ioNormalizeTag(physical.deviceTag);
      const sameDir = candidates.filter(function(candidate) { return candidate.direction === physical.direction; });
      let best: IOMappingEntry | null = null;
      let bestScore = -1;
      sameDir.forEach(function(candidate) {
        const score = candidate.norm === norm ? 1 : (candidate.norm.endsWith(norm) || norm.endsWith(candidate.norm) ? 0.7 : 0);
        if (score > bestScore) {
          bestScore = score;
          best = { physicalIOId: physical.id, appVariable: candidate.appVariable, status: score >= 1 ? 'matched' : 'unmatched', matchScore: score };
        }
      });
      if (!best || bestScore <= 0) return { physicalIOId: physical.id, appVariable: '', status: 'unmatched', matchScore: 0 };
      return best;
    });
    return io.entries;
  }

  export function ioBuildCandidateOptions(context: IOMappingContext, direction: string): string[] {
    return ioCollectCandidateVariables(context)
      .filter(function(candidate) { return candidate.direction === direction; })
      .map(function(candidate) { return candidate.appVariable; });
  }

  function findEntryByLabel(entries: VarEntry[], label: string): VarEntry | null {
    return entries.find(function(entry) { return String((entry.data as ProjectVariable | UnitConfig)?.label || '') === label; }) || null;
  }

  function setSignalAddress(context: IOMappingContext, entry: VarEntry, sig: DeviceSignal, value: string): void {
    const project = context.project;
    if (entry.source === 'unit' && project.unitConfig && project.unitConfig[entry.key]) {
      const cfg = project.unitConfig[entry.key];
      const isKnownPath = context.varsApi.UNIT_SIGNALS.some(function(unitSig) { return unitSig.path === sig.path; });
      if (isKnownPath && sig.path) context.varsApi.gvtSetUnitAddr(cfg, sig.path, value);
      else {
        if (!cfg.signalAddresses) cfg.signalAddresses = {};
        cfg.signalAddresses[sig.id] = value;
      }
    } else if ((entry.source === 'imported' || entry.source === 'user') && project.variables && entry.bucket) {
      const list = project.variables[entry.bucket] as ProjectVariable[] | undefined;
      const rec = list && list[entry.key as number];
      if (!rec) return;
      if (!rec.signalAddresses) rec.signalAddresses = {};
      rec.signalAddresses[sig.id] = value;
    } else if (entry.source === 'excel' && project.excelVars) {
      const rec = project.excelVars[entry.key as number];
      if (!rec) return;
      if (!rec.signalAddresses) rec.signalAddresses = {};
      rec.signalAddresses[sig.id] = value;
    }
  }

  export function ioResolveVariableAddressTarget(context: IOMappingContext, appVariable: string): AddressTarget | null {
    const parts = String(appVariable || '').split('.');
    const label = parts.shift();
    const sigName = parts.join('.');
    if (!label) return null;
    const entry = findEntryByLabel(context.varsApi.gvtGetEntries(context.varsContext), label);
    if (!entry) return null;
    const variable = entry.data || {};
    if (!sigName) {
      return {
        get: function() { return (variable as ProjectVariable).address || ''; },
        set: function(value) { (variable as ProjectVariable).address = value; }
      };
    }
    const sigList = entry.source === 'unit'
      ? context.varsApi.gvtGetUnitSigList(context.varsContext)
      : context.varsApi.gvtGetSigList(context.varsContext, variable as ProjectVariable);
    const sig = sigList.find(function(item) { return item.name === sigName; });
    if (!sig) return null;
    return {
      get: function() {
        return entry.source === 'unit'
          ? context.varsApi.gvtGetUnitAddr(variable as UnitConfig, sig.path || sig.id)
          : context.varsApi.gvtGetExcelSignalAddress(variable as ProjectVariable, sig);
      },
      set: function(value) { setSignalAddress(context, entry, sig, value); }
    };
  }

  export function ioSetEntryMapped(context: IOMappingContext, physicalIOId: string, appVariable: string): IOMappingEntry | null {
    const io = ensureIO(context);
    const physical = (io.physicalIOs || []).find(function(item) { return item.id === physicalIOId; });
    if (!physical) return null;
    let entry = (io.entries || []).find(function(item) { return item.physicalIOId === physicalIOId; });
    if (!entry) {
      entry = { physicalIOId, appVariable: '', status: 'unmatched', matchScore: 0 };
      io.entries.push(entry);
    }
    entry.appVariable = appVariable || '';
    entry.status = entry.appVariable ? 'mapped' : 'unmatched';
    entry.matchScore = entry.appVariable ? 1 : 0;
    const target = ioResolveVariableAddressTarget(context, entry.appVariable);
    if (target) target.set(physical.plcAddress || '');
    return entry;
  }

  export function ioUnmapEntry(context: IOMappingContext, physicalIOId: string): IOMappingEntry | null {
    const io = ensureIO(context);
    const entry = (io.entries || []).find(function(item) { return item.physicalIOId === physicalIOId; });
    if (!entry) return null;
    const target = ioResolveVariableAddressTarget(context, entry.appVariable);
    if (target) target.set('');
    entry.appVariable = '';
    entry.status = 'unmatched';
    entry.matchScore = 0;
    return entry;
  }

  export interface IOMappingApi {
    ioNormalizeTag(value: unknown): string;
    ioCollectCandidateVariables(context: IOMappingContext): CandidateVariable[];
    ioAutoMatchEntries(context: IOMappingContext): IOMappingEntry[];
    ioBuildCandidateOptions(context: IOMappingContext, direction: string): string[];
    ioResolveVariableAddressTarget(context: IOMappingContext, appVariable: string): AddressTarget | null;
    ioSetEntryMapped(context: IOMappingContext, physicalIOId: string, appVariable: string): IOMappingEntry | null;
    ioUnmapEntry(context: IOMappingContext, physicalIOId: string): IOMappingEntry | null;
  }

  export const api: IOMappingApi = {
    ioNormalizeTag,
    ioCollectCandidateVariables,
    ioAutoMatchEntries,
    ioBuildCandidateOptions,
    ioResolveVariableAddressTarget,
    ioSetEntryMapped,
    ioUnmapEntry
  };
}

GrafcetStudioInterop.registerBridge('ioMapping', GrafcetStudioIOMapping.api);



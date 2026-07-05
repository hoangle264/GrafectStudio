namespace GrafcetStudioTree {
  type Project = GrafcetStudioProject.Project;
  type Unit = GrafcetStudioProject.Unit;
  type DiagramMeta = GrafcetStudioProject.DiagramMeta;
  type DeviceType = GrafcetStudioProject.DeviceType;
  type DeviceSignal = GrafcetStudioProject.DeviceSignal;
  type ProjectVariable = GrafcetStudioProject.ProjectVariable;

  export interface TreeContext {
    project: Project;
    now(): number;
    findNextAvailableBaseMr?: (unitId?: string | null, excludeDiagId?: string) => string;
    syncVariableSignalAddressesFromDeviceTypes?: () => boolean;
  }

  export interface DeviceSignalInput {
    id?: string;
    name: string;
    dataType: string;
    varType: string;
    address?: string;
    comment?: string;
  }

  export interface ConfirmDeviceTypeInput {
    modalDeviceId?: string | null;
    name: string;
    categoryId?: string;
    signals: DeviceSignalInput[];
  }

  export interface MutationResult<T> {
    ok: boolean;
    value?: T;
    message?: string;
  }

  export interface ConfirmDeviceTypeResult extends MutationResult<DeviceType> {
    renamedSignalIds: Record<string, string>;
  }

  export interface AddDiagramResult {
    diagram: DiagramMeta;
    emptyState: GrafcetStudioProject.DiagramState;
    nextId: number;
    nextStepNum: number;
    viewX: number;
    viewY: number;
    viewScale: number;
  }

  function ensureUnits(project: Project): Unit[] {
    if (!Array.isArray(project.units)) project.units = [];
    return project.units;
  }

  function ensureDiagrams(project: Project): DiagramMeta[] {
    if (!Array.isArray(project.diagrams)) project.diagrams = [];
    return project.diagrams;
  }

  function ensureDevices(project: Project): DeviceType[] {
    if (!Array.isArray(project.devices)) project.devices = [];
    return project.devices;
  }

  export function getNextUnitName(project: Project): string {
    return 'Unit_' + String(((project.units || []).length) + 1).padStart(2, '0') + '_';
  }

  export function addUnit(context: TreeContext, name: string): MutationResult<Unit> {
    const value = String(name || '').trim();
    if (!value) return { ok: false, message: 'Unit name is required.' };
    const unit: Unit = { id: 'unit-' + context.now(), name: value, open: true };
    ensureUnits(context.project).push(unit);
    return { ok: true, value: unit };
  }

  export function renameUnit(context: TreeContext, id: string, name: string): MutationResult<Unit> {
    const value = String(name || '').trim();
    if (!value) return { ok: false, message: 'Unit name is required.' };
    const unit = ensureUnits(context.project).find(item => item.id === id);
    if (!unit) return { ok: false, message: 'Unit not found.' };
    unit.name = value;
    return { ok: true, value: unit };
  }

  export function removeUnit(context: TreeContext, id: string): MutationResult<{ removed?: Unit; unassignedCount: number }> {
    const units = ensureUnits(context.project);
    const unit = units.find(item => item.id === id);
    if (!unit) return { ok: false, message: 'Unit not found.' };
    const diagrams = ensureDiagrams(context.project);
    const affected = diagrams.filter(diagram => diagram.unitId === id);
    affected.forEach(diagram => { diagram.unitId = null as unknown as string | undefined; });
    context.project.units = units.filter(item => item.id !== id);
    return { ok: true, value: { removed: unit, unassignedCount: affected.length } };
  }

  export function toggleUnitOpen(context: TreeContext, id: string): MutationResult<Unit> {
    const unit = ensureUnits(context.project).find(item => item.id === id);
    if (!unit) return { ok: false, message: 'Unit not found.' };
    unit.open = unit.open === false ? true : false;
    return { ok: true, value: unit };
  }

  export function addDiagramInUnit(context: TreeContext, unitId: string | null | undefined, mode: string): AddDiagramResult {
    const id = 'diag-' + context.now();
    const diagrams = ensureDiagrams(context.project);
    const unit = unitId ? (ensureUnits(context.project).find(item => item.id === unitId)?.name || '') : '';
    const resolvedMode = mode || 'Auto';
    const diagram: DiagramMeta = {
      id,
      name: 'GRAFCET_' + resolvedMode,
      unitId: (unitId || null) as unknown as string | undefined,
      mode: resolvedMode,
      diagramType: 'Macro',
      machine: context.project.machineName || context.project.name || 'Machine',
      unit,
      description: '',
      addressMode: 'bool',
      boolAddressMode: 'linear',
      baseMr: context.findNextAvailableBaseMr ? context.findNextAvailableBaseMr(unitId || null, id) : 100
    };
    diagrams.push(diagram);
    return {
      diagram,
      emptyState: { steps: [], transitions: [], connections: [], parallels: [], vars: [] } as GrafcetStudioProject.DiagramState,
      nextId: 1,
      nextStepNum: 1,
      viewX: 100,
      viewY: 80,
      viewScale: 1
    };
  }

  export function makeSignalIdFromName(signalName: string): string {
    return String(signalName || '').trim().replace(/[^a-zA-Z0-9_]+/g, '_').replace(/^_+|_+$/g, '');
  }

  function getVariableGroups(project: Project): ProjectVariable[][] {
    const groups: ProjectVariable[][] = [];
    if (project.variables) {
      groups.push(project.variables.imported || []);
      groups.push(project.variables.user || []);
    }
    groups.push(project.excelVars || []);
    return groups;
  }

  function rewriteSignalAddressKeys(project: Project, formatName: string, idMap: Record<string, string>): void {
    getVariableGroups(project).forEach(function(list) {
      (list || []).forEach(function(variable) {
        if (!variable || variable.format !== formatName || !variable.signalAddresses) return;
        const signalAddresses = variable.signalAddresses;
        Object.keys(idMap).forEach(function(oldId) {
          const newId = idMap[oldId];
          if (!Object.prototype.hasOwnProperty.call(signalAddresses, oldId)) return;
          if (!Object.prototype.hasOwnProperty.call(signalAddresses, newId)) {
            signalAddresses[newId] = signalAddresses[oldId];
          }
          delete signalAddresses[oldId];
        });
      });
    });
  }

  export function confirmDeviceType(context: TreeContext, input: ConfirmDeviceTypeInput): ConfirmDeviceTypeResult {
    const name = String(input.name || '').trim();
    if (!name) return { ok: false, message: 'Please enter a struct data name.', renamedSignalIds: {} };
    const devices = ensureDevices(context.project);
    const oldDevice = input.modalDeviceId ? devices.find(item => item.id === input.modalDeviceId) : null;
    const oldDeviceName = oldDevice && oldDevice.name ? oldDevice.name : name;
    const idMap: Record<string, string> = {};
    const usedSignalIds = new Set<string>();
    const duplicateIds = new Set<string>();
    const signals: DeviceSignal[] = [];

    (input.signals || []).forEach(function(signal) {
      const signalName = String(signal && signal.name || '').trim();
      if (!signalName) return;
      const nextId = makeSignalIdFromName(signalName);
      if (!nextId) {
        duplicateIds.add('');
        return;
      }
      if (usedSignalIds.has(nextId)) duplicateIds.add(nextId);
      usedSignalIds.add(nextId);
      const oldId = String(signal.id || '').trim();
      if (oldId && oldId !== nextId) idMap[oldId] = nextId;
      signals.push({
        id: nextId,
        name: signalName,
        dataType: signal.dataType || 'Bool',
        varType: signal.varType || 'Input',
        address: signal.address || '',
        comment: signal.comment || ''
      });
    });

    if (duplicateIds.has('')) {
      return { ok: false, message: 'Signal name cannot create a valid id. Use letters, numbers, or underscore.', renamedSignalIds: idMap };
    }
    if (duplicateIds.size) {
      return { ok: false, message: 'Duplicate signal name/id is not allowed: ' + Array.from(duplicateIds).join(', '), renamedSignalIds: idMap };
    }

    let device: DeviceType;
    if (input.modalDeviceId) {
      if (!oldDevice) return { ok: false, message: 'Struct data not found.', renamedSignalIds: idMap };
      oldDevice.name = name;
      oldDevice.categoryId = input.categoryId || 'cat-other';
      oldDevice.signals = signals;
      device = oldDevice;
    } else {
      device = { id: 'dev-' + context.now(), name, categoryId: input.categoryId || 'cat-other', open: true, signals };
      devices.push(device);
    }

    if (Object.keys(idMap).length) {
      rewriteSignalAddressKeys(context.project, oldDeviceName, idMap);
      if (oldDeviceName !== name) rewriteSignalAddressKeys(context.project, name, idMap);
    }
    if (context.syncVariableSignalAddressesFromDeviceTypes) context.syncVariableSignalAddressesFromDeviceTypes();
    return { ok: true, value: device, renamedSignalIds: idMap };
  }

  export function removeDeviceType(context: TreeContext, devId: string): MutationResult<DeviceType> {
    const devices = ensureDevices(context.project);
    const device = devices.find(item => item.id === devId);
    if (!device) return { ok: false, message: 'Struct data not found.' };
    context.project.devices = devices.filter(item => item.id !== devId);
    return { ok: true, value: device };
  }

  export interface TreeApi {
    getNextUnitName(project: Project): string;
    addUnit(context: TreeContext, name: string): MutationResult<Unit>;
    renameUnit(context: TreeContext, id: string, name: string): MutationResult<Unit>;
    removeUnit(context: TreeContext, id: string): MutationResult<{ removed?: Unit; unassignedCount: number }>;
    toggleUnitOpen(context: TreeContext, id: string): MutationResult<Unit>;
    addDiagramInUnit(context: TreeContext, unitId: string | null | undefined, mode: string): AddDiagramResult;
    makeSignalIdFromName(signalName: string): string;
    confirmDeviceType(context: TreeContext, input: ConfirmDeviceTypeInput): ConfirmDeviceTypeResult;
    removeDeviceType(context: TreeContext, devId: string): MutationResult<DeviceType>;
  }

  export const api: TreeApi = {
    getNextUnitName,
    addUnit,
    renameUnit,
    removeUnit,
    toggleUnitOpen,
    addDiagramInUnit,
    makeSignalIdFromName,
    confirmDeviceType,
    removeDeviceType
  };
}

GrafcetStudioInterop.registerBridge('tree', GrafcetStudioTree.api);




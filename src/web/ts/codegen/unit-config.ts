// Minimal Unit Config bridge for showGenerateCodeModal.
// The heavy in-browser Unit Config/template generator was removed; generation is routed to C# via modal.js.

type UcDeviceSignal = GrafcetStudioProject.DeviceSignal;
type UcDeviceType = GrafcetStudioProject.DeviceType;
type UcProjectVariable = GrafcetStudioProject.ProjectVariable;

declare function ensureProjectVariables(): GrafcetStudioProject.ProjectVariables;

let UC_UNIT_CONFIG: unknown = null;
let UC_CYLINDER_TYPES: unknown = null;
let UC_RUNTIME_DEVICE_META: unknown = null;
let UC_DEVICE_COMMAND_LIBRARY: unknown = null;

function ucGetUnitStationVars(): UcProjectVariable[] {
  if (typeof ensureProjectVariables === 'function') ensureProjectVariables();
  const buckets: UcProjectVariable[][] = [];
  if (typeof project !== 'undefined') {
    buckets.push(project.excelVars || []);
    if (project.variables) {
      buckets.push(project.variables.imported || []);
      buckets.push(project.variables.user || []);
    }
  }
  return buckets.flat().filter(function(v) {
    return v && v.format === 'Unit Station';
  });
}

function ucEnsureCylinderDeviceType(): UcDeviceType | null {
  if (typeof project === 'undefined') return null;
  project.devices = project.devices || [];
  let existing = project.devices.find(function(d) { return d && d.name === 'Cylinder'; });
  if (existing) return existing;

  const signals = (typeof GVT_CYL_SIGNALS !== 'undefined' ? GVT_CYL_SIGNALS : []).map(function(s) {
    return {
      id: s.id,
      name: s.name,
      dataType: s.dataType || 'Bool',
      varType: s.varType || 'Var',
      comment: s.comment || ''
    };
  });
  existing = { id: 'devtype-cylinder', name: 'Cylinder', signals: signals };
  project.devices.push(existing);
  return existing;
}

function ucBuildSyntheticConfig(selectedUnitId?: string): { unit: { label: string; unitIndex: number }; devices: unknown[] } | null {
  const units = (typeof project !== 'undefined' && project.units) || [];
  const unit = selectedUnitId && selectedUnitId !== '__none__'
    ? units.find(function(u) { return u.id === selectedUnitId; })
    : units[0];
  const unitVars = ucGetUnitStationVars();
  const picked = unitVars.find(function(v) {
    return unit && (v.label === unit.name || v.label === unit.id);
  }) || unitVars[0];

  if (!unit && !picked) return null;
  return {
    unit: {
      label: (picked && picked.label) || (unit && (unit.name || unit.id)) || 'Unit',
      unitIndex: 0
    },
    devices: []
  };
}

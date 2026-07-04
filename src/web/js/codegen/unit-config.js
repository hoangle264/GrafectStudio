"use strict";

// Minimal Unit Config bridge for showGenerateCodeModal.
// The heavy in-browser Unit Config/template generator was removed; generation is routed to C# via modal.js.

let UC_UNIT_CONFIG = null;
let UC_CYLINDER_TYPES = null;
let UC_RUNTIME_DEVICE_META = null;
let UC_DEVICE_COMMAND_LIBRARY = null;

function ucGetUnitStationVars() {
  if (typeof ensureProjectVariables === 'function') ensureProjectVariables();
  const buckets = [];
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

function ucEnsureCylinderDeviceType() {
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

function ucBuildSyntheticConfig(selectedUnitId) {
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

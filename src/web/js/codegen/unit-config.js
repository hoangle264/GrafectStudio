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

function ucLoadDeviceCommandLibrary(config) {
  UC_DEVICE_COMMAND_LIBRARY = config || null;
}

function cgUCLoadFile(inputId, onSuccess) {
  const input = document.getElementById(inputId);
  const file = input && input.files && input.files[0];
  if (!file) return;

  const reader = new FileReader();
  reader.onload = function() {
    try {
      const data = JSON.parse(reader.result || '{}');
      if (typeof onSuccess === 'function') onSuccess(data);
    } catch (err) {
      console.error('[unit-config] Invalid JSON:', err);
      if (typeof toast === 'function') toast('Invalid JSON: ' + (err.message || err));
    }
  };
  reader.readAsText(file);
}

function cgUCUpdateStatus() {
  const el = document.getElementById('uc-status');
  if (!el) return;
  const parts = [];
  parts.push(UC_UNIT_CONFIG ? 'Unit Config loaded' : 'Unit Config not loaded');
  if (UC_CYLINDER_TYPES) parts.push('Cylinder Types loaded');
  if (UC_RUNTIME_DEVICE_META) parts.push('Runtime Metadata loaded');
  if (UC_DEVICE_COMMAND_LIBRARY) parts.push('Device Library loaded');
  el.textContent = parts.join(' | ');
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

function cgUCBuildContext(unitConfig, selectedUnitId) {
  const cfg = unitConfig || ucBuildSyntheticConfig(selectedUnitId) || { unit: {}, devices: [] };
  return {
    unit: cfg.unit || {},
    devices: cfg.devices || [],
    cylinders: (cfg.devices || cfg.cylinders || []).filter(function(d) {
      return (d.kind || 'cylinder') === 'cylinder';
    }),
    stationFlows: [],
    originSteps: [],
    warnings: []
  };
}

function cgUCBuildTemplateContext(ctx) {
  return ctx || cgUCBuildContext(null, null);
}

function cgGenerateUnitConfig() {
  return JSON.stringify(UC_UNIT_CONFIG || ucBuildSyntheticConfig(null) || {}, null, 2);
}

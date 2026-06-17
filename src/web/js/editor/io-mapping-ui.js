function ioNormalizeTag(v) {
  return ioGetApi().ioNormalizeTag(v);
}

function ioCollectCandidateVariables() {
  return ioGetApi().ioCollectCandidateVariables(ioGetContext());
}

function ioAutoMatchEntries() {
  ioGetApi().ioAutoMatchEntries(ioGetContext());
  saveProject();
}
function ioBuildCandidateOptions(direction) {
  return ioGetApi().ioBuildCandidateOptions(ioGetContext(), direction);
}

function ioResolveVariableAddressTarget(appVariable) {
  const target = ioGetApi().ioResolveVariableAddressTarget(ioGetContext(), appVariable);
  if (!target) return null;
  return {
    get: target.get,
    set: function(value) {
      target.set(value);
      saveProject();
      renderGlobalVarTable();
      if(typeof updateVarDatalist === 'function') updateVarDatalist();
    }
  };
}

function ioSetEntryMapped(physicalIOId, appVariable) {
  const entry = ioGetApi().ioSetEntryMapped(ioGetContext(), physicalIOId, appVariable);
  if (!entry) return;
  saveProject();
  renderIOMappingTable(document.getElementById('iomap-filter')?.value || 'All');
}
function ioUnmapEntry(physicalIOId) {
  const entry = ioGetApi().ioUnmapEntry(ioGetContext(), physicalIOId);
  if (!entry) return;
  saveProject();
  renderIOMappingTable(document.getElementById('iomap-filter')?.value || 'All');
}
function ioConfirmManual(physicalIOId) {
  const sel = document.getElementById('iomap-sel-' + physicalIOId);
  if (!sel) return;
  ioSetEntryMapped(physicalIOId, sel.value || '');
}

function renderIOMappingTable(filter) {
  if (typeof ensureProjectIOMapping === 'function') ensureProjectIOMapping();
  const tbody = document.getElementById('iomap-tbody');
  if (!tbody) return;
  const io = project.ioMapping || { physicalIOs: [], entries: [] };
  const byId = Object.create(null);
  (io.entries || []).forEach(function (e) { byId[e.physicalIOId] = e; });
  const eff = filter || 'All';
  const rows = io.physicalIOs.filter(function (p) {
    const e = byId[p.id] || { status: 'unmatched' };
    if (eff === 'Input') return p.direction === 'Input';
    if (eff === 'Output') return p.direction === 'Output';
    if (eff === 'Unmatched') return e.status === 'unmatched';
    return true;
  });
  tbody.innerHTML = '';
  if (!rows.length) {
    tbody.innerHTML = '<tr><td colspan="8" class="vt-empty">No IO mapping rows</td></tr>';
    return;
  }
  rows.forEach(function (p) {
    const e = byId[p.id] || { physicalIOId: p.id, appVariable: '', status: 'unmatched', matchScore: 0 };
    const tr = document.createElement('tr');
    const opts = ioBuildCandidateOptions(p.direction);
    const mappedAddress = ioResolveVariableAddressTarget(e.appVariable)?.get() || '';
    const manualCell = e.status === 'unmatched'
      ? '<select id="iomap-sel-' + p.id + '" class="dp-select iomap-select"><option value=""></option>' + opts.map(function (o) { return '<option value="' + esc2(o) + '">' + esc2(o) + '</option>'; }).join('') + '</select>'
      : '<span class="iomap-var">' + esc2(e.appVariable || '') + '</span><span class="iomap-address">' + esc2(mappedAddress || p.plcAddress || '') + '</span>';
    const statusClass = e.status === 'unmatched' ? 'iomap-status-unmapped' : 'iomap-status-mapped';
    const action = e.status === 'unmatched'
      ? '<button class="panel-head-btn btn-cyan" onclick="ioConfirmManual(\'' + p.id + '\')">Map</button>'
      : '<button class="panel-head-btn" onclick="ioUnmapEntry(\'' + p.id + '\')">Unmap</button>';
    tr.innerHTML = '<td>' + esc2(p.deviceTag || '') + '</td><td class="vt-cell addr">' + esc2(p.plcAddress || '') + '</td><td>' + esc2(p.direction || '') + '</td><td>' + esc2(p.description || '') + '</td><td>' + manualCell + '</td><td><span class="iomap-status ' + statusClass + '">' + esc2(e.status || 'unmatched') + '</span></td><td>' + esc2(String(e.matchScore ?? 0)) + '</td><td class="iomap-actions">' + action + '</td>';
    tbody.appendChild(tr);
  });
}

function clearIOMappingImport() {
  if (typeof ensureProjectIOMapping === 'function') ensureProjectIOMapping();
  const hasData = (project.ioMapping.physicalIOs || []).length || (project.ioMapping.entries || []).length;
  if (!hasData) {
    toast('No IO mapping data to clear');
    return;
  }
  if (!confirm('Clear all imported IO Mapping data?')) return;
  project.ioMapping.physicalIOs = [];
  project.ioMapping.entries = [];
  saveProject();
  renderIOMappingTable(document.getElementById('iomap-filter')?.value || 'All');
  toast('IO Mapping data cleared');
}

function exportIOCode() {
  if (typeof ensureProjectIOMapping === 'function') ensureProjectIOMapping();
  const io = project.ioMapping || { physicalIOs: [], entries: [] };
  const byId = Object.create(null);
  (io.entries || []).forEach(function (e) { byId[e.physicalIOId] = e; });

  const inputLines = [];
  const outputLines = [];

  (io.physicalIOs || []).forEach(function (p) {
    const e = byId[p.id] || { appVariable: '' };
    if (!e.appVariable) return;

    const plcAddr = p.plcAddress || '';
    const appAddr = ioResolveVariableAddressTarget(e.appVariable)?.get() || e.appVariable || '';

    if (p.direction === 'Input') {
      inputLines.push('LD ' + plcAddr);
      inputLines.push('OUT ' + appAddr);
    } else if (p.direction === 'Output') {
      outputLines.push('LD ' + appAddr);
      outputLines.push('OUT ' + plcAddr);
    }
  });

  let code = [];
  if (inputLines.length) {
    code.push(';Input');
    code = code.concat(inputLines);
  }
  if (outputLines.length) {
    code.push(';Output');
    code = code.concat(outputLines);
  }

  if (!code.length) {
    toast('No mapped IO entries to export');
    return;
  }

  const text = code.join('\n');
  const blob = new Blob([text], { type: 'text/plain;charset=utf-8' });
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = (project.name || 'io-mapping').replace(/\s+/g, '_') + '_code.txt';
  a.click();
  toast('IO Mapping code exported');
}

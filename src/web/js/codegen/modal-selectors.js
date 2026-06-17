//  Build unit selector cho Unit Config mode

// Build unit radio list 
function cgBuildUnitList() {
  const wrap = document.getElementById('cg-unit-list');
  if (!wrap) return;
  wrap.innerHTML = '';

  const units = project.units || [];
  const allDiags = project.diagrams || [];
  const hasOrphans = allDiags.some(d => !d.unitId);

  if (!units.length && !hasOrphans) {
    wrap.innerHTML = '<span style="font-size:10px;color:var(--text3)">No units found</span>';
    return;
  }

  let firstId = null;

  units.forEach((u, i) => {
    const count = allDiags.filter(d => d.unitId === u.id).length;
    const lbl = document.createElement('label');
    lbl.className = 'cg-radio-lbl';
    lbl.innerHTML = `<input type="radio" name="cg-unit-radio" value="${u.id}"
      onchange="cgOnUnitSelect('${u.id}')">${esc2(u.name || u.id)}
      <span style="color:var(--text3);font-size:8px;margin-left:2px;">(${count})</span>`;
    wrap.appendChild(lbl);
    if (i === 0) firstId = u.id;
  });

  if (hasOrphans) {
    const count = allDiags.filter(d => !d.unitId).length;
    const lbl = document.createElement('label');
    lbl.className = 'cg-radio-lbl';
    lbl.innerHTML = `<input type="radio" name="cg-unit-radio" value="__none__"
      onchange="cgOnUnitSelect('__none__')">(No unit)
      <span style="color:var(--text3);font-size:8px;margin-left:2px;">(${count})</span>`;
    wrap.appendChild(lbl);
    if (!firstId) firstId = '__none__';
  }

  // Pre-select first unit and render its diagrams
  if (firstId) {
    const radio = wrap.querySelector(`input[value="${firstId}"]`);
    if (radio) radio.checked = true;
    cgBuildDiagForUnit(firstId);
  }
}

//  Called when user selects a unit radio button 
function cgOnUnitSelect(unitId) {
  cgBuildDiagForUnit(unitId);
  const pre = document.getElementById('cg-preview');
  const stat = document.getElementById('cg-stat');
  if (pre) pre.textContent = '; Unit selected. Click Send selected to generate payload.';
  if (stat) stat.textContent = 'Unit selected';
}

// Build diagram checkboxes for the selected unit
function cgBuildDiagForUnit(unitId) {
  const listWrap = document.getElementById('cg-diag-list');
  const section  = document.getElementById('cg-unit-diag-section');
  if (!listWrap) return;
  listWrap.innerHTML = '';

  const diags = (project.diagrams || []).filter(d =>
    unitId === '__none__' ? !d.unitId : d.unitId === unitId
  );

  if (section) section.style.display = diags.length ? '' : 'none';

  diags.forEach(d => {
    const modeName = d.mode || 'Auto';
    const lbl = document.createElement('label');
    lbl.className = 'cg-diag-chip';
    lbl.dataset.diagId = d.id;
    lbl.innerHTML = `<input type="checkbox" value="${d.id}" checked
      onchange="cgUpdatePreview()" style="margin-right:4px;">
      <span>${esc2(d.name || d.id)}</span>
      <span style="color:var(--text3);font-size:8px;margin-left:2px;">[${modeName}]</span>`;
    listWrap.appendChild(lbl);
  });
}

function cgSelectAll(val) {
  document.querySelectorAll('#cg-diag-list input[type=checkbox]')
    .forEach(c => { c.checked = val; });
  cgUpdatePreview();
}


//  Live preview 
function cgUpdatePreview() {
  const target = document.getElementById('cg-target')?.value || 'unit-config';
  const platform = cgResolveHostPlatform(target);

  // All codegen targets use a single selected Unit Config payload.
  const baseMRWrap  = document.getElementById('cg-base-mr-wrap');
  const unitWrap    = document.getElementById('cg-unit-wrap');
  if (baseMRWrap) baseMRWrap.style.display = 'none';
  if (unitWrap)   unitWrap.style.display   = '';

  const pre  = document.getElementById('cg-preview');
  const stat = document.getElementById('cg-stat');
  if (!pre) return;

  pre.textContent = '; Select a unit above to generate payload.';
  if (stat) stat.textContent = 'Waiting for unit selection';
}

// Syntax highlight cho Unit Config output 
function cgResolveHostPlatform(target) {
  return {
    'csharp-kv-5500': 'kv-5500',
    'csharp-twincat-st': 'twincat-st'
  }[target] || target || 'kv-5500';
}
function cgGetDefaultUnitId() {
  const unitRadio = document.querySelector('#cg-unit-list input[name="cg-unit-radio"]:checked');
  if (unitRadio) return unitRadio.value;
  const firstUnit = (project.units || [])[0];
  if (firstUnit) return firstUnit.id || '';
  return (project.diagrams || []).some(d => !d.unitId) ? '__none__' : '';
}

function cgGetSelectedDiagramIds() {
  return Array.from(
    document.querySelectorAll('#cg-diag-list input[type=checkbox]:checked')
  ).map(c => c.value);
}

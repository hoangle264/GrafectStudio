"use strict";

type EiExcelImportResult = GrafcetStudioProject.ExcelImportResult;

declare function syncStructDataFromProjectData(): boolean;
declare function renderVarTable(): void;

interface EiImportOptions {
  structType?: string;
}

function eiGetParserApi(): GrafcetStudioExcelImport.ExcelImportApi {
  const api = window.GrafcetStudio && window.GrafcetStudio.excelImport as GrafcetStudioExcelImport.ExcelImportApi | undefined;
  if (!api) throw new Error('GrafcetStudio excel import bridge is not loaded');
  return api;
}

function eiParseCSV(text: string): string[][] {
  return eiGetParserApi().parseCSV(text);
}

function eiParseUnitCSV(rows: string[][]): GrafcetStudioProject.UnitCSVParseResult {
  return eiGetParserApi().parseUnitCSV(rows);
}

function eiParseStructCSV(rows: string[][], structTypeName: string): GrafcetStudioProject.StructCSVParseResult {
  return eiGetParserApi().parseStructCSV(rows, structTypeName, project.devices || []);
}

function eiParsePhysicalIOCSV(rows: string[][]): GrafcetStudioProject.PhysicalIOCSVParseResult {
  return eiGetParserApi().parsePhysicalIOCSV(
    rows,
    typeof normalizeIOMappingDirection === 'function' ? normalizeIOMappingDirection : undefined
  );
}

// Labels of every struct instance in the project; a plain variable may not reuse one.
function eiStructInstanceLabels(): string[] {
  const vars = project.variables || { imported: [], user: [] };
  const structNames = new Set((project.devices || []).map(function (d) { return d && d.name; }));
  return ([] as GrafcetStudioProject.ProjectVariable[])
    .concat(vars.imported || [], vars.user || [], project.excelVars || [])
    .filter(function (v) {
      return !!v && !!v.label && (v.kind === 'struct' || !!v.signalAddresses || structNames.has(v.dataType || v.format || ''));
    })
    .map(function (v) { return v.label as string; });
}

function eiImportedVarCount(): number {
  return ((project.variables && project.variables.imported) || []).length + (project.excelVars || []).length;
}

function eiImportFromCSVText(csvText: string, csvType: string, options?: EiImportOptions): EiExcelImportResult {
  const rows = eiParseCSV(csvText);
  if (!rows.length) return { ok: false, message: 'CSV file is empty or unreadable.', added: 0 };

  if (csvType === 'physical-io') {
    if (typeof ensureProjectIOMapping === 'function') ensureProjectIOMapping();
    const parsed = eiParsePhysicalIOCSV(rows);
    if (parsed.errors.length) return { ok: false, message: 'Validation errors:\n' + parsed.errors.join('\n'), added: 0 };
    project.ioMapping.physicalIOs = parsed.physicalIOs;
    if (typeof ioAutoMatchEntries === 'function') ioAutoMatchEntries();
    if (typeof saveProject === 'function') saveProject();
    return { ok: true, message: 'Import thanh cong ' + parsed.physicalIOs.length + ' Physical IO.', added: parsed.physicalIOs.length };
  }

  if (csvType === 'unit') {
    const parsed = eiParseUnitCSV(rows);
    if (parsed.errors.length) return { ok: false, message: 'Validation errors:\n' + parsed.errors.join('\n'), added: 0 };
    if (!parsed.configs.length) return { ok: false, message: 'No valid Unit Station rows found.', added: 0 };

    if (!project.unitConfig) project.unitConfig = {};
    parsed.configs.forEach(function (cfg) { project.unitConfig[cfg.label] = cfg; });

    if (typeof syncStructDataFromProjectData === 'function') syncStructDataFromProjectData();
    if (typeof saveProject === 'function') saveProject();
    return { ok: true, message: 'Import thanh cong ' + parsed.configs.length + ' unit station.', added: parsed.configs.length };
  }

  if (csvType === 'primitive') {
    const parsed = eiGetParserApi().parsePrimitiveCSV(rows, project.devices || [], eiStructInstanceLabels());
    if (parsed.errors.length) return { ok: false, message: 'Validation errors:\n' + parsed.errors.join('\n'), added: 0 };
    if (!parsed.vars.length) return { ok: false, message: 'No valid variable rows found.', added: 0 };
    if (typeof ensureProjectVariables === 'function') ensureProjectVariables();
    parsed.vars.forEach(function (v) { upsertProjectVariable('imported', v); });
    if (typeof saveProject === 'function') saveProject();
    if (typeof updateVarDatalist === 'function') updateVarDatalist();
    return { ok: true, message: 'Import thanh cong ' + parsed.vars.length + ' bien.', added: parsed.vars.length };
  }

  const selectedStructType = (options && options.structType) || '';
  if (!selectedStructType) return { ok: false, message: 'Select a Struct Data type before importing.', added: 0 };

  const parsed = eiParseStructCSV(rows, selectedStructType);
  if (parsed.errors.length) return { ok: false, message: 'Validation errors:\n' + parsed.errors.join('\n'), added: 0 };
  if (!parsed.vars.length) return { ok: false, message: 'No valid data rows found for "' + selectedStructType + '".', added: 0 };

  if (typeof ensureProjectVariables === 'function') ensureProjectVariables();
  if (!project.excelVars) project.excelVars = [];
  parsed.vars.forEach(function (v) {
    if (typeof upsertProjectVariable === 'function') {
      upsertProjectVariable('imported', v);
    } else {
      const idx = project.excelVars.findIndex(function (e) { return e.label === v.label && e.format === v.format; });
      if (idx >= 0) project.excelVars[idx] = v;
      else project.excelVars.push(v);
    }
  });

  if (typeof syncStructDataFromProjectData === 'function') syncStructDataFromProjectData();
  if (typeof saveProject === 'function') saveProject();
  return { ok: true, message: 'Import thanh cong ' + parsed.vars.length + ' instance "' + selectedStructType + '".', added: parsed.vars.length };
}
// ── Modal ──────────────────────────────────────────────────────────────────
function showExcelImportModal(defaultType?: string): void {
  const existing = document.getElementById('modal-excel-import');
  if (existing) existing.remove();

  const structTypes  = (project.devices || []).map(function (d) { return d.name; });
  const structOptions = structTypes.length
    ? structTypes.map(function (n) { return '<option value="' + n + '">' + n + '</option>'; }).join('')
    : '<option value="">(No Struct Data yet)</option>';

  const unitCount = Object.keys(project.unitConfig || {}).length;
  const varCount  = eiImportedVarCount();
  const initialType = defaultType === 'physical-io' || defaultType === 'unit' || defaultType === 'struct' || defaultType === 'primitive' ? defaultType : 'struct';

  const el = document.createElement('div');
  el.id = 'modal-excel-import';
  el.className = 'modal-bg show';
  el.innerHTML = `
    <div class="modal dlg-modal ei-modal">

      <div class="dlg-head">
        <span class="dlg-head-icon">📥</span>
        <span class="ei-title">EXCEL IMPORT</span>
        <span class="ei-spacer"></span>
        <button class="btn" onclick="closeModal('modal-excel-import')">✕</button>
      </div>

      <div class="ei-summary">
        Currently:
        <span class="ei-accent">${varCount} variables</span>
        <span class="ei-accent">${unitCount} unit station</span>
        ${varCount > 0
          ? '<button class="btn r btn-sm" onclick="eiClearExcelVars()">🗑 Clear all</button>'
          : ''}
      </div>

      <div class="ei-body">
        <div class="ei-field">
          <div class="ei-label">DATA TYPE</div>
          <div class="ei-radios">
            <label class="ei-radio">
              <input type="radio" name="ei-import-type" value="unit" ${initialType === 'unit' ? 'checked' : ''} onchange="eiOnImportTypeChange()"> Unit Station
            </label>
            <label class="ei-radio">
              <input type="radio" name="ei-import-type" value="struct" ${initialType === 'struct' ? 'checked' : ''} onchange="eiOnImportTypeChange()"> Struct Data
            </label>
            <label class="ei-radio">
              <input type="radio" name="ei-import-type" value="primitive" ${initialType === 'primitive' ? 'checked' : ''} onchange="eiOnImportTypeChange()"> Variables
            </label>
            <label class="ei-radio">
              <input type="radio" name="ei-import-type" value="physical-io" ${initialType === 'physical-io' ? 'checked' : ''} onchange="eiOnImportTypeChange()"> Physical IO
            </label>
          </div>
        </div>

        <div id="ei-struct-wrap" class="ei-field${initialType === 'struct' ? '' : ' is-hidden'}">
          <div class="ei-label">STRUCT DATA TYPE</div>
          <select id="ei-struct-type" class="ei-select" onchange="eiOnImportTypeChange()">
            ${structOptions}
          </select>
          <div class="ei-note">
            Col 0 = Label · Header row names the signals to fill (nested: Member.Signal); any subset, any order.
            Signals without a column or with an empty cell are imported blank, replacing existing addresses.
          </div>
        </div>

        <div class="ei-field">
          <div class="ei-label">FILE CSV</div>
          <input type="file" id="ei-file-input" class="ei-file" accept=".csv,.txt"
            onchange="eiPreviewFile(this)">
        </div>

        <div id="ei-preview" class="ei-field is-hidden">
          <div class="ei-label">PREVIEW</div>
          <pre id="ei-preview-text" class="ei-preview-text"></pre>
        </div>

        <div id="ei-schema-hint" class="ei-schema-hint"></div>
      </div>

      <div class="dlg-foot">
        <span id="ei-stat" class="ei-stat"></span>
        <button class="btn" onclick="closeModal('modal-excel-import')">Close</button>
        <button class="btn a" id="ei-import-btn" onclick="eiDoImport()" disabled>↓ Import</button>
      </div>
    </div>`;

  document.body.appendChild(el);
  eiOnImportTypeChange();
}

function eiGetSchemaHintHtml(type: string, structType?: string): string {
  if (type === 'unit') {
    return '<b class="ei-accent">Unit Station CSV</b>: UnitName · flagOrigin · flagAuto · flagManual · originBaseAddr · autoBaseAddr · flagError · btnStart · hmiStop · btnReset · eStop · outHomed';
  }
  if (type === 'physical-io') {
    return '<b class="ei-accent">Physical IO CSV</b>: DeviceTag · PLCAddress · Direction · Description<br>Direction: input/in or output/out';
  }
  if (type === 'primitive') {
    return '<b class="ei-accent">Variables CSV</b>: Label · Address · Type · Comment<br>Type defaults to BOOL. Names already used by a struct instance are rejected.';
  }
  const leaves = structType ? eiGetParserApi().structLeafPaths(structType, project.devices || []) : [];
  if (!leaves.length) return '<b class="ei-accent">Struct Data CSV</b>: Label | Signal1 | Signal2 | ...';
  return '<b class="ei-accent">Struct Data CSV</b> (' + esc(structType) + '): Label · ' + leaves.map(esc).join(' · ')
    + '<br>Without a header row, columns follow this order.';
}

function eiGetImportTypeLabel(type: string, structType?: string): string {
  if (type === 'unit') return 'Unit Station';
  if (type === 'physical-io') return 'Physical IO';
  if (type === 'primitive') return 'Variables';
  return 'Struct Data' + (structType ? ' (' + structType + ')' : '');
}

// ── Xử lý thay đổi loại import ────────────────────────────────────────────
function eiOnImportTypeChange(): void {
  const radio = document.querySelector<HTMLInputElement>('input[name="ei-import-type"]:checked');
  const type  = radio ? radio.value : 'struct';
  const structWrap = document.getElementById('ei-struct-wrap');
  const schemaHint = document.getElementById('ei-schema-hint');
  const st         = document.getElementById('ei-struct-type') as HTMLSelectElement | null;

  if (structWrap) structWrap.classList.toggle('is-hidden', type !== 'struct');
  if (schemaHint) {
    schemaHint.innerHTML = eiGetSchemaHintHtml(type, st ? st.value : '');
  }

  if (_eiPendingText) {
    const rows = eiParseCSV(_eiPendingText);
    const stat = document.getElementById('ei-stat');
    if (stat) stat.textContent = 'Mode: ' + eiGetImportTypeLabel(type, st ? st.value : '') + '  (' + rows.length + ' rows)';
  }
}

// ── Preview file ───────────────────────────────────────────────────────────
let _eiPendingText: string | null = null;

function eiPreviewFile(inputEl: HTMLInputElement): void {
  _eiPendingText = null;
  const file = inputEl.files && inputEl.files[0];
  if (!file) return;

  const reader = new FileReader();
  reader.onload = function (e) {
    _eiPendingText = e.target!.result as string;

    const previewEl  = document.getElementById('ei-preview');
    const previewTxt = document.getElementById('ei-preview-text');
    const importBtn  = document.getElementById('ei-import-btn') as HTMLButtonElement | null;
    const stat       = document.getElementById('ei-stat');

    if (previewEl)  previewEl.classList.remove('is-hidden');
    if (importBtn)  importBtn.disabled = false;
    if (previewTxt) {
      const lines = _eiPendingText!.split(/\r?\n/).slice(0, 10);
      previewTxt.textContent = lines.join('\n') + (lines.length >= 10 ? '\n...' : '');
    }
    if (stat) {
      const radio  = document.querySelector<HTMLInputElement>('input[name="ei-import-type"]:checked');
      const type   = radio ? radio.value : 'struct';
      const st     = document.getElementById('ei-struct-type') as HTMLSelectElement | null;
      const rows   = eiParseCSV(_eiPendingText!);
      const label  = eiGetImportTypeLabel(type, st ? st.value : '');
      stat.textContent = 'Mode: ' + label + '  (' + rows.length + ' rows)';
    }
  };
  reader.readAsText(file);
}

// ── Thực hiện import ───────────────────────────────────────────────────────
function eiDoImport(): void {
  if (!_eiPendingText) return;

  const radio            = document.querySelector<HTMLInputElement>('input[name="ei-import-type"]:checked');
  const csvType          = radio ? radio.value : 'struct';
  const structTypeSel    = document.getElementById('ei-struct-type') as HTMLSelectElement | null;
  const selectedStructType = structTypeSel ? structTypeSel.value : '';
  const result           = eiImportFromCSVText(_eiPendingText, csvType, { structType: selectedStructType });

  const stat = document.getElementById('ei-stat');
  if (stat) stat.textContent = result.message;

  if (result.ok) {
    if (typeof toast === 'function') toast('✓ ' + result.message);
    if (typeof renderVarTable === 'function') renderVarTable();
    if (typeof renderTree === 'function') renderTree();
    _eiPendingText = null;
    setTimeout(function () { closeModal('modal-excel-import'); showExcelImportModal(csvType); }, 400);
  } else {
    if (typeof toast === 'function') toast('⚠ ' + result.message.split('\n')[0]);
    console.error('[excel-import]', result.message);
  }
}

// ── Xoá toàn bộ dữ liệu đã import ────────────────────────────────────────
async function eiClearExcelVars(): Promise<void> {
  if (!(await confirmDialog('Clear Excel data', 'Clear all imported Excel data?', 'Clear', true))) return;
  runUndoableProjectChange('Excel import data cleared', () => {
    if (typeof ensureProjectVariables === 'function') ensureProjectVariables().imported = [];
    project.excelVars = [];
    project.unitConfig = {};
    if (typeof saveProject === 'function') saveProject();
    if (typeof renderVarTable === 'function') renderVarTable();
    if (typeof updateVarDatalist === 'function') updateVarDatalist();
    closeModal('modal-excel-import');
    showExcelImportModal();
  });
}

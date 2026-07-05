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

function eiImportFromCSVText(csvText: string, csvType: string, options?: EiImportOptions): EiExcelImportResult {
  const rows = eiParseCSV(csvText);
  if (!rows.length) return { ok: false, message: 'File CSV trong hoac khong doc duoc.', added: 0 };

  if (csvType === 'physical-io') {
    if (typeof ensureProjectIOMapping === 'function') ensureProjectIOMapping();
    const parsed = eiParsePhysicalIOCSV(rows);
    if (parsed.errors.length) return { ok: false, message: 'Loi validate:\n' + parsed.errors.join('\n'), added: 0 };
    project.ioMapping.physicalIOs = parsed.physicalIOs;
    if (typeof ioAutoMatchEntries === 'function') ioAutoMatchEntries();
    if (typeof saveProject === 'function') saveProject();
    return { ok: true, message: 'Import thanh cong ' + parsed.physicalIOs.length + ' Physical IO.', added: parsed.physicalIOs.length };
  }

  if (csvType === 'unit') {
    const parsed = eiParseUnitCSV(rows);
    if (parsed.errors.length) return { ok: false, message: 'Loi validate:\n' + parsed.errors.join('\n'), added: 0 };
    if (!parsed.configs.length) return { ok: false, message: 'Khong tim thay dong Unit Station hop le.', added: 0 };

    if (!project.unitConfig) project.unitConfig = {};
    parsed.configs.forEach(function (cfg) { project.unitConfig[cfg.label] = cfg; });

    if (typeof syncStructDataFromProjectData === 'function') syncStructDataFromProjectData();
    if (typeof saveProject === 'function') saveProject();
    return { ok: true, message: 'Import thanh cong ' + parsed.configs.length + ' unit station.', added: parsed.configs.length };
  }

  const selectedStructType = (options && options.structType) || '';
  if (!selectedStructType) return { ok: false, message: 'Vui long chon Struct Data truoc khi import.', added: 0 };

  const parsed = eiParseStructCSV(rows, selectedStructType);
  if (parsed.errors.length) return { ok: false, message: 'Loi validate:\n' + parsed.errors.join('\n'), added: 0 };
  if (!parsed.vars.length) return { ok: false, message: 'Khong tim thay dong du lieu hop le cho "' + selectedStructType + '".', added: 0 };

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
function showExcelImportModal(): void {
  const existing = document.getElementById('modal-excel-import');
  if (existing) existing.remove();

  const structTypes  = (project.devices || []).map(function (d) { return d.name; });
  const structOptions = structTypes.length
    ? structTypes.map(function (n) { return '<option value="' + n + '">' + n + '</option>'; }).join('')
    : '<option value="">(Chưa có Struct Data)</option>';

  const unitCount = Object.keys(project.unitConfig || {}).length;
  const varCount  = (project.excelVars || []).length;

  const el = document.createElement('div');
  el.id = 'modal-excel-import';
  el.className = 'modal-bg show';
  el.innerHTML = `
    <div class="modal" style="min-width:500px;max-width:90vw;max-height:88vh;
      display:flex;flex-direction:column;padding:0;overflow:hidden;">

      <div style="padding:12px 20px;background:var(--s3);border-bottom:1px solid var(--border);
        display:flex;align-items:center;gap:10px;flex-shrink:0;">
        <span style="font-size:14px;">📥</span>
        <span style="font-size:12px;letter-spacing:2px;font-family:'Orbitron',monospace;">EXCEL IMPORT</span>
        <span style="flex:1;"></span>
        <button class="btn" onclick="closeModal('modal-excel-import')">✕</button>
      </div>

      <div style="padding:10px 20px;background:var(--s2);border-bottom:1px solid var(--border);
        font-size:10px;color:var(--text3);flex-shrink:0;display:flex;align-items:center;gap:12px;">
        Hiện có:
        <span style="color:var(--cyan);">${varCount} biến</span>
        <span style="color:var(--cyan);">${unitCount} unit station</span>
        ${varCount > 0
          ? '<button class="btn" onclick="eiClearExcelVars()" style="font-size:9px;padding:2px 8px;border-color:#f87171;color:#f87171;">🗑 Xoá tất cả</button>'
          : ''}
      </div>

      <div style="padding:16px 20px;flex:1;overflow-y:auto;">
        <div style="margin-bottom:14px;">
          <div style="font-size:9px;color:var(--text3);letter-spacing:1px;margin-bottom:6px;">LOẠI DỮ LIỆU</div>
          <div style="display:flex;gap:16px;">
            <label style="font-size:10px;color:var(--cyan);display:flex;align-items:center;gap:4px;cursor:pointer;">
              <input type="radio" name="ei-import-type" value="unit" onchange="eiOnImportTypeChange()"> Unit Station
            </label>
            <label style="font-size:10px;color:var(--cyan);display:flex;align-items:center;gap:4px;cursor:pointer;">
              <input type="radio" name="ei-import-type" value="struct" checked onchange="eiOnImportTypeChange()"> Struct Data
            </label>
            <label style="font-size:10px;color:var(--cyan);display:flex;align-items:center;gap:4px;cursor:pointer;">
              <input type="radio" name="ei-import-type" value="physical-io" onchange="eiOnImportTypeChange()"> Physical IO
            </label>
          </div>
        </div>

        <div id="ei-struct-wrap" style="margin-bottom:14px;">
          <div style="font-size:9px;color:var(--text3);letter-spacing:1px;margin-bottom:6px;">STRUCT DATA TYPE</div>
          <select id="ei-struct-type"
            style="width:100%;font-size:10px;color:var(--cyan);background:var(--bg);
            border:1px solid var(--border);border-radius:3px;padding:4px 8px;">
            ${structOptions}
          </select>
          <div style="margin-top:4px;font-size:9px;color:var(--text3);">
            Col 0 = Label · Col 1..N map theo thứ tự signal của Struct Data đã chọn
          </div>
        </div>

        <div style="margin-bottom:14px;">
          <div style="font-size:9px;color:var(--text3);letter-spacing:1px;margin-bottom:6px;">FILE CSV</div>
          <input type="file" id="ei-file-input" accept=".csv,.txt"
            style="font-size:10px;color:var(--cyan);background:var(--bg);
            border:1px solid var(--border);border-radius:3px;padding:4px 8px;width:100%;box-sizing:border-box;"
            onchange="eiPreviewFile(this)">
        </div>

        <div id="ei-preview" style="display:none;margin-bottom:14px;">
          <div style="font-size:9px;color:var(--text3);letter-spacing:1px;margin-bottom:6px;">XEM TRƯỚC</div>
          <pre id="ei-preview-text" style="font-size:10px;font-family:'JetBrains Mono',monospace;
            background:var(--bg);border:1px solid var(--border);border-radius:3px;
            padding:8px;max-height:180px;overflow:auto;color:var(--text2);margin:0;"></pre>
        </div>

        <div id="ei-schema-hint" style="font-size:9px;color:var(--text3);line-height:1.6;">
          <b style="color:var(--cyan);">Struct Data CSV</b>: Label | Signal1 | Signal2 | ...
        </div>
      </div>

      <div style="padding:10px 20px;border-top:1px solid var(--border);
        display:flex;gap:8px;justify-content:flex-end;flex-shrink:0;background:var(--s3);">
        <span id="ei-stat" style="flex:1;font-size:9px;color:var(--text3);align-self:center;"></span>
        <button class="btn" onclick="closeModal('modal-excel-import')">Đóng</button>
        <button class="btn a" id="ei-import-btn" onclick="eiDoImport()" disabled>↓ Import</button>
      </div>
    </div>`;

  document.body.appendChild(el);
}

// ── Xử lý thay đổi loại import ────────────────────────────────────────────
function eiOnImportTypeChange(): void {
  const radio = document.querySelector<HTMLInputElement>('input[name="ei-import-type"]:checked');
  const type  = radio ? radio.value : 'struct';
  const structWrap = document.getElementById('ei-struct-wrap');
  const schemaHint = document.getElementById('ei-schema-hint');

  if (structWrap) structWrap.style.display = type === 'struct' ? '' : 'none';
  if (schemaHint) {
    schemaHint.innerHTML = type === 'unit'
      ? '<b style="color:var(--cyan);">Unit Station CSV</b>: UnitName · flagOrigin · flagAuto · flagManual · originBaseAddr · autoBaseAddr · flagError · btnStart · hmiStop · btnReset · eStop · outHomed'
      : '<b style="color:var(--cyan);">Struct Data CSV</b>: Label | Signal1 | Signal2 | ...';
  }

  if (_eiPendingText) {
    const rows = eiParseCSV(_eiPendingText);
    const st   = document.getElementById('ei-struct-type') as HTMLSelectElement | null;
    const stat = document.getElementById('ei-stat');
    if (stat) stat.textContent = 'Mode: ' + (type === 'unit' ? 'Unit Station' : 'Struct Data' + (st ? ' (' + st.value + ')' : '')) + '  (' + rows.length + ' dòng)';
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

    if (previewEl)  previewEl.style.display = '';
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
      const label  = type === 'unit' ? 'Unit Station' : 'Struct Data' + (st ? ' (' + st.value + ')' : '');
      stat.textContent = 'Mode: ' + label + '  (' + rows.length + ' dòng)';
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
    setTimeout(function () { closeModal('modal-excel-import'); showExcelImportModal(); }, 400);
  } else {
    if (typeof toast === 'function') toast('⚠ ' + result.message.split('\n')[0]);
    console.error('[excel-import]', result.message);
  }
}

// ── Xoá toàn bộ dữ liệu đã import ────────────────────────────────────────
function eiClearExcelVars(): void {
  if (!confirm('Xoá toàn bộ dữ liệu Excel đã import?\nHành động này không thể hoàn tác.')) return;
  project.excelVars = [];
  project.unitConfig = {};
  if (typeof saveProject === 'function') saveProject();
  if (typeof toast === 'function') toast('✓ Đã xoá dữ liệu Excel import');
  if (typeof renderVarTable === 'function') renderVarTable();
  closeModal('modal-excel-import');
  showExcelImportModal();
}

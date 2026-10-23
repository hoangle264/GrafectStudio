namespace GrafcetStudioExcelImport {
  type DeviceType = GrafcetStudioProject.DeviceType;
  type DeviceSignal = GrafcetStudioProject.DeviceSignal;
  type ProjectVariable = GrafcetStudioProject.ProjectVariable;
  type UnitConfig = GrafcetStudioProject.UnitConfig;
  type PhysicalIO = GrafcetStudioProject.PhysicalIO;
  type UnitCSVParseResult = GrafcetStudioProject.UnitCSVParseResult;
  type StructCSVParseResult = GrafcetStudioProject.StructCSVParseResult;
  type SiemensBlockCSVParseResult = GrafcetStudioProject.SiemensBlockCSVParseResult;
  type PlcBlock = GrafcetStudioProject.PlcBlock;
  type PhysicalIOCSVParseResult = GrafcetStudioProject.PhysicalIOCSVParseResult;

  const KV_ADDRESS_RE = /^@?(MR|LR|DM|CR|AR|WR|HR)\d+$/i;

  export function validateAddress(address: string): boolean {
    if (!address || !address.trim()) return true;
    return KV_ADDRESS_RE.test(address.trim());
  }

  export function parseCSV(text: string): string[][] {
    const lines = String(text || '').replace(/^\uFEFF/, '').replace(/\r\n/g, '\n').replace(/\r/g, '\n').split('\n');
    const result: string[][] = [];
    lines.forEach(function(line) {
      if (!line.trim()) return;
      const delimiter = line.includes('\t') ? '\t' : ',';
      const columns: string[] = [];
      let current = '';
      let inQuote = false;
      for (let index = 0; index < line.length; index++) {
        const character = line[index];
        if (inQuote) {
          if (character === '"') {
            if (line[index + 1] === '"') {
              current += '"';
              index++;
            } else {
              inQuote = false;
            }
          } else {
            current += character;
          }
        } else {
          if (character === '"') inQuote = true;
          else if (character === delimiter) {
            columns.push(current.trim());
            current = '';
          } else {
            current += character;
          }
        }
      }
      columns.push(current.trim());
      result.push(columns);
    });
    return result;
  }

  function normalizeHeader(value: unknown): string {
    return String(value || '').toLowerCase().replace(/[^a-z0-9]/g, '');
  }

  function normalizeStructHeader(value: unknown): string {
    return String(value || '').replace(/[^a-zA-Z0-9]/g, '').toLowerCase();
  }

  function looksLikeAddress(value: unknown): boolean {
    return KV_ADDRESS_RE.test(String(value || '').trim());
  }

  export function parseUnitCSV(rows: string[][]): UnitCSVParseResult {
    const configs: UnitConfig[] = [];
    const errors: string[] = [];

    const firstRow = rows[0] || [];
    const firstRowNorm = firstRow.map(normalizeHeader);
    const hasHeader = firstRowNorm.includes('unitname') || firstRowNorm.includes('unit') || firstRowNorm.includes('label');
    if (!hasHeader) {
      return { configs, errors: ['Missing header row. First row must contain a "UnitName" (or "Unit"/"Label") column.'] };
    }
    const headerIndexMap: Record<string, number> = {};
    firstRowNorm.forEach(function(name, index) {
      if (name && headerIndexMap[name] == null) headerIndexMap[name] = index;
    });

    function getByHeader(columns: string[], names: string[]): string {
      for (let index = 0; index < names.length; index++) {
        const headerIndex = headerIndexMap[names[index]];
        if (headerIndex != null) return columns[headerIndex] || '';
      }
      return '';
    }

    rows.forEach(function(columns, rowIndex) {
      if (rowIndex === 0) return;
      if (!columns[0] || !columns[0].trim()) return;

      const unitName = getByHeader(columns, ['unitname', 'unit', 'label']).trim();
      if (!unitName) return;

      const parsedUnitIndex = parseInt(getByHeader(columns, ['unitindex', 'index']), 10);
      const unitIndex = Number.isNaN(parsedUnitIndex) ? configs.length : parsedUnitIndex;

      const io = {
        originBaseAddr: getByHeader(columns, ['originbase', 'originbaseaddr']),
        autoBaseAddr: getByHeader(columns, ['autobase', 'autobaseaddr']),
        flagOrigin: getByHeader(columns, ['originflag', 'flagorigin']),
        flagAuto: getByHeader(columns, ['autoflag', 'flagauto']),
        flagManual: getByHeader(columns, ['manualflag', 'flagmanual']),
        flagError: getByHeader(columns, ['errorflag', 'flagerror']),
        btnStart: getByHeader(columns, ['start', 'btnstart']),
        hmiStop: getByHeader(columns, ['stop', 'hmistop', 'btnstop']),
        btnReset: getByHeader(columns, ['reset', 'btnreset']),
        eStop: getByHeader(columns, ['estop']),
        outHomed: getByHeader(columns, ['homedone', 'outhomed'])
      };

      let hasError = false;
      Object.keys(io).forEach(function(key) {
        if (key === 'originBaseAddr' || key === 'autoBaseAddr') return;
        const value = io[key as keyof typeof io];
        if (value && !validateAddress(value)) {
          errors.push('Row ' + (rowIndex + 1) + ' [' + unitName + '.' + key + ']: invalid address "' + value + '"');
          hasError = true;
        }
      });

      if (!hasError) {
        configs.push({
          label: unitName,
          unitIndex: unitIndex,
          originBaseAddr: io.originBaseAddr || '@MR100',
          autoBaseAddr: io.autoBaseAddr || '@MR300',
          flags: {
            flagOrigin: io.flagOrigin,
            flagAuto: io.flagAuto,
            flagManual: io.flagManual,
            flagError: io.flagError
          },
          io: {
            btnStart: io.btnStart,
            hmiStop: io.hmiStop,
            btnReset: io.btnReset,
            eStop: io.eStop,
            outHomed: io.outHomed
          }
        });
      }
    });

    return { configs, errors };
  }

  export function parseStructCSV(rows: string[][], structTypeName: string, deviceTypes: DeviceType[]): StructCSVParseResult {
    const structType = (deviceTypes || []).find(function(device) { return device.name === structTypeName; });
    if (!structType) return { vars: [], errors: ['Struct Data "' + structTypeName + '" not found.'] };

    const signals = structType.signals || [];
    if (!signals.length) return { vars: [], errors: ['Struct Data "' + structTypeName + '" has no signals.'] };

    const vars: ProjectVariable[] = [];
    const errors: string[] = [];

    const firstRow = rows[0] || [];
    const hasHeader = firstRow.length > 1 && !KV_ADDRESS_RE.test(firstRow[1]);
    const headerMap: Record<string, number> = {};
    if (hasHeader) {
      firstRow.forEach(function(header, index) {
        const key = normalizeStructHeader(header);
        if (key && headerMap[key] == null) headerMap[key] = index;
      });
    }

    rows.forEach(function(columns, rowIndex) {
      if (hasHeader && rowIndex === 0) return;
      if (!columns[0] || !columns[0].trim()) return;

      const id = columns[0].trim();
      const signalMap: Record<string, string> = {};
      let hasError = false;

      signals.forEach(function(signal: DeviceSignal, signalIndex) {
        const key = (signal.name || signal.id || ('sig-' + signalIndex)).trim();
        const columnIndex = hasHeader ? headerMap[normalizeStructHeader(key)] : (signalIndex + 1);
        const address = (columnIndex != null ? columns[columnIndex] : '') || '';
        signalMap[key] = address;
        if (address && !validateAddress(address)) {
          errors.push('Row ' + (rowIndex + 1) + ' [' + id + '.' + key + ']: invalid address "' + address + '"');
          hasError = true;
        }
      });

      if (!hasError) {
        vars.push({
          label: id,
          format: structTypeName,
          address: '',
          comment: 'Excel import',
          signalAddresses: signalMap,
          _sigExpanded: true,
          _source: 'excel'
        });
      }
    });

    return { vars, errors };
  }
  function getHeaderValue(columns: string[], headerIndexMap: Record<string, number>, names: string[]): string {
    for (let index = 0; index < names.length; index++) {
      const headerIndex = headerIndexMap[names[index]];
      if (headerIndex != null) return columns[headerIndex] || '';
    }
    return '';
  }

  function createBlockId(blockName: string): string {
    return 'plc-block-' + blockName.trim().toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');
  }

  export function parseSiemensBlockCSV(rows: string[][], deviceTypes: DeviceType[]): SiemensBlockCSVParseResult {
    const vars: ProjectVariable[] = [];
    const blocks: PlcBlock[] = [];
    const errors: string[] = [];
    const blockMap = new Map<string, PlcBlock>();
    const typeNames = new Set((deviceTypes || []).map(device => String(device && device.name || '').toLowerCase()).filter(Boolean));

    const header = (rows[0] || []).map(normalizeHeader);
    const hasHeader = header.includes('name') && (header.includes('datatype') || header.includes('type'));
    const headerIndexMap: Record<string, number> = {};
    if (hasHeader) {
      header.forEach(function(name, index) {
        if (name && headerIndexMap[name] == null) headerIndexMap[name] = index;
      });
    }

    rows.forEach(function(columns, rowIndex) {
      if (hasHeader && rowIndex === 0) return;
      const name = (hasHeader ? getHeaderValue(columns, headerIndexMap, ['name']) : (columns[0] || '')).trim();
      const dataType = (hasHeader ? getHeaderValue(columns, headerIndexMap, ['datatype', 'type']) : (columns[1] || '')).trim();
      const comment = (hasHeader ? getHeaderValue(columns, headerIndexMap, ['comment', 'description']) : (columns[2] || '')).trim();
      const blockName = (hasHeader ? getHeaderValue(columns, headerIndexMap, ['block', 'db', 'udt']) : (columns[3] || '')).trim();
      if (!name && !dataType && !blockName) return;
      if (!name || !dataType) {
        errors.push('Row ' + (rowIndex + 1) + ': missing Name or DataType');
        return;
      }

      let blockId = '';
      if (blockName) {
        const key = blockName.toLowerCase();
        let block = blockMap.get(key);
        if (!block) {
          const upper = blockName.toUpperCase();
          block = {
            id: createBlockId(blockName),
            name: blockName,
            kind: upper.startsWith('UDT') ? 'UDT' : 'DB',
            memberVarIds: [],
            comment: 'Excel import'
          };
          blockMap.set(key, block);
          blocks.push(block);
        }
        blockId = block.id;
      }

      const variableId = 'sb-' + rowIndex + '-' + Date.now();
      if (blockId) {
        const block = blocks.find(item => item.id === blockId);
        if (block) {
          if (!block.memberVarIds) block.memberVarIds = [];
          block.memberVarIds.push(variableId);
        }
      }

      vars.push({
        id: variableId,
        label: name,
        format: dataType,
        dataType: dataType,
        address: '',
        comment: comment || 'Siemens block import',
        declarationMode: 'SymbolicBlock',
        blockId: blockId,
        kind: typeNames.has(dataType.toLowerCase()) ? 'struct' : 'primitive',
        signalAddresses: {},
        _source: 'excel'
      });
    });

    return { vars, blocks, errors };
  }

  export function parsePhysicalIOCSV(rows: string[][], normalizeDirection?: (value: string) => string): PhysicalIOCSVParseResult {
    const errors: string[] = [];
    const physicalIOs: PhysicalIO[] = [];
    const header = (rows[0] || []).map(normalizeHeader);
    const hasHeader = header.includes('devicetag') || header.includes('plcaddress') || header.includes('direction');
    const idxTag = hasHeader ? header.indexOf('devicetag') : 0;
    const idxAddr = hasHeader ? header.indexOf('plcaddress') : 1;
    const idxDir = hasHeader ? header.indexOf('direction') : 2;
    const idxDesc = hasHeader ? header.indexOf('description') : 3;
    rows.forEach(function(columns, rowIndex) {
      if (hasHeader && rowIndex === 0) return;
      const deviceTag = String(columns[idxTag] || '').trim();
      const plcAddress = String(columns[idxAddr] || '').trim();
      const direction = normalizeDirection ? normalizeDirection(columns[idxDir] || '') : '';
      const description = String(columns[idxDesc] || '').trim();
      if (!deviceTag || !plcAddress) {
        errors.push('Row ' + (rowIndex + 1) + ': missing DeviceTag or PLCAddress');
        return;
      }
      if (!direction) {
        errors.push('Row ' + (rowIndex + 1) + ': Direction must be input/output');
        return;
      }
      physicalIOs.push({ id: 'pio-' + rowIndex + '-' + Date.now(), deviceTag, plcAddress, direction, description });
    });
    return { physicalIOs, errors };
  }

  export interface ExcelImportApi {
    parseCSV(text: string): string[][];
    parseUnitCSV(rows: string[][]): UnitCSVParseResult;
    parseStructCSV(rows: string[][], structTypeName: string, deviceTypes: DeviceType[]): StructCSVParseResult;
    parseSiemensBlockCSV(rows: string[][], deviceTypes: DeviceType[]): SiemensBlockCSVParseResult;
    parsePhysicalIOCSV(rows: string[][], normalizeDirection?: (value: string) => string): PhysicalIOCSVParseResult;
    validateAddress(address: string): boolean;
  }

  export const api: ExcelImportApi = {
    parseCSV,
    parseUnitCSV,
    parseStructCSV,
    parseSiemensBlockCSV,
    parsePhysicalIOCSV,
    validateAddress
  };
}

GrafcetStudioInterop.registerBridge('excelImport', GrafcetStudioExcelImport.api);

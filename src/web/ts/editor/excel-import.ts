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

  // Address cells are taken as written: any PLC notation is accepted, the PLC tool reports bad ones on import.

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

  /** Normalizes each dotted segment separately so "Sensor.Fwd" never matches a flat "SensorFwd". */
  function normalizeStructHeader(value: unknown): string {
    return String(value || '').split('.')
      .map(function(segment) { return segment.replace(/[^a-zA-Z0-9]/g, '').toLowerCase(); })
      .join('.');
  }

  const LABEL_HEADERS = ['label', 'name', 'tag', 'variable', 'var'];

  /** Leaf keys of a struct, in declaration order; nested leaves are dotted paths ("Sensor.Fwd"). */
  export function structLeafPaths(structTypeName: string, deviceTypes: DeviceType[]): string[] {
    const devices = deviceTypes || [];
    const structType = devices.find(function(device) { return !!device && device.name === structTypeName; });
    return GrafcetStudioStructNesting.flattenSignals(devices, structType)
      .map(function(signal) { return (signal.name || signal.id || '').trim(); })
      .filter(Boolean);
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
    });

    return { configs, errors };
  }

  export function parseStructCSV(rows: string[][], structTypeName: string, deviceTypes: DeviceType[]): StructCSVParseResult {
    const structType = (deviceTypes || []).find(function(device) { return device.name === structTypeName; });
    if (!structType) return { vars: [], errors: ['Struct Data "' + structTypeName + '" not found.'] };

    // Leaves only: a struct-typed member is expanded to its dotted leaf paths, matching signalAddresses keys.
    const devices = deviceTypes || [];
    const leafKeys = structLeafPaths(structTypeName, devices);
    if (!leafKeys.length) return { vars: [], errors: ['Struct Data "' + structTypeName + '" has no signals.'] };

    const leafByNorm: Record<string, string> = {};
    leafKeys.forEach(function(key) {
      const norm = normalizeStructHeader(key);
      if (norm && leafByNorm[norm] == null) leafByNorm[norm] = key;
    });
    const containerByNorm: Record<string, string> = {};
    GrafcetStudioStructNesting.containerPaths(devices, structType).forEach(function(path) {
      containerByNorm[normalizeStructHeader(path)] = path;
    });

    const vars: ProjectVariable[] = [];
    const errors: string[] = [];

    // A header row names its label column, or names at least one leaf / struct member.
    const firstRow = rows[0] || [];
    const hasHeader = LABEL_HEADERS.indexOf(normalizeHeader(firstRow[0])) >= 0
      || firstRow.slice(1).some(function(cell) {
        const norm = normalizeStructHeader(cell);
        return !!norm && (leafByNorm[norm] != null || containerByNorm[norm] != null);
      });

    // Column index -> leaf key. With a header the file may list any subset of leaves, in any order.
    const columnLeaf: Record<number, string> = {};
    if (hasHeader) {
      const seen: Record<string, number> = {};
      firstRow.forEach(function(header, index) {
        if (index === 0) return;
        const raw = String(header || '').trim();
        if (!raw) return;
        const norm = normalizeStructHeader(raw);
        const leaf = leafByNorm[norm];
        if (leaf == null) {
          if (containerByNorm[norm] != null) {
            errors.push('Column "' + raw + '" is a struct member of "' + structTypeName + '"; use its leaves (e.g. "' + containerByNorm[norm] + '.<signal>").');
          } else {
            errors.push('Column "' + raw + '" is not a signal of "' + structTypeName + '".');
          }
          return;
        }
        if (seen[leaf] != null) {
          errors.push('Column "' + raw + '" repeats signal "' + leaf + '".');
          return;
        }
        seen[leaf] = index;
        columnLeaf[index] = leaf;
      });
      if (errors.length) {
        errors.push('Valid columns: ' + leafKeys.join(', '));
        return { vars: [], errors };
      }
    } else {
      leafKeys.forEach(function(key, leafIndex) { columnLeaf[leafIndex + 1] = key; });
    }

    rows.forEach(function(columns, rowIndex) {
      if (hasHeader && rowIndex === 0) return;
      if (!columns[0] || !columns[0].trim()) return;

      const id = columns[0].trim();
      // The file is the source of truth: leaves without a column or with an empty cell end up blank.
      const signalMap: Record<string, string> = {};
      leafKeys.forEach(function(key) { signalMap[key] = ''; });
      let hasError = false;

      columns.forEach(function(cell, columnIndex) {
        if (columnIndex === 0) return;
        const address = String(cell || '').trim();
        if (!address) return;
        const key = columnLeaf[columnIndex];
        if (key == null) {
          errors.push('Row ' + (rowIndex + 1) + ' [' + id + ']: column ' + (columnIndex + 1) + (hasHeader
            ? ' has a value but no header.'
            : ' is beyond the ' + leafKeys.length + ' signals of "' + structTypeName + '"; add a header row to pick signals by name.'));
          hasError = true;
          return;
        }
        signalMap[key] = address;
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
  /** Rows of a Struct Data CSV in the layout parseStructCSV reads: Label, then one column per leaf path. */
  export function buildStructCSVRows(variables: ProjectVariable[], structTypeName: string, deviceTypes: DeviceType[]): string[][] {
    const leafKeys = structLeafPaths(structTypeName, deviceTypes);
    const rows: string[][] = [['Label'].concat(leafKeys)];
    (variables || []).forEach(function(variable) {
      if (!variable || (variable.format || variable.dataType) !== structTypeName) return;
      const addresses = variable.signalAddresses || {};
      rows.push([variable.label || ''].concat(leafKeys.map(function(key) { return addresses[key] || ''; })));
    });
    return rows;
  }

  const PRIMITIVE_COLUMNS: Array<[string, string[]]> = [
    ['label', LABEL_HEADERS],
    ['address', ['address', 'addr', 'device', 'plcaddress']],
    ['type', ['type', 'datatype', 'format']],
    ['comment', ['comment', 'description', 'desc', 'note']]
  ];

  /**
   * Plain (non-struct) variables: Label, Address, Type, Comment. A header row may reorder or omit
   * columns; without one the columns follow that order. Type defaults to BOOL. A label already used
   * by a struct instance (reservedLabels) or a Struct Data type name in Type is rejected.
   */
  export function parsePrimitiveCSV(rows: string[][], deviceTypes: DeviceType[], reservedLabels: string[]): StructCSVParseResult {
    const vars: ProjectVariable[] = [];
    const errors: string[] = [];
    const firstRow = rows[0] || [];
    const colIndex: Record<string, number> = { label: 0, address: 1, type: 2, comment: 3 };
    const hasHeader = firstRow.some(function(cell) {
      const norm = normalizeHeader(cell);
      return PRIMITIVE_COLUMNS.some(function(column) { return column[1].indexOf(norm) >= 0; });
    });
    if (hasHeader) {
      PRIMITIVE_COLUMNS.forEach(function(column) {
        colIndex[column[0]] = firstRow.findIndex(function(cell) { return column[1].indexOf(normalizeHeader(cell)) >= 0; });
      });
      if (colIndex.label < 0) return { vars: [], errors: ['Header row has no Label column.'] };
    }
    const structNames = new Set((deviceTypes || []).map(function(device) { return String(device && device.name || '').toLowerCase(); }));
    const reserved = new Set((reservedLabels || []).map(function(label) { return String(label || '').toLowerCase(); }));
    const seen = new Set<string>();
    const cell = function(columns: string[], key: string): string {
      const index = colIndex[key];
      return index >= 0 ? String(columns[index] || '').trim() : '';
    };

    rows.forEach(function(columns, rowIndex) {
      if (hasHeader && rowIndex === 0) return;
      const label = cell(columns, 'label');
      if (!label) return;
      const where = 'Row ' + (rowIndex + 1) + ' [' + label + ']: ';
      const key = label.toLowerCase();
      const dataType = cell(columns, 'type') || 'BOOL';
      if (reserved.has(key)) { errors.push(where + 'name is already used by a struct instance.'); return; }
      if (seen.has(key)) { errors.push(where + 'label appears more than once in the file.'); return; }
      if (structNames.has(dataType.toLowerCase())) { errors.push(where + 'type "' + dataType + '" is a Struct Data type; import it as Struct Data.'); return; }
      seen.add(key);
      vars.push({
        label: label,
        address: cell(columns, 'address'),
        format: dataType,
        dataType: dataType,
        kind: 'primitive',
        comment: cell(columns, 'comment'),
        _source: 'excel'
      });
    });
    return { vars, errors };
  }

  /** Rows of a plain-variable CSV in the layout parsePrimitiveCSV reads. */
  export function buildPrimitiveCSVRows(variables: ProjectVariable[]): string[][] {
    const rows: string[][] = [['Label', 'Address', 'Type', 'Comment']];
    (variables || []).forEach(function(variable) {
      if (!variable) return;
      rows.push([variable.label || '', variable.address || '', variable.dataType || variable.format || 'BOOL', variable.comment || '']);
    });
    return rows;
  }

  // Header names parseUnitCSV accepts, paired with where the value lives in a UnitConfig.
  const UNIT_CSV_COLUMNS: Array<[string, string]> = [
    ['originBaseAddr', 'originBaseAddr'], ['autoBaseAddr', 'autoBaseAddr'],
    ['flagOrigin', 'flags.flagOrigin'], ['flagAuto', 'flags.flagAuto'], ['flagManual', 'flags.flagManual'], ['flagError', 'flags.flagError'],
    ['btnStart', 'io.btnStart'], ['hmiStop', 'io.hmiStop'], ['btnReset', 'io.btnReset'], ['eStop', 'io.eStop'], ['outHomed', 'io.outHomed']
  ];

  /** Rows of a Unit Station CSV in the layout parseUnitCSV reads. */
  export function buildUnitCSVRows(configs: UnitConfig[], getAddress: (config: UnitConfig, path: string) => string): string[][] {
    const rows: string[][] = [['UnitName', 'UnitIndex'].concat(UNIT_CSV_COLUMNS.map(function(column) { return column[0]; }))];
    (configs || []).forEach(function(config) {
      if (!config) return;
      rows.push([config.label || '', config.unitIndex != null ? String(config.unitIndex) : '']
        .concat(UNIT_CSV_COLUMNS.map(function(column) { return String(getAddress(config, column[1]) || ''); })));
    });
    return rows;
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
    structLeafPaths(structTypeName: string, deviceTypes: DeviceType[]): string[];
    buildStructCSVRows(variables: ProjectVariable[], structTypeName: string, deviceTypes: DeviceType[]): string[][];
    buildUnitCSVRows(configs: UnitConfig[], getAddress: (config: UnitConfig, path: string) => string): string[][];
    parsePrimitiveCSV(rows: string[][], deviceTypes: DeviceType[], reservedLabels: string[]): StructCSVParseResult;
    buildPrimitiveCSVRows(variables: ProjectVariable[]): string[][];
    parseSiemensBlockCSV(rows: string[][], deviceTypes: DeviceType[]): SiemensBlockCSVParseResult;
    parsePhysicalIOCSV(rows: string[][], normalizeDirection?: (value: string) => string): PhysicalIOCSVParseResult;
  }

  export const api: ExcelImportApi = {
    parseCSV,
    parseUnitCSV,
    parseStructCSV,
    structLeafPaths,
    buildStructCSVRows,
    buildUnitCSVRows,
    parsePrimitiveCSV,
    buildPrimitiveCSVRows,
    parseSiemensBlockCSV,
    parsePhysicalIOCSV
  };
}

GrafcetStudioInterop.registerBridge('excelImport', GrafcetStudioExcelImport.api);

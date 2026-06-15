"use strict";
var GrafcetStudioExcelImport;
(function (GrafcetStudioExcelImport) {
    const KV_ADDRESS_RE = /^@?(MR|LR|DM|CR|AR|WR|HR)\d+$/i;
    function validateAddress(address) {
        if (!address || !address.trim())
            return true;
        return KV_ADDRESS_RE.test(address.trim());
    }
    GrafcetStudioExcelImport.validateAddress = validateAddress;
    function parseCSV(text) {
        const lines = String(text || '').replace(/^\uFEFF/, '').replace(/\r\n/g, '\n').replace(/\r/g, '\n').split('\n');
        const result = [];
        lines.forEach(function (line) {
            if (!line.trim())
                return;
            const delimiter = line.includes('\t') ? '\t' : ',';
            const columns = [];
            let current = '';
            let inQuote = false;
            for (let index = 0; index < line.length; index++) {
                const character = line[index];
                if (inQuote) {
                    if (character === '"') {
                        if (line[index + 1] === '"') {
                            current += '"';
                            index++;
                        }
                        else {
                            inQuote = false;
                        }
                    }
                    else {
                        current += character;
                    }
                }
                else {
                    if (character === '"')
                        inQuote = true;
                    else if (character === delimiter) {
                        columns.push(current.trim());
                        current = '';
                    }
                    else {
                        current += character;
                    }
                }
            }
            columns.push(current.trim());
            result.push(columns);
        });
        return result;
    }
    GrafcetStudioExcelImport.parseCSV = parseCSV;
    function normalizeHeader(value) {
        return String(value || '').toLowerCase().replace(/[^a-z0-9]/g, '');
    }
    function normalizeStructHeader(value) {
        return String(value || '').replace(/[^a-zA-Z0-9]/g, '').toLowerCase();
    }
    function looksLikeAddress(value) {
        return KV_ADDRESS_RE.test(String(value || '').trim());
    }
    function parseUnitCSV(rows) {
        const configs = [];
        const errors = [];
        const firstRow = rows[0] || [];
        const firstRowNorm = firstRow.map(normalizeHeader);
        const hasHeader = firstRowNorm.includes('unitname') || firstRowNorm.includes('unit') || firstRowNorm.includes('label');
        const headerIndexMap = {};
        if (hasHeader) {
            firstRowNorm.forEach(function (name, index) {
                if (name && headerIndexMap[name] == null)
                    headerIndexMap[name] = index;
            });
        }
        function getByHeader(columns, names) {
            for (let index = 0; index < names.length; index++) {
                const headerIndex = headerIndexMap[names[index]];
                if (headerIndex != null)
                    return columns[headerIndex] || '';
            }
            return '';
        }
        rows.forEach(function (columns, rowIndex) {
            if (rowIndex === 0 && hasHeader)
                return;
            if (rowIndex === 0 && !hasHeader && isNaN(parseInt(columns[1], 10)) && !looksLikeAddress(columns[1]))
                return;
            if (!columns[0] || !columns[0].trim())
                return;
            const unitName = (hasHeader ? getByHeader(columns, ['unitname', 'unit', 'label']) : columns[0]).trim();
            if (!unitName)
                return;
            const unitIndexRaw = hasHeader
                ? getByHeader(columns, ['unitindex', 'index'])
                : (looksLikeAddress(columns[1]) ? '' : columns[1] || '');
            const parsedUnitIndex = parseInt(unitIndexRaw, 10);
            const unitIndex = Number.isNaN(parsedUnitIndex) ? configs.length : parsedUnitIndex;
            const noIndexLayout = !hasHeader && looksLikeAddress(columns[1]) && looksLikeAddress(columns[2]);
            function column(headerNames, legacyIndex) {
                return hasHeader ? getByHeader(columns, headerNames) : (columns[legacyIndex] || '');
            }
            const io = {
                originBaseAddr: column(['originbase', 'originbaseaddr'], noIndexLayout ? 4 : 2),
                autoBaseAddr: column(['autobase', 'autobaseaddr'], noIndexLayout ? 5 : 3),
                flagOrigin: column(['originflag', 'flagorigin'], noIndexLayout ? 1 : 2),
                flagAuto: column(['autoflag', 'flagauto'], noIndexLayout ? 2 : 3),
                flagManual: column(['manualflag', 'flagmanual'], noIndexLayout ? 3 : 4),
                flagError: column(['errorflag', 'flagerror'], noIndexLayout ? 6 : 5),
                btnStart: column(['start', 'btnstart'], noIndexLayout ? 7 : 6),
                hmiStop: column(['stop', 'hmistop', 'btnstop'], noIndexLayout ? 8 : 7),
                btnReset: column(['reset', 'btnreset'], noIndexLayout ? 9 : 8),
                eStop: column(['estop'], noIndexLayout ? 10 : 9),
                outHomed: column(['homedone', 'outhomed'], noIndexLayout ? 11 : 10)
            };
            let hasError = false;
            Object.keys(io).forEach(function (key) {
                if (key === 'originBaseAddr' || key === 'autoBaseAddr')
                    return;
                const value = io[key];
                if (value && !validateAddress(value)) {
                    errors.push('Dong ' + (rowIndex + 1) + ' [' + unitName + '.' + key + ']: dia chi khong hop le "' + value + '"');
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
    GrafcetStudioExcelImport.parseUnitCSV = parseUnitCSV;
    function parseStructCSV(rows, structTypeName, deviceTypes) {
        const structType = (deviceTypes || []).find(function (device) { return device.name === structTypeName; });
        if (!structType)
            return { vars: [], errors: ['Khong tim thay Struct Data "' + structTypeName + '".'] };
        const signals = structType.signals || [];
        if (!signals.length)
            return { vars: [], errors: ['Struct Data "' + structTypeName + '" chua co signal.'] };
        const vars = [];
        const errors = [];
        const firstRow = rows[0] || [];
        const hasHeader = firstRow.length > 1 && !KV_ADDRESS_RE.test(firstRow[1]);
        const headerMap = {};
        if (hasHeader) {
            firstRow.forEach(function (header, index) {
                const key = normalizeStructHeader(header);
                if (key && headerMap[key] == null)
                    headerMap[key] = index;
            });
        }
        rows.forEach(function (columns, rowIndex) {
            if (hasHeader && rowIndex === 0)
                return;
            if (!columns[0] || !columns[0].trim())
                return;
            const id = columns[0].trim();
            const signalMap = {};
            let hasError = false;
            signals.forEach(function (signal, signalIndex) {
                const signalId = signal.id || ('sig-' + signalIndex);
                const columnIndex = hasHeader ? headerMap[normalizeStructHeader(signal.name || signalId)] : (signalIndex + 1);
                const address = (columnIndex != null ? columns[columnIndex] : '') || '';
                signalMap[signalId] = address;
                if (address && !validateAddress(address)) {
                    errors.push('Dong ' + (rowIndex + 1) + ' [' + id + '.' + (signal.name || signalId) + ']: dia chi khong hop le "' + address + '"');
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
    GrafcetStudioExcelImport.parseStructCSV = parseStructCSV;
    function parsePhysicalIOCSV(rows, normalizeDirection) {
        const errors = [];
        const physicalIOs = [];
        const header = (rows[0] || []).map(normalizeHeader);
        const hasHeader = header.includes('devicetag') || header.includes('plcaddress') || header.includes('direction');
        const idxTag = hasHeader ? header.indexOf('devicetag') : 0;
        const idxAddr = hasHeader ? header.indexOf('plcaddress') : 1;
        const idxDir = hasHeader ? header.indexOf('direction') : 2;
        const idxDesc = hasHeader ? header.indexOf('description') : 3;
        rows.forEach(function (columns, rowIndex) {
            if (hasHeader && rowIndex === 0)
                return;
            const deviceTag = String(columns[idxTag] || '').trim();
            const plcAddress = String(columns[idxAddr] || '').trim();
            const direction = normalizeDirection ? normalizeDirection(columns[idxDir] || '') : '';
            const description = String(columns[idxDesc] || '').trim();
            if (!deviceTag || !plcAddress) {
                errors.push('Dong ' + (rowIndex + 1) + ': thieu DeviceTag hoac PLCAddress');
                return;
            }
            if (!direction) {
                errors.push('Dong ' + (rowIndex + 1) + ': Direction phai la input/output');
                return;
            }
            physicalIOs.push({ id: 'pio-' + rowIndex + '-' + Date.now(), deviceTag, plcAddress, direction, description });
        });
        return { physicalIOs, errors };
    }
    GrafcetStudioExcelImport.parsePhysicalIOCSV = parsePhysicalIOCSV;
    GrafcetStudioExcelImport.api = {
        parseCSV,
        parseUnitCSV,
        parseStructCSV,
        parsePhysicalIOCSV,
        validateAddress
    };
})(GrafcetStudioExcelImport || (GrafcetStudioExcelImport = {}));
GrafcetStudioInterop.registerBridge('excelImport', GrafcetStudioExcelImport.api);

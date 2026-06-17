"use strict";
var GrafcetStudioVars;
(function (GrafcetStudioVars) {
    GrafcetStudioVars.CYL_SIGNALS = [
        { id: 'cyl_coilA', name: 'CoilA', dataType: 'Bool', varType: 'Output', comment: 'Output coil A (extend)' },
        { id: 'cyl_coilB', name: 'CoilB', dataType: 'Bool', varType: 'Output', comment: 'Output coil B (retract)' },
        { id: 'cyl_lsh', name: 'LSH', dataType: 'Bool', varType: 'Input', comment: 'Limit switch high (extended)' },
        { id: 'cyl_lsl', name: 'LSL', dataType: 'Bool', varType: 'Input', comment: 'Limit switch low (retracted)' },
        { id: 'cyl_lockA', name: 'LockA', dataType: 'Bool', varType: 'Var', comment: 'Interlock coil A' },
        { id: 'cyl_lockB', name: 'LockB', dataType: 'Bool', varType: 'Var', comment: 'Interlock coil B' },
        { id: 'cyl_disSnsH', name: 'DisSnsH', dataType: 'Bool', varType: 'Var', comment: 'Disable sensor LSH' },
        { id: 'cyl_disSnsL', name: 'DisSnsL', dataType: 'Bool', varType: 'Var', comment: 'Disable sensor LSL' },
        { id: 'cyl_errA', name: 'ErrorA', dataType: 'Bool', varType: 'Var', comment: 'Error flag dir A' },
        { id: 'cyl_errB', name: 'ErrorB', dataType: 'Bool', varType: 'Var', comment: 'Error flag dir B' },
        { id: 'cyl_state', name: 'State', dataType: 'Bool', varType: 'Var', comment: 'Cylinder state' },
        { id: 'cyl_hmiMan', name: 'HmiManBtn', dataType: 'Bool', varType: 'Var', comment: 'HMI manual button' }
    ];
    GrafcetStudioVars.UNIT_SIGNALS = [
        { id: 'originBaseAddr', name: 'originBaseAddr', dataType: 'Word', varType: 'Var', path: 'originBaseAddr' },
        { id: 'autoBaseAddr', name: 'autoBaseAddr', dataType: 'Word', varType: 'Var', path: 'autoBaseAddr' },
        { id: 'flagOrigin', name: 'flagOrigin', dataType: 'Bool', varType: 'Var', path: 'flags.flagOrigin' },
        { id: 'flagAuto', name: 'flagAuto', dataType: 'Bool', varType: 'Var', path: 'flags.flagAuto' },
        { id: 'flagManual', name: 'flagManual', dataType: 'Bool', varType: 'Var', path: 'flags.flagManual' },
        { id: 'flagError', name: 'flagError', dataType: 'Bool', varType: 'Var', path: 'flags.flagError' },
        { id: 'btnStart', name: 'btnStart', dataType: 'Bool', varType: 'Input', path: 'io.btnStart' },
        { id: 'hmiStop', name: 'hmiStop', dataType: 'Bool', varType: 'Input', path: 'io.hmiStop' },
        { id: 'btnReset', name: 'btnReset', dataType: 'Bool', varType: 'Input', path: 'io.btnReset' },
        { id: 'eStop', name: 'eStop', dataType: 'Bool', varType: 'Input', path: 'io.eStop' },
        { id: 'outHomed', name: 'outHomed', dataType: 'Bool', varType: 'Output', path: 'io.outHomed' }
    ];
    function ensureVars(context) {
        if (context.ensureProjectVariables)
            context.ensureProjectVariables();
    }
    GrafcetStudioVars.ensureVars = ensureVars;
    function gvtGetEntries(context) {
        ensureVars(context);
        const project = context.project;
        const imported = ((project.variables && project.variables.imported) || []).map(function (v, idx) {
            return { source: 'imported', bucket: 'imported', key: idx, label: v.label || '', format: v.format || v.dataType || 'BOOL', data: v };
        });
        const user = ((project.variables && project.variables.user) || []).map(function (v, idx) {
            return { source: 'user', bucket: 'user', key: idx, label: v.label || '', format: v.format || v.dataType || 'BOOL', data: v };
        });
        const unitImported = Object.keys(project.unitConfig || {}).map(function (key) {
            const cfg = project.unitConfig[key] || { label: key };
            return { source: 'unit', bucket: 'imported', key, label: cfg.label || key, format: 'Unit Station', data: cfg };
        });
        if (imported.length || user.length || unitImported.length)
            return imported.concat(unitImported, user);
        const unitConfig = project.unitConfig || {};
        const hasExcelUnitStation = (project.excelVars || []).some(function (v) { return v && v.format === 'Unit Station'; });
        const excelEntries = (project.excelVars || []).map(function (v, idx) {
            return { source: 'excel', key: idx, label: v.label || '', format: v.format || 'Struct Data', data: v };
        });
        const unitEntries = hasExcelUnitStation ? [] : Object.keys(unitConfig).map(function (key) {
            const cfg = unitConfig[key] || { label: key };
            return { source: 'unit', key, label: cfg.label || key, format: 'Unit Station', data: cfg };
        });
        return excelEntries.concat(unitEntries);
    }
    GrafcetStudioVars.gvtGetEntries = gvtGetEntries;
    function gvtGetUnitAddr(cfg, path) {
        if (!cfg)
            return '';
        if (cfg.signalAddresses && Object.prototype.hasOwnProperty.call(cfg.signalAddresses, path))
            return cfg.signalAddresses[path] || '';
        return path.split('.').reduce(function (cur, part) {
            return cur && typeof cur === 'object' && cur[part] != null ? cur[part] : '';
        }, cfg) || '';
    }
    GrafcetStudioVars.gvtGetUnitAddr = gvtGetUnitAddr;
    function gvtSetUnitAddr(cfg, path, value) {
        const parts = path.split('.');
        let cur = cfg;
        for (let i = 0; i < parts.length - 1; i++) {
            if (!cur[parts[i]] || typeof cur[parts[i]] !== 'object')
                cur[parts[i]] = {};
            cur = cur[parts[i]];
        }
        cur[parts[parts.length - 1]] = value;
    }
    GrafcetStudioVars.gvtSetUnitAddr = gvtSetUnitAddr;
    function gvtGetUnitSigList(context) {
        const devType = (context.project.devices || []).find(device => device.name === 'Unit Station');
        const devSigs = devType ? (devType.signals || []) : [];
        if (!devSigs.length)
            return GrafcetStudioVars.UNIT_SIGNALS;
        const unitPaths = GrafcetStudioVars.UNIT_SIGNALS.reduce(function (map, sig) {
            map[sig.id] = sig.path;
            return map;
        }, {});
        return devSigs.map(function (sig) { return Object.assign({}, sig, { path: unitPaths[sig.id] || sig.path || sig.id }); });
    }
    GrafcetStudioVars.gvtGetUnitSigList = gvtGetUnitSigList;
    function gvtGetSigList(context, variable) {
        const devType = (context.project.devices || []).find(device => device.name === (variable.format || ''));
        const devSigs = devType ? (devType.signals || []) : [];
        if (variable.format === 'Cylinder')
            return devSigs.length ? devSigs : GrafcetStudioVars.CYL_SIGNALS;
        return devSigs;
    }
    GrafcetStudioVars.gvtGetSigList = gvtGetSigList;
    function gvtGetExcelSignalAddress(variable, sig) {
        const sAddr = (variable && variable.signalAddresses) || {};
        if (!sig)
            return '';
        if ((variable && variable.format) === 'Cylinder') {
            const key = String(sig.name || '').toLowerCase().replace(/[^a-z0-9]/g, '');
            if (key === 'lsh')
                return sAddr.cyl_lsh || sAddr.LSH || '';
            if (key === 'lsl')
                return sAddr.cyl_lsl || sAddr.LSL || '';
            if (key === 'locka')
                return sAddr.cyl_lockA || sAddr.LockA || '';
            if (key === 'lockb')
                return sAddr.cyl_lockB || sAddr.LockB || '';
            if (key === 'dissnslsh' || key === 'dissnsh')
                return sAddr.cyl_disSnsH || sAddr.DisSnsLSH || sAddr.DisSnsH || '';
            if (key === 'dissnslsl' || key === 'dissnsl')
                return sAddr.cyl_disSnsL || sAddr.DisSnsLSL || sAddr.DisSnsL || '';
            if (key === 'state')
                return sAddr.cyl_state || sAddr.State || '';
            if (key === 'errora' || key === 'erra')
                return sAddr.cyl_errA || sAddr.ErrorA || sAddr.ErrA || '';
            if (key === 'errorb' || key === 'errb')
                return sAddr.cyl_errB || sAddr.ErrorB || sAddr.ErrB || '';
            if (key === 'coila')
                return sAddr.cyl_coilA || sAddr.CoilA || '';
            if (key === 'coilb')
                return sAddr.cyl_coilB || sAddr.CoilB || '';
            if (key === 'hmimanbtn' || key === 'hmiman')
                return sAddr.cyl_hmiMan || sAddr.HmiManBtn || sAddr.HmiMan || '';
        }
        if (sig.id && sAddr[sig.id])
            return sAddr[sig.id];
        return '';
    }
    GrafcetStudioVars.gvtGetExcelSignalAddress = gvtGetExcelSignalAddress;
    function getVars(context) {
        ensureVars(context);
        const imported = (context.project.variables && context.project.variables.imported) || [];
        const user = (context.project.variables && context.project.variables.user) || [];
        return imported.concat(user);
    }
    GrafcetStudioVars.getVars = getVars;
    function gvtResolveEntry(context, source, key) {
        ensureVars(context);
        const idx = parseInt(String(key), 10);
        const variables = context.project.variables || { imported: [], user: [] };
        if (source === 'imported')
            return { list: variables.imported, item: variables.imported[idx] || null, idx, bucket: 'imported' };
        if (source === 'user')
            return { list: variables.user, item: variables.user[idx] || null, idx, bucket: 'user' };
        if (source === 'excel')
            return { list: context.project.excelVars || [], item: (context.project.excelVars || [])[idx] || null, idx, bucket: 'excel' };
        return { list: null, item: null, idx, bucket: '' };
    }
    GrafcetStudioVars.gvtResolveEntry = gvtResolveEntry;
    function gvtEditVar(context, source, key, field, value) {
        const hit = gvtResolveEntry(context, source, key);
        if (!hit.item)
            return hit;
        hit.item[field] = value;
        if (field === 'format') {
            hit.item.dataType = String(value || '');
            const devType = (context.project.devices || []).find(device => device.name === value);
            if (devType) {
                hit.item.kind = 'struct';
                if (!hit.item.signalAddresses)
                    hit.item.signalAddresses = {};
            }
            else {
                hit.item.kind = 'primitive';
                delete hit.item.signalAddresses;
            }
        }
        return hit;
    }
    GrafcetStudioVars.gvtEditVar = gvtEditVar;
    GrafcetStudioVars.api = {
        CYL_SIGNALS: GrafcetStudioVars.CYL_SIGNALS,
        UNIT_SIGNALS: GrafcetStudioVars.UNIT_SIGNALS,
        gvtGetEntries,
        gvtGetUnitAddr,
        gvtSetUnitAddr,
        gvtGetUnitSigList,
        gvtGetSigList,
        gvtGetExcelSignalAddress,
        getVars,
        gvtResolveEntry,
        gvtEditVar
    };
})(GrafcetStudioVars || (GrafcetStudioVars = {}));
GrafcetStudioInterop.registerBridge('vars', GrafcetStudioVars.api);

"use strict";
// -------------------------------------------------------------------
//  store.js � Grafcet Studio
//  Project state singleton + localStorage persistence.
//  Must be loaded BEFORE grafcet-studio-v2.js and grafcet-codegen.js.
//
//  NOTE: saveDiagramData / flushState reference runtime globals
//  (state, nextId, nextStepNum, viewX, viewY, viewScale) that are
//  declared in grafcet-studio-v2.js. This is intentional � those are
//  diagram-render globals and belong with the canvas layer. They are
//  only accessed at call-time (not parse-time), so load order is safe.
// -------------------------------------------------------------------
// -- Project state -----------------------------------------------
let project = {
    id: 'proj-1',
    name: 'My Project',
    machineName: 'Machine',
    units: [],
    diagrams: [],
    devices: [],
    variables: { imported: [], user: [] },
    excelVars: [],
    unitConfig: {},
    ioMapping: { physicalIOs: [], entries: [] }
};
let openTabs = [];
let activeDiagramId = null;
const PROJECT_UNIT_STRUCT_SIGNALS = [
    { id: 'originBaseAddr', name: 'originBaseAddr', dataType: 'Word', varType: 'Var', comment: 'Origin base address' },
    { id: 'autoBaseAddr', name: 'autoBaseAddr', dataType: 'Word', varType: 'Var', comment: 'Auto base address' },
    { id: 'flagOrigin', name: 'flagOrigin', dataType: 'Bool', varType: 'Var', comment: 'Origin mode flag' },
    { id: 'flagAuto', name: 'flagAuto', dataType: 'Bool', varType: 'Var', comment: 'Auto mode flag' },
    { id: 'flagManual', name: 'flagManual', dataType: 'Bool', varType: 'Var', comment: 'Manual mode flag' },
    { id: 'flagError', name: 'flagError', dataType: 'Bool', varType: 'Var', comment: 'Error flag' },
    { id: 'btnStart', name: 'btnStart', dataType: 'Bool', varType: 'Input', comment: 'Start input' },
    { id: 'hmiStop', name: 'hmiStop', dataType: 'Bool', varType: 'Input', comment: 'Stop input' },
    { id: 'btnReset', name: 'btnReset', dataType: 'Bool', varType: 'Input', comment: 'Reset input' },
    { id: 'eStop', name: 'eStop', dataType: 'Bool', varType: 'Input', comment: 'Emergency stop input' },
    { id: 'outHomed', name: 'outHomed', dataType: 'Bool', varType: 'Output', comment: 'Homed output' }
];
var GrafcetStudioStoreHelpers;
(function (GrafcetStudioStoreHelpers) {
    GrafcetStudioStoreHelpers.GF_ADDRESS_DEFAULT_BOOL_SPAN = 200;
    function normalizeBoolAddressMode(mode) {
        const value = String(mode || '').trim().toLowerCase();
        return value === 'block' ? 'block' : 'linear';
    }
    function getFlowStepMaxNumber(context, diagId) {
        const data = context.loadDiagramData(diagId);
        const steps = (data && data.state && data.state.steps) || [];
        return steps.reduce(function (max, step) { return Math.max(max, Number(step && step.number) || 0); }, 0);
    }
    function getBoolAddressRange(context, flow) {
        if (!flow || flow.addressMode !== 'bool')
            return null;
        const maxStepNumber = Math.max(1, getFlowStepMaxNumber(context, flow.id));
        const start = Number(flow.baseMr || 0);
        return { start, end: start + maxStepNumber * 2 - 1 };
    }
    function boolAddressRangesOverlap(a, b) {
        return !!a && !!b && a.start <= b.end && b.start <= a.end;
    }
    function findNextAvailableBaseMr(context, unitId, excludeDiagId) {
        const currentProject = context.getProject();
        let base = 100;
        while (base < 100000) {
            const candidate = { start: base, end: base + GrafcetStudioStoreHelpers.GF_ADDRESS_DEFAULT_BOOL_SPAN - 1 };
            const overlaps = (currentProject.diagrams || []).some(function (diag) {
                if (!diag || diag.id === excludeDiagId)
                    return false;
                if ((diag.unitId || null) !== (unitId || null))
                    return false;
                if ((diag.addressMode || 'bool') !== 'bool' || diag.baseMr === undefined || diag.baseMr === null || diag.baseMr === '')
                    return false;
                return boolAddressRangesOverlap(candidate, getBoolAddressRange(context, diag));
            });
            if (!overlaps)
                return base;
            base += GrafcetStudioStoreHelpers.GF_ADDRESS_DEFAULT_BOOL_SPAN;
        }
        return base;
    }
    GrafcetStudioStoreHelpers.findNextAvailableBaseMr = findNextAvailableBaseMr;
    function ensureFlowAddressConfig(context, diag, assignUniqueBase) {
        if (!diag)
            return false;
        let changed = false;
        if (!diag.addressMode) {
            diag.addressMode = 'bool';
            changed = true;
        }
        if (diag.addressMode === 'bool') {
            if (!diag.boolAddressMode) {
                diag.boolAddressMode = 'linear';
                changed = true;
            }
            else {
                const normalized = normalizeBoolAddressMode(diag.boolAddressMode);
                if (diag.boolAddressMode !== normalized) {
                    diag.boolAddressMode = normalized;
                    changed = true;
                }
            }
            if (diag.baseMr === undefined || diag.baseMr === null || diag.baseMr === '') {
                diag.baseMr = assignUniqueBase ? findNextAvailableBaseMr(context, diag.unitId || null, diag.id) : 100;
                changed = true;
            }
            else {
                const n = Number(diag.baseMr);
                if (Number.isFinite(n) && diag.baseMr !== n) {
                    diag.baseMr = n;
                    changed = true;
                }
            }
        }
        if (diag.addressMode === 'word') {
            if (!diag.activeWord) {
                diag.activeWord = 'DM0';
                changed = true;
            }
            if (!diag.completeWord) {
                diag.completeWord = 'DM100';
                changed = true;
            }
        }
        return changed;
    }
    GrafcetStudioStoreHelpers.ensureFlowAddressConfig = ensureFlowAddressConfig;
    function migrateFlowAddressConfigs(context) {
        let changed = false;
        (context.getProject().diagrams || []).forEach(function (diag) {
            changed = ensureFlowAddressConfig(context, diag, true) || changed;
        });
        return changed;
    }
    GrafcetStudioStoreHelpers.migrateFlowAddressConfigs = migrateFlowAddressConfigs;
    function migrateFlowControlState(context) {
        let changed = false;
        (context.getProject().diagrams || []).forEach(function (diag) {
            if (!diag)
                return;
            if (!diag.controlState) {
                diag.controlState = diag.mode || 'Auto';
                changed = true;
            }
            if (!diag.category) {
                diag.category = 'normal';
                changed = true;
            }
        });
        return changed;
    }
    GrafcetStudioStoreHelpers.migrateFlowControlState = migrateFlowControlState;
    function ensureStructDataType(context, name, signals, categoryId) {
        const currentProject = context.getProject();
        if (!name)
            return false;
        if (!currentProject.devices)
            currentProject.devices = [];
        if (currentProject.devices.some(function (device) { return device && device.name === name; }))
            return false;
        currentProject.devices.push({
            id: 'dev-sync-' + name.toLowerCase().replace(/[^a-z0-9]+/g, '-') + '-' + Date.now(),
            name,
            categoryId: categoryId || 'cat-other',
            open: true,
            signals: (signals || []).map(function (sig, idx) {
                return {
                    id: sig.id || ('sig-sync-' + idx),
                    name: sig.name || sig.id || ('Signal' + (idx + 1)),
                    dataType: sig.dataType || 'Bool',
                    varType: sig.varType || 'Var',
                    comment: sig.comment || 'Auto-synced from Global Variables'
                };
            })
        });
        return true;
    }
    function syncStructData(context) {
        const currentProject = context.getProject();
        let changed = false;
        const excelVars = currentProject.excelVars || [];
        const unitConfigs = currentProject.unitConfig || {};
        if (excelVars.some(function (v) { return v && v.format === 'Cylinder'; }) &&
            !(currentProject.devices || []).some(function (device) { return device && device.name === 'Cylinder'; }) &&
            context.ensureCylinderDeviceType) {
            context.ensureCylinderDeviceType();
            changed = true;
        }
        if (Object.keys(unitConfigs).length || excelVars.some(function (v) { return v && v.format === 'Unit Station'; })) {
            changed = ensureStructDataType(context, 'Unit Station', context.unitStructSignals, 'cat-other') || changed;
        }
        excelVars.forEach(function (v) {
            const formatName = (v && v.format || '').trim();
            if (!formatName || formatName === 'Cylinder')
                return;
            if ((currentProject.devices || []).some(function (device) { return device && device.name === formatName; }))
                return;
            const signalIds = Object.keys((v && v.signalAddresses) || {});
            const genericSignals = signalIds.map(function (sigId) {
                return {
                    id: sigId,
                    name: sigId,
                    dataType: 'Bool',
                    varType: 'Var',
                    comment: 'Auto-synced from Global Variables'
                };
            });
            changed = ensureStructDataType(context, formatName, genericSignals, 'cat-other') || changed;
        });
        return changed;
    }
    GrafcetStudioStoreHelpers.syncStructData = syncStructData;
    function ensureProjectVariables(context) {
        const currentProject = context.getProject();
        if (!currentProject.variables || Array.isArray(currentProject.variables)) {
            currentProject.variables = { imported: [], user: [] };
        }
        if (!Array.isArray(currentProject.variables.imported))
            currentProject.variables.imported = [];
        if (!Array.isArray(currentProject.variables.user))
            currentProject.variables.user = [];
        return currentProject.variables;
    }
    GrafcetStudioStoreHelpers.ensureProjectVariables = ensureProjectVariables;
    function syncVariableSignalAddressesFromDeviceTypes(context) {
        const currentProject = context.getProject();
        const devicesByName = new Map((currentProject.devices || [])
            .filter(function (device) { return !!device && !!device.name && Array.isArray(device.signals); })
            .map(function (device) { return [device.name, device]; }));
        const groups = [];
        const vars = ensureProjectVariables(context);
        groups.push(vars.imported, vars.user, currentProject.excelVars || []);
        let changed = false;
        groups.forEach(function (list) {
            (list || []).forEach(function (v) {
                const format = v && (v.format || v.dataType || '');
                const device = devicesByName.get(format);
                if (!device)
                    return;
                if (!v.signalAddresses || typeof v.signalAddresses !== 'object') {
                    v.signalAddresses = {};
                    changed = true;
                }
                const signalAddresses = v.signalAddresses;
                (device.signals || []).forEach(function (sig) {
                    const id = sig && (sig.id || sig.name);
                    if (!id || Object.prototype.hasOwnProperty.call(signalAddresses, id))
                        return;
                    signalAddresses[id] = '';
                    changed = true;
                });
            });
        });
        return changed;
    }
    GrafcetStudioStoreHelpers.syncVariableSignalAddressesFromDeviceTypes = syncVariableSignalAddressesFromDeviceTypes;
    function normalizeIOMappingDirection(v) {
        const raw = String(v || '').trim().toLowerCase();
        if (raw === 'input' || raw === 'in')
            return 'Input';
        if (raw === 'output' || raw === 'out' || raw === 'ouput')
            return 'Output';
        return '';
    }
    GrafcetStudioStoreHelpers.normalizeIOMappingDirection = normalizeIOMappingDirection;
    function ensureProjectIOMapping(context) {
        const currentProject = context.getProject();
        if (!currentProject.ioMapping || typeof currentProject.ioMapping !== 'object') {
            currentProject.ioMapping = { physicalIOs: [], entries: [] };
        }
        if (!Array.isArray(currentProject.ioMapping.physicalIOs))
            currentProject.ioMapping.physicalIOs = [];
        if (!Array.isArray(currentProject.ioMapping.entries))
            currentProject.ioMapping.entries = [];
        currentProject.ioMapping.physicalIOs = currentProject.ioMapping.physicalIOs.map(function (item, idx) {
            const rec = Object.assign({}, item || {});
            if (!rec.id)
                rec.id = 'pio-' + idx + '-' + Date.now();
            rec.direction = normalizeIOMappingDirection(rec.direction || rec.Direction);
            return rec;
        });
        return currentProject.ioMapping;
    }
    GrafcetStudioStoreHelpers.ensureProjectIOMapping = ensureProjectIOMapping;
    function normalizeVariableRecord(v, bucket) {
        const out = Object.assign({}, v || {});
        if (!out.id)
            out.id = 'var-' + bucket + '-' + Date.now() + '-' + Math.random().toString(36).slice(2, 7);
        if (!out.kind)
            out.kind = out.signalAddresses ? 'struct' : 'primitive';
        if (!out.dataType)
            out.dataType = out.format || 'BOOL';
        out.format = out.dataType;
        if (!out.source)
            out.source = bucket === 'imported' ? 'csv' : 'manual';
        return out;
    }
    GrafcetStudioStoreHelpers.normalizeVariableRecord = normalizeVariableRecord;
    function upsertProjectVariable(context, bucket, variableDef) {
        const vars = ensureProjectVariables(context);
        const list = bucket === 'user' ? vars.user : vars.imported;
        const next = normalizeVariableRecord(variableDef, bucket === 'user' ? 'user' : 'imported');
        const idx = list.findIndex(function (item) {
            return item.id === next.id || (!!item.label && item.label === next.label);
        });
        if (idx >= 0)
            list[idx] = next;
        else
            list.push(next);
        return next;
    }
    GrafcetStudioStoreHelpers.upsertProjectVariable = upsertProjectVariable;
    GrafcetStudioStoreHelpers.api = {
        ensureProjectVariables,
        upsertProjectVariable,
        normalizeVariableRecord,
        syncStructData,
        syncVariableSignalAddressesFromDeviceTypes,
        normalizeIOMappingDirection,
        ensureProjectIOMapping,
        findNextAvailableBaseMr,
        ensureFlowAddressConfig,
        migrateFlowAddressConfigs,
        migrateFlowControlState,
    };
})(GrafcetStudioStoreHelpers || (GrafcetStudioStoreHelpers = {}));
GrafcetStudioInterop.registerBridge('store', GrafcetStudioStoreHelpers.api);
function getStoreContext() {
    return {
        getProject: function () { return project; },
        loadDiagramData,
        ensureCylinderDeviceType: typeof ucEnsureCylinderDeviceType === 'function' ? ucEnsureCylinderDeviceType : undefined,
        unitStructSignals: PROJECT_UNIT_STRUCT_SIGNALS
    };
}
// -- Flow address configuration wrappers -------------------------
function findNextAvailableBaseMr(unitId, excludeDiagId) {
    return GrafcetStudioStoreHelpers.findNextAvailableBaseMr(getStoreContext(), unitId, excludeDiagId);
}
function ensureFlowAddressConfig(diag, assignUniqueBase) {
    return GrafcetStudioStoreHelpers.ensureFlowAddressConfig(getStoreContext(), diag, assignUniqueBase);
}
function migrateFlowAddressConfigs() {
    return GrafcetStudioStoreHelpers.migrateFlowAddressConfigs(getStoreContext());
}
function migrateFlowControlState() {
    return GrafcetStudioStoreHelpers.migrateFlowControlState(getStoreContext());
}
// -- Persistence wrappers ----------------------------------------
function saveProject() {
    GrafcetStudioStorePersistence.saveProject(project);
}
function saveDiagramData(id, s, nid, nsn, vx, vy, vs) {
    GrafcetStudioStorePersistence.saveDiagramData(id, {
        state: s || state,
        nextId: nid ?? nextId,
        nextStepNum: nsn ?? nextStepNum,
        viewX: vx ?? viewX,
        viewY: vy ?? viewY,
        viewScale: vs ?? viewScale
    });
}
function loadDiagramData(id) {
    return GrafcetStudioStorePersistence.loadDiagramData(id);
}
function deleteDiagramData(id) {
    GrafcetStudioStorePersistence.deleteDiagramData(id);
}
// -- Store helper wrappers ---------------------------------------
function syncStructDataFromProjectData() {
    return GrafcetStudioStoreHelpers.syncStructData(getStoreContext());
}
function syncStructData() {
    return syncStructDataFromProjectData();
}
function ensureProjectVariables() {
    return GrafcetStudioStoreHelpers.ensureProjectVariables(getStoreContext());
}
function syncVariableSignalAddressesFromDeviceTypes() {
    return GrafcetStudioStoreHelpers.syncVariableSignalAddressesFromDeviceTypes(getStoreContext());
}
function normalizeIOMappingDirection(v) {
    return GrafcetStudioStoreHelpers.normalizeIOMappingDirection(v);
}
function ensureProjectIOMapping() {
    return GrafcetStudioStoreHelpers.ensureProjectIOMapping(getStoreContext());
}
function normalizeVariableRecord(v, bucket) {
    return GrafcetStudioStoreHelpers.normalizeVariableRecord(v, bucket);
}
function upsertProjectVariable(bucket, variableDef) {
    return GrafcetStudioStoreHelpers.upsertProjectVariable(getStoreContext(), bucket, variableDef);
}
// -- Project load ------------------------------------------------
function loadProject() {
    try {
        const raw = localStorage.getItem('gf2-project');
        if (raw) {
            project = JSON.parse(raw);
            let projectChanged = false;
            ensureProjectVariables();
            ensureProjectIOMapping();
            projectChanged = syncStructDataFromProjectData() || projectChanged;
            projectChanged = syncVariableSignalAddressesFromDeviceTypes() || projectChanged;
            projectChanged = migrateFlowAddressConfigs() || projectChanged;
            projectChanged = migrateFlowControlState() || projectChanged;
            if (projectChanged)
                saveProject();
            const lastId = localStorage.getItem('gf2-active');
            if (lastId && project.diagrams.find(function (diagram) { return diagram.id === lastId; })) {
                openTab(lastId);
            }
            else if (project.diagrams.length > 0) {
                openTab(project.diagrams[0].id);
            }
            else {
                addDiagram(true);
            }
        }
        else {
            addDiagram(true);
            // Auto-seed standard device templates cho project m?i
            addStandardDeviceTemplates();
        }
    }
    catch (e) {
        addDiagram(true);
    }
}
// -- Flush active diagram to localStorage ------------------------
function flushState() {
    if (!activeDiagramId || activeDiagramId === '__vars__' || activeDiagramId === '__io_mapping__' || String(activeDiagramId).startsWith('__struct__:'))
        return;
    saveDiagramData(activeDiagramId);
    markModified(activeDiagramId, false);
}

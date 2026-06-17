"use strict";
var GrafcetStudioTree;
(function (GrafcetStudioTree) {
    function ensureUnits(project) {
        if (!Array.isArray(project.units))
            project.units = [];
        return project.units;
    }
    function ensureDiagrams(project) {
        if (!Array.isArray(project.diagrams))
            project.diagrams = [];
        return project.diagrams;
    }
    function ensureDevices(project) {
        if (!Array.isArray(project.devices))
            project.devices = [];
        return project.devices;
    }
    function getNextUnitName(project) {
        return 'Unit_' + String(((project.units || []).length) + 1).padStart(2, '0') + '_';
    }
    GrafcetStudioTree.getNextUnitName = getNextUnitName;
    function addUnit(context, name) {
        const value = String(name || '').trim();
        if (!value)
            return { ok: false, message: 'Unit name is required.' };
        const unit = { id: 'unit-' + context.now(), name: value, open: true };
        ensureUnits(context.project).push(unit);
        return { ok: true, value: unit };
    }
    GrafcetStudioTree.addUnit = addUnit;
    function renameUnit(context, id, name) {
        const value = String(name || '').trim();
        if (!value)
            return { ok: false, message: 'Unit name is required.' };
        const unit = ensureUnits(context.project).find(item => item.id === id);
        if (!unit)
            return { ok: false, message: 'Unit not found.' };
        unit.name = value;
        return { ok: true, value: unit };
    }
    GrafcetStudioTree.renameUnit = renameUnit;
    function removeUnit(context, id) {
        const units = ensureUnits(context.project);
        const unit = units.find(item => item.id === id);
        if (!unit)
            return { ok: false, message: 'Unit not found.' };
        const diagrams = ensureDiagrams(context.project);
        const affected = diagrams.filter(diagram => diagram.unitId === id);
        affected.forEach(diagram => { diagram.unitId = null; });
        context.project.units = units.filter(item => item.id !== id);
        return { ok: true, value: { removed: unit, unassignedCount: affected.length } };
    }
    GrafcetStudioTree.removeUnit = removeUnit;
    function toggleUnitOpen(context, id) {
        const unit = ensureUnits(context.project).find(item => item.id === id);
        if (!unit)
            return { ok: false, message: 'Unit not found.' };
        unit.open = unit.open === false ? true : false;
        return { ok: true, value: unit };
    }
    GrafcetStudioTree.toggleUnitOpen = toggleUnitOpen;
    function addDiagramInUnit(context, unitId, mode) {
        const id = 'diag-' + context.now();
        const diagrams = ensureDiagrams(context.project);
        const unit = unitId ? (ensureUnits(context.project).find(item => item.id === unitId)?.name || '') : '';
        const resolvedMode = mode || 'Auto';
        const diagram = {
            id,
            name: 'GRAFCET_' + resolvedMode,
            unitId: (unitId || null),
            mode: resolvedMode,
            diagramType: 'Main',
            machine: context.project.machineName || context.project.name || 'Machine',
            unit,
            description: '',
            addressMode: 'bool',
            boolAddressMode: 'linear',
            baseMr: context.findNextAvailableBaseMr ? context.findNextAvailableBaseMr(unitId || null, id) : 100
        };
        diagrams.push(diagram);
        return {
            diagram,
            emptyState: { steps: [], transitions: [], connections: [], parallels: [], vars: [] },
            nextId: 1,
            nextStepNum: 1,
            viewX: 100,
            viewY: 80,
            viewScale: 1
        };
    }
    GrafcetStudioTree.addDiagramInUnit = addDiagramInUnit;
    function makeSignalIdFromName(signalName) {
        return String(signalName || '').trim().replace(/[^a-zA-Z0-9_]+/g, '_').replace(/^_+|_+$/g, '');
    }
    GrafcetStudioTree.makeSignalIdFromName = makeSignalIdFromName;
    function getVariableGroups(project) {
        const groups = [];
        if (project.variables) {
            groups.push(project.variables.imported || []);
            groups.push(project.variables.user || []);
        }
        groups.push(project.excelVars || []);
        return groups;
    }
    function rewriteSignalAddressKeys(project, formatName, idMap) {
        getVariableGroups(project).forEach(function (list) {
            (list || []).forEach(function (variable) {
                if (!variable || variable.format !== formatName || !variable.signalAddresses)
                    return;
                const signalAddresses = variable.signalAddresses;
                Object.keys(idMap).forEach(function (oldId) {
                    const newId = idMap[oldId];
                    if (!Object.prototype.hasOwnProperty.call(signalAddresses, oldId))
                        return;
                    if (!Object.prototype.hasOwnProperty.call(signalAddresses, newId)) {
                        signalAddresses[newId] = signalAddresses[oldId];
                    }
                    delete signalAddresses[oldId];
                });
            });
        });
    }
    function confirmDeviceType(context, input) {
        const name = String(input.name || '').trim();
        if (!name)
            return { ok: false, message: 'Please enter a struct data name.', renamedSignalIds: {} };
        const devices = ensureDevices(context.project);
        const oldDevice = input.modalDeviceId ? devices.find(item => item.id === input.modalDeviceId) : null;
        const oldDeviceName = oldDevice && oldDevice.name ? oldDevice.name : name;
        const idMap = {};
        const usedSignalIds = new Set();
        const duplicateIds = new Set();
        const signals = [];
        (input.signals || []).forEach(function (signal) {
            const signalName = String(signal && signal.name || '').trim();
            if (!signalName)
                return;
            const nextId = makeSignalIdFromName(signalName);
            if (!nextId) {
                duplicateIds.add('');
                return;
            }
            if (usedSignalIds.has(nextId))
                duplicateIds.add(nextId);
            usedSignalIds.add(nextId);
            const oldId = String(signal.id || '').trim();
            if (oldId && oldId !== nextId)
                idMap[oldId] = nextId;
            signals.push({
                id: nextId,
                name: signalName,
                dataType: signal.dataType || 'Bool',
                varType: signal.varType || 'Input',
                address: signal.address || '',
                comment: signal.comment || ''
            });
        });
        if (duplicateIds.has('')) {
            return { ok: false, message: 'Signal name cannot create a valid id. Use letters, numbers, or underscore.', renamedSignalIds: idMap };
        }
        if (duplicateIds.size) {
            return { ok: false, message: 'Duplicate signal name/id is not allowed: ' + Array.from(duplicateIds).join(', '), renamedSignalIds: idMap };
        }
        let device;
        if (input.modalDeviceId) {
            if (!oldDevice)
                return { ok: false, message: 'Struct data not found.', renamedSignalIds: idMap };
            oldDevice.name = name;
            oldDevice.categoryId = input.categoryId || 'cat-other';
            oldDevice.signals = signals;
            device = oldDevice;
        }
        else {
            device = { id: 'dev-' + context.now(), name, categoryId: input.categoryId || 'cat-other', open: true, signals };
            devices.push(device);
        }
        if (Object.keys(idMap).length) {
            rewriteSignalAddressKeys(context.project, oldDeviceName, idMap);
            if (oldDeviceName !== name)
                rewriteSignalAddressKeys(context.project, name, idMap);
        }
        if (context.syncVariableSignalAddressesFromDeviceTypes)
            context.syncVariableSignalAddressesFromDeviceTypes();
        return { ok: true, value: device, renamedSignalIds: idMap };
    }
    GrafcetStudioTree.confirmDeviceType = confirmDeviceType;
    function removeDeviceType(context, devId) {
        const devices = ensureDevices(context.project);
        const device = devices.find(item => item.id === devId);
        if (!device)
            return { ok: false, message: 'Struct data not found.' };
        context.project.devices = devices.filter(item => item.id !== devId);
        return { ok: true, value: device };
    }
    GrafcetStudioTree.removeDeviceType = removeDeviceType;
    GrafcetStudioTree.api = {
        getNextUnitName,
        addUnit,
        renameUnit,
        removeUnit,
        toggleUnitOpen,
        addDiagramInUnit,
        makeSignalIdFromName,
        confirmDeviceType,
        removeDeviceType
    };
})(GrafcetStudioTree || (GrafcetStudioTree = {}));
GrafcetStudioInterop.registerBridge('tree', GrafcetStudioTree.api);

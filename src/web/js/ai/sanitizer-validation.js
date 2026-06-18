"use strict";
var GrafcetStudioAISanitizerValidation;
(function (GrafcetStudioAISanitizerValidation) {
    function containsForbiddenText(value) {
        const json = JSON.stringify(value);
        return /C:\\Users|\\\\BUILD-SERVER|templates\\|config\\|apiKey|secret|password|token|machineName|templateRootPath|deviceLibraryPath|outputPath|localConfig|hostName|BUILD-SERVER|sk-test-secret|AIzaFake/i.test(json);
    }
    function assert(condition, message, errors) {
        if (!condition)
            errors.push(message);
    }
    function runSanitizerValidation() {
        const errors = [];
        const rawVariable = {
            id: 'var-1',
            label: 'MotorStart',
            format: 'BOOL',
            address: 'M100',
            comment: 'secret=sk-test-secret-123456',
            source: 'manual',
            filePath: 'C:\\Users\\Nitro\\secret.csv',
            apiKey: 'AIzaFakeSecretKey1234567890'
        };
        const raw = {
            project: {
                id: 'proj-1',
                name: 'Demo',
                machineName: 'BUILD-SERVER-01',
                templateRootPath: 'C:\\Users\\Nitro\\templates',
                deviceLibraryPath: 'config\\Devices.json',
                outputPath: 'C:\\Users\\Nitro\\out',
                variables: { imported: [rawVariable], user: [] },
                units: [{ id: 'unit-1', name: 'Station 1', localConfig: { path: 'C:\\tmp\\unit.json' }, secretToken: 'token-abc' }],
                diagrams: [{ id: 'diag-1', name: 'Main', mode: 'Main', unitId: 'unit-1', machine: 'BUILD-SERVER-01', filePath: '\\\\BUILD-SERVER\\share\\main.gf' }],
                devices: [{ id: 'dev-1', name: 'ServoAxis', categoryId: 'cat-motion', signals: [{ id: 'Enable', name: 'Enable', dataType: 'Bool' }] }],
                ioMapping: {
                    physicalIOs: [{ id: 'io-1', deviceTag: 'StartPB', plcAddress: 'X0', direction: 'Input', password: 'pw' }],
                    entries: [{ physicalIOId: 'io-1', appVariable: 'MotorStart', status: 'mapped', matchScore: 0.95, apiKey: 'sk-test-secret' }],
                    sourcePath: 'C:\\Users\\Nitro\\io.csv'
                }
            },
            state: {
                steps: [{ id: 's1', number: 1, label: 'Start', initial: true, actions: [{ variable: 'MotorStart', address: 'M100', secret: 'sk-test-secret' }], templatePath: 'templates\\step.tpl' }],
                transitions: [{ id: 't1', condition: 'MotorStart', fromStepIds: ['s1'], toStepIds: ['s2'], hostName: 'BUILD-SERVER-01' }],
                connections: [{ from: 's1', to: 't1', filePath: 'C:\\tmp\\conn.json' }],
                localStorageKey: 'gf2-project'
            },
            selection: { unitId: 'unit-1', diagramId: 'diag-1', secret: 'token-abc' }
        };
        const sanitized = GrafcetStudioAISanitizer.sanitizeAiContext(raw, {
            intent: 'create-flow',
            scopes: ['variable', 'unit', 'diagram', 'step', 'io'],
            maxItems: 20
        });
        assert(sanitized !== raw, 'sanitizeAiContext must return a new context object.', errors);
        assert(sanitized.variables !== raw.project.variables.imported, 'variables array must be newly constructed.', errors);
        assert(sanitized.variables != null && sanitized.variables[0] !== rawVariable, 'variable records must be newly constructed.', errors);
        assert(!containsForbiddenText(sanitized), 'sanitized context must not contain path-like, machine, local config, or secret-like fields.', errors);
        assert(!!sanitized.variables && Object.keys(sanitized.variables[0]).sort().join(',') === 'address,id,format,label,source'.split(',').sort().join(','), 'variable sanitizer must only expose whitelisted fields present in fixture.', errors);
        assert(!!sanitized.unit && Object.keys(sanitized.unit).sort().join(',') === 'id,name', 'unit sanitizer must only expose whitelisted fields present in fixture.', errors);
        assert(!!sanitized.diagram && Object.keys(sanitized.diagram).sort().join(',') === 'id,mode,name,unitId'.split(',').sort().join(','), 'diagram sanitizer must only expose whitelisted fields present in fixture.', errors);
        assert(!!sanitized.ioMapping && Object.keys(sanitized.ioMapping.physicalIOs[0]).sort().join(',') === 'deviceTag,direction,id,plcAddress'.split(',').sort().join(','), 'io physical entry must only expose whitelisted fields present in fixture.', errors);
        assert(!!sanitized.steps && Object.keys(sanitized.steps[0]).sort().join(',') === 'actions,id,initial,label,number'.split(',').sort().join(','), 'step sanitizer must only expose whitelisted fields present in fixture.', errors);
        const mapIoOnly = GrafcetStudioAISanitizer.sanitizeAiContext(raw, {
            intent: 'map-io',
            scopes: ['variable', 'unit', 'diagram', 'step', 'io']
        });
        assert(!!mapIoOnly.variables, 'map-io context should keep variable scope when requested.', errors);
        assert(!!mapIoOnly.ioMapping, 'map-io context should keep io scope when requested.', errors);
        assert(!mapIoOnly.unit && !mapIoOnly.diagram && !mapIoOnly.steps, 'map-io context should not include scopes outside the selected intent.', errors);
        const structureOnly = GrafcetStudioAISanitizer.sanitizeAiContext(raw, {
            intent: 'create-structure',
            scopes: ['structure']
        });
        assert(!!structureOnly.existingStructures && structureOnly.existingStructures[0] === 'ServoAxis', 'create-structure context should include existing structure names.', errors);
        assert(!containsForbiddenText(structureOnly), 'structure context must not contain forbidden text.', errors);
        assert(JSON.stringify(structureOnly).indexOf('Enable') < 0 && JSON.stringify(structureOnly).indexOf('cat-motion') < 0 && JSON.stringify(structureOnly).indexOf('dev-1') < 0, 'structure sanitizer must expose only structure names, not signals or internal fields.', errors);
        const fallbackStructures = GrafcetStudioAISanitizer.sanitizeScope('structure', { devices: [{ name: 'ValveBlock' }, { name: '' }, { name: 123 }] });
        assert(Array.isArray(fallbackStructures) && fallbackStructures.length === 1 && fallbackStructures[0] === 'ValveBlock', 'structure scope should fallback to raw.devices and include safe names only.', errors);
        const manyDevices = { project: { devices: Array.from({ length: 60 }, function (_, index) { return { name: 'Struct' + index, signals: [{ name: 'Hidden' }] }; }) } };
        const limitedStructures = GrafcetStudioAISanitizer.sanitizeScope('structure', manyDevices);
        assert(limitedStructures.length === 50, 'structure sanitizer must limit existingStructures to 50 names.', errors);
        return { ok: errors.length === 0, errors, sanitized };
    }
    GrafcetStudioAISanitizerValidation.runSanitizerValidation = runSanitizerValidation;
})(GrafcetStudioAISanitizerValidation || (GrafcetStudioAISanitizerValidation = {}));

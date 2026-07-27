namespace GrafcetStudioCodegenPayload {
  type Project = GrafcetStudioProject.Project;
  type DiagramMeta = GrafcetStudioProject.DiagramMeta;
  type DiagramState = GrafcetStudioProject.DiagramState;
  type StoredDiagramData = GrafcetStudioProject.StoredDiagramData;
  type Step = GrafcetStudioProject.Step;
  type Transition = GrafcetStudioProject.Transition;
  type Connection = GrafcetStudioProject.Connection;
  type DeviceSignal = GrafcetStudioProject.DeviceSignal;
  type DeviceType = GrafcetStudioProject.DeviceType;
  type ProjectVariable = GrafcetStudioProject.ProjectVariable;
  type DeviceVariable = GrafcetStudioProject.DeviceVariable;
  type FlowInfo = GrafcetStudioProject.FlowInfo;
  type CodegenPayload = GrafcetStudioProject.CodegenPayload;
  type UnitInfo = GrafcetStudioProject.UnitInfo;
  type PlcBlock = GrafcetStudioProject.PlcBlock;

  export interface CodegenAssets {
    deviceLibraryPath: string;
    templateRootPath: string;
    outputPath: string;
    templateProfile?: string;
  }

  export interface PayloadContext {
    project: Project;
    loadDiagramData(diagramId: string): StoredDiagramData | null;
    getAssets(): CodegenAssets;
    ensureFlowAddressConfig?: (diagram: DiagramMeta, assignUniqueBase: boolean) => unknown;
    ensureProjectVariables?: () => GrafcetStudioProject.ProjectVariables;
    syncVariableSignalAddressesFromDeviceTypes?: () => boolean;
    saveProject?: () => void;
    getDefaultUnitId?: () => string;
    unitSignals?: DeviceSignal[];
    projectUnitStructSignals?: DeviceSignal[];
  }

  export interface ResolvedStepAddress {
    execAddress: string;
    doneAddress: string;
  }

  interface FlowBuildResult extends FlowInfo {
    variables: DeviceVariable[];
  }

  interface FlowRange {
    start: number;
    end: number;
  }

  interface BoolFlowRange {
    name: string;
    range: FlowRange | null;
  }

  interface ParsedWordAddress {
    prefix: string;
    number: number;
    width: number;
  }

  function splitSiemensSymbolicPath(tagName: string): string[] {
    const source = String(tagName || '').trim();
    if (!source) throw new Error('Siemens symbolic tag name is required');

    const components: string[] = [];
    let current = '';
    let inQuotes = false;

    for (let index = 0; index < source.length; index++) {
      const char = source[index];
      if (char === '"') {
        inQuotes = !inQuotes;
        current += char;
        continue;
      }

      if (char === '.' && !inQuotes) {
        const component = current.trim();
        if (!component) throw new Error('Invalid Siemens symbolic path: ' + tagName);
        components.push(component);
        current = '';
        continue;
      }

      current += char;
    }

    if (inQuotes) throw new Error('Invalid Siemens symbolic path: ' + tagName);

    const last = current.trim();
    if (!last) throw new Error('Invalid Siemens symbolic path: ' + tagName);
    components.push(last);
    return components;
  }

  function normalizeSiemensSymbolicComponent(component: string, index: number): string {
    const value = String(component || '').trim();
    if (!value) throw new Error('Invalid Siemens symbolic component');

    const quotedMatch = value.match(/^"([^"]+)"$/);
    if (quotedMatch) return '"' + quotedMatch[1] + '"';

    return index === 0 ? '"' + value + '"' : value;
  }

  function formatSiemensBitSlice(tagName: string, bit: number): string {
    const components = splitSiemensSymbolicPath(tagName);
    return components.map((component, index) => normalizeSiemensSymbolicComponent(component, index)).join('.') + '.%X' + bit;
  }

  interface ParsedAddressBase {
    prefix: string;
    number: number;
    width: number;
  }

  function parseWordAddress(address: string): ParsedWordAddress {
    const match = String(address || '').trim().match(/^([A-Za-z]+)(\d+)$/);
    if (!match) throw new Error('Invalid word address: ' + address);
    return { prefix: match[1].toUpperCase(), number: Number(match[2]), width: match[2].length };
  }

  function formatWordAddress(base: string, offset: number): string {
    const parsed = parseWordAddress(base);
    const next = parsed.number + offset;
    const numberText = parsed.width > 1 ? String(next).padStart(parsed.width, '0') : String(next);
    return parsed.prefix + numberText;
  }

  function parseAddressBase(value: unknown, fallback: string): ParsedAddressBase {
    const source = String(value == null || value === '' ? fallback : value).trim();
    const match = source.match(/^([A-Za-z]+)(\d+)$/);
    if (!match) throw new Error('Invalid base address: ' + value);
    return { prefix: match[1].toUpperCase(), number: Number(match[2]), width: match[2].length };
  }

  function formatBaseAddress(base: ParsedAddressBase, number: number): string {
    const numberText = base.width > 1 ? String(number).padStart(base.width, '0') : String(number);
    return base.prefix + numberText;
  }

  function normalizeBoolAddressMode(mode: unknown): string {
    const value = String(mode || '').trim().toLowerCase();
    return value === 'block' ? 'block' : 'linear';
  }

  function resolveBoolMr(baseMr: unknown, offset: number, boolAddressMode: unknown): number {
    const base = parseAddressBase(baseMr, 'MR100').number;
    if (normalizeBoolAddressMode(boolAddressMode) === 'block') {
      return base + Math.floor(offset / 8) * 100 + (offset % 8);
    }
    return base + offset;
  }

  export function resolveStepAddress(step: Step, flow: DiagramMeta): ResolvedStepAddress {
    const stepNumber = Number(step && step.number);
    if (!Number.isInteger(stepNumber) || stepNumber < 1) {
      throw new Error('Step number must be an integer >= 1');
    }

    const mode = String((flow && flow.addressMode) || 'bool').toLowerCase();
    if (mode === 'word') {
      if (stepNumber > 32) throw new Error('Word address mode supports at most 32 steps per flow');
      const bitIndex = stepNumber - 1;
      const wordOffset = Math.floor(bitIndex / 16);
      const bit = bitIndex % 16;
      const activeWordTag = String((flow && (flow as any).activeWordTag) || '').trim();
      const completeWordTag = String((flow && (flow as any).completeWordTag) || '').trim();
      return {
        execAddress: activeWordTag
          ? formatSiemensBitSlice(wordOffset > 0 ? activeWordTag + '_' + wordOffset : activeWordTag, bit)
          : formatWordAddress(flow.activeWord || 'DM0', wordOffset) + '.' + bit,
        doneAddress: completeWordTag
          ? formatSiemensBitSlice(wordOffset > 0 ? completeWordTag + '_' + wordOffset : completeWordTag, bit)
          : formatWordAddress(flow.completeWord || 'DM100', wordOffset) + '.' + bit
      };
    }

    const pairOffset = (stepNumber - 1) * 2;
    const boolBase = parseAddressBase(flow.baseMr, 'MR100');
    return {
      execAddress: formatBaseAddress(boolBase, resolveBoolMr(flow.baseMr, pairOffset, flow.boolAddressMode)),
      doneAddress: formatBaseAddress(boolBase, resolveBoolMr(flow.baseMr, pairOffset + 1, flow.boolAddressMode))
    };
  }

  function getFlowAddressRange(flow: DiagramMeta, steps: Step[]): FlowRange | null {
    if (!flow || flow.addressMode !== 'bool') return null;
    const maxStepNumber = (steps || []).reduce((max, step) => Math.max(max, Number(step.number) || 0), 0);
    const start = parseAddressBase(flow.baseMr, 'MR100').number;
    return { start, end: start + Math.max(1, maxStepNumber) * 2 - 1 };
  }

  function validateStepNumbers(steps: Step[], flowName: string): void {
    const seen = new Set<number>();
    (steps || []).forEach(function(step) {
      const stepNumber = Number(step.number);
      if (!Number.isInteger(stepNumber) || stepNumber < 1) throw new Error('Flow "' + flowName + '" has a step.number that is not >= 1.');
      if (seen.has(stepNumber)) throw new Error('Flow "' + flowName + '" has duplicate step.number ' + stepNumber + '.');
      seen.add(stepNumber);
    });
  }

  export function validateUnitAddressConfig(context: PayloadContext, unitDiagrams: DiagramMeta[]): void {
    const boolFlows: BoolFlowRange[] = [];
    const usedWords = new Map<string, string>();
    (unitDiagrams || []).forEach(function(diagram) {
      if (context.ensureFlowAddressConfig) context.ensureFlowAddressConfig(diagram, true);
      const data = context.loadDiagramData(diagram.id);
      const steps = ((data && data.state && data.state.steps) || []);
      validateStepNumbers(steps, diagram.name || diagram.id);

      if (diagram.addressMode === 'word') {
        if (steps.some(step => Number(step.number) > 32)) {
          throw new Error('Flow "' + (diagram.name || diagram.id) + '" uses word mode but has step.number > 32.');
        }
        const maxStepNumber = steps.reduce((max, step) => Math.max(max, Number(step.number) || 0), 0);
        const wordCount = maxStepNumber > 16 ? 2 : 1;
        [diagram.activeWord || 'DM0', diagram.completeWord || 'DM100'].forEach(function(word) {
          for (let offset = 0; offset < wordCount; offset++) {
            const key = formatWordAddress(word, offset).toUpperCase();
            if (usedWords.has(key)) throw new Error('Word address ' + key + ' is used by both "' + usedWords.get(key) + '" and "' + (diagram.name || diagram.id) + '".');
            usedWords.set(key, diagram.name || diagram.id);
          }
        });
        return;
      }

      const range = getFlowAddressRange(diagram, steps);
      boolFlows.forEach(function(existing) {
        if (range && existing.range && range.start <= existing.range.end && existing.range.start <= range.end) {
          throw new Error('MR range overlap between "' + existing.name + '" and "' + (diagram.name || diagram.id) + '".');
        }
      });
      boolFlows.push({ name: diagram.name || diagram.id, range });
    });
  }

  function normalizeCSharpSignal(context: PayloadContext, deviceTypeName: string, signal: DeviceSignal): DeviceSignal {
    const canonicalUnitSignals = new Map<string, DeviceSignal>((context.projectUnitStructSignals || []).map(item => [item.id, item]));
    const canonical = deviceTypeName === 'Unit Station' ? canonicalUnitSignals.get(signal && signal.id) : null;
    const normalized = Object.assign({}, signal || {}, canonical || {});
    const signalId = normalized.id || normalized.name || '';
    const signalName = normalized.name || signalId;
    return Object.assign({}, normalized, { id: signalId, name: signalName });
  }

  function getCSharpDeviceTypes(context: PayloadContext): DeviceType[] {
    return ((context.project && context.project.devices) || []).map(deviceType => {
      if (!deviceType) return deviceType;
      return Object.assign({}, deviceType, {
        signals: (deviceType.signals || []).map(signal => normalizeCSharpSignal(context, deviceType.name, signal))
      });
    });
  }

  function getCSharpSignalAddresses(context: PayloadContext, variable: ProjectVariable): Record<string, string> {
    const format = variable && (variable.format || variable.dataType || '');
    const deviceType = ((context.project && context.project.devices) || []).find(device => device && device.name === format);
    const rawAddresses = (variable && variable.signalAddresses) || {};
    if (!deviceType || !Array.isArray(deviceType.signals)) return Object.assign({}, rawAddresses);

    const signalAddresses: Record<string, string> = {};
    deviceType.signals.forEach(signal => {
      const normalized = normalizeCSharpSignal(context, deviceType.name, signal);
      const outputKey = normalized.name || normalized.id;
      if (!outputKey) return;
      const address = rawAddresses[normalized.name] || rawAddresses[signal && signal.name] || '';
      signalAddresses[outputKey] = address;
    });
    return signalAddresses;
  }

  function normalizeVariableFormat(variable: { format?: string; dataType?: string; structure?: string } | null | undefined): string {
    return String((variable && (variable.format || variable.dataType || variable.structure)) || '').trim();
  }

  function findMacroPortVariable(flowName: string, variables: DeviceVariable[]): DeviceVariable | null {
    if (!flowName) return null;
    const matches = (variables || []).filter(variable => variable && String(variable.label || (variable as any).name || '').trim().toLowerCase() === flowName.trim().toLowerCase());
    if (matches.length === 0) return null;
    if (matches.length > 1) throw new Error('Duplicate MacroPort variable name for MacroStep "' + flowName + '".');
    const variable = matches[0];
    const format = normalizeVariableFormat(variable as any);
    if (format !== 'MacroPort') throw new Error('MacroStep "' + flowName + '" has variable with same name but format/dataType/structure is "' + format + '", expected "MacroPort".');
    return variable;
  }

  function getCSharpVariables(context: PayloadContext, diagramState: DiagramState & { vars?: ProjectVariable[] }): DeviceVariable[] {
    const vars: DeviceVariable[] = [];
    const seen = new Set<string>();
    const add = function(variable: ProjectVariable | null | undefined): void {
      if (!variable || !variable.label) return;
      const signalAddresses = getCSharpSignalAddresses(context, variable);
      if (seen.has(variable.label)) return;
      seen.add(variable.label);
      vars.push({
        label: variable.label,
        format: normalizeVariableFormat(variable as any),
        address: variable.address || null,
        signalAddresses: signalAddresses,
        declarationMode: variable.declarationMode,
        blockId: variable.blockId || ''
      });
    };

    (diagramState.vars || []).forEach(add);
    if (context.ensureProjectVariables) {
      const grouped = context.ensureProjectVariables();
      (grouped.imported || []).forEach(add);
      (grouped.user || []).forEach(add);
    }
    Object.keys((context.project && context.project.unitConfig) || {}).forEach(key => {
      const cfg = context.project.unitConfig[key] || { label: key };
      const signalAddresses = Object.assign({}, cfg.signalAddresses || {});
      (context.unitSignals || []).forEach(signal => {
        if (!signal || !signal.id || !signal.path) return;
        const address = signal.path.split('.').reduce<unknown>((current, part) => {
          return current && typeof current === 'object' && (current as Record<string, unknown>)[part] != null
            ? (current as Record<string, unknown>)[part]
            : '';
        }, cfg) || '';
        if (address) signalAddresses[signal.name || signal.id] = String(address);
      });
      add({ label: cfg.label || key, format: 'Unit Station', address: null, signalAddresses });
    });
    (context.project.excelVars || []).forEach(add);
    return vars;
  }

  export function buildCSharpFlow(context: PayloadContext, diagramId: string): FlowBuildResult {
    const diagram = (context.project.diagrams || []).find(item => item.id === diagramId) || ({ id: diagramId, name: diagramId, mode: '' } as DiagramMeta);
    const data = context.loadDiagramData(diagramId);
    const state = ((data && data.state) || { steps: [], transitions: [], connections: [], vars: [] }) as DiagramState & { vars?: ProjectVariable[] };
    if (context.ensureFlowAddressConfig) context.ensureFlowAddressConfig(diagram, true);
    const steps = (state.steps || []).map(step => {
      const address = resolveStepAddress(step, diagram);
      return {
        id: step.id || '',
        number: Number(step.number || 0),
        label: step.label || '',
        initial: !!step.initial,
        kind: step.kind || 'normal',
        macroFlowId: step.macroFlowId || null,
        execAddress: address.execAddress,
        doneAddress: address.doneAddress,
        actions: (step.actions || []).map(action => ({
          variable: action.variable || '',
          address: action.address || null,
          qualifier: action.qualifier || 'N',
          timeMs: Number(action.timeMs || action.time || 0)
        }))
      };
    });
    const stepIds = new Set(steps.map(step => step.id));
    const transitions = (state.transitions || []).map((transition: Transition) => ({
      id: transition.id || '',
      label: transition.label || '',
      condition: transition.condition || '',
      fromStepIds: (state.connections || [])
        .filter((connection: Connection) => connection.to === transition.id && !!connection.from && stepIds.has(connection.from))
        .map((connection: Connection) => connection.from || ''),
      toStepIds: (state.connections || [])
        .filter((connection: Connection) => connection.from === transition.id && !!connection.to && stepIds.has(connection.to))
        .map((connection: Connection) => connection.to || '')
    }));

    const variables = getCSharpVariables(context, state);
    const macroPortVariable = String(diagram.diagramType || 'Macro').toLowerCase() === 'macrostep'
      ? findMacroPortVariable(diagram.name || diagramId, variables)
      : null;

    return {
      diagram: {
        id: diagram.id || diagramId,
        name: diagram.name || diagramId,
        mode: diagram.mode || '',
        controlState: diagram.controlState || diagram.mode || 'Auto',
        category: diagram.category || 'normal',
        orchestratorConfig: diagram.category === 'orchestrator' ? (diagram.orchestratorConfig || { elements: [] }) : undefined,
        unitId: diagram.unitId || '',
        unit: diagram.unit || '',
        diagramType: diagram.diagramType || 'Macro',
        addressMode: diagram.addressMode || 'bool',
        boolAddressMode: diagram.boolAddressMode || 'linear',
        baseMr: diagram.baseMr == null || diagram.baseMr === '' ? null : String(diagram.baseMr),
        activeWord: diagram.activeWord || '',
        completeWord: diagram.completeWord || '',
        activeWordTag: String((diagram as any).activeWordTag || ''),
        completeWordTag: String((diagram as any).completeWordTag || '')
      },
      steps,
      transitions,
      macroPortVariable,
      variables
    };
  }

  function normalizeFlowType(mode: unknown): string {
    const value = String(mode || '').trim().toLowerCase();
    return value === 'origin' ? 'origin' : 'auto';
  }

  function buildProjectInfo(context: PayloadContext) {
    const plcConfig = (context.project && context.project.plcConfig) || {};
    return {
      id: context.project.id || '',
      name: context.project.name || '',
      machineName: context.project.machineName || '',
      plc: {
        namePlc: String(plcConfig.type || plcConfig.name || context.project.plcName || context.project.machineName || '').trim(),
        deviceCode: String(plcConfig.deviceCode ?? '').trim()
      }
    };
  }

  function buildBlocksInfo(context: PayloadContext): PlcBlock[] {
    return ((context.project && context.project.blocks) || []).map(block => ({
      id: block.id || '',
      name: block.name || block.id || '',
      kind: block.kind || '',
      memberVarIds: (block.memberVarIds || []).slice(),
      comment: block.comment || ''
    }));
  }

  function buildUnitsInfo(context: PayloadContext): UnitInfo[] {
    const units = (context.project.units || []).map(unit => ({
      id: unit.id || '',
      name: unit.name || unit.id || '',
      label: unit.name || unit.id || ''
    }));

    if ((context.project.diagrams || []).some(diagram => !diagram.unitId)) {
      units.push({ id: '__none__', name: 'No unit', label: 'No unit' });
    }

    return units;
  }

  function buildSharedFlowStructInfo(context: PayloadContext): GrafcetStudioProject.SharedFlowStructSchema {
    const project = context.project;
    const shared: GrafcetStudioProject.SharedFlowStructSchema = (project && project.sharedFlowStruct)
      ? JSON.parse(JSON.stringify(project.sharedFlowStruct))
      : { enabled: true, structTypeName: 'UDT_FlowData', members: [] };

    const typeName = shared.structTypeName || 'UDT_FlowData';

    if (!shared.members || shared.members.length === 0) {
      const devices = (project && project.devices) || [];
      const matchingDevice = devices.find(d => d && d.name === typeName);
      if (matchingDevice && Array.isArray(matchingDevice.signals)) {
        shared.members = matchingDevice.signals.map(s => ({
          id: s.id || s.name,
          name: s.name,
          type: s.dataType || 'BOOL',
          comment: s.comment || ''
        }));
      }
    }

    return shared;
  }

  function buildCSharpPayloadCore(
    context: PayloadContext,
    platform: string,
    unit: UnitInfo | null | undefined,
    flowsWithVariables: FlowBuildResult[]
  ): CodegenPayload {
    const allVars: DeviceVariable[] = [];
    const seenVars = new Set<string>();
    const addVar = (variable: DeviceVariable): void => {
      if (!variable || !variable.label || seenVars.has(variable.label)) return;
      seenVars.add(variable.label);
      allVars.push(variable);
    };

    const sharedStruct = buildSharedFlowStructInfo(context);
    const structTypeName = sharedStruct.structTypeName || 'UDT_FlowData';

    const projectVars = getCSharpVariables(context, { steps: [], transitions: [], connections: [], vars: [] });
    const projectVarMap = new Map<string, DeviceVariable>();
    projectVars.forEach(v => {
      if (v && v.label) {
        projectVarMap.set(v.label, v);
        addVar(v);
      }
    });

    const flows = flowsWithVariables.map(flow => {
      (flow.variables || []).forEach(addVar);
      const flowName = (flow.diagram && flow.diagram.name) || 'Flow';
      const instanceName = 'ST_' + flowName.replace(/[^a-zA-Z0-9_]/g, '_');
      const existingProjectVar = projectVarMap.get(instanceName);

      const flowVar: DeviceVariable = existingProjectVar ? existingProjectVar : {
        label: instanceName,
        format: structTypeName,
        declarationMode: 'SymbolicBlock'
      };

      if (sharedStruct.enabled !== false && !existingProjectVar) {
        addVar(flowVar);
      }
      return {
        id: flow.diagram && flow.diagram.id,
        name: flow.diagram && flow.diagram.name,
        type: normalizeFlowType(flow.diagram && flow.diagram.mode),
        controlState: flow.diagram && (flow.diagram.controlState || flow.diagram.mode),
        category: flow.diagram && (flow.diagram.category || 'normal'),
        diagramType: flow.diagram && (flow.diagram.diagramType || 'Macro'),
        orchestratorConfig: flow.diagram && flow.diagram.category === 'orchestrator'
          ? (flow.diagram.orchestratorConfig || { elements: [] })
          : undefined,
        diagram: flow.diagram,
        steps: flow.steps,
        transitions: flow.transitions,
        macroPortVariable: (flow as any).macroPortVariable || null,
        structInstanceName: instanceName,
        structTypeName: structTypeName,
        flowVariable: flowVar
      };
    });
    getCSharpVariables(context, { steps: [], transitions: [], connections: [], vars: [] }).forEach(addVar);


    const assets = context.getAssets();
    const unitId = unit && unit.id ? unit.id : '';
    const units = unit
      ? [{
          id: unit.id || '',
          name: unit.name || unit.id || '',
          label: unit.label || unit.name || unit.id || ''
        }]
      : buildUnitsInfo(context);

    return {
      platform,
      deviceLibraryPath: assets.deviceLibraryPath,
      templateRootPath: assets.templateRootPath,
      templateProfile: assets.templateProfile || 'simple',
      outputPath: assets.outputPath,
      project: buildProjectInfo(context),
      unit: unit ? {
        id: unit.id || '',
        name: unit.name || unit.id || '',
        label: unit.label || unit.name || unit.id || ''
      } : undefined,
      units,
      flows,
      variables: allVars,
      blocks: buildBlocksInfo(context),
      deviceTypes: getCSharpDeviceTypes(context),
      sharedFlowStruct: sharedStruct,
      ioMapping: JSON.parse(JSON.stringify((context.project && context.project.ioMapping) || { physicalIOs: [], entries: [] })),
      unitConfig: JSON.parse(JSON.stringify((context.project && context.project.unitConfig) || {}))
    };
  }

  export function buildCSharpUnitPayload(context: PayloadContext, platform: string, unitId: string): CodegenPayload {
    const units = context.project.units || [];
    const selectedUnit = unitId && unitId !== '__none__'
      ? units.find(unit => unit.id === unitId)
      : null;
    const unitDiagrams = (context.project.diagrams || []).filter(diagram =>
      unitId === '__none__' ? !diagram.unitId : diagram.unitId === unitId
    );
    validateUnitAddressConfig(context, unitDiagrams);

    const flowResults = unitDiagrams.map(diagram => buildCSharpFlow(context, diagram.id));
    const unit = {
      id: selectedUnit ? (selectedUnit.id || '') : (unitId || ''),
      name: selectedUnit ? (selectedUnit.name || selectedUnit.id || '') : (unitId === '__none__' ? 'No unit' : ''),
      label: selectedUnit ? (selectedUnit.name || selectedUnit.id || '') : (unitId === '__none__' ? 'No unit' : '')
    };

    return buildCSharpPayloadCore(context, platform, unit, flowResults);
  }

  export function buildCSharpProjectPayload(context: PayloadContext, platform: string): CodegenPayload {
    const allDiagrams = context.project.diagrams || [];
    const flowsByUnit = new Map<string, DiagramMeta[]>();

    allDiagrams.forEach(diagram => {
      const key = diagram.unitId || '__none__';
      const list = flowsByUnit.get(key) || [];
      list.push(diagram);
      flowsByUnit.set(key, list);
    });

    flowsByUnit.forEach(unitDiagrams => {
      validateUnitAddressConfig(context, unitDiagrams);
    });

    const flowResults = allDiagrams.map(diagram => buildCSharpFlow(context, diagram.id));
    return buildCSharpPayloadCore(context, platform, null, flowResults);
  }

  export function buildCSharpPayload(context: PayloadContext, platform: string, unitId?: string): CodegenPayload {
    if (context.syncVariableSignalAddressesFromDeviceTypes && context.syncVariableSignalAddressesFromDeviceTypes()) {
      if (context.saveProject) context.saveProject();
    }
    if (unitId === '__all__') return buildCSharpProjectPayload(context, platform);
    const resolvedUnitId = unitId || (context.getDefaultUnitId ? context.getDefaultUnitId() : '') || '';
    return buildCSharpUnitPayload(context, platform, resolvedUnitId);
  }

  export interface CodegenPayloadApi {
    buildCSharpPayload(context: PayloadContext, platform: string, unitId?: string): CodegenPayload;
    buildCSharpFlow(context: PayloadContext, diagramId: string): FlowBuildResult;
    buildCSharpUnitPayload(context: PayloadContext, platform: string, unitId: string): CodegenPayload;
    buildCSharpProjectPayload(context: PayloadContext, platform: string): CodegenPayload;
    validateUnitAddressConfig(context: PayloadContext, unitDiagrams: DiagramMeta[]): void;
    resolveStepAddress(step: Step, flow: DiagramMeta): ResolvedStepAddress;
  }

  export const api: CodegenPayloadApi = {
    buildCSharpPayload,
    buildCSharpFlow,
    buildCSharpUnitPayload,
    buildCSharpProjectPayload,
    validateUnitAddressConfig,
    resolveStepAddress
  };
}

GrafcetStudioInterop.registerBridge('codegenPayload', GrafcetStudioCodegenPayload.api);








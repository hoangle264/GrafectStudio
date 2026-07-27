namespace GrafcetStudioProject {
  export type JsonPrimitive = string | number | boolean | null;
  export type JsonValue = JsonPrimitive | JsonObject | JsonValue[];
  export interface JsonObject { [key: string]: JsonValue | undefined; }

  export type DiagramMode = 'Main' | 'Sub' | 'Error' | 'Manual' | 'Drivers' | string;
  export type FlowCategory = 'normal' | 'orchestrator';

  export type DiagramType = 'Macro' | 'MacroStep' | 'Grafcet' | 'Driver' | string;
  export type StepKind = 'normal' | 'macro';
  export type AddressMode = 'bool' | 'word' | string;
  export type BoolAddressMode = 'linear' | 'block' | string;
  export type SignalVarType = 'Input' | 'Output' | 'Var' | 'Instance' | string;
  export type IODirection = 'Input' | 'Output' | '' | string;
  export type IOMappingStatus = 'matched' | 'mapped' | 'unmatched' | string;
  export type VariableKind = 'primitive' | 'struct' | string;
  export type VariableSource = 'csv' | 'manual' | 'excel' | string;
  export type DeclarationMode = 'AddressMapped' | 'SymbolicBlock' | string;
  export type BlockKind = 'DB' | 'UDT' | 'GVL' | string;
  export type ActionQualifier = 'N' | 'S' | 'R' | 'P' | 'P0' | 'L' | 'D' | 'SD' | 'DS' | 'SL';

  export interface SharedFlowStructMember {
    id: string;
    name: string;
    type: string;
    comment?: string;
    defaultValue?: string;
  }

  export interface SharedFlowStructSchema {
    enabled: boolean;
    structTypeName: string;
    members: SharedFlowStructMember[];
  }

  export interface PlcBlock {
    id: string;
    name: string;              // vd "DB_Motor", "UDT_Cylinder", "GVL_IO"
    kind: BlockKind;
    memberVarIds?: string[];   // ProjectVariable.id references owned by this block
    comment?: string;
    [key: string]: unknown;
  }
  export interface Project {
    id: string;
    name: string;
    machineName: string;
    units: Unit[];
    diagrams: DiagramMeta[];
    devices: DeviceType[];
    variables: ProjectVariables;
    excelVars: ProjectVariable[];
    unitConfig: Record<string, UnitConfig>;
    ioMapping: IOMapping;
    plcConfig?: PlcConfig;
    plcName?: string;
    blocks?: PlcBlock[];
    sharedFlowStruct?: SharedFlowStructSchema;
    [key: string]: unknown;
  }

  export interface Unit {
    id: string;
    name: string;
    open?: boolean;
    [key: string]: unknown;
  }

  export interface DiagramMeta {
    id: string;
    name: string;
    mode: DiagramMode;
    controlState?: string;
    category?: FlowCategory;
    orchestratorConfig?: OrchestratorConfig;
    unitId?: string;
    unit?: string;
    diagramType?: DiagramType;
    machine?: string;
    description?: string;
    addressMode?: AddressMode;
    boolAddressMode?: BoolAddressMode;
    baseMr?: number | string;
    activeWord?: string;
    completeWord?: string;
    activeWordTag?: string;
    completeWordTag?: string;
    [key: string]: unknown;
  }

  export interface DiagramState {
    steps: Step[];
    transitions: Transition[];
    connections: Connection[];
    variables?: DeviceVariable[];
    [key: string]: unknown;
  }

  export interface StoredDiagramData {
    state: DiagramState;
    nextId: number;
    nextStepNum: number;
    viewX: number;
    viewY: number;
    viewScale: number;
    [key: string]: unknown;
  }

  export interface Step {
    id: string;
    number?: number;
    label?: string;
    initial?: boolean;
    kind?: StepKind;
    macroFlowId?: string | null;
    actions?: StepAction[];
    execAddress?: string | null;
    doneAddress?: string | null;
    // canvas-only, not serialized to C#
    x?: number;
    y?: number;
    w?: number;
    h?: number;
    connections?: Connection[];
  }

  export interface Transition {
    id: string;
    label?: string;
    condition?: string;
    fromStepIds?: string[];
    toStepIds?: string[];
    // canvas-only, not serialized to C#
    x?: number;
    y?: number;
    w?: number;
    h?: number;
    connections?: Connection[];
  }

  export interface Connection {
    from?: string;
    to?: string;
    fromId?: string;
    toId?: string;
    parallelGroupId?: string | null;
    [key: string]: unknown;
  }

  export interface StepAction {
    variable: string;
    address?: string | null;
    qualifier: ActionQualifier;
    timeMs: number;
    // UI-only, not serialized to C#
    time?: number | string;
    complete?: StepActionCompletion | null;
    sensorRef?: string | null;
  }

  export interface StepActionCompletion {
    sensor: string;
    sensorLabel: string;
    address: string;
    [key: string]: unknown;
  }

  export interface DeviceType {
    id: string;
    name: string;
    signals: DeviceSignal[];
    categoryId?: string;
    open?: boolean;
    [key: string]: unknown;
  }

  export interface DeviceSignal {
    id: string;
    name: string;
    dataType: string;
    varType?: SignalVarType;
    ioType?: SignalVarType;
    address?: string;
    comment?: string;
    path?: string;
    nestedTypeId?: string;   // nested DeviceType reference; optional, can be inferred from dataType
    [key: string]: unknown;
  }

  export interface DeviceVariable {
    label: string;
    format: string;
    address?: string | null;
    signalAddresses?: Record<string, string>;
    declarationMode?: DeclarationMode;   // absent => AddressMapped (legacy behavior)
    blockId?: string;   // id PlcBlock; '' = no block
  }

  export interface ProjectVariable extends DeviceVariable {
    id?: string;
    kind?: VariableKind;
    dataType?: string;
    comment?: string;
    source?: VariableSource;
    _source?: VariableSource;
    _sigExpanded?: boolean;
  }

  export interface ProjectVariables {
    imported: ProjectVariable[];
    user: ProjectVariable[];
    [key: string]: ProjectVariable[] | unknown;
  }

  export interface FlowInfo {
    id?: string;
    name?: string;
    type?: string;
    mode?: DiagramMode;
    controlState?: string;
    category?: FlowCategory;
    orchestratorConfig?: OrchestratorConfig;
    diagramType?: DiagramType;
    diagram?: DiagramInfo;
    steps: Step[];
    transitions: Transition[];
    macroPortVariable?: DeviceVariable | null;
    structInstanceName?: string;
    structTypeName?: string;
    flowVariable?: DeviceVariable | null;
    [key: string]: unknown;
  }

  export interface DiagramInfo {
    id?: string;
    name?: string;
    mode?: DiagramMode;
    controlState?: string;
    category?: FlowCategory;
    orchestratorConfig?: OrchestratorConfig;
    unitId?: string;
    unit?: string;
    diagramType?: DiagramType;
    addressMode?: AddressMode;
    boolAddressMode?: BoolAddressMode;
    baseMr?: string | null;
    activeWord?: string;
    completeWord?: string;
    activeWordTag?: string;
    completeWordTag?: string;
    [key: string]: unknown;
  }

  export interface OrchestratorConfig {
    elements: Array<{ type: string; config: JsonValue }>;
  }

  export interface CodegenFile {
    path: string;
    content: string;
  }

  export interface CodegenOutput {
    files: CodegenFile[];
  }

  export interface CodegenPayload {
    platform: string;
    templateRootPath: string;
    project?: ProjectInfo;
    unit?: UnitInfo;
    units: UnitInfo[];
    flows: FlowInfo[];
    variables: DeviceVariable[];
    blocks?: PlcBlock[];
    deviceTypes: DeviceType[];
    deviceLibraryPath: string;
    templateProfile: string;
    sharedFlowStruct?: SharedFlowStructSchema;
    ioMapping?: IOMapping;
    unitConfig?: Record<string, UnitConfig>;
    system?: SystemControlInfo;
        // UI-only, not serialized to C#
    outputPath?: string;
  }

  export interface ProjectInfo {
    id?: string;
    name?: string;
    machineName?: string;
    plc?: PlcPayloadInfo;
    [key: string]: unknown;
  }

  export interface PlcPayloadInfo {
    namePlc?: string;
    deviceCode?: string;
    [key: string]: unknown;
  }

  export interface UnitInfo {
    id?: string;
    name?: string;
    label?: string;
    [key: string]: unknown;
  }

  export interface UnitConfig {
    label: string;
    unitIndex?: number;
    originBaseAddr?: string;
    autoBaseAddr?: string;
    flags?: UnitConfigFlags;
    io?: UnitConfigIO;
    signalAddresses?: Record<string, string>;
    _sigExpanded?: boolean;
    [key: string]: unknown;
  }

  export interface UnitConfigFlags {
    flagOrigin?: string;
    flagAuto?: string;
    flagManual?: string;
    flagError?: string;
    [key: string]: string | undefined;
  }

  export interface UnitConfigIO {
    btnStart?: string;
    hmiStop?: string;
    btnReset?: string;
    eStop?: string;
    outHomed?: string;
    [key: string]: string | undefined;
  }

  export interface IOMapping {
    physicalIOs: PhysicalIO[];
    entries: IOMappingEntry[];
    [key: string]: unknown;
  }

  export interface PhysicalIO {
    id: string;
    deviceTag: string;
    plcAddress: string;
    direction: IODirection;
    description?: string;
    Direction?: IODirection;
    [key: string]: unknown;
  }

  export interface IOMappingEntry {
    physicalIOId: string;
    appVariable: string;
    status: IOMappingStatus;
    matchScore: number;
    [key: string]: unknown;
  }

  export interface UnitCSVParseResult {
    configs: UnitConfig[];
    errors: string[];
  }

  export interface SystemControlInfo {
    label: string;
    signalAddresses: Record<string, string>;
  }

  export interface StructCSVParseResult {
    vars: ProjectVariable[];
    errors: string[];
  }

  export interface SiemensBlockCSVParseResult {
    vars: ProjectVariable[];
    blocks: PlcBlock[];
    errors: string[];
  }

  export interface PhysicalIOCSVParseResult {
    physicalIOs: PhysicalIO[];
    errors: string[];
  }

  export interface ExcelImportResult {
    ok: boolean;
    message: string;
    added: number;
  }
  export interface PlcConfig {
    name?: string;
    deviceCode?: string | number;
    ip?: string;
    port?: string | number;
    [key: string]: unknown;
  }
}





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
  export type SignalVarType = 'Input' | 'Output' | 'Var' | string;
  export type IODirection = 'Input' | 'Output' | '' | string;
  export type IOMappingStatus = 'matched' | 'mapped' | 'unmatched' | string;
  export type VariableKind = 'primitive' | 'struct' | string;
  export type VariableSource = 'csv' | 'manual' | 'excel' | string;
  export type ActionQualifier = 'N' | 'S' | 'R' | 'L' | 'D' | 'P' | 'P0' | 'P1' | string;

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
    isInitial?: boolean;
    x?: number;
    y?: number;
    w?: number;
    h?: number;
    kind?: StepKind;
    macroFlowId?: string | null;
    actions?: StepAction[];
    connections?: Connection[];
    execAddress?: string | null;
    doneAddress?: string | null;
    [key: string]: unknown;
  }

  export interface Transition {
    id: string;
    label?: string;
    condition?: string;
    x?: number;
    y?: number;
    w?: number;
    h?: number;
    fromStepIds?: string[];
    toStepIds?: string[];
    connections?: Connection[];
    [key: string]: unknown;
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
    qualifier?: ActionQualifier;
    time?: number | string;
    timeMs?: number;
    complete?: StepActionCompletion | null;
    sensorRef?: string | null;
    [key: string]: unknown;
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
    [key: string]: unknown;
  }

  export interface DeviceVariable {
    label: string;
    format: string;
    dataType?: string;
    structure?: string;
    address?: string | null;
    signalAddresses?: Record<string, string>;
    [key: string]: unknown;
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

  export interface AppConfig {
    platform?: string;
    templateRootPath?: string;
    deviceLibraryPath?: string;
    outputPath?: string;
    project?: ProjectInfo;
    unit?: UnitInfo;
    units?: UnitInfo[];
    flows?: FlowInfo[];
    variables?: DeviceVariable[];
    deviceTypes?: DeviceType[];
    [key: string]: unknown;
  }

  export interface ProjectInfo {
    id?: string;
    name?: string;
    machineName?: string;
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

  export interface StructCSVParseResult {
    vars: ProjectVariable[];
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
    ip?: string;
    port?: string | number;
    [key: string]: unknown;
  }
}






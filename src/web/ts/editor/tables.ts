namespace GrafcetStudioTables {
  type Project = GrafcetStudioProject.Project;
  type DiagramState = GrafcetStudioProject.DiagramState;
  type Step = GrafcetStudioProject.Step;
  type Transition = GrafcetStudioProject.Transition;
  type Connection = GrafcetStudioProject.Connection;
  type ProjectVariable = GrafcetStudioProject.ProjectVariable;
  type DeviceType = GrafcetStudioProject.DeviceType;

  export interface TablesContext {
    project: Project;
    resolveStepsThrough(elementId: string, direction: string, connections: Connection[], steps: Step[], parallels: ParallelBar[]): Step[];
    getStepActionsStatic(step: Step): GrafcetStudioProject.StepAction[];
  }

  export interface ParallelBar {
    id: string;
    type?: string;
    ports?: number;
    width?: number;
    [key: string]: unknown;
  }

  export interface StepRow {
    id: string;
    number: number;
    numberText: string;
    label: string;
    initial: boolean;
    actions: GrafcetStudioProject.StepAction[];
  }

  export interface TransitionRow {
    tid: string;
    label: string;
    fromStep: Step | null;
    toStep: Step | null;
    condition: string;
  }

  export interface BranchRow {
    id: string;
    type: string;
    isSplit: boolean;
    ports: number;
    width: number;
    singleTransitions: Transition[];
    branchSteps: Step[][];
    descriptionParts: string[];
  }

  export interface VariableSignalRow {
    kind: 'signal';
    label: string;
    dataFormat: string;
    address: string;
    varType: string;
    comment: string;
  }

  export interface VariableRow {
    kind: 'variable';
    index: number;
    label: string;
    format: string;
    address: string;
    comment: string;
    deviceType?: DeviceType | null;
    signalRows: VariableSignalRow[];
  }

  export interface StepTableData {
    rows: StepRow[];
    stats: { steps: number; initial: number; actions: number };
  }

  export interface TransitionTableData {
    rows: TransitionRow[];
    stats: { transitions: number; withCondition: number; stepPairs: number };
  }

  export interface BranchTableData {
    rows: BranchRow[];
    stats: { split: number; join: number };
  }

  export interface VariablesTableData {
    rows: VariableRow[];
    stats: { variables: number; deviceInstances: number };
  }

  function parallelsOf(state: DiagramState): ParallelBar[] {
    return ((state as unknown as { parallels?: ParallelBar[] }).parallels || []);
  }

  export function buildStepTableData(context: TablesContext, state: DiagramState): StepTableData {
    const rows = (state.steps || []).slice().sort(function(a, b) { return (Number(a.number) || 0) - (Number(b.number) || 0); }).map(function(step) {
      const number = Number(step.number) || 0;
      return {
        id: step.id,
        number,
        numberText: String(number).padStart(2, '0'),
        label: step.label || '',
        initial: !!step.initial,
        actions: context.getStepActionsStatic(step)
      };
    });
    return {
      rows,
      stats: {
        steps: rows.length,
        initial: rows.filter(row => row.initial).length,
        actions: rows.reduce(function(total, row) { return total + row.actions.length; }, 0)
      }
    };
  }

  export function buildTransitionTableData(context: TablesContext, state: DiagramState): TransitionTableData {
    const transitions = state.transitions || [];
    const steps = state.steps || [];
    const connections = state.connections || [];
    const parallels = parallelsOf(state);
    const rows: TransitionRow[] = [];

    transitions.forEach(function(transition) {
      const fromSteps = context.resolveStepsThrough(transition.id, 'upstream', connections, steps, parallels);
      const toSteps = context.resolveStepsThrough(transition.id, 'downstream', connections, steps, parallels);
      if (fromSteps.length === 0 && toSteps.length === 0) rows.push({ tid: transition.id, label: transition.label || '', fromStep: null, toStep: null, condition: transition.condition || '' });
      else if (fromSteps.length === 0) toSteps.forEach(toStep => rows.push({ tid: transition.id, label: transition.label || '', fromStep: null, toStep, condition: transition.condition || '' }));
      else if (toSteps.length === 0) fromSteps.forEach(fromStep => rows.push({ tid: transition.id, label: transition.label || '', fromStep, toStep: null, condition: transition.condition || '' }));
      else fromSteps.forEach(fromStep => toSteps.forEach(toStep => rows.push({ tid: transition.id, label: transition.label || '', fromStep, toStep, condition: transition.condition || '' })));
    });

    return {
      rows,
      stats: {
        transitions: transitions.length,
        withCondition: transitions.filter(transition => !!transition.condition).length,
        stepPairs: rows.length
      }
    };
  }

  export function buildBranchTableData(_context: TablesContext, state: DiagramState): BranchTableData {
    const parallels = parallelsOf(state);
    const steps = state.steps || [];
    const transitions = state.transitions || [];
    const connections = state.connections || [];
    const rows = parallels.map(function(parallel) {
      const isSplit = parallel.type === 'split';
      const ports = Number(parallel.ports) || 3;
      const width = Number(parallel.width) || 0;
      const singleConns = connections.filter(function(connection) {
        return isSplit
          ? connection.to === parallel.id && connection.toPort === 'top'
          : connection.from === parallel.id && connection.fromPort === 'bottom';
      });
      const singleTransitions = singleConns.map(function(connection) {
        return transitions.find(item => item.id === (isSplit ? connection.from : connection.to));
      }).filter(function(item): item is Transition { return !!item; });
      const branchSteps: Step[][] = [];
      for (let index = 0; index < ports; index++) {
        const branchPort = isSplit ? 'bottom-' + index : 'top-' + index;
        const branchConns = connections.filter(function(connection) {
          return isSplit
            ? connection.from === parallel.id && connection.fromPort === branchPort
            : connection.to === parallel.id && connection.toPort === branchPort;
        });
        branchSteps.push(branchConns.map(function(connection) {
          return steps.find(item => item.id === (isSplit ? connection.to : connection.from));
        }).filter(function(item): item is Step { return !!item; }));
      }
      const descriptionParts = [ports + ' branches, width=' + width + 'px'];
      if (singleTransitions.length) {
        descriptionParts.push(isSplit
          ? 'Triggered by: ' + singleTransitions.map(transition => transition.id + (transition.condition ? ' [' + transition.condition + ']' : '')).join(', ')
          : 'Converges to: ' + singleTransitions.map(transition => transition.id).join(', '));
      }
      return { id: parallel.id, type: parallel.type || '', isSplit, ports, width, singleTransitions, branchSteps, descriptionParts };
    });
    return {
      rows,
      stats: {
        split: parallels.filter(parallel => parallel.type === 'split').length,
        join: parallels.filter(parallel => parallel.type === 'join').length
      }
    };
  }

  export function buildVariablesTableData(context: TablesContext, state: DiagramState): VariablesTableData {
    const variables = ((state as unknown as { vars?: ProjectVariable[] }).vars || []);
    const rows = variables.map(function(variable, index) {
      const deviceType = (context.project.devices || []).find(device => device.name === (variable.format || '')) || null;
      const signalRows = deviceType ? (deviceType.signals || []).map(function(signal) {
        return {
          kind: 'signal' as const,
          label: (variable.label || '?') + '.' + signal.name,
          dataFormat: signal.dataType || 'Bool',
          address: (variable.signalAddresses || {})[signal.id] || '',
          varType: signal.varType || '',
          comment: signal.comment || ''
        };
      }) : [];
      return {
        kind: 'variable' as const,
        index: index + 1,
        label: variable.label || '',
        format: variable.format || '',
        address: variable.address || '',
        comment: variable.comment || '',
        deviceType,
        signalRows
      };
    });
    return {
      rows,
      stats: {
        variables: variables.length,
        deviceInstances: rows.filter(row => !!row.deviceType).length
      }
    };
  }

  export interface TablesApi {
    buildStepTableData(context: TablesContext, state: DiagramState): StepTableData;
    buildTransitionTableData(context: TablesContext, state: DiagramState): TransitionTableData;
    buildBranchTableData(context: TablesContext, state: DiagramState): BranchTableData;
    buildVariablesTableData(context: TablesContext, state: DiagramState): VariablesTableData;
  }

  export const api: TablesApi = {
    buildStepTableData,
    buildTransitionTableData,
    buildBranchTableData,
    buildVariablesTableData
  };
}

GrafcetStudioInterop.registerBridge('tables', GrafcetStudioTables.api);

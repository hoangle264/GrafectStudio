// ═══════════════════════════════════════════════════════════
//  flow-review.ts — Grafcet Studio
//  Deterministic structural review of one Grafcet diagram.
//  Runs offline; its findings are also handed to the AI assistant
//  so an AI review starts from facts instead of guesses.
// ═══════════════════════════════════════════════════════════

namespace GrafcetStudioFlowReview {
  type Step = GrafcetStudioProject.Step;
  type Transition = GrafcetStudioProject.Transition;
  type Connection = GrafcetStudioProject.Connection;

  export type Severity = 'error' | 'warning' | 'info';

  export interface Finding {
    severity: Severity;
    code: string;
    message: string;
    stepNumbers?: number[];
    transitionIds?: string[];
  }

  export interface ReviewInput {
    steps: Step[];
    transitions: Transition[];
    connections: Connection[];
    parallels?: Array<{ id: string }>;
    /** Labels that actions and conditions may reference; base label before the first '.' is matched. */
    knownVariables?: string[];
  }

  const conditionWords = new Set(['and', 'or', 'not', 'xor', 'true', 'false', 'always']);

  function endpoint(connection: Connection, key: 'from' | 'to'): string {
    return String((key === 'from' ? (connection.from || connection.fromId) : (connection.to || connection.toId)) || '');
  }

  function stepName(step: Step): string {
    return 'Step ' + (step.number ?? '?') + (step.label ? ' (' + step.label + ')' : '');
  }

  /** Neighbours of a node in one direction, passing through parallel bars, filtered to wanted node ids. */
  function neighbours(id: string, downstream: boolean, connections: Connection[], parallelIds: Set<string>, wanted: Set<string>, visited = new Set<string>()): string[] {
    if (visited.has(id)) return [];
    visited.add(id);
    const out: string[] = [];
    connections.forEach(function(c) {
      if (endpoint(c, downstream ? 'from' : 'to') !== id) return;
      const next = endpoint(c, downstream ? 'to' : 'from');
      if (wanted.has(next)) out.push(next);
      else if (parallelIds.has(next)) out.push.apply(out, neighbours(next, downstream, connections, parallelIds, wanted, visited));
    });
    return out;
  }

  function identifiers(text: string): string[] {
    const cleaned = text.replace(/\bt#[0-9a-z_.]+/gi, ' ').replace(/"[^"]*"/g, ' ');
    return (cleaned.match(/[A-Za-z_][A-Za-z0-9_.]*/g) || []).filter(function(token) { return !conditionWords.has(token.toLowerCase()); });
  }

  export function reviewDiagram(input: ReviewInput): Finding[] {
    const findings: Finding[] = [];
    const steps = input.steps || [];
    const transitions = input.transitions || [];
    const connections = input.connections || [];
    const parallelIds = new Set((input.parallels || []).map(function(p) { return p.id; }));
    const stepIds = new Set(steps.map(function(s) { return s.id; }));
    const transitionIds = new Set(transitions.map(function(t) { return t.id; }));
    const stepById = new Map(steps.map(function(s) { return [s.id, s] as [string, Step]; }));

    if (!steps.length) {
      findings.push({ severity: 'info', code: 'empty', message: 'The diagram has no steps.' });
      return findings;
    }

    // Initial steps
    const initials = steps.filter(function(s) { return !!s.initial; });
    if (!initials.length) findings.push({ severity: 'error', code: 'no-initial', message: 'No initial step is marked.' });
    else if (initials.length > 1) findings.push({ severity: 'warning', code: 'multiple-initial', message: 'Multiple initial steps: ' + initials.map(stepName).join(', ') + '.', stepNumbers: initials.map(function(s) { return s.number || 0; }) });

    // Duplicate step numbers
    const byNumber = new Map<number, Step[]>();
    steps.forEach(function(s) { if (typeof s.number === 'number') byNumber.set(s.number, (byNumber.get(s.number) || []).concat([s])); });
    byNumber.forEach(function(list, number) {
      if (list.length > 1) findings.push({ severity: 'warning', code: 'duplicate-number', message: list.length + ' steps share number ' + number + '.', stepNumbers: [number] });
    });

    // Dangling connections
    connections.forEach(function(c) {
      [endpoint(c, 'from'), endpoint(c, 'to')].forEach(function(id) {
        if (id && !stepIds.has(id) && !transitionIds.has(id) && !parallelIds.has(id)) {
          findings.push({ severity: 'error', code: 'dangling-connection', message: 'A connection references a missing element: ' + id + '.' });
        }
      });
    });

    // Transitions: inputs, outputs, conditions
    const outgoingByStep = new Map<string, Transition[]>();
    const successors = new Map<string, Set<string>>();
    transitions.forEach(function(t) {
      const from = neighbours(t.id, false, connections, parallelIds, stepIds);
      const to = neighbours(t.id, true, connections, parallelIds, stepIds);
      const name = 'Transition ' + (t.label || t.id);
      if (!from.length) findings.push({ severity: 'error', code: 'transition-no-input', message: name + ' has no input step.', transitionIds: [t.id] });
      if (!to.length) findings.push({ severity: 'error', code: 'transition-no-output', message: name + ' has no output step.', transitionIds: [t.id] });
      // An empty condition is valid: either "true" or an implicit wait for the previous step to complete.
      from.forEach(function(stepId) {
        outgoingByStep.set(stepId, (outgoingByStep.get(stepId) || []).concat([t]));
        const set = successors.get(stepId) || new Set<string>();
        to.forEach(function(target) { set.add(target); });
        successors.set(stepId, set);
      });
    });

    // Steps: the last step implicitly loops back to the initial step, so one step without an outgoing
    // transition is the normal end of the cycle. Several of them usually means a transition is missing.
    const terminal = steps.filter(function(s) { return !(outgoingByStep.get(s.id) || []).length; });
    if (terminal.length > 1) findings.push({ severity: 'warning', code: 'multiple-ends', message: terminal.length + ' steps have no outgoing transition (' + terminal.map(stepName).join(', ') + '); only the last step of the cycle should end without one.', stepNumbers: terminal.map(function(s) { return s.number || 0; }) });
    steps.forEach(function(s) {
      const outgoing = outgoingByStep.get(s.id) || [];
      const seen = new Map<string, string>();
      outgoing.forEach(function(t) {
        const key = String(t.condition || '').replace(/\s+/g, '').toLowerCase();
        if (!key) return;
        if (seen.has(key)) findings.push({ severity: 'error', code: 'duplicate-branch-condition', message: stepName(s) + ': transitions ' + seen.get(key) + ' and ' + (t.label || t.id) + ' have the same condition "' + t.condition + '".', stepNumbers: [s.number || 0], transitionIds: [t.id] });
        else seen.set(key, t.label || t.id);
      });
    });

    // Reachability from initial steps
    if (initials.length) {
      const reached = new Set<string>();
      // A step without outgoing transitions loops back to the initial steps.
      terminal.forEach(function(s) { successors.set(s.id, new Set(initials.map(function(i) { return i.id; }))); });
      const queue = initials.map(function(s) { return s.id; });
      while (queue.length) {
        const id = queue.shift()!;
        if (reached.has(id)) continue;
        reached.add(id);
        (successors.get(id) || new Set<string>()).forEach(function(next) { if (!reached.has(next)) queue.push(next); });
      }
      const unreachable = steps.filter(function(s) { return !reached.has(s.id); });
      if (unreachable.length) findings.push({ severity: 'warning', code: 'unreachable', message: 'Not reachable from the initial step: ' + unreachable.map(stepName).join(', ') + '.', stepNumbers: unreachable.map(function(s) { return s.number || 0; }) });
    }

    // Unknown variables (only when the caller supplies the known set)
    if (input.knownVariables) {
      const known = new Set(input.knownVariables.map(function(v) { return v.split('.')[0].toLowerCase(); }));
      const isKnown = function(name: string) { return known.has(name.split('.')[0].toLowerCase()); };
      steps.forEach(function(s) {
        (s.actions || []).forEach(function(a) {
          if (a.variable && !isKnown(a.variable)) findings.push({ severity: 'error', code: 'unknown-action-variable', message: stepName(s) + ' action uses unknown variable "' + a.variable + '".', stepNumbers: [s.number || 0] });
        });
      });
      transitions.forEach(function(t) {
        const unknown = identifiers(String(t.condition || '')).filter(function(id) { return !isKnown(id); });
        if (unknown.length) findings.push({ severity: 'warning', code: 'unknown-condition-variable', message: 'Transition ' + (t.label || t.id) + ' condition uses unknown name(s): ' + Array.from(new Set(unknown)).join(', ') + '.', transitionIds: [t.id] });
      });
    }

    const order: Record<Severity, number> = { error: 0, warning: 1, info: 2 };
    return findings.sort(function(a, b) { return order[a.severity] - order[b.severity]; });
  }

  export function formatFindings(findings: Finding[]): string {
    if (!findings.length) return 'No structural problems found.';
    return findings.map(function(f) { return '- [' + f.severity.toUpperCase() + '] ' + f.message; }).join('\n');
  }

  export const api = { reviewDiagram, formatFindings };
}

(window as unknown as { GrafcetStudioFlowReview: typeof GrafcetStudioFlowReview.api }).GrafcetStudioFlowReview = GrafcetStudioFlowReview.api;

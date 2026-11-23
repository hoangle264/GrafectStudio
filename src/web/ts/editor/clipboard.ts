"use strict";

// ═══════════════════════════════════════════════════════════
//  clipboard.ts — Grafcet Studio
//  Ctrl+C / Ctrl+V of a selected cluster (steps, transitions, branches and the
//  connections between them) within a flow or into another flow of the project.
//  Connections leaving the cluster are dropped. Pasted elements get new ids and
//  new step numbers from the target flow; manual step addresses are reset so the
//  target's address config applies.
// ═══════════════════════════════════════════════════════════

interface ClipVar { label?: string; [key: string]: unknown; }
interface ClipElement { id: string; x?: number; y?: number; [key: string]: unknown; }
interface ClipStep extends ClipElement {
  number?: number;
  initial?: boolean;
  actions?: Array<{ variable?: string; [key: string]: unknown }>;
  execAddress?: string | null;
  doneAddress?: string | null;
  addressManual?: boolean;
}
interface ClipTransition extends ClipElement { fromStepIds?: string[]; toStepIds?: string[]; }
interface ClipConnection { id: string; from: string; to: string; [key: string]: unknown; }
interface ClipDiagramState {
  steps: ClipStep[];
  transitions: ClipTransition[];
  parallels: ClipElement[];
  connections: ClipConnection[];
  vars?: ClipVar[];
}
interface ClipPayload {
  sourceDiagramId: string | null;
  steps: ClipStep[];
  transitions: ClipTransition[];
  parallels: ClipElement[];
  connections: ClipConnection[];
  vars: ClipVar[];          // source-flow local variables the copied actions use
  pasteCount: number;       // offsets repeated pastes into the source flow
}

// state / selIds / nextId / nextStepNum / afterChange are declared by store.ts, history.ts, change-manager.ts.
function clipState(): ClipDiagramState {
  return state as unknown as ClipDiagramState;
}

const CLIP_OFFSET = 40;
let clipPayload: ClipPayload | null = null;

function clipClone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function clipboardCopy(): boolean {
  const state = clipState();
  const ids = selIds;
  const steps = state.steps.filter(s => ids.has(s.id));
  const transitions = state.transitions.filter(t => ids.has(t.id));
  const parallels = (state.parallels || []).filter(p => ids.has(p.id));
  if (!steps.length && !transitions.length && !parallels.length) return false;

  const inside = new Set<string>([...steps, ...transitions, ...parallels].map(e => e.id));
  const connections = state.connections.filter(c => inside.has(c.from) && inside.has(c.to));

  // Local variables of this flow that the copied actions reference by label.
  const used = new Set<string>();
  steps.forEach(s => (s.actions || []).forEach(a => { if (a && a.variable) used.add(String(a.variable).split('.')[0]); }));
  const vars = (state.vars || []).filter(v => !!v && !!v.label && used.has(String(v.label)));

  clipPayload = clipClone({
    sourceDiagramId: activeDiagramId,
    steps, transitions, parallels, connections, vars,
    pasteCount: 0
  });
  const n = inside.size;
  toast(`Copied ${n} element${n !== 1 ? 's' : ''}`);
  return true;
}

function clipboardPaste(): boolean {
  if (!clipPayload) return false;
  const state = clipState();
  const src = clipPayload;
  const sameFlow = src.sourceDiagramId === activeDiagramId;
  if (sameFlow) src.pasteCount++;
  const offset = sameFlow ? CLIP_OFFSET * src.pasteCount : 0;

  const idMap = new Map<string, string>();
  // Guard against a stale nextId / nextStepNum: never reuse an id or a step number already in this flow.
  const taken = new Set<string>([...state.steps, ...state.transitions, ...(state.parallels || []), ...state.connections].map(e => e.id));
  const newId = (prefix: string): string => {
    let id = prefix + (nextId++);
    while (taken.has(id)) id = prefix + (nextId++);
    taken.add(id);
    return id;
  };
  nextStepNum = Math.max(nextStepNum, ...state.steps.map(s => (s.number || 0) + 1));
  const move = <T extends ClipElement>(el: T): T => {
    if (typeof el.x === 'number') el.x += offset;
    if (typeof el.y === 'number') el.y += offset;
    return el;
  };
  const hasInitial = state.steps.some(s => s.initial);

  // Steps keep their relative order and take the next numbers of this flow.
  const steps = clipClone(src.steps).sort((a, b) => (a.number || 0) - (b.number || 0)).map(s => {
    const id = newId('S');
    idMap.set(s.id, id);
    s.id = id;
    s.number = Math.max(1, nextStepNum++);
    if (hasInitial) s.initial = false;
    if (s.addressManual) { s.addressManual = false; s.execAddress = null; s.doneAddress = null; }
    return move(s);
  });
  const transitions = clipClone(src.transitions).map(t => {
    const id = newId('T');
    idMap.set(t.id, id);
    t.id = id;
    return move(t);
  });
  const parallels = clipClone(src.parallels).map(p => {
    const id = newId('B');
    idMap.set(p.id, id);
    p.id = id;
    return move(p);
  });
  const remapList = (list?: string[]): string[] | undefined =>
    list ? list.filter(x => idMap.has(x)).map(x => idMap.get(x)!) : list;
  transitions.forEach(t => { t.fromStepIds = remapList(t.fromStepIds); t.toStepIds = remapList(t.toStepIds); });
  const connections = clipClone(src.connections).map(c => {
    c.id = newId('C');
    c.from = idMap.get(c.from)!;
    c.to = idMap.get(c.to)!;
    return c;
  });

  // Local variables: add the ones this flow lacks; an existing label in the target wins.
  let addedVars = 0;
  if (!sameFlow && src.vars.length) {
    if (!Array.isArray(state.vars)) state.vars = [];
    const have = new Set(state.vars.map(v => String(v && v.label || '').toLowerCase()));
    clipClone(src.vars).forEach(v => {
      const key = String(v.label || '').toLowerCase();
      if (!key || have.has(key)) return;
      delete v.id;
      state.vars!.push(v);
      have.add(key);
      addedVars++;
    });
  }

  state.steps.push(...steps);
  state.transitions.push(...transitions);
  if (!Array.isArray(state.parallels)) state.parallels = [];
  state.parallels.push(...parallels);
  state.connections.push(...connections);

  selIds.clear();
  idMap.forEach(id => selIds.add(id));
  afterChange();
  render();
  updateProps();
  if (addedVars && typeof updateVarDatalist === 'function') updateVarDatalist();
  const n = idMap.size;
  toast(`Pasted ${n} element${n !== 1 ? 's' : ''}` + (addedVars ? ` · ${addedVars} local variable${addedVars !== 1 ? 's' : ''} added` : ''));
  return true;
}

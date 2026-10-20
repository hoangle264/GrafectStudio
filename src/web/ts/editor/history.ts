// ═══════════════════════════════════════════════════════════
//  history.ts — Grafcet Studio
//  Snapshot-based Undo/Redo for the active diagram canvas.
//  historyCommit() is called from afterChange() and after a diagram
//  is loaded; it records the previous snapshot whenever the diagram
//  content differs from the last committed one.
// ═══════════════════════════════════════════════════════════

declare function render(): void;
declare function updateProps(): void;
declare let selIds: Set<string>;

const HISTORY_LIMIT = 100;
/** Commits closer together than this merge into one undo step (typing, multi-delete). */
const HISTORY_MERGE_MS = 600;

interface DiagramHistory {
  undo: string[];
  redo: string[];
  last: string;
  lastCommitAt: number;
}

const diagramHistories = new Map<string, DiagramHistory>();

function historyIsCanvasDiagram(id: string | null): id is string {
  return !!id && id !== VARS_TAB_ID && id !== IO_MAPPING_TAB_ID && !String(id).startsWith(STRUCT_TAB_PREFIX);
}

function historySerialize(): string {
  return JSON.stringify({ state: state, nextId: nextId, nextStepNum: nextStepNum });
}

function historyCommit(): void {
  if (!historyIsCanvasDiagram(activeDiagramId)) return;
  const current = historySerialize();
  const entry = diagramHistories.get(activeDiagramId);
  if (!entry) {
    diagramHistories.set(activeDiagramId, { undo: [], redo: [], last: current, lastCommitAt: 0 });
    historyUpdateUi();
    return;
  }
  if (current === entry.last) return;
  const now = Date.now();
  if (now - entry.lastCommitAt > HISTORY_MERGE_MS || entry.undo.length === 0) {
    entry.undo.push(entry.last);
    if (entry.undo.length > HISTORY_LIMIT) entry.undo.shift();
  }
  entry.redo = [];
  entry.last = current;
  entry.lastCommitAt = now;
  historyUpdateUi();
}

function historyApply(snapshot: string): void {
  const data = JSON.parse(snapshot);
  state = data.state;
  nextId = data.nextId;
  nextStepNum = data.nextStepNum;
  selIds.clear();
  render();
  updateProps();
  saveDiagramData(activeDiagramId!);
}

function historyStep(direction: 'undo' | 'redo'): boolean {
  if (!historyIsCanvasDiagram(activeDiagramId)) return false;
  // Capture any change that bypassed afterChange() so it is not lost.
  historyCommit();
  const entry = diagramHistories.get(activeDiagramId);
  if (!entry) return false;
  const from = direction === 'undo' ? entry.undo : entry.redo;
  const to = direction === 'undo' ? entry.redo : entry.undo;
  const snapshot = from.pop();
  if (snapshot === undefined) {
    toast(direction === 'undo' ? 'Nothing to undo' : 'Nothing to redo', { kind: 'info', duration: 1200 });
    return false;
  }
  to.push(entry.last);
  entry.last = snapshot;
  // A following edit must start a new undo step.
  entry.lastCommitAt = 0;
  historyApply(snapshot);
  historyUpdateUi();
  return true;
}

function historyUndo(): boolean { return historyStep('undo'); }
function historyRedo(): boolean { return historyStep('redo'); }

function historyForget(id: string): void {
  diagramHistories.delete(id);
}

/** Drop all canvas history, e.g. when the whole project is replaced. */
function historyReset(): void {
  diagramHistories.clear();
  historyUpdateUi();
}

function historyUpdateUi(): void {
  const entry = historyIsCanvasDiagram(activeDiagramId) ? diagramHistories.get(activeDiagramId) : undefined;
  const undoBtn = document.getElementById('menu-undo') as HTMLButtonElement | null;
  const redoBtn = document.getElementById('menu-redo') as HTMLButtonElement | null;
  if (undoBtn) undoBtn.disabled = !entry || entry.undo.length === 0;
  if (redoBtn) redoBtn.disabled = !entry || entry.redo.length === 0;
}

Object.assign(window, {
  historyCommit,
  historyUndo,
  historyRedo,
  historyForget,
  historyReset,
  historyUpdateUi,
});

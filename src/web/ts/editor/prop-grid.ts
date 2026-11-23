"use strict";

// ═══════════════════════════════════════════════════════════
//  prop-grid.ts — Grafcet Studio
//  Visual Studio style property grid for canvas elements:
//  collapsible categories (remembered per viewer) and a multi-selection
//  grid that shows the properties the selected elements share. A field whose
//  values differ shows "(multiple)"; editing it sets every selected element.
// ═══════════════════════════════════════════════════════════

interface PgElement { id: string; x?: number; y?: number; [key: string]: unknown; }
interface PgState { steps: PgElement[]; transitions: PgElement[]; parallels: PgElement[]; }
type PgKind = 'step' | 'transition' | 'parallel';
interface PgField {
  key: string;
  label: string;
  kinds: PgKind[];
  type: 'text' | 'number' | 'check';
  mono?: boolean;
  numericOnly?: boolean;   // shown only in numeric step mode flows
}

declare function snap(v: number): number;
declare function isNumericFlow(): boolean;

const PG_COLLAPSED_KEY = 'gs.propGrid.collapsed';

const PG_MULTI_FIELDS: Array<{ title: string; fields: PgField[] }> = [
  { title: 'Common', fields: [
    { key: 'label', label: 'Label', kinds: ['step', 'transition'], type: 'text' }
  ] },
  { title: 'Step', fields: [
    { key: 'initial', label: 'Initial', kinds: ['step'], type: 'check' },
    { key: 'stepValue', label: 'Step value', kinds: ['step'], type: 'number', mono: true, numericOnly: true }
  ] },
  { title: 'Transition', fields: [
    { key: 'condition', label: 'Condition', kinds: ['transition'], type: 'text', mono: true }
  ] },
  { title: 'Layout', fields: [
    { key: 'x', label: 'X', kinds: ['step', 'transition', 'parallel'], type: 'number', mono: true },
    { key: 'y', label: 'Y', kinds: ['step', 'transition', 'parallel'], type: 'number', mono: true }
  ] }
];

function pgState(): PgState {
  return state as unknown as PgState;
}

function pgReadCollapsed(): Set<string> {
  try { return new Set(JSON.parse(localStorage.getItem(PG_COLLAPSED_KEY) || '[]') as string[]); }
  catch (e) { return new Set(); }
}

function pgWriteCollapsed(keys: Set<string>): void {
  try { localStorage.setItem(PG_COLLAPSED_KEY, JSON.stringify([...keys])); } catch (e) { /* storage unavailable */ }
}

function pgToggle(head: HTMLElement): void {
  const cat = head.closest<HTMLElement>('.pg-cat');
  if (!cat) return;
  const collapsed = cat.classList.toggle('collapsed');
  const key = cat.dataset.pgCat;
  if (!key) return;
  const keys = pgReadCollapsed();
  if (collapsed) keys.add(key); else keys.delete(key);
  pgWriteCollapsed(keys);
}

function pgRestoreCollapsed(root?: ParentNode): void {
  const keys = pgReadCollapsed();
  (root || document).querySelectorAll<HTMLElement>('.pg-cat[data-pg-cat]').forEach(cat => {
    cat.classList.toggle('collapsed', keys.has(cat.dataset.pgCat || ''));
  });
}

function pgSelected(ids: string[]): Array<{ kind: PgKind; el: PgElement }> {
  const st = pgState();
  const out: Array<{ kind: PgKind; el: PgElement }> = [];
  ids.forEach(id => {
    const s = st.steps.find(x => x.id === id);
    if (s) { out.push({ kind: 'step', el: s }); return; }
    const t = st.transitions.find(x => x.id === id);
    if (t) { out.push({ kind: 'transition', el: t }); return; }
    const p = (st.parallels || []).find(x => x.id === id);
    if (p) out.push({ kind: 'parallel', el: p });
  });
  return out;
}

function renderMultiProps(ids: string[]): void {
  const summary = document.getElementById('mx-summary');
  const body = document.getElementById('mx-body');
  if (!summary || !body) return;
  const items = pgSelected(ids);
  const counts: Record<PgKind, number> = { step: 0, transition: 0, parallel: 0 };
  items.forEach(item => counts[item.kind]++);
  const parts = ([['step', 'step'], ['transition', 'transition'], ['parallel', 'branch']] as Array<[PgKind, string]>)
    .filter(([kind]) => counts[kind] > 0)
    .map(([kind, word]) => counts[kind] + ' ' + word + (counts[kind] !== 1 ? 's' : ''));
  summary.textContent = items.length + ' selected · ' + parts.join(', ');

  body.innerHTML = '';
  PG_MULTI_FIELDS.forEach(group => {
    const rows: HTMLElement[] = [];
    group.fields.forEach(field => {
      if (field.numericOnly && !(typeof isNumericFlow === 'function' && isNumericFlow())) return;
      const targets = items.filter(item => field.kinds.indexOf(item.kind) >= 0);
      if (!targets.length) return;
      rows.push(pgMultiRow(field, targets.map(item => item.el)));
    });
    if (!rows.length) return;
    const cat = document.createElement('div');
    cat.className = 'pg-cat';
    cat.dataset.pgCat = 'multi-' + group.title.toLowerCase();
    cat.innerHTML = '<div class="pg-cat-head" onclick="pgToggle(this)"><span class="pg-chev">&#9662;</span>' + esc2(group.title) + '</div>';
    const groupBody = document.createElement('div');
    groupBody.className = 'pg-body';
    rows.forEach(row => groupBody.appendChild(row));
    cat.appendChild(groupBody);
    body.appendChild(cat);
  });
  if (counts.step >= 2) body.appendChild(pgRenumberTools(items.filter(item => item.kind === 'step').map(item => item.el)));
  pgRestoreCollapsed(body);
}

/** Renumber the selected steps in reading order (top to bottom, then left to right). */
function pgRenumberTools(steps: PgElement[]): HTMLElement {
  const cat = document.createElement('div');
  cat.className = 'pg-cat';
  cat.dataset.pgCat = 'multi-renumber';
  cat.innerHTML = '<div class="pg-cat-head" onclick="pgToggle(this)"><span class="pg-chev">&#9662;</span>Renumber steps</div>';
  const body = document.createElement('div');
  body.className = 'pg-body';
  const ordered = steps.slice().sort((a, b) => ((a.y || 0) - (b.y || 0)) || ((a.x || 0) - (b.x || 0)));
  const firstNumber = Math.min(...steps.map(s => Number(s.number) || 1));
  const numeric = typeof isNumericFlow === 'function' && isNumericFlow();
  const firstValue = numeric ? Math.min(...steps.map(s => pgStepValue(s))) : 10;

  const addRow = (label: string, start: number, stepBy: number | null, key: 'number' | 'stepValue') => {
    const row = document.createElement('div');
    row.className = 'pg-row';
    row.innerHTML = '<label class="pg-name">' + esc2(label) + '</label>';
    const val = document.createElement('div');
    val.className = 'pg-val pg-inline';
    const startInput = document.createElement('input');
    startInput.type = 'number'; startInput.min = '1'; startInput.className = 'pg-input mono'; startInput.value = String(start);
    startInput.title = 'Start';
    val.appendChild(startInput);
    let byInput: HTMLInputElement | null = null;
    if (stepBy != null) {
      byInput = document.createElement('input');
      byInput.type = 'number'; byInput.min = '1'; byInput.className = 'pg-input mono'; byInput.value = String(stepBy);
      byInput.title = 'Increment';
      val.appendChild(byInput);
    }
    const btn = document.createElement('button');
    btn.className = 'btn btn-sm'; btn.textContent = 'Apply';
    btn.addEventListener('click', () => pgRenumber(ordered, key, Number(startInput.value), byInput ? Number(byInput.value) : 1));
    val.appendChild(btn);
    row.appendChild(val);
    body.appendChild(row);
  };
  addRow('Number from', firstNumber, null, 'number');
  if (numeric) addRow('Value from / by', firstValue, 10, 'stepValue');
  const hint = document.createElement('div');
  hint.className = 'pg-hint';
  hint.textContent = 'Order: top to bottom, then left to right.';
  body.appendChild(hint);
  cat.appendChild(body);
  return cat;
}

function pgStepValue(step: PgElement): number {
  const v = Number(step.stepValue);
  return Number.isInteger(v) && v > 0 ? v : Math.max(1, Number(step.number) || 1) * 10;
}

function pgRenumber(ordered: PgElement[], key: 'number' | 'stepValue', start: number, by: number): void {
  if (!Number.isInteger(start) || start < 1 || !Number.isInteger(by) || by < 1) { toast('Enter whole numbers of 1 or more', { kind: 'warn' }); return; }
  const next = ordered.map((_, i) => start + i * by);
  // The new numbers must not clash with steps outside the selection.
  const chosen = new Set(ordered.map(s => s.id));
  const others = pgState().steps.filter(s => !chosen.has(s.id));
  const taken = new Set(others.map(s => key === 'number' ? Number(s.number) : pgStepValue(s)));
  const clash = next.find(n => taken.has(n));
  if (clash != null) { toast((key === 'number' ? 'Step number ' : 'Step value ') + clash + ' is already used by a step outside the selection', { kind: 'warn' }); return; }
  ordered.forEach((s, i) => { s[key] = next[i]; });
  if (key === 'number') {
    const maxNumber = Math.max(...pgState().steps.map(s => Number(s.number) || 0));
    if (nextStepNum <= maxNumber) nextStepNum = maxNumber + 1;
  }
  afterChange();
  render();
  renderMultiProps([...selIds]);
  toast('Renumbered ' + ordered.length + ' steps');
}

function pgMultiRow(field: PgField, targets: PgElement[]): HTMLElement {
  const values = targets.map(el => field.type === 'check' ? !!el[field.key] : el[field.key] == null ? '' : String(el[field.key]));
  const same = values.every(v => v === values[0]);
  const row = document.createElement('div');
  row.className = 'pg-row';
  const name = document.createElement('label');
  name.className = 'pg-name';
  name.textContent = field.label;
  const val = document.createElement('div');
  val.className = 'pg-val';
  const input = document.createElement('input');
  if (field.type === 'check') {
    input.type = 'checkbox';
    input.className = 'pg-check';
    input.checked = same && values[0] === true;
    input.indeterminate = !same;
    input.addEventListener('change', () => pgApply(field, targets, input.checked));
  } else {
    input.type = field.type;
    input.className = 'pg-input' + (field.mono ? ' mono' : '');
    input.value = same ? String(values[0]) : '';
    if (!same) input.placeholder = '(multiple)';
    // Apply on commit, not per keystroke, so a half-typed value never lands on every element.
    input.addEventListener('change', () => {
      if (field.type === 'number') {
        if (input.value.trim() === '') return;
        const n = Number(input.value);
        if (!Number.isFinite(n)) return;
        const isCoord = field.key === 'x' || field.key === 'y';
        if (!isCoord && (!Number.isInteger(n) || n < 1)) return;
        pgApply(field, targets, isCoord && typeof snap === 'function' ? snap(n) : n);
      } else {
        pgApply(field, targets, input.value);
      }
    });
  }
  val.appendChild(input);
  row.appendChild(name);
  row.appendChild(val);
  return row;
}

function pgApply(field: PgField, targets: PgElement[], value: unknown): void {
  targets.forEach(el => { el[field.key] = value; });
  afterChange();
  render();
  renderMultiProps([...selIds]);
}

window.addEventListener('load', () => pgRestoreCollapsed());

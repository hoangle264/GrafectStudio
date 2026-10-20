// ═══════════════════════════════════════════════════════════
//  ui-feedback.ts — Grafcet Studio
//  In-app confirm/choice dialogs (replacing window.confirm) and
//  one-shot "Undo" for project-level destructive actions.
// ═══════════════════════════════════════════════════════════

declare function openTab(id: string): void;
declare function renderTabs(): void;

interface DialogChoice<T> {
  label: string;
  value: T;
  /** 'primary' = default action, 'danger' = destructive action */
  style?: 'primary' | 'danger';
}

interface ChoiceDialogOptions<T> {
  title: string;
  message: string;
  choices: DialogChoice<T>[];
  /** Value returned on Escape / backdrop click / Cancel */
  cancelValue: T;
  cancelLabel?: string;
}

/** Modal dialog with custom buttons. Resolves with the chosen value. */
function choiceDialog<T>(opts: ChoiceDialogOptions<T>): Promise<T> {
  return new Promise(function(resolve) {
    const bg = document.createElement('div');
    bg.className = 'modal-bg show ui-dialog-bg';
    const box = document.createElement('div');
    box.className = 'modal ui-dialog';
    box.setAttribute('role', 'dialog');
    box.setAttribute('aria-modal', 'true');

    const h = document.createElement('h2');
    h.textContent = opts.title;
    const p = document.createElement('p');
    p.className = 'ui-dialog-msg';
    p.textContent = opts.message;
    const btns = document.createElement('div');
    btns.className = 'modal-btns';

    let done = false;
    const finish = function(value: T): void {
      if (done) return;
      done = true;
      document.removeEventListener('keydown', onKey, true);
      bg.remove();
      resolve(value);
    };
    const onKey = function(e: KeyboardEvent): void {
      if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); finish(opts.cancelValue); }
    };

    const cancel = document.createElement('button');
    cancel.className = 'btn';
    cancel.textContent = opts.cancelLabel || 'Cancel';
    cancel.onclick = function() { finish(opts.cancelValue); };
    btns.appendChild(cancel);

    let focusBtn: HTMLButtonElement = cancel;
    opts.choices.forEach(function(choice) {
      const b = document.createElement('button');
      b.className = 'btn' + (choice.style === 'danger' ? ' r' : choice.style === 'primary' ? ' a' : '');
      b.textContent = choice.label;
      b.onclick = function() { finish(choice.value); };
      btns.appendChild(b);
      // Focus the primary action; destructive actions never get default focus.
      if (choice.style === 'primary') focusBtn = b;
    });

    bg.addEventListener('mousedown', function(e) { if (e.target === bg) finish(opts.cancelValue); });
    document.addEventListener('keydown', onKey, true);
    box.append(h, p, btns);
    bg.appendChild(box);
    document.body.appendChild(bg);
    focusBtn.focus();
  });
}

/** Promise-based replacement for window.confirm(). */
function confirmDialog(title: string, message: string, okLabel = 'OK', danger = false): Promise<boolean> {
  return choiceDialog<boolean>({
    title: title,
    message: message,
    choices: [{ label: okLabel, value: true, style: danger ? 'danger' : 'primary' }],
    cancelValue: false
  });
}

// ── Project-level undo ─────────────────────────────────────

interface ProjectSnapshot {
  project: string;
  diagrams: Array<[string, unknown]>;
  openTabs: string;
  activeDiagramId: string | null;
}

function captureProjectSnapshot(): ProjectSnapshot {
  // Make sure the open canvas is persisted before snapshotting diagram data.
  if (typeof saveAutoDiagram === 'function') saveAutoDiagram();
  return {
    project: JSON.stringify(project),
    diagrams: (project.diagrams || []).map(function(d) { return [d.id, loadDiagramData(d.id)] as [string, unknown]; }),
    openTabs: JSON.stringify(openTabs),
    activeDiagramId: activeDiagramId
  };
}

function restoreProjectSnapshot(snap: ProjectSnapshot): void {
  const keep = new Set(snap.diagrams.map(function(entry) { return entry[0]; }));
  // Drop diagram data created after the snapshot (e.g. by New Project).
  (project.diagrams || []).forEach(function(d) { if (!keep.has(d.id)) deleteDiagramData(d.id); });
  project = JSON.parse(snap.project);
  historyReset();
  snap.diagrams.forEach(function(entry) {
    if (entry[1]) GrafcetStudioStorePersistence.saveDiagramData(entry[0], entry[1] as GrafcetStudioStorePersistence.DiagramSaveData);
  });
  saveProject();
  openTabs = JSON.parse(snap.openTabs);
  // Clear first so openTab() does not flush the current canvas over restored data.
  activeDiagramId = null;
  if (snap.activeDiagramId) openTab(snap.activeDiagramId);
  else { renderTree(); renderTabs(); }
}

/**
 * Run a destructive project change and offer a one-shot Undo in the toast.
 * `change` returns false when it did nothing (then no toast is shown).
 */
function runUndoableProjectChange(doneMessage: string, change: () => boolean | void): void {
  const snap = captureProjectSnapshot();
  if (change() === false) return;
  toast(doneMessage, {
    action: {
      label: 'Undo',
      run: function() {
        restoreProjectSnapshot(snap);
        toast('Restored');
      }
    }
  });
}

// ── Save status (status bar) ───────────────────────────────

let saveFailed = false;

/** Called after every localStorage write; shows the last save time or a failure. */
function noteSaveResult(ok: boolean): void {
  const el = document.getElementById('s-save');
  if (ok) {
    saveFailed = false;
    if (!el) return;
    const t = new Date();
    const hhmm = String(t.getHours()).padStart(2, '0') + ':' + String(t.getMinutes()).padStart(2, '0');
    el.textContent = '✓ Saved ' + hhmm;
    el.title = 'All changes are saved automatically';
    el.classList.remove('s-save-error');
    return;
  }
  // Notify once per failure streak, not on every keystroke.
  if (!saveFailed) notifyError('Could not save: browser storage is full or unavailable. Export the project to keep your work.');
  saveFailed = true;
  if (el) {
    el.textContent = '⚠ Not saved';
    el.title = 'Last save failed — use File > Export Project to keep a copy';
    el.classList.add('s-save-error');
  }
}

Object.assign(window, {
  noteSaveResult,
  choiceDialog,
  confirmDialog,
  runUndoableProjectChange,
});

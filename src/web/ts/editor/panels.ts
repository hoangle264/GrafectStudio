"use strict";

// ═══════════════════════════════════════════════════════════
//  PANEL TOGGLE
// ═══════════════════════════════════════════════════════════

declare function drawGrid(): void;
declare function loadProject(): void;
declare function renderTabs(): void;
declare function renderTree(): void;
declare function applyView(): void;
declare function onKey(e: KeyboardEvent): void;
declare function hideCtx(): void;
declare function onWheel(e: WheelEvent): void;
declare function miniMap(): void;
declare function setRightPanelTab(tab: string): void;

interface PanelState {
  sidebar: boolean;
  rpanel: boolean;
  proj: boolean;
  tools: boolean;
  sbWidth: number;
  rpWidth: number;
  [key: string]: boolean | number;
}

const PANEL_KEY = 'gf2-panels';
const PIN_ICON_SVG = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 17v5"/><path d="M9 10.8a2 2 0 0 1-1.1 1.8l-1.8.9A2 2 0 0 0 5 15.2V16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-.8a2 2 0 0 0-1.1-1.8l-1.8-.9A2 2 0 0 1 15 10.8V7a1 1 0 0 1 1-1 2 2 0 0 0 0-4H8a2 2 0 0 0 0 4 1 1 0 0 1 1 1z"/></svg>';
const PANEL_MIN_W = 200;
const PANEL_MAX_W = 480;
const PANEL_DEFAULT_W = 256;
const PANEL_NARROW_BP = 1280;

// Persisted preferences (what the user chose on a wide screen)
const panelState: PanelState = { sidebar: true, rpanel: true, proj: true, tools: true, sbWidth: PANEL_DEFAULT_W, rpWidth: PANEL_DEFAULT_W };
// Effective open/closed state — the single source applied to the DOM
const panelView = { sidebar: true, rpanel: true };
let panelNarrow = false;

function clampPanelWidth(w: number): number {
  return Math.max(PANEL_MIN_W, Math.min(PANEL_MAX_W, Math.round(w) || PANEL_DEFAULT_W));
}

function loadPanelState(): void {
  try {
    const s = JSON.parse(localStorage.getItem(PANEL_KEY) || '{}');
    Object.assign(panelState, s);
  } catch (e) { /* ignore */ }
  panelState.sbWidth = clampPanelWidth(panelState.sbWidth);
  panelState.rpWidth = clampPanelWidth(panelState.rpWidth);
  panelNarrow = window.innerWidth < PANEL_NARROW_BP;
  panelView.sidebar = panelState.sidebar && !panelNarrow;
  panelView.rpanel = panelState.rpanel && !panelNarrow;
}
function savePanelState(): void {
  try { localStorage.setItem(PANEL_KEY, JSON.stringify(panelState)); } catch (e) { /* ignore */ }
}

function applyPanelState(): void {
  const app = document.querySelector('.app') as HTMLElement | null;
  app?.style.setProperty('--sb-w', panelState.sbWidth + 'px');
  app?.style.setProperty('--rp-w', panelState.rpWidth + 'px');

  document.getElementById('sidebar')?.classList.toggle('panel-closed', !panelView.sidebar);
  document.getElementById('right-panel')?.classList.toggle('panel-closed', !panelView.rpanel);
  document.getElementById('menu-toggle-sidebar')?.classList.toggle('checked', panelView.sidebar);
  document.getElementById('menu-toggle-rpanel')?.classList.toggle('checked', panelView.rpanel);

  // Sub panels
  ['proj'].forEach(id => {
    const open = !!panelState[id];
    const sp = document.getElementById('sub-' + id);
    const pb = document.getElementById('pin-' + id);
    if (sp) sp.classList.toggle('sub-closed', !open);
    if (pb) {
      pb.classList.toggle('pinned', open);
      pb.innerHTML = PIN_ICON_SVG;
      pb.title = open ? 'Collapse section' : 'Expand section';
    }
  });

  const rpb = document.getElementById('pin-rpanel');
  if (rpb) {
    rpb.innerHTML = PIN_ICON_SVG;
    rpb.title = 'Hide panel (Ctrl+Alt+B)';
  }
  drawGrid();
}

function setPanelOpen(which: 'sidebar' | 'rpanel', open: boolean, persist = true): void {
  panelView[which] = open;
  // On narrow screens toggles are temporary; wide-screen choices are remembered
  if (persist && !panelNarrow) { panelState[which] = open; savePanelState(); }
  applyPanelState();
}

function toggleSidebar(): void { setPanelOpen('sidebar', !panelView.sidebar); }
function toggleRPanel(): void { setPanelOpen('rpanel', !panelView.rpanel); }

function toggleSubPanel(id: string): void {
  panelState[id] = !panelState[id];
  savePanelState(); applyPanelState();
}

// Called on selection change: reveal Properties when something gets selected
function revealPropsForSelection(hasSelection: boolean): void {
  if (!hasSelection || panelView.rpanel) return;
  setPanelOpen('rpanel', true, false);
  setRightPanelTab('props');
}

function onPanelResize(): void {
  const narrow = window.innerWidth < PANEL_NARROW_BP;
  if (narrow === panelNarrow) return;
  panelNarrow = narrow;
  panelView.sidebar = panelState.sidebar && !narrow;
  panelView.rpanel = panelState.rpanel && !narrow;
  applyPanelState();
}

function onPanelKey(e: KeyboardEvent): void {
  if (!(e.ctrlKey || e.metaKey) || e.shiftKey || e.key.toLowerCase() !== 'b') return;
  e.preventDefault();
  if (e.altKey) toggleRPanel(); else toggleSidebar();
}

function initPanelResizers(): void {
  const defs: Array<[string, 'sbWidth' | 'rpWidth', number]> = [
    ['sidebar-resizer', 'sbWidth', 1],
    ['rpanel-resizer', 'rpWidth', -1],
  ];
  defs.forEach(([id, key, dir]) => {
    const handle = document.getElementById(id);
    if (!handle) return;
    handle.addEventListener('pointerdown', (e: PointerEvent) => {
      e.preventDefault();
      const startX = e.clientX;
      const startW = panelState[key] as number;
      handle.setPointerCapture(e.pointerId);
      document.body.classList.add('panel-resizing');
      const move = (ev: PointerEvent) => {
        panelState[key] = clampPanelWidth(startW + (ev.clientX - startX) * dir);
        applyPanelState();
      };
      const up = () => {
        handle.removeEventListener('pointermove', move);
        handle.removeEventListener('pointerup', up);
        document.body.classList.remove('panel-resizing');
        savePanelState(); miniMap();
      };
      handle.addEventListener('pointermove', move);
      handle.addEventListener('pointerup', up);
    });
    handle.addEventListener('dblclick', () => { panelState[key] = PANEL_DEFAULT_W; savePanelState(); applyPanelState(); });
  });
}

function init(): void {
  loadPanelState();
  loadProject();
  drawGrid();
  renderTabs();
  renderTree();
  applyPanelState();
  initPanelResizers();
  window.addEventListener('resize', () => { onPanelResize(); drawGrid(); miniMap(); });
  document.addEventListener('keydown', onPanelKey);
  document.addEventListener('keydown', onKey);
  ['sidebar', 'right-panel'].forEach(id => document.getElementById(id)?.addEventListener('transitionend', e => {
    if ((e as TransitionEvent).propertyName === 'width') { drawGrid(); miniMap(); }
  }));
  document.addEventListener('click', e => { if (!(e.target as HTMLElement).closest('#ctx')) hideCtx(); });
  const cw = document.getElementById('canvas-wrap');
  cw?.addEventListener('wheel', onWheel as EventListener, { passive: false });
  applyView();
}

// esc2() and esc() → moved to src/js/modules/utils.js

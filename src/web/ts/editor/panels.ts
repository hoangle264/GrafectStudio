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

interface PanelState {
  sidebar: boolean;
  rpanel: boolean;
  proj: boolean;
  tools: boolean;
  [key: string]: boolean;
}

const PANEL_KEY = 'gf2-panels';
const panelState: PanelState = { sidebar: true, rpanel: true, proj: true, tools: true };

function loadPanelState(): void {
  try {
    const s = JSON.parse(localStorage.getItem(PANEL_KEY) || '{}');
    Object.assign(panelState, s);
  } catch (e) { /* ignore */ }
}
function savePanelState(): void {
  try { localStorage.setItem(PANEL_KEY, JSON.stringify(panelState)); } catch (e) { /* ignore */ }
}

function applyPanelState(): void {
  // Sidebar
  const sb = document.getElementById('sidebar');
  const stb = document.getElementById('sidebar-toggle-btn');
  sb?.classList.toggle('panel-closed', !panelState.sidebar);
  if (stb) stb.textContent = panelState.sidebar ? '◀' : '▶';

  // Sub panels
  ['proj', 'tools'].forEach(id => {
    const sp = document.getElementById('sub-' + id);
    const pb = document.getElementById('pin-' + id);
    if (sp) sp.classList.toggle('sub-closed', !panelState[id]);
    if (pb) {
      pb.classList.toggle('pinned', panelState[id]);
      pb.textContent = panelState[id] ? '📌' : '📍';
      pb.title = panelState[id] ? 'Collapse section' : 'Expand section';
    }
  });

  // Right panel
  const rp = document.getElementById('right-panel');
  const rpb = document.getElementById('pin-rpanel');
  rp?.classList.toggle('panel-closed', !panelState.rpanel);
  if (rpb) {
    rpb.classList.toggle('pinned', panelState.rpanel);
    rpb.textContent = panelState.rpanel ? '📌' : '📍';
    rpb.title = panelState.rpanel ? 'Collapse' : 'Expand';
  }
  drawGrid();
}

function toggleSidebar(): void {
  panelState.sidebar = !panelState.sidebar;
  savePanelState(); applyPanelState();
}

// ─── Sidebar bottom-tab switcher ───
function switchSidebarTab(tab: string): void {
  // panels
  document.getElementById('sidebar-panel-proj')?.classList.toggle('active', tab === 'proj');
  document.getElementById('sidebar-panel-tools')?.classList.toggle('active', tab === 'tools');
  // tab buttons
  const tabProj = document.getElementById('stab-proj');
  const tabTools = document.getElementById('stab-tools');
  tabProj?.classList.toggle('active', tab === 'proj');
  tabTools?.classList.toggle('active', tab === 'tools');
  // ensure sidebar is open
  if (panelState.sidebar === false) { panelState.sidebar = true; savePanelState(); applyPanelState(); }
}
function toggleRPanel(): void {
  panelState.rpanel = !panelState.rpanel;
  savePanelState(); applyPanelState();
}
function toggleSubPanel(id: string): void {
  panelState[id] = !panelState[id];
  savePanelState(); applyPanelState();
}
function init(): void {
  loadPanelState();
  loadProject();
  drawGrid();
  renderTabs();
  renderTree();
  applyPanelState();
  window.addEventListener('resize', () => { drawGrid(); miniMap(); });
  document.addEventListener('keydown', onKey);
  document.addEventListener('click', e => { if (!(e.target as HTMLElement).closest('#ctx')) hideCtx(); });
  const cw = document.getElementById('canvas-wrap');
  cw?.addEventListener('wheel', onWheel as EventListener, { passive: false });
  applyView();
}

// esc2() and esc() → moved to src/js/modules/utils.js

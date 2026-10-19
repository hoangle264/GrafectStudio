// ═══════════════════════════════════════════════════════════
//  constants.ts — Grafcet Studio
//  Pure geometry/layout constants. No runtime canvas state here —
//  see editor/canvas-state.js for the mutable diagram-render globals
//  (state, nextId, viewX, dragging, connecting, ...), which stay JS
//  because they are read/written continuously by Tier D
//  (canvas.js, events.js, elements.js, project.js, actions.js).
// ═══════════════════════════════════════════════════════════
const SW = 160, SH = 48;    // Step width/height (wider)
const TW = 70, TH = 8;      // Transition width/height
const PH = 8;                // Parallel bar height (per line)
const GRID = 20;
const ACT_W = 160;           // Action box width (wider)
const SNAP_ENTER_THRESHOLD = 24; // px
const SNAP_EXIT_THRESHOLD = 58;  // px
const PAR_PORT_INSET = 50;
const PAR_PORT_MIN_INSET = 8;
const PAR_PORT_MIN_USABLE = 16;

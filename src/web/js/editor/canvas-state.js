"use strict";
// ═══════════════════════════════════════════════════════════
//  canvas-state.js — Grafcet Studio
//  Mutable diagram-render runtime state (Tier D globals).
//  Read/written continuously by canvas.js, events.js, elements.js,
//  project.js, actions.js. NOT converted to TS — see REFACTOR_PLAN.md
//  Nhat ky (Phase 1) for the deferred typing decision.
// ═══════════════════════════════════════════════════════════

// ── Per-Diagram runtime state ──
let state = { steps:[], transitions:[], parallels:[], connections:[] };
let nextId = 1, nextStepNum = 1;
let viewX=0, viewY=0, viewScale=1;
let snapOn=true;

// ── Interaction ──
let tool='select';
let selIds = new Set();     // multi-select
let dragging=false, dragMap=new Map(); // id -> {dx,dy}
let dragSnapState=null, dragSnapCandidates=[], dragSnapPrimaryId=null;
let panning=false, panSX=0, panSY=0;
let connecting=false, connFrom=null; // {id, type, port}
let selBoxing=false, selBoxSX=0, selBoxSY=0;
let resizingBar=null, resizeStartX=0, resizeStartW=0;
let ctxTarget=null;
let renameMode=null; // 'project' | 'diagram:{id}'

"use strict";
// ═══════════════════════════════════════════════════════════
//  graph-utils.js — Grafcet Studio
//  Pure graph traversal helpers for Grafcet sequence resolution.
//  Must be loaded BEFORE grafcet-studio-v2.js and grafcet-codegen.js.
// ═══════════════════════════════════════════════════════════

/**
 * Resolve all STEPS reachable from an element in one direction,
 * traversing through parallel bars.
 *
 * @param {string}   startId    - ID of the starting element
 * @param {string}   direction  - 'downstream' | 'upstream'
 * @param {Array}    connections
 * @param {Array}    steps
 * @param {Array}    parallels
 * @param {Set}      visited    - internal recursion guard
 * @returns {Array} list of step objects
 */
function resolveStepsThrough(startId, direction, connections, steps, parallels, visited=new Set()) {
  if(visited.has(startId)) return [];
  visited.add(startId);

  const result = [];
  // Get direct neighbours in the given direction
  const neighbours = direction === 'downstream'
    ? connections.filter(c=>c.from===startId).map(c=>c.to)
    : connections.filter(c=>c.to===startId).map(c=>c.from);

  for(const nId of neighbours){
    const step = steps.find(x=>x.id===nId);
    if(step){ result.push(step); continue; }
    // It's a parallel bar — traverse through it
    const bar = parallels.find(x=>x.id===nId);
    if(bar){
      // From the bar, continue in same direction
      const barNeighbours = direction === 'downstream'
        ? connections.filter(c=>c.from===nId).map(c=>c.to)
        : connections.filter(c=>c.to===nId).map(c=>c.from);
      for(const bnId of barNeighbours){
        const bStep = steps.find(x=>x.id===bnId);
        if(bStep) result.push(bStep);
        // Could be another bar — recurse
        else if(parallels.find(x=>x.id===bnId)){
          result.push(...resolveStepsThrough(bnId, direction, connections, steps, parallels, visited));
        }
      }
    }
  }
  return result;
}

/**
 * Resolve normalized branch-port spacing for a parallel bar.
 * Uses optional constants when they are available in the host page.
 *
 * @param {Object} parallelBar
 * @param {Object} options
 * @returns {{ports:number,inset:number,gap:number,startX:number,usableWidth:number}}
 */
function getParallelPortMetrics(parallelBar, options={}) {
  const ports = Math.max(2, parallelBar.ports || 3);
  const minInset = options.portInset ?? (typeof PAR_PORT_INSET !== 'undefined' ? PAR_PORT_INSET : 50);
  const minUsable = options.minUsable ?? (typeof PAR_PORT_MIN_USABLE !== 'undefined' ? PAR_PORT_MIN_USABLE : 16);
  const minAllowedInset = options.minInset ?? (typeof PAR_PORT_MIN_INSET !== 'undefined' ? PAR_PORT_MIN_INSET : 8);
  const maxInset = (parallelBar.width - minUsable) / 2;
  const inset = Math.min(minInset, Math.max(minAllowedInset, maxInset));
  const usableWidth = Math.max(1, parallelBar.width - inset * 2);
  const gap = ports === 1 ? 0 : usableWidth / (ports - 1);
  return { ports, inset, gap, startX: parallelBar.x + inset, usableWidth };
}

if (typeof window !== 'undefined') {
  window.getParallelPortMetrics = getParallelPortMetrics;
}

if (typeof module !== 'undefined' && module.exports) {
  module.exports = { resolveStepsThrough, getParallelPortMetrics };
}

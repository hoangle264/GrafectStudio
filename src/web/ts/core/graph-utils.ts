// ═══════════════════════════════════════════════════════════
//  graph-utils.ts — Grafcet Studio
//  Pure graph traversal helpers for Grafcet sequence resolution.
//  Must be loaded BEFORE grafcet-studio-v2.js and grafcet-codegen.js.
// ═══════════════════════════════════════════════════════════

type GraphUtilsStep = GrafcetStudioProject.Step;
type GraphUtilsConnection = GrafcetStudioProject.Connection;

interface GraphUtilsParallelBar {
  id: string;
  type?: string;
  ports?: number;
  width?: number;
  x: number;
  [key: string]: unknown;
}

interface ParallelPortMetricsOptions {
  portInset?: number;
  minUsable?: number;
  minInset?: number;
}

interface ParallelPortMetrics {
  ports: number;
  inset: number;
  gap: number;
  startX: number;
  usableWidth: number;
}

/**
 * Resolve all STEPS reachable from an element in one direction,
 * traversing through parallel bars.
 */
function resolveStepsThrough(
  startId: string,
  direction: string,
  connections: GraphUtilsConnection[],
  steps: GraphUtilsStep[],
  parallels: GraphUtilsParallelBar[],
  visited: Set<string> = new Set()
): GraphUtilsStep[] {
  if(visited.has(startId)) return [];
  visited.add(startId);

  const result: GraphUtilsStep[] = [];
  // Get direct neighbours in the given direction
  const neighbours = (direction === 'downstream'
    ? connections.filter(c=>c.from===startId).map(c=>c.to)
    : connections.filter(c=>c.to===startId).map(c=>c.from)
  ).filter((id): id is string => !!id);

  for(const nId of neighbours){
    const step = steps.find(x=>x.id===nId);
    if(step){ result.push(step); continue; }
    // It's a parallel bar — traverse through it
    const bar = parallels.find(x=>x.id===nId);
    if(bar){
      // From the bar, continue in same direction
      const barNeighbours = (direction === 'downstream'
        ? connections.filter(c=>c.from===nId).map(c=>c.to)
        : connections.filter(c=>c.to===nId).map(c=>c.from)
      ).filter((id): id is string => !!id);
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
 */
function getParallelPortMetrics(parallelBar: GraphUtilsParallelBar, options: ParallelPortMetricsOptions = {}): ParallelPortMetrics {
  const ports = Math.max(2, parallelBar.ports || 3);
  const minInset = options.portInset ?? (typeof PAR_PORT_INSET !== 'undefined' ? PAR_PORT_INSET : 50);
  const minUsable = options.minUsable ?? (typeof PAR_PORT_MIN_USABLE !== 'undefined' ? PAR_PORT_MIN_USABLE : 16);
  const minAllowedInset = options.minInset ?? (typeof PAR_PORT_MIN_INSET !== 'undefined' ? PAR_PORT_MIN_INSET : 8);
  const maxInset = ((parallelBar.width ?? 0) - minUsable) / 2;
  const inset = Math.min(minInset, Math.max(minAllowedInset, maxInset));
  const usableWidth = Math.max(1, (parallelBar.width ?? 0) - inset * 2);
  const gap = ports === 1 ? 0 : usableWidth / (ports - 1);
  return { ports, inset, gap, startX: parallelBar.x + inset, usableWidth };
}

window.getParallelPortMetrics = getParallelPortMetrics;

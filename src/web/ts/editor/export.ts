// ═══════════════════════════════════════════════════════════
//  export.ts — Grafcet Studio
//  IMPORT / EXPORT of project/diagram JSON, HTML snapshot export.
// ═══════════════════════════════════════════════════════════

type ExportStep = GrafcetStudioProject.Step;
type ExportTransition = GrafcetStudioProject.Transition;
type ExportConnection = GrafcetStudioProject.Connection;
type ExportDiagramState = GrafcetStudioProject.DiagramState;
type ExportStepAction = GrafcetStudioProject.StepAction;
type ExportDiagramMeta = GrafcetStudioProject.DiagramMeta;
type ExportProject = GrafcetStudioProject.Project;
type ExportStoredDiagramData = GrafcetStudioProject.StoredDiagramData;

interface ExportParallelBar {
  id: string;
  type?: string;
  ports?: number;
  width: number;
  x: number;
  y: number;
  [key: string]: unknown;
}

declare function flushState(): void;
declare function saveDiagramData(id: string, state?: ExportDiagramState, nextId?: number, nextStepNum?: number, viewX?: number, viewY?: number, viewScale?: number): void;
declare function loadDiagramData(id: string): ExportStoredDiagramData | null;
declare function deleteDiagramData(id: string): void;
declare function saveProject(): void;
declare function renderTree(): void;
declare function renderTabs(): void;
declare function openTab(id: string): void;
declare function ensureProjectVariables(): GrafcetStudioProject.ProjectVariables;
declare function upsertProjectVariable(bucket: string, variableDef: Partial<GrafcetStudioProject.ProjectVariable>): GrafcetStudioProject.ProjectVariable;
declare function ensureProjectIOMapping(): void;
declare function ensureFlowAddressConfig(diag: ExportDiagramMeta | null | undefined, assignUniqueBase: boolean): boolean;
declare function findNextAvailableBaseMr(unitId?: string | null, excludeDiagId?: string): string;

declare const ACT_QUALIFIERS: string[];
declare const ACT_QUAL_COLORS: Record<string, string>;

interface ImportRawDiagramV1 {
  state?: ExportDiagramState & { parallels?: ExportParallelBar[]; vars?: unknown[] };
  nextId?: number;
  nextStepNum?: number;
}

interface ImportRawDiagramEntry {
  id?: string;
  name?: string;
  unitId?: string | null;
  mode?: string;
  diagramType?: string;
  machine?: string;
  unit?: string;
  description?: string;
  addressMode?: GrafcetStudioProject.AddressMode;
  boolAddressMode?: GrafcetStudioProject.BoolAddressMode;
  baseMr?: string | number;
  activeWord?: string;
  completeWord?: string;
  activeWordTag?: string;
  completeWordTag?: string;
  data?: {
    state?: ExportDiagramState & { parallels?: ExportParallelBar[]; vars?: unknown[] };
    nextId?: number;
    nextStepNum?: number;
    viewX?: number;
    viewY?: number;
    viewScale?: number;
  };
}

interface ImportRawProjectPayload {
  version?: string;
  project?: { id?: string; name?: string; machineName?: string };
  units?: Array<{ id: string; [key: string]: unknown }>;
  devCategories?: Array<{ id: string; [key: string]: unknown }>;
  devices?: Array<{ id?: string; name?: string; signals?: unknown[]; [key: string]: unknown }>;
  variables?: { imported?: unknown[]; user?: unknown[] };
  ioMapping?: GrafcetStudioProject.IOMapping;
  excelVars?: unknown[];
  unitConfig?: Record<string, GrafcetStudioProject.UnitConfig>;
  diagrams?: ImportRawDiagramEntry[];
}

// ───────────────────────────────────────────────────────────
//  IMPORT / EXPORT
// ───────────────────────────────────────────────────────────
function triggerImport(): void {
  (document.getElementById('file-input') as HTMLInputElement).click();
}

function handleImport(e: Event): void {
  const input = e.target as HTMLInputElement;
  const file = input.files && input.files[0];
  if (!file) return;
  const reader = new FileReader();
  reader.onload = ev => {
    try {
      const raw = JSON.parse(String((ev.target as FileReader).result)) as ImportRawDiagramV1 & ImportRawProjectPayload;

      // v1: single diagram
      if (raw.state && raw.state.steps !== undefined) {
        const id = 'diag-' + Date.now();
        const name = 'Imported: ' + file.name.replace(/\.(grafcet|json)$/, '');
        project.diagrams.push({
          id, name, unitId: undefined,
          mode: 'Auto', diagramType: 'Macro',
          machine: project.machineName || '', unit: '', description: '',
          addressMode: 'bool', boolAddressMode: 'linear',
          baseMr: typeof findNextAvailableBaseMr === 'function' ? findNextAvailableBaseMr(undefined, id) : 'MR100'
        });
        if (!raw.state.parallels) raw.state.parallels = [];
        if (!raw.state.vars) raw.state.vars = [];
        if (raw.state.connections) raw.state.connections = raw.state.connections.map(c => ({
          ...c, fromPort: (c as unknown as { fromPort?: string }).fromPort || 'bottom', toPort: (c as unknown as { toPort?: string }).toPort || 'top'
        } as ExportConnection));
        saveDiagramData(id, raw.state, raw.nextId || 1, Math.max(1, raw.nextStepNum || 1), 60, 40, 1);
        saveProject(); renderTree(); openTab(id);
        toast('Imported v1 diagram');
        return;
      }

      // v2/v3: full project
      if (raw.project && raw.diagrams) {
        const ver = raw.version || '2.0';
        const mode = confirm(
          `Import project "${raw.project.name}" (v${ver})?\n\n` +
          `REPLACE - clear current project and load this one\n` +
          ` MERGE - to current project\n\n` +
          `OK = Replace  |  Cancel = Merge`
        );

        if (mode) {
          // REPLACE: clear everything
          project.diagrams.forEach(d => deleteDiagramData(d.id));
          project = {
            id: raw.project.id || ('proj-' + Date.now()),
            name: raw.project.name || 'Imported',
            machineName: raw.project.machineName || raw.project.name || 'Machine',
            units: (raw.units || []).map(u => ({ ...u })) as GrafcetStudioProject.Unit[],
            devices: (raw.devices || []).map(d => ({
              ...d,
              signals: (d.signals || []).map(s => ({ ...(s as object) }))
            })) as GrafcetStudioProject.DeviceType[],
            variables: raw.variables ? JSON.parse(JSON.stringify(raw.variables)) : { imported: [], user: [] },
            ioMapping: JSON.parse(JSON.stringify(raw.ioMapping || { physicalIOs: [], entries: [] })),
            excelVars: (raw.excelVars || []).map(v => ({ ...(v as object) })) as GrafcetStudioProject.ProjectVariable[],
            unitConfig: JSON.parse(JSON.stringify(raw.unitConfig || {})),
            diagrams: []
          };
          if (raw.devCategories) (project as unknown as { devCategories?: unknown[] }).devCategories = raw.devCategories.map(c => ({ ...c }));
          openTabs = []; activeDiagramId = null;
        } else {
          // MERGE: add units from import (avoid duplicate IDs)
          if (!project.units) project.units = [];
          const projectWithCategories = project as unknown as { devCategories?: Array<{ id: string; [key: string]: unknown }> };
          if (!projectWithCategories.devCategories) projectWithCategories.devCategories = [];
          if (!project.devices) project.devices = [];
          (raw.units || []).forEach(u => {
            if (!project.units.find(x => x.id === u.id)) {
              project.units.push({ ...u } as GrafcetStudioProject.Unit);
            }
          });
          (raw.devCategories || []).forEach(cat => {
            if (!projectWithCategories.devCategories!.find(x => x.id === cat.id)) {
              projectWithCategories.devCategories!.push({ ...cat });
            }
          });
          (raw.devices || []).forEach(dev => {
            const exists = project.devices.find(x => x.id === dev.id || x.name === dev.name);
            if (!exists) {
              project.devices.push({
                ...dev,
                signals: (dev.signals || []).map(s => ({ ...(s as object) }))
              } as GrafcetStudioProject.DeviceType);
            }
          });
        }

        if (!mode && raw.variables) {
          if (typeof ensureProjectVariables === 'function') ensureProjectVariables();
          (raw.variables.imported || []).forEach(v => typeof upsertProjectVariable === 'function' ? upsertProjectVariable('imported', v as GrafcetStudioProject.ProjectVariable) : project.variables.imported.push(v as GrafcetStudioProject.ProjectVariable));
          (raw.variables.user || []).forEach(v => typeof upsertProjectVariable === 'function' ? upsertProjectVariable('user', v as GrafcetStudioProject.ProjectVariable) : project.variables.user.push(v as GrafcetStudioProject.ProjectVariable));
        }
        if (!mode && raw.ioMapping) {
          if (typeof ensureProjectIOMapping === 'function') ensureProjectIOMapping();
          project.ioMapping = JSON.parse(JSON.stringify(raw.ioMapping || { physicalIOs: [], entries: [] }));
        }

        // Add diagrams
        raw.diagrams.forEach(d => {
          const newId = mode ? d.id! : ('diag-' + Date.now() + '-' + Math.random().toString(36).slice(2, 6));

          // Normalize imported diagram data before saving it into localStorage.
          const data = d.data || {};
          if (!data.state) data.state = { steps: [], transitions: [], parallels: [], connections: [], vars: [] };
          if (data.state.connections) data.state.connections = data.state.connections.map(c => ({
            ...c, fromPort: (c as unknown as { fromPort?: string }).fromPort || 'bottom', toPort: (c as unknown as { toPort?: string }).toPort || 'top'
          } as ExportConnection));

          // Restore full descriptor
          project.diagrams.push({
            id: newId,
            name: d.name || 'Diagram',
            unitId: d.unitId || undefined,
            mode: d.mode || 'Auto',
            diagramType: d.diagramType || 'Macro',
            machine: d.machine || raw.project?.machineName || '',
            unit: d.unit || '',
            description: d.description || '',
            addressMode: d.addressMode,
            boolAddressMode: d.boolAddressMode,
            baseMr: d.baseMr,
            activeWord: d.activeWord,
            completeWord: d.completeWord,
            activeWordTag: d.activeWordTag,
            completeWordTag: d.completeWordTag
          });
          if (typeof ensureFlowAddressConfig === 'function') {
            ensureFlowAddressConfig(project.diagrams[project.diagrams.length - 1], true);
          }

          saveDiagramData(
            newId,
            data.state,
            data.nextId || 1,
            Math.max(1, data.nextStepNum || 1),
            data.viewX ?? 60, data.viewY ?? 40, data.viewScale ?? 1
          );
        });

        saveProject(); renderTree(); renderTabs();
        const firstDiag = project.diagrams[0];
        if (firstDiag) openTab(firstDiag.id);
        toast(` ${mode ? 'Replaced' : 'Merged'}: ${raw.diagrams.length} diagrams, ${(raw.units || []).length} units`);
        return;
      }

      toast('  Unknown file format');
    } catch (err) { toast(' Import error: ' + (err as Error).message); console.error(err); }
  };
  reader.readAsText(file); input.value = '';
}

function exportProject(): void {
  flushState(); // ensure active diagram is saved first
  const diagrams = project.diagrams.map(d => ({
    // Full diagram descriptor
    id: d.id,
    name: d.name,
    unitId: d.unitId || undefined,
    mode: d.mode || 'Auto',
    diagramType: d.diagramType || 'Macro',
    machine: d.machine || project.machineName || '',
    unit: d.unit || '',
    description: d.description || '',
    addressMode: d.addressMode || 'bool',
    boolAddressMode: d.boolAddressMode || 'linear',
    baseMr: d.baseMr || 'MR100',
    activeWord: d.activeWord || '',
    completeWord: d.completeWord || '',
    activeWordTag: d.activeWordTag || '',
    completeWordTag: d.completeWordTag || '',
    // Full diagram data (steps, transitions, vars, etc.)
    data: loadDiagramData(d.id) || {}
  }));
  const projectWithCategories = project as unknown as { devCategories?: Array<{ id: string; [key: string]: unknown }> };
  const exp = {
    project: {
      id: project.id,
      name: project.name,
      machineName: project.machineName || project.name,
    },
    units: (project.units || []).map(u => ({ ...u })),  // full units array
    devCategories: (projectWithCategories.devCategories || []).map(c => ({ ...c })),
    devices: (project.devices || []).map(d => ({
      ...d,
      signals: (d.signals || []).map(s => ({ ...s }))
    })),
    variables: JSON.parse(JSON.stringify(project.variables || { imported: [], user: [] })),
    ioMapping: JSON.parse(JSON.stringify(project.ioMapping || { physicalIOs: [], entries: [] })),
    unitConfig: JSON.parse(JSON.stringify(project.unitConfig || {})),
    diagrams,
    version: '3.0',
    exported: new Date().toISOString()
  };
  const blob = new Blob([JSON.stringify(exp, null, 2)], { type: 'application/json' });
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = project.name.replace(/\s+/g, '_') + '.grafcet2';
  a.click();
  toast('Project exported (v3.0)');
}

function exportHTML(): void {
  if (!activeDiagramId) { toast('No active diagram'); return; }
  flushState();
  const data = loadDiagramData(activeDiagramId);
  if (!data) { return; }
  const s2 = data.state as ExportDiagramState & { parallels?: ExportParallelBar[] };
  const all = [
    ...s2.steps.map(s => {
      const acts = getStepActionsStatic(s);
      const hasActs = acts.length > 0;
      const aH = hasActs ? Math.max(SH, acts.length * 15 + 12) : SH;
      return { x: s.x!, y: s.y!, w: SW + (hasActs ? ACT_W : 0), h: aH };
    }),
    ...s2.transitions.map(t => ({ x: t.x!, y: t.y!, w: TW, h: TH })),
    ...(s2.parallels || []).map(p => ({ x: p.x, y: p.y, w: p.width, h: PH * 2 + 4 }))
  ];
  let vb = '0 0 800 600';
  if (all.length) {
    const minX = Math.min(...all.map(a => a.x)) - 40, minY = Math.min(...all.map(a => a.y)) - 40,
      maxX = Math.max(...all.map(a => a.x + a.w)) + 200, maxY = Math.max(...all.map(a => a.y + a.h)) + 60;
    vb = `${minX} ${minY} ${maxX - minX} ${maxY - minY}`;
  }
  const svgContent = buildExportSVGContent(s2);
  const diagName = project.diagrams.find(d => d.id === activeDiagramId)?.name || 'Diagram';
  const html = `<!DOCTYPE html>
<html lang="vi">
<head><meta charset="UTF-8"><title>GRAFCET — ${diagName}</title>
<style>
body{background:#0b0d11;margin:0;display:flex;flex-direction:column;align-items:center;justify-content:flex-start;min-height:100vh;font-family:monospace;color:#c8d0e0;padding:30px;}
h1{font-size:13px;letter-spacing:4px;color:#f5a623;margin-bottom:6px;}
.sub{font-size:10px;color:#3a4a6a;margin-bottom:24px;letter-spacing:2px;}
svg{border:1px solid #222d44;background:#0b0d11;max-width:95vw;}
</style></head>
<body>
<h1>GRAFCET ${diagName.toUpperCase()}</h1>
<div class="sub">IEC 60848 · ${project.name} · Exported ${new Date().toLocaleString('vi-VN')} · ${s2.steps.length} steps · ${s2.transitions.length} transitions</div>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="${vb}" style="height:80vh;">${svgContent}</svg>
<div style="margin-top:16px;font-size:9px;color:#222d44;letter-spacing:2px;">GENERATED BY GRAFCET STUDIO v2</div>
</body></html>`;
  const safeProj = project.name.replace(/[^a-zA-Z0-9_\-À-ɏḀ-ỿ ]/g, '').trim().replace(/\s+/g, '_');
  const safeDiag = diagName.replace(/[^a-zA-Z0-9_\-À-ɏḀ-ỿ ]/g, '').trim().replace(/\s+/g, '_');
  const blob = new Blob([html], { type: 'text/html' });
  const a = document.createElement('a'); a.href = URL.createObjectURL(blob);
  a.download = (safeProj ? safeProj + '_' : '') + safeDiag + '.html'; a.click();
  toast('HTML exported');
}

function buildExportSVGContent(s2: ExportDiagramState & { parallels?: ExportParallelBar[] }): string {
  const out: string[] = [];
  out.push('<defs><marker id="arr" markerWidth="7" markerHeight="5" refX="6" refY="2.5" orient="auto"><polygon points="0 0,7 2.5,0 5" fill="#3a4a6a"/></marker></defs>');
  // Connections
  (s2.connections || []).forEach(c => {
    const fp = getPortXYStatic(c.from!, (c as unknown as { fromPort?: string }).fromPort || 'bottom', s2);
    const tp = getPortXYStatic(c.to!, (c as unknown as { toPort?: string }).toPort || 'top', s2);
    if (!fp || !tp) return;
    const dx = fp.x - tp.x;
    const d = Math.abs(dx) < 2 ? `M${fp.x},${fp.y} L${tp.x},${tp.y}` : `M${fp.x},${fp.y} L${fp.x},${(fp.y + tp.y) / 2} L${tp.x},${(fp.y + tp.y) / 2} L${tp.x},${tp.y}`;
    out.push(`<path d="${d}" stroke="#3a4a6a" stroke-width="1.5" fill="none" marker-end="url(#arr)"/>`);
  });
  // Steps
  (s2.steps || []).forEach(s => {
    const sc = s.initial ? '#f5a623' : '#4fa3e3';
    out.push(`<rect x="${s.x}" y="${s.y}" width="${SW}" height="${SH}" rx="2" fill="#111520" stroke="${sc}" stroke-width="1.5"/>`);
    if (s.initial) out.push(`<rect x="${s.x! + 3}" y="${s.y! + 3}" width="${SW - 6}" height="${SH - 6}" rx="1" fill="none" stroke="#f5a623" stroke-width="1"/>`);
    out.push(`<line x1="${s.x! + 34}" y1="${s.y! + 4}" x2="${s.x! + 34}" y2="${s.y! + SH - 4}" stroke="#2a3a55" stroke-width="1"/>`);
    out.push(`<text x="${s.x! + 17}" y="${s.y! + SH / 2 + 4}" text-anchor="middle" fill="#4fa3e3" font-size="12" font-family="monospace" font-weight="bold">${String(s.number).padStart(2, '0')}</text>`);
    if (s.label) out.push(`<text x="${s.x! + 40}" y="${s.y! + SH / 2 + 4}" fill="#c8d0e0" font-size="10" font-family="monospace">${esc(s.label)}</text>`);
    const al = getStepActionsStatic(s);
    if (al.length) {
      const lineH = 15, pad = 6;
      const aH = Math.max(SH, al.length * lineH + pad * 2);
      out.push(`<rect x="${s.x! + SW}" y="${s.y}" width="${ACT_W}" height="${aH}" fill="#0c1420" stroke="#4fa3e3" stroke-width="1"/>`);
      out.push(`<line x1="${s.x! + SW + 18}" y1="${s.y! + 2}" x2="${s.x! + SW + 18}" y2="${s.y! + aH - 2}" stroke="#1e3a5a" stroke-width="1"/>`);
      al.forEach((act, i) => {
        const qc = ACT_QUAL_COLORS[act.qualifier || 'N'] || '#f5a623';
        const y0 = s.y! + pad + lineH * i + lineH - 4;
        out.push(`<rect x="${s.x! + SW + 2}" y="${s.y! + pad + lineH * i + 1}" width="14" height="${lineH - 3}" rx="2" fill="${qc}" opacity=".18"/>`);
        out.push(`<text x="${s.x! + SW + 9}" y="${y0 - 1}" text-anchor="middle" fill="${qc}" font-size="9" font-family="monospace" font-weight="bold">${esc(act.qualifier || 'N')}</text>`);
        const vdisp = act.variable || (act.address ? '@' + act.address : '');
        out.push(`<text x="${s.x! + SW + 22}" y="${y0 - 1}" fill="#6a9fc0" font-size="10" font-family="monospace">${esc(vdisp.length > 14 ? vdisp.slice(0, 13) + '…' : vdisp)}</text>`);
        if ((act.qualifier === 'L' || act.qualifier === 'D') && act.time) out.push(`<text x="${s.x! + SW + ACT_W - 3}" y="${y0 - 1}" text-anchor="end" fill="#22d3ee" font-size="8" font-family="monospace">${esc(act.time)}</text>`);
        if (i < al.length - 1) out.push(`<line x1="${s.x! + SW + 1}" y1="${s.y! + pad + lineH * (i + 1)}" x2="${s.x! + SW + ACT_W - 1}" y2="${s.y! + pad + lineH * (i + 1)}" stroke="#1e3050" stroke-width="0.5"/>`);
      });
    }
  });
  // Transitions
  (s2.transitions || []).forEach(t => {
    const cx = t.x! + TW / 2;
    out.push(`<line x1="${cx}" y1="${t.y! - 10}" x2="${cx}" y2="${t.y! + TH + 10}" stroke="#5a6580" stroke-width="1.5"/>`);
    out.push(`<rect x="${t.x}" y="${t.y}" width="${TW}" height="${TH}" rx="1" fill="#171d2c" stroke="#39d353" stroke-width="1.5"/>`);
    if (t.condition) out.push(`<text x="${t.x! + TW + 8}" y="${t.y! + 7}" fill="#39d353" font-size="10" font-family="monospace">${esc(t.condition)}</text>`);
  });
  // Parallel bars
  (s2.parallels || []).forEach(p => {
    const barH = PH * 2 + 4;
    out.push(`<line x1="${p.x}" y1="${p.y}" x2="${p.x + p.width}" y2="${p.y}" stroke="#a78bfa" stroke-width="2"/>`);
    out.push(`<line x1="${p.x}" y1="${p.y + barH}" x2="${p.x + p.width}" y2="${p.y + barH}" stroke="#a78bfa" stroke-width="2"/>`);
    const cx = p.x + p.width / 2;
    const isSplit = p.type === 'split';
    if (isSplit) out.push(`<line x1="${cx}" y1="${p.y - 12}" x2="${cx}" y2="${p.y}" stroke="#5a6580" stroke-width="1.5"/>`);
    else out.push(`<line x1="${cx}" y1="${p.y + barH}" x2="${cx}" y2="${p.y + barH + 12}" stroke="#5a6580" stroke-width="1.5"/>`);
    const ports = p.ports || 3, spacing = p.width / ports;
    for (let i = 0; i < ports; i++) {
      const bx = p.x + spacing * (i + .5);
      if (isSplit) out.push(`<line x1="${bx}" y1="${p.y + barH}" x2="${bx}" y2="${p.y + barH + 12}" stroke="#5a6580" stroke-width="1.5"/>`);
      else out.push(`<line x1="${bx}" y1="${p.y - 12}" x2="${bx}" y2="${p.y}" stroke="#5a6580" stroke-width="1.5"/>`);
    }
    out.push(`<text x="${p.x}" y="${p.y - 6}" fill="#a78bfa" font-size="9" font-family="monospace">${isSplit ? 'AND-SPLIT' : 'AND-JOIN'}</text>`);
  });
  return out.join('\n');
}

function getStepActionsStatic(s: ExportStep | null | undefined): ExportStepAction[] {
  if (!s) return [];
  if (Array.isArray(s.actions)) return s.actions;
  const rawActions = s.actions as unknown;
  if (typeof rawActions === 'string' && rawActions.trim())
    return rawActions.split('\n').filter(l => l.trim()).map(line => {
      const parts = line.trim().split(/\s+/);
      const q = ACT_QUALIFIERS.includes(parts[0]) ? parts[0] : 'N';
      const v = ACT_QUALIFIERS.includes(parts[0]) ? parts.slice(1).join(' ') : line.trim();
      return { qualifier: q as GrafcetStudioProject.ActionQualifier, variable: v, address: '', timeMs: 0 };
    });
  return [];
}

function getPortXYStatic(id: string, port: string, s2: ExportDiagramState & { parallels?: ExportParallelBar[] }): { x: number; y: number } | null {
  const s = (s2.steps || []).find(x => x.id === id);
  if (s) { const cx = s.x! + SW / 2; return port === 'top' ? { x: cx, y: s.y! } : port === 'bottom' ? { x: cx, y: s.y! + SH } : { x: cx, y: s.y! + SH / 2 }; }
  const t = (s2.transitions || []).find(x => x.id === id);
  if (t) { const cx = t.x! + TW / 2; return port === 'top' ? { x: cx, y: t.y! - 10 } : port === 'bottom' ? { x: cx, y: t.y! + TH + 10 } : { x: cx, y: t.y! + TH / 2 }; }
  const p = (s2.parallels || []).find(x => x.id === id);
  if (p) {
    const barH = PH * 2 + 4, cx = p.x + p.width / 2;
    if (port === 'top') return { x: cx, y: p.y };
    if (port === 'bottom') return { x: cx, y: p.y + barH };
    if (port?.startsWith('top-')) { const idx = +port.split('-')[1]; const sp2 = p.width / (p.ports || 3); return { x: p.x + sp2 * (idx + .5), y: p.y }; }
    if (port?.startsWith('bottom-')) { const idx = +port.split('-')[1]; const sp2 = p.width / (p.ports || 3); return { x: p.x + sp2 * (idx + .5), y: p.y + barH }; }
    return { x: cx, y: p.y + barH / 2 };
  }
  return null;
}

// ───────────────────────────────────────────────────────────
//  HELPERS
//  show / hide / toast / toastTimer / closeModal → moved to core/utils.ts
// ───────────────────────────────────────────────────────────

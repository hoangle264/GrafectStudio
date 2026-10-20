namespace GrafcetStudioStorePersistence {
  type Project = GrafcetStudioProject.Project;
  type DiagramState = GrafcetStudioProject.DiagramState;
  type StoredDiagramData = GrafcetStudioProject.StoredDiagramData;

  export interface DiagramSaveData {
    state: DiagramState;
    nextId: number;
    nextStepNum: number;
    viewX: number;
    viewY: number;
    viewScale: number;
  }

  /** Returns false when localStorage rejects the write (e.g. quota exceeded). */
  export function saveProject(project: Project): boolean {
    try { localStorage.setItem('gf2-project', JSON.stringify(project)); return true; } catch (e) { return false; }
  }

  export function saveDiagramData(id: string, data: DiagramSaveData): boolean {
    try {
      localStorage.setItem('gf2-diag-' + id, JSON.stringify({
        state: data.state,
        nextId: data.nextId,
        nextStepNum: data.nextStepNum,
        viewX: data.viewX,
        viewY: data.viewY,
        viewScale: data.viewScale
      }));
      return true;
    } catch (e) { return false; }
  }

  export function loadDiagramData(id: string): StoredDiagramData | null {
    try {
      const raw = localStorage.getItem('gf2-diag-' + id);
      if (raw) return JSON.parse(raw) as StoredDiagramData;
    } catch (e) {}
    return null;
  }

  export function deleteDiagramData(id: string): void {
    try { localStorage.removeItem('gf2-diag-' + id); } catch (e) {}
  }

  export interface StorePersistenceApi {
    saveProject(project: Project): boolean;
    saveDiagramData(id: string, data: DiagramSaveData): boolean;
    loadDiagramData(id: string): StoredDiagramData | null;
    deleteDiagramData(id: string): void;
  }

  export const api: StorePersistenceApi = {
    saveProject,
    saveDiagramData,
    loadDiagramData,
    deleteDiagramData
  };
}



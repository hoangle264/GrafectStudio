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

  export function saveProject(project: Project): void {
    try { localStorage.setItem('gf2-project', JSON.stringify(project)); } catch (e) {}
  }

  export function saveDiagramData(id: string, data: DiagramSaveData): void {
    try {
      localStorage.setItem('gf2-diag-' + id, JSON.stringify({
        state: data.state,
        nextId: data.nextId,
        nextStepNum: data.nextStepNum,
        viewX: data.viewX,
        viewY: data.viewY,
        viewScale: data.viewScale
      }));
    } catch (e) {}
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
    saveProject(project: Project): void;
    saveDiagramData(id: string, data: DiagramSaveData): void;
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



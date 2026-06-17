"use strict";
var GrafcetStudioStorePersistence;
(function (GrafcetStudioStorePersistence) {
    function saveProject(project) {
        try {
            localStorage.setItem('gf2-project', JSON.stringify(project));
        }
        catch (e) { }
    }
    GrafcetStudioStorePersistence.saveProject = saveProject;
    function saveDiagramData(id, data) {
        try {
            localStorage.setItem('gf2-diag-' + id, JSON.stringify({
                state: data.state,
                nextId: data.nextId,
                nextStepNum: data.nextStepNum,
                viewX: data.viewX,
                viewY: data.viewY,
                viewScale: data.viewScale
            }));
        }
        catch (e) { }
    }
    GrafcetStudioStorePersistence.saveDiagramData = saveDiagramData;
    function loadDiagramData(id) {
        try {
            const raw = localStorage.getItem('gf2-diag-' + id);
            if (raw)
                return JSON.parse(raw);
        }
        catch (e) { }
        return null;
    }
    GrafcetStudioStorePersistence.loadDiagramData = loadDiagramData;
    function deleteDiagramData(id) {
        try {
            localStorage.removeItem('gf2-diag-' + id);
        }
        catch (e) { }
    }
    GrafcetStudioStorePersistence.deleteDiagramData = deleteDiagramData;
    GrafcetStudioStorePersistence.api = {
        saveProject,
        saveDiagramData,
        loadDiagramData,
        deleteDiagramData
    };
})(GrafcetStudioStorePersistence || (GrafcetStudioStorePersistence = {}));

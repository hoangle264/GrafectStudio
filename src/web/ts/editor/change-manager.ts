declare function render(): void;

const saveAutoDiagram = (): void => {
  if (activeDiagramId) saveDiagramData(activeDiagramId);
};

const afterChange = (): void => {
  render();
  markModified(activeDiagramId!, true);
  saveAutoDiagram();
};

Object.assign(window, {
  afterChange,
  saveAutoDiagram,
});

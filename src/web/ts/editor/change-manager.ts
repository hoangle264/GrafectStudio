declare function historyCommit(): void;
declare function render(): void;

const saveAutoDiagram = (): void => {
  if (activeDiagramId) saveDiagramData(activeDiagramId);
};

const afterChange = (): void => {
  historyCommit();
  render();
  // Changes are auto-saved; the status bar shows save state, so tabs no longer get a "modified" dot.
  saveAutoDiagram();
};

Object.assign(window, {
  afterChange,
  saveAutoDiagram,
});

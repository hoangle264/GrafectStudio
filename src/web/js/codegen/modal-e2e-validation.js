"use strict";

function runCodegenModalE2EValidation() {
  const errors = [];
  const root = document.createElement('div');
  root.innerHTML = '<div id="cg-files"></div>';
  document.body.appendChild(root);

  const files = [
    { path: 'MapIO.st', content: 'map-io-content' },
    { path: 'Units/Unit_Main.st', content: 'unit-content' },
    { path: 'Devices/DeviceManager_Motor.st', content: 'device-content' }
  ];

  window.GrafcetStudio = window.GrafcetStudio || {};
  window.GrafcetStudio.codegenLastFiles = files;

  if (typeof cgRenderGeneratedFiles === 'function') {
    cgRenderGeneratedFiles(files);
  } else {
    errors.push('cgRenderGeneratedFiles is unavailable.');
  }

  const rendered = document.getElementById('cg-files');
  if (!rendered || rendered.textContent.indexOf('Units/Unit_Main.st') < 0) {
    errors.push('export modal should render multi-file paths.');
  }
  if (!rendered || rendered.textContent.indexOf('DeviceManager_Motor.st') < 0) {
    errors.push('export modal should render device manager file.');
  }
  if (!rendered || rendered.textContent.indexOf('Copy') < 0 || rendered.textContent.indexOf('Download .st') < 0) {
    errors.push('export modal should expose copy/download actions for each file.');
  }

  document.body.removeChild(root);
  return { ok: errors.length === 0, errors };
}

window.runCodegenModalE2EValidation = runCodegenModalE2EValidation;

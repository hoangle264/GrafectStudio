// JSON Files panel toggle 

// --- Codegen asset paths ----------------------------------------------------
function cgGetCodegenAssets() {
  const devPath = document.getElementById('cg-device-library-path')?.value || 'config/Devices.json';
  const tplRoot = document.getElementById('cg-template-root-path')?.value || 'templates';
  const outRoot = document.getElementById('cg-output-root-path')?.value || '';
  return {
    deviceLibraryPath: devPath.trim(),
    templateRootPath: tplRoot.trim(),
    outputPath: outRoot.trim()
  };
}

function cgUpdateAssetPathStatus() {
  const assets = cgGetCodegenAssets();
  const status = document.getElementById('asset-paths-status');
  const summary = document.getElementById('asset-paths-summary');
  const ok = !!assets.deviceLibraryPath && !!assets.templateRootPath && !!assets.outputPath;
  const text = ok
    ? 'C# host will resolve: ' + assets.deviceLibraryPath + ' | ' + assets.templateRootPath + ' | ' + assets.outputPath
    : 'Missing device library, template root, or output path';
  if (status) {
    status.textContent = text;
    status.style.color = ok ? 'var(--cyan)' : 'var(--amber)';
  }
  if (summary) {
    summary.textContent = ok ? '? paths ready' : 'missing path';
    summary.style.color = ok ? 'var(--cyan)' : 'var(--amber)';
  }
}

function cgBrowseCodegenPath(target) {
  if (!(window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function')) {
    toast('Browse is available only inside the WPF host');
    return false;
  }

  window.chrome.webview.postMessage({
    type: 'BROWSE_CODEGEN_PATH',
    payload: { target }
  });
  return true;
}

function receiveCodegenPath(payload) {
  const data = typeof payload === 'string' ? { target: 'templateRoot', path: payload } : (payload || {});
  const inputId = data.target === 'deviceLibrary'
    ? 'cg-device-library-path'
    : data.target === 'outputRoot'
      ? 'cg-output-root-path'
      : 'cg-template-root-path';
  const input = document.getElementById(inputId);
  if (!input || !data.path) return;
  input.value = data.path;
  cgUpdateAssetPathStatus();
  cgUpdatePreview();
}

function receiveSavedPaths(payload) {
  cgSavedPaths = payload || {};
  cgApplySavedPaths();
}

function cgApplySavedPaths() {
  const data = cgSavedPaths || {};
  const deviceInput = document.getElementById('cg-device-library-path');
  const templateInput = document.getElementById('cg-template-root-path');
  const outputInput = document.getElementById('cg-output-root-path');
  if (deviceInput && data.deviceLibraryPath) deviceInput.value = data.deviceLibraryPath;
  if (templateInput && (data.templateRootPath || data.templatePath)) templateInput.value = data.templateRootPath || data.templatePath;
  if (outputInput && data.outputPath) outputInput.value = data.outputPath;
  cgUpdateAssetPathStatus();
  cgUpdatePreview();
}

function cgToggleAssetPaths() {
  const body = document.getElementById('asset-paths-body');
  const chevron = document.getElementById('asset-paths-chevron');
  if (!body) return;
  const open = body.style.display !== 'none';
  body.style.display = open ? 'none' : '';
  if (chevron) chevron.textContent = open ? '>' : 'v';
  if (!open) cgUpdateAssetPathStatus();
}

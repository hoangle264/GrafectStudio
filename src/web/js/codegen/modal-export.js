// Status badge cho file load
function cgExportViaHost(code, platform) {
  if (!(window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function')) {
    return false;
  }
  window.chrome.webview.postMessage({
    type: 'EXPORT_CODE',
    payload: { files: code, platform }
  });
  toast('Export dialog opened');
  return true;
}

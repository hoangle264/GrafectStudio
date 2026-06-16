// Status badge cho file load 

//  Download / Copy 
function cgDownloadCode() {
  cgGenerateSelectedUnit();
}
function cgExportViaHost(code, platform) {
  if (!(window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function')) {
    return false;
  }
  window.chrome.webview.postMessage({
    type: 'EXPORT_CODE',
    payload: { code, platform }
  });
  toast('Export dialog opened');
  return true;
}

function cgCopyCode() {
  const pre = document.getElementById('cg-preview');
  if (!pre) return;
  navigator.clipboard.writeText(pre.textContent).then(() => toast('✓ Copied to clipboard'));
}

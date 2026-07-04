function cgDebugLog(label, data) {
  try {
    const line = '[cg] ' + label + ' ' + JSON.stringify(data);
    console.log(line);
  } catch (err) {
    console.log('[cg] ' + label + ' <unserializable>');
  }
}
function cgGetPayloadApi() {
  return window.GrafcetStudio && window.GrafcetStudio.codegenPayload;
}

function cgBuildPayloadContext() {
  return {
    project,
    loadDiagramData,
    getAssets: cgGetCodegenAssets,
    ensureFlowAddressConfig: typeof ensureFlowAddressConfig === 'function' ? ensureFlowAddressConfig : undefined,
    ensureProjectVariables: typeof ensureProjectVariables === 'function' ? ensureProjectVariables : undefined,
    syncVariableSignalAddressesFromDeviceTypes: typeof syncVariableSignalAddressesFromDeviceTypes === 'function' ? syncVariableSignalAddressesFromDeviceTypes : undefined,
    saveProject: typeof saveProject === 'function' ? saveProject : undefined,
    getDefaultUnitId: cgGetDefaultUnitId,
    unitSignals: typeof GVT_UNIT_SIGNALS !== 'undefined' ? GVT_UNIT_SIGNALS : [],
    projectUnitStructSignals: typeof PROJECT_UNIT_STRUCT_SIGNALS !== 'undefined' ? PROJECT_UNIT_STRUCT_SIGNALS : [],
    onCodegenWarnings: function(warnings) { if (typeof toast === 'function' && warnings && warnings.length) toast('Warning: ' + warnings.join('; ')); }
  };
}

function resolveStepAddress(step, flow) {
  return cgGetPayloadApi().resolveStepAddress(step, flow);
}

function cgBuildCSharpPayload(platform, unitId) {
  if (typeof gvtFlushFocusedAddressInput === 'function') gvtFlushFocusedAddressInput();
  if (activeDiagramId && typeof flushState === 'function') flushState();
  return cgGetPayloadApi().buildCSharpPayload(cgBuildPayloadContext(), platform, unitId || cgGetDefaultUnitId() || '');
}

function cgGenerateSelectedUnit() {
  const target = document.getElementById('cg-target')?.value || 'unit-config';
  const platform = cgResolveHostPlatform(target);
  const unitId = cgGetDefaultUnitId();
  if (!unitId) {
    const pre = document.getElementById('cg-files');
    const stat = document.getElementById('cg-stat');
    if (pre) pre.textContent = 'No unit selected.';
    if (stat) stat.textContent = 'No unit selected';
    return false;
  }
  return cgGenerateViaHost(platform, unitId);
}
function cgGenerateAllUnits() {
  const target = document.getElementById('cg-target')?.value || 'unit-config';
  const platform = cgResolveHostPlatform(target);
  return cgGenerateViaHost(platform, '__all__');
}
function cgGenerateViaHost(platform, diagId) {
  if (!(window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function')) {
    const pre = document.getElementById('cg-files');
    const stat = document.getElementById('cg-stat');
    if (pre) pre.textContent = 'C# generator is available only inside the WPF host.';
    if (stat) stat.textContent = 'Host bridge unavailable';
    return false;
  }

  try {
    if (typeof cgShouldPushSiemensTia === 'function' && cgShouldPushSiemensTia()) {
      const codegenPayload = cgBuildCSharpPayload(platform, diagId);
      cgDebugLog('push payload summary', {
        platform,
        diagId,
        flowCount: (codegenPayload.flows || []).length,
        flows: (codegenPayload.flows || []).map(function(flow) {
          const firstStep = (flow.steps || [])[0] || null;
          return {
            id: flow.id,
            name: flow.name,
            stepCount: (flow.steps || []).length,
            addressMode: flow.diagram && flow.diagram.addressMode,
            activeWord: flow.diagram && flow.diagram.activeWord,
            activeWordTag: flow.diagram && flow.diagram.activeWordTag,
            completeWord: flow.diagram && flow.diagram.completeWord,
            completeWordTag: flow.diagram && flow.diagram.completeWordTag,
            firstExec: firstStep && firstStep.execAddress,
            firstDone: firstStep && firstStep.doneAddress
          };
        })
      });
      const tiaConfig = typeof cgGetSiemensTiaConfig === 'function' ? cgGetSiemensTiaConfig() : {};
      if (!tiaConfig.deviceName || !tiaConfig.plcName || !tiaConfig.targetFolderPath) {
        if (typeof cgSetSiemensLadStatus === 'function') cgSetSiemensLadStatus('Push failed: device, PLC, and block folder are required', false);
        if (stat) stat.textContent = 'Siemens TIA target config is incomplete';
        return false;
      }
      if (typeof cgSetSiemensLadStatus === 'function') cgSetSiemensLadStatus('Generating Siemens LAD XML in host...', null);
      window.chrome.webview.postMessage({
        type: 'PUSH_SIEMENS_LAD',
        payload: {
          platform: 'siemens-lad',
          templateRootPath: codegenPayload.templateRootPath || '',
          deviceLibraryPath: codegenPayload.deviceLibraryPath || '',
          outputPath: codegenPayload.outputPath || '',
          codegenPayload,
          ...tiaConfig
        }
      });
      return true;
    }

    window.chrome.webview.postMessage({
      type: 'GENERATE_CODE',
      payload: cgBuildCSharpPayload(platform, diagId)
    });
    return true;
  } catch (err) {
    const pre = document.getElementById('cg-files');
    const stat = document.getElementById('cg-stat');
    const message = err && err.message ? err.message : String(err);
    if (pre) pre.textContent = 'Payload validation error: ' + message;
    if (stat) stat.textContent = 'Payload validation failed';
    if (typeof toast === 'function') toast('? ' + message);
    return false;
  }
}

function receiveGeneratedCode(output) {
  const pre = document.getElementById('cg-files');
  const stat = document.getElementById('cg-stat');
  const files = output && Array.isArray(output.files) ? output.files : null;

  if (!files) {
    if (pre) pre.textContent = output || '';
  } else if (typeof cgRenderGeneratedFiles === 'function') {
    cgRenderGeneratedFiles(files);
  } else if (pre) {
    pre.textContent = files.map(file => {
      const path = file && file.path ? file.path : 'Untitled.st';
      const content = file && file.content ? file.content : '';
      return '// FILE: ' + path + '\n' + content;
    }).join('\n\n');
  }

  if (stat) stat.textContent = files ? 'Generated ' + files.length + ' file(s) by C# host' : 'Generated by C# host';
}

function receiveError(payload) {
  const pre = document.getElementById('cg-files');
  const stat = document.getElementById('cg-stat');
  const source = payload && payload.source ? payload.source : 'host';
  const message = payload && payload.message ? payload.message : String(payload || 'Unknown error');
  if (typeof toast === 'function' && (source === 'template-loader' || source === 'template-validation')) {
    toast('? ' + source + ': ' + message);
  }
  if (pre) pre.textContent = source + ' error: ' + message;
  if (stat) stat.textContent = 'C# host error';
}


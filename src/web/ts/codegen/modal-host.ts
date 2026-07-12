type MhCodegenPayload = GrafcetStudioProject.CodegenPayload;
type MhFlowInfo = GrafcetStudioProject.FlowInfo;

declare function toast(msg: string): void;
declare function flushState(): void;
declare function ensureFlowAddressConfig(diag: GrafcetStudioProject.DiagramMeta | null | undefined, assignUniqueBase: boolean): unknown;
declare function ensureProjectVariables(): GrafcetStudioProject.ProjectVariables;
declare function syncVariableSignalAddressesFromDeviceTypes(): boolean;
declare function saveProject(): void;
declare function gvtFlushFocusedAddressInput(): void;
declare function cgResolveHostPlatform(target: string): string;
declare function cgGetDefaultUnitId(): string;
declare function cgShouldPushSiemensTia(): boolean;
declare function cgGetSiemensTiaConfig(): {
  projectPath: string;
  deviceName: string;
  plcName: string;
  targetFolderPath: string;
  overwriteMode: string;
};
declare function cgSetSiemensLadStatus(message: string, ok: boolean | null): void;
declare function cgRenderGeneratedFiles(files: GrafcetStudioProject.CodegenFile[]): void;

interface MhWebViewMessage {
  type: string;
  payload: unknown;
}

function cgDebugLog(label: string, data: unknown): void {
  try {
    const line = '[cg] ' + label + ' ' + JSON.stringify(data);
    console.log(line);
  } catch (err) {
    console.log('[cg] ' + label + ' <unserializable>');
  }
}
function cgGetPayloadApi(): GrafcetStudioCodegenPayload.CodegenPayloadApi | null {
  return (window.GrafcetStudio && window.GrafcetStudio.codegenPayload as GrafcetStudioCodegenPayload.CodegenPayloadApi) || null;
}

function cgBuildPayloadContext(): GrafcetStudioCodegenPayload.PayloadContext {
  const context = {
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
    onCodegenWarnings: function(warnings: string[]) { if (typeof toast === 'function' && warnings && warnings.length) toast('Warning: ' + warnings.join('; ')); }
  };
  return context;
}

function resolveStepAddress(step: GrafcetStudioProject.Step, flow: GrafcetStudioProject.DiagramMeta): GrafcetStudioCodegenPayload.ResolvedStepAddress {
  return cgGetPayloadApi()!.resolveStepAddress(step, flow);
}

function cgBuildCSharpPayload(platform: string, unitId?: string): MhCodegenPayload {
  if (typeof gvtFlushFocusedAddressInput === 'function') gvtFlushFocusedAddressInput();
  if (activeDiagramId && typeof flushState === 'function') flushState();
  return cgGetPayloadApi()!.buildCSharpPayload(cgBuildPayloadContext(), platform, unitId || cgGetDefaultUnitId() || '');
}

function cgGenerateSelectedUnit(): boolean {
  const target = (document.getElementById('cg-target') as HTMLSelectElement | null)?.value || 'Keyence';
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
function cgGenerateAllUnits(): boolean {
  const target = (document.getElementById('cg-target') as HTMLSelectElement | null)?.value || 'Keyence';
  const platform = cgResolveHostPlatform(target);
  return cgGenerateViaHost(platform, '__all__');
}
function cgGenerateViaHost(platform: string, diagId: string): boolean {
  const chromeHost = (window as Window & { chrome?: { webview?: { postMessage?: (message: unknown) => void } } }).chrome;
  const stat = document.getElementById('cg-stat');
  if (!(chromeHost && chromeHost.webview && typeof chromeHost.webview.postMessage === 'function')) {
    const pre = document.getElementById('cg-files');
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
        flows: (codegenPayload.flows || []).map(function(flow: MhFlowInfo) {
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
      const tiaConfig = typeof cgGetSiemensTiaConfig === 'function' ? cgGetSiemensTiaConfig() : { projectPath: '', deviceName: '', plcName: '', targetFolderPath: '', overwriteMode: '' };
      if (!tiaConfig.deviceName || !tiaConfig.plcName || !tiaConfig.targetFolderPath) {
        if (typeof cgSetSiemensLadStatus === 'function') cgSetSiemensLadStatus('Push failed: device, PLC, and block folder are required', false);
        if (stat) stat.textContent = 'Siemens TIA target config is incomplete';
        return false;
      }
      if (typeof cgSetSiemensLadStatus === 'function') cgSetSiemensLadStatus('Generating Siemens LAD XML in host...', null);
      chromeHost.webview.postMessage({
        type: 'PUSH_SIEMENS_LAD',
        payload: {
          platform: platform === 'siemens-db' ? 'siemens-db' : 'siemens-lad',
          templateRootPath: codegenPayload.templateRootPath || '',
          deviceLibraryPath: codegenPayload.deviceLibraryPath || '',
          outputPath: codegenPayload.outputPath || '',
          codegenPayload,
          ...tiaConfig
        }
      });
      return true;
    }

    chromeHost.webview.postMessage({
      type: 'GENERATE_CODE',
      payload: cgBuildCSharpPayload(platform, diagId)
    });
    return true;
  } catch (err) {
    const pre = document.getElementById('cg-files');
    const message = err instanceof Error ? err.message : String(err);
    if (pre) pre.textContent = 'Payload validation error: ' + message;
    if (stat) stat.textContent = 'Payload validation failed';
    if (typeof toast === 'function') toast('? ' + message);
    return false;
  }
}

function receiveGeneratedCode(output: { files?: GrafcetStudioProject.CodegenFile[] } | string): void {
  const pre = document.getElementById('cg-files');
  const stat = document.getElementById('cg-stat');
  const files = output && typeof output === 'object' && Array.isArray(output.files) ? output.files : null;

  if (!files) {
    if (pre) pre.textContent = (typeof output === 'string' ? output : '') || '';
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

function receiveError(payload: { source?: string; message?: string } | string): void {
  const pre = document.getElementById('cg-files');
  const stat = document.getElementById('cg-stat');
  const data = typeof payload === 'object' && payload ? payload : {};
  const source = data.source ? data.source : 'host';
  const message = data.message ? data.message : String(payload || 'Unknown error');
  if (typeof toast === 'function' && (source === 'template-loader' || source === 'template-validation')) {
    toast('? ' + source + ': ' + message);
  }
  if (pre) pre.textContent = source + ' error: ' + message;
  if (stat) stat.textContent = 'C# host error';
}

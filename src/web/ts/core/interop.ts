namespace GrafcetStudioInterop {
  export type BridgeName = 'ai' | 'codegenPayload' | 'excelImport' | 'store' | 'vars' | 'ioMapping' | 'tree' | 'tables';

  export interface BridgeRegistry {
    ai?: unknown;
    codegenPayload?: unknown;
    excelImport?: unknown;
    store?: unknown;
    vars?: unknown;
    ioMapping?: unknown;
    tree?: unknown;
    tables?: unknown;
  }

  export function getRegistry(): BridgeRegistry {
    const host = window as Window & { GrafcetStudio?: BridgeRegistry };
    if (!host.GrafcetStudio) host.GrafcetStudio = {};
    return host.GrafcetStudio;
  }

  export function registerBridge<T>(name: BridgeName, api: T): T {
    const registry = getRegistry() as Record<BridgeName, unknown>;
    registry[name] = api;
    return api;
  }
}

interface Window {
  GrafcetStudio?: GrafcetStudioInterop.BridgeRegistry;
}

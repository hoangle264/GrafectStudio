"use strict";
var GrafcetStudioInterop;
(function (GrafcetStudioInterop) {
    function getRegistry() {
        const host = window;
        if (!host.GrafcetStudio)
            host.GrafcetStudio = {};
        return host.GrafcetStudio;
    }
    GrafcetStudioInterop.getRegistry = getRegistry;
    function registerBridge(name, api) {
        const registry = getRegistry();
        registry[name] = api;
        return api;
    }
    GrafcetStudioInterop.registerBridge = registerBridge;
})(GrafcetStudioInterop || (GrafcetStudioInterop = {}));

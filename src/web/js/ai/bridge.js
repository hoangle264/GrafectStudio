"use strict";
var GrafcetStudioAIBridge;
(function (GrafcetStudioAIBridge) {
    const compiledModules = ['src/web/ts/ai/contracts.ts', 'src/web/ts/ai/sanitizer.ts', 'src/web/ts/ai/sanitizer-validation.ts', 'src/web/ts/ai/context-builder.ts', 'src/web/ts/ai/context-builder-validation.ts', 'src/web/ts/ai/parser.ts', 'src/web/ts/ai/parser-validation.ts', 'src/web/ts/ai/mock-service.ts', 'src/web/ts/ai/mock-service-validation.ts', 'src/web/ts/ai/apply.ts', 'src/web/ts/ai/apply-validation.ts', 'src/web/ts/ai/bridge.ts'];
    const plainJavaScriptModules = [
        'src/web/js/editor/*-ui.js',
        'src/web/js/editor/actions.js',
        'src/web/js/editor/ai-chat-ui.js',
        'src/web/js/editor/canvas.js',
        'src/web/js/editor/events.js',
        'src/web/js/editor/panels.js',
        'src/web/js/editor/project.js',
        'src/web/js/codegen/modal*.js',
        'src/web/js/codegen/unit-config.js'
    ];
    GrafcetStudioAIBridge.api = {
        version: '0.9.0-p9',
        contracts: GrafcetStudioAIContracts.api,
        sanitizer: GrafcetStudioAISanitizer.api,
        contextBuilder: GrafcetStudioAIContextBuilder.api,
        proposalParser: GrafcetStudioAIProposalParser.api,
        mockService: GrafcetStudioAIMockService.api,
        applyLayer: GrafcetStudioAIApply.api,
        runSanitizerValidation: GrafcetStudioAISanitizerValidation.runSanitizerValidation,
        runContextBuilderValidation: GrafcetStudioAIContextBuilderValidation.runContextBuilderValidation,
        runProposalParserValidation: GrafcetStudioAIProposalParserValidation.runProposalParserValidation,
        runMockServiceValidation: GrafcetStudioAIMockServiceValidation.runMockServiceValidation,
        runApplyValidation: GrafcetStudioAIApplyValidation.runApplyValidation,
        modules: compiledModules,
        getBuildInfo: function () {
            return {
                bridgeName: 'window.GrafcetStudioAI',
                generatedScript: 'src/web/js/ai/bridge.js',
                sourceModule: 'src/web/ts/ai/bridge.ts',
                runtimeModel: 'global-script WebView runtime; no ES modules or bundler entrypoint',
                compiledModules,
                plainJavaScriptModules
            };
        }
    };
})(GrafcetStudioAIBridge || (GrafcetStudioAIBridge = {}));
GrafcetStudioInterop.registerBridge('ai', GrafcetStudioAIBridge.api);
window.GrafcetStudioAI = GrafcetStudioAIBridge.api;

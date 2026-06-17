"use strict";
var GrafcetStudioAIProposalParserValidation;
(function (GrafcetStudioAIProposalParserValidation) {
    function assert(condition, message, errors) {
        if (!condition)
            errors.push(message);
    }
    function parseObject(value) {
        return JSON.parse(value);
    }
    function runProposalParserValidation() {
        const errors = [];
        const validResult = GrafcetStudioAIProposalParser.parseAiProposalResponse('Assistant text before JSON.\n```json\n' + GrafcetStudioAIMockService.getFixtureRawText('create-variable') + '\n```');
        assert(validResult.ok, 'valid fixture should parse and validate.', errors);
        assert(!!validResult.value && validResult.value.intent === 'create-variable', 'valid fixture should preserve proposal intent.', errors);
        assert(!!validResult.value && validResult.value.status === 'validated', 'valid fixture should normalize status to validated.', errors);
        const malformedJsonResult = GrafcetStudioAIProposalParser.parseAiProposalResponse(GrafcetStudioAIMockService.getFixtureRawText('malformed-json'));
        assert(!malformedJsonResult.ok, 'malformed JSON fixture should fail parser result.', errors);
        assert(malformedJsonResult.errors.length > 0, 'malformed JSON fixture should return errors.', errors);
        const missingFields = parseObject(GrafcetStudioAIMockService.getFixtureRawText('create-variable'));
        delete missingFields.id;
        const missingFieldsResult = GrafcetStudioAIProposalParser.parseAiProposalResponse(JSON.stringify(missingFields));
        assert(!missingFieldsResult.ok, 'missing required fields should fail parser validation.', errors);
        const wrongIntentShape = parseObject(GrafcetStudioAIMockService.getFixtureRawText('create-variable'));
        wrongIntentShape.intent = 'map-io';
        const wrongIntentShapeResult = GrafcetStudioAIProposalParser.parseAiProposalResponse(JSON.stringify(wrongIntentShape));
        assert(!wrongIntentShapeResult.ok, 'wrong data shape for declared intent should fail validation.', errors);
        const unknownIntent = parseObject(GrafcetStudioAIMockService.getFixtureRawText('create-variable'));
        unknownIntent.intent = 'delete-project';
        const unknownIntentResult = GrafcetStudioAIProposalParser.parseAiProposalResponse(JSON.stringify(unknownIntent));
        assert(!unknownIntentResult.ok, 'unknown intent should fail validation.', errors);
        const unsupportedSchema = parseObject(GrafcetStudioAIMockService.getFixtureRawText('create-variable'));
        unsupportedSchema.schemaVersion = '999.0.0';
        const unsupportedSchemaResult = GrafcetStudioAIProposalParser.parseAiProposalResponse(JSON.stringify(unsupportedSchema));
        assert(!unsupportedSchemaResult.ok, 'unsupported schema version should fail validation.', errors);
        return {
            ok: errors.length === 0,
            errors,
            validResult,
            malformedJsonResult,
            missingFieldsResult,
            wrongIntentShapeResult,
            unknownIntentResult,
            unsupportedSchemaResult
        };
    }
    GrafcetStudioAIProposalParserValidation.runProposalParserValidation = runProposalParserValidation;
})(GrafcetStudioAIProposalParserValidation || (GrafcetStudioAIProposalParserValidation = {}));

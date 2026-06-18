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
        const cloneArrayShape = parseObject(GrafcetStudioAIMockService.getFixtureRawText('clone-variable'));
        cloneArrayShape.data = {
            sources: [{ label: 'Imported_A' }, { label: 'Imported_B' }],
            variables: [
                { label: 'Cloned_A', format: 'bool', address: 'M10', kind: 'primitive' },
                { label: 'Cloned_B', format: 'bool', address: 'M11', kind: 'primitive' }
            ]
        };
        const cloneArrayShapeResult = GrafcetStudioAIProposalParser.parseAiProposalResponse(JSON.stringify(cloneArrayShape));
        assert(cloneArrayShapeResult.ok, 'clone-variable array-shaped AI response should normalize to first source and variable.', errors);
        assert(!!cloneArrayShapeResult.value && (cloneArrayShapeResult.value.warnings || []).length > 0, 'clone-variable array normalization should add warnings.', errors);
        const createStructureResult = GrafcetStudioAIProposalParser.parseAiProposalResponse(GrafcetStudioAIMockService.getFixtureRawText('create-structure'));
        assert(createStructureResult.ok, 'create-structure fixture should parse and validate.', errors);
        assert(!!createStructureResult.value && createStructureResult.value.intent === 'create-structure', 'create-structure fixture should preserve proposal intent.', errors);
        return {
            ok: errors.length === 0,
            errors,
            validResult,
            malformedJsonResult,
            missingFieldsResult,
            wrongIntentShapeResult,
            unknownIntentResult,
            unsupportedSchemaResult,
            cloneArrayShapeResult,
            createStructureResult
        };
    }
    GrafcetStudioAIProposalParserValidation.runProposalParserValidation = runProposalParserValidation;
})(GrafcetStudioAIProposalParserValidation || (GrafcetStudioAIProposalParserValidation = {}));

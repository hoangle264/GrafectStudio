"use strict";
var GrafcetStudioAIProposalParser;
(function (GrafcetStudioAIProposalParser) {
    function isOpeningBracket(value) {
        return value === '{' || value === '[';
    }
    function matchingBracket(value) {
        return value === '{' ? '}' : ']';
    }
    function findMatchingJsonEnd(text, startIndex) {
        const stack = [];
        let inString = false;
        let escaping = false;
        for (let index = startIndex; index < text.length; index += 1) {
            const char = text.charAt(index);
            if (inString) {
                if (escaping) {
                    escaping = false;
                }
                else if (char === '\\') {
                    escaping = true;
                }
                else if (char === '"') {
                    inString = false;
                }
                continue;
            }
            if (char === '"') {
                inString = true;
            }
            else if (isOpeningBracket(char)) {
                stack.push(matchingBracket(char));
            }
            else if (char === '}' || char === ']') {
                if (!stack.length || stack[stack.length - 1] !== char)
                    return -1;
                stack.pop();
                if (!stack.length)
                    return index;
            }
        }
        return -1;
    }
    function extractFromFence(text) {
        const fencePattern = /```(?:json|JSON)?\s*([\s\S]*?)```/g;
        let match = fencePattern.exec(text);
        while (match) {
            const candidate = match[1].trim();
            if (candidate)
                return candidate;
            match = fencePattern.exec(text);
        }
        return null;
    }
    function extractStructuredJson(rawText) {
        if (typeof rawText !== 'string' || !rawText.trim()) {
            return { ok: false, rawText: typeof rawText === 'string' ? rawText : '', errors: ['AI response must be a non-empty string.'] };
        }
        const text = rawText.trim();
        const fenced = extractFromFence(text);
        if (fenced)
            return { ok: true, value: fenced, rawText: text, errors: [] };
        for (let index = 0; index < text.length; index += 1) {
            const char = text.charAt(index);
            if (!isOpeningBracket(char))
                continue;
            const end = findMatchingJsonEnd(text, index);
            if (end >= index) {
                return { ok: true, value: text.slice(index, end + 1), rawText: text, errors: [] };
            }
        }
        return { ok: false, rawText: text, errors: ['AI response does not contain a complete JSON object or array.'] };
    }
    GrafcetStudioAIProposalParser.extractStructuredJson = extractStructuredJson;
    function isRecord(value) {
        return !!value && typeof value === 'object' && !Array.isArray(value);
    }
    function firstRecord(value) {
        if (isRecord(value))
            return value;
        if (Array.isArray(value)) {
            for (let index = 0; index < value.length; index += 1) {
                if (isRecord(value[index]))
                    return value[index];
            }
        }
        return null;
    }
    function normalizeCloneVariableShape(normalized) {
        if (normalized.intent !== 'clone-variable' || !isRecord(normalized.data))
            return;
        const data = normalized.data;
        const warnings = normalized.warnings || [];
        const source = firstRecord(data.source) || firstRecord(data.sources);
        if (source && data.source !== source) {
            data.source = source;
            warnings.push('AI returned multiple clone sources; using the first source for this proposal.');
        }
        let variables = null;
        if (Array.isArray(data.variables) && data.variables.length > 0)
            variables = data.variables;
        else if (isRecord(data.variable)) {
            variables = [data.variable];
            warnings.push('AI returned single variable; clone is most useful with 2 or more variables. Consider requesting multiple clones.');
        }
        if (variables) {
            data.variables = variables;
            if (variables.length === 1 && !warnings.some(function (w) { return w.indexOf('clone is most useful') >= 0; })) {
                warnings.push('AI returned single variable; clone is most useful with 2 or more variables. Consider requesting multiple clones.');
            }
        }
        if ('variable' in data)
            delete data.variable;
        if ('sources' in data)
            delete data.sources;
        normalized.warnings = warnings;
    }
    function normalizeAiProposal(proposal) {
        const normalized = JSON.parse(JSON.stringify(proposal));
        normalized.schemaVersion = GrafcetStudioAIContracts.schemaVersion;
        normalized.status = 'validated';
        normalized.warnings = normalized.warnings || [];
        normalized.errors = normalized.errors || [];
        normalizeCloneVariableShape(normalized);
        return normalized;
    }
    GrafcetStudioAIProposalParser.normalizeAiProposal = normalizeAiProposal;
    function parseAiProposalResponse(rawText) {
        const extraction = extractStructuredJson(rawText);
        if (!extraction.ok || !extraction.value) {
            return { ok: false, rawText: extraction.rawText, errors: extraction.errors };
        }
        let parsed;
        try {
            parsed = JSON.parse(extraction.value);
        }
        catch (error) {
            return {
                ok: false,
                rawText: extraction.rawText,
                jsonText: extraction.value,
                errors: ['AI response JSON is malformed: ' + (error instanceof Error ? error.message : String(error))]
            };
        }
        if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
            normalizeCloneVariableShape(parsed);
        }
        const validation = GrafcetStudioAIContracts.validateAiProposal(parsed);
        if (!validation.ok || !validation.value) {
            return {
                ok: false,
                rawText: extraction.rawText,
                jsonText: extraction.value,
                errors: validation.errors
            };
        }
        return {
            ok: true,
            value: normalizeAiProposal(validation.value),
            rawText: extraction.rawText,
            jsonText: extraction.value,
            errors: []
        };
    }
    GrafcetStudioAIProposalParser.parseAiProposalResponse = parseAiProposalResponse;
    GrafcetStudioAIProposalParser.api = {
        extractStructuredJson,
        parseAiProposalResponse,
        normalizeAiProposal
    };
})(GrafcetStudioAIProposalParser || (GrafcetStudioAIProposalParser = {}));

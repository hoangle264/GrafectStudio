namespace GrafcetStudioAIProposalParser {
  export interface AiProposalParseResult extends GrafcetStudioAIContracts.ValidationResult<GrafcetStudioAIContracts.AiProposal> {
    rawText: string;
    jsonText?: string;
  }

  export interface JsonExtractionResult extends GrafcetStudioAIContracts.ValidationResult<string> {
    rawText: string;
  }

  export interface ProposalParserApi {
    extractStructuredJson(rawText: unknown): JsonExtractionResult;
    parseAiProposalResponse(rawText: unknown): AiProposalParseResult;
    normalizeAiProposal(proposal: GrafcetStudioAIContracts.AiProposal): GrafcetStudioAIContracts.AiProposal;
  }

  function isOpeningBracket(value: string): boolean {
    return value === '{' || value === '[';
  }

  function matchingBracket(value: string): string {
    return value === '{' ? '}' : ']';
  }

  function findMatchingJsonEnd(text: string, startIndex: number): number {
    const stack: string[] = [];
    let inString = false;
    let escaping = false;

    for (let index = startIndex; index < text.length; index += 1) {
      const char = text.charAt(index);

      if (inString) {
        if (escaping) {
          escaping = false;
        } else if (char === '\\') {
          escaping = true;
        } else if (char === '"') {
          inString = false;
        }
        continue;
      }

      if (char === '"') {
        inString = true;
      } else if (isOpeningBracket(char)) {
        stack.push(matchingBracket(char));
      } else if (char === '}' || char === ']') {
        if (!stack.length || stack[stack.length - 1] !== char) return -1;
        stack.pop();
        if (!stack.length) return index;
      }
    }

    return -1;
  }

  function extractFromFence(text: string): string | null {
    const fencePattern = /```(?:json|JSON)?\s*([\s\S]*?)```/g;
    let match = fencePattern.exec(text);
    while (match) {
      const candidate = match[1].trim();
      if (candidate) return candidate;
      match = fencePattern.exec(text);
    }
    return null;
  }

  export function extractStructuredJson(rawText: unknown): JsonExtractionResult {
    if (typeof rawText !== 'string' || !rawText.trim()) {
      return { ok: false, rawText: typeof rawText === 'string' ? rawText : '', errors: ['AI response must be a non-empty string.'] };
    }

    const text = rawText.trim();
    const fenced = extractFromFence(text);
    if (fenced) return { ok: true, value: fenced, rawText: text, errors: [] };

    for (let index = 0; index < text.length; index += 1) {
      const char = text.charAt(index);
      if (!isOpeningBracket(char)) continue;
      const end = findMatchingJsonEnd(text, index);
      if (end >= index) {
        return { ok: true, value: text.slice(index, end + 1), rawText: text, errors: [] };
      }
    }

    return { ok: false, rawText: text, errors: ['AI response does not contain a complete JSON object or array.'] };
  }

  export function normalizeAiProposal(proposal: GrafcetStudioAIContracts.AiProposal): GrafcetStudioAIContracts.AiProposal {
    const normalized = JSON.parse(JSON.stringify(proposal)) as GrafcetStudioAIContracts.AiProposal;
    normalized.schemaVersion = GrafcetStudioAIContracts.schemaVersion;
    normalized.status = 'validated';
    normalized.warnings = normalized.warnings || [];
    normalized.errors = normalized.errors || [];
    return normalized;
  }

  export function parseAiProposalResponse(rawText: unknown): AiProposalParseResult {
    const extraction = extractStructuredJson(rawText);
    if (!extraction.ok || !extraction.value) {
      return { ok: false, rawText: extraction.rawText, errors: extraction.errors };
    }

    let parsed: unknown;
    try {
      parsed = JSON.parse(extraction.value);
    } catch (error) {
      return {
        ok: false,
        rawText: extraction.rawText,
        jsonText: extraction.value,
        errors: ['AI response JSON is malformed: ' + (error instanceof Error ? error.message : String(error))]
      };
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

  export const api: ProposalParserApi = {
    extractStructuredJson,
    parseAiProposalResponse,
    normalizeAiProposal
  };
}

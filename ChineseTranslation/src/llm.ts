import { readFileSync, existsSync } from "node:fs";
import { resolve } from "node:path";

interface LLMConfig {
  url: string;
  apiKey: string;
  model: string;
}

let config: LLMConfig | null = null;

// 测试模式标志，用于在测试时返回固定翻译
let mockMode = false;
const mockTranslations: Map<string, string> = new Map();

/**
 * 设置测试模式
 */
export function setMockMode(enabled: boolean): void {
  mockMode = enabled;
  if (enabled) {
    mockTranslations.clear();
  }
}

/**
 * 设置固定翻译映射（用于测试）
 */
export function setMockTranslation(input: string, output: string): void {
  mockTranslations.set(input, output);
}

/**
 * 清除所有固定翻译映射
 */
export function clearMockTranslations(): void {
  mockTranslations.clear();
}

/**
 * 加载 .env 配置
 */
function loadConfig(): LLMConfig {
  if (config) {
    return config;
  }

  const envPath = resolve(process.cwd(), "..", ".env");
  const envContent = existsSync(envPath) ? readFileSync(envPath, "utf-8") : "";

  const envVars: Record<string, string> = {};
  for (const line of envContent.split("\n")) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith("#")) {
      continue;
    }
    const [key, ...valueParts] = trimmed.split("=");
    if (key && valueParts.length > 0) {
      envVars[key] = valueParts.join("=");
    }
  }

  config = {
    url: envVars["OPENAI_URL"] || "https://api.kimi.com/coding/v1",
    apiKey: envVars["OPENAI_API_KEY"] || "",
    model: envVars["OPENAI_MODEL"] || "K2.5",
  };

  return config;
}

/**
 * 调用 LLM API
 */
async function callLLM(messages: Array<{ role: string; content: string }>): Promise<string> {
  const cfg = loadConfig();

  const response = await fetch(`${cfg.url}/chat/completions`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${cfg.apiKey}`,
    },
    body: JSON.stringify({
      model: cfg.model,
      messages,
    }),
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(`LLM API error: ${response.status} ${errorText}`);
  }

  const data = (await response.json()) as {
    choices: Array<{ message: { content: string } }>;
  };

  return data.choices[0]?.message?.content || "";
}

/**
 * 翻译单个文本
 */
export async function translateWithLLM(
  text: string,
  from: string = "English",
  to: string = "Chinese"
): Promise<string> {
  // 测试模式返回固定翻译
  if (mockMode) {
    return `[MOCK] ${text} -> 中文翻译`;
  }

  const messages = [
    {
      role: "system",
      content: `You are a professional translator. Translate the following text from ${from} to ${to}. Only output the translation, nothing else.`,
    },
    {
      role: "user",
      content: text,
    },
  ];

  return callLLM(messages);
}

/**
 * 批量翻译文本
 */
export async function translateBatch(
  texts: string[],
  from: string = "English",
  to: string = "Chinese"
): Promise<string[]> {
  if (texts.length === 0) {
    return [];
  }

  // 测试模式返回固定翻译
  if (mockMode) {
    if (mockTranslations.size > 0) {
      return texts.map((t) => mockTranslations.get(t) || `[MOCK] ${t} -> 中文`);
    }
    return texts.map((t) => `[MOCK] ${t} -> 中文`);
  }

  const cfg = loadConfig();

  // Kimi API 支持批量，但为了简单起见，逐个翻译
  // 如果文本较多，可以考虑合并为一个 prompt
  const combinedText = texts.map((t, i) => `${i + 1}. ${t}`).join("\n");

  const messages = [
    {
      role: "system",
      content: `You are a professional translator. Translate the following texts from ${from} to ${to}. Output ONLY the translations, one per line, in the same order. No numbering, no explanations.`,
    },
    {
      role: "user",
      content: combinedText,
    },
  ];

  const result = await callLLM(messages);

  // 解析翻译结果
  return parseTranslationResult(result, texts);
}

/**
 * 解析 LLM 返回的翻译结果
 */
function parseTranslationResult(result: string, originalTexts: string[]): string[] {
  // 清理结果：移除 thinking 标签内容
  let cleaned = result.replace(/<think>[\s\S]*?<\/think>/gi, "");

  // 移除编号前缀（如 "1. " 或 "1、"）
  cleaned = cleaned.replace(/^\d+[\.\、]\s*/gm, "");

  // 分割行
  const lines = cleaned.split("\n").map((line) => line.trim()).filter((line) => line.length > 0);

  // 如果行数少于期望，尝试按顺序匹配原文来分配
  if (lines.length < originalTexts.length) {
    const translations: string[] = [];
    for (let i = 0; i < originalTexts.length; i++) {
      const original = originalTexts[i];
      // 在剩余行中找到包含原始文本的行（说明该行包含翻译）
      const matchIndex = lines.findIndex(
        (line) => line.includes(original) || (i < lines.length && lines[i].length > 0)
      );
      if (matchIndex >= 0) {
        translations.push(lines[matchIndex]);
        lines.splice(matchIndex, 1);
      } else {
        // 没有找到翻译，使用原文
        translations.push(original);
      }
    }
    return translations;
  }

  return lines.slice(0, originalTexts.length);
}

import * as path from "node:path";
import {
  getOriginPath,
  getTargetPath,
  getFileType,
  loadTranslationManifest,
} from "./config";
import {
  readJsonFile,
  writeJsonFile,
  readTmxFile,
  writeTmxFile,
  extractFromTmx,
  replaceTmxPropertyValue,
} from "./fileHandler";
import {
  computeJsonDiff,
  getTranslatedKeysFromManifest,
  getNewTranslationKeys,
} from "./diff";
import { translateBatch } from "./llm";
import { FileType } from "./types";
import type { ModConfig, FileEntry, TranslationResult } from "./types";

/**
 * 翻译单个文件
 */
export async function translateFile(
  baseDir: string,
  entry: FileEntry
): Promise<TranslationResult> {
  const fileType = getFileType(entry.file);
  const originPath = getOriginPath(baseDir, entry.file);
  const targetPath = getTargetPath(baseDir, entry.target);

  const result: TranslationResult = {
    success: true,
    file: entry.file,
    translatedCount: 0,
    skippedCount: 0,
    errors: [],
  };

  try {
    switch (fileType) {
      case FileType.I18nDefault:
        await translateI18nFile(originPath, targetPath, result);
        break;
      case FileType.Json:
        await translateJsonFile(
          baseDir,
          originPath,
          targetPath,
          entry,
          result
        );
        break;
      case FileType.Tmx:
        translateTmxFile(
          baseDir,
          originPath,
          targetPath,
          entry,
          result
        );
        break;
      default:
        result.errors?.push(`Unsupported file type: ${fileType}`);
        result.success = false;
    }
  } catch (e) {
    result.success = false;
    result.errors?.push(`Error: ${e}`);
  }

  return result;
}

/**
 * 翻译 i18n/default.json 文件
 * 基于旧版本增量更新
 * 注意：i18n/default.json 是扁平 key-value 结构，直接用 key 访问
 */
export async function translateI18nFile(
  originPath: string,
  targetPath: string,
  result: TranslationResult
): Promise<void> {
  const originData = readJsonFile<Record<string, unknown>>(originPath);
  const targetData = readJsonFile<Record<string, unknown> | null>(targetPath);

  if (!originData) {
    result.errors?.push(`Origin file not found: ${originPath}`);
    result.success = false;
    return;
  }

  // 计算差异
  const diffKeys = computeJsonDiff(originData, targetData);

  // 如果有差异，复制旧文件并更新差异部分
  let outputData = targetData ? { ...targetData } : {};

  // 收集需要翻译的 key-value 对
  const keysToTranslate: string[] = [];
  const valuesToTranslate: string[] = [];
  const keyValueMap = new Map<string, string>();

  for (const key of diffKeys) {
    // i18n/default.json 是扁平结构，直接用 key 访问
    const value = originData[key];
    if (typeof value === "string" && value.trim()) {
      keysToTranslate.push(key);
      valuesToTranslate.push(value);
      keyValueMap.set(key, value);
    } else if (value !== undefined) {
      // 非字符串值直接复制
      outputData[key] = value;
    }
  }

  // 调用 LLM 批量翻译
  if (valuesToTranslate.length > 0) {
    console.log(`Translating ${valuesToTranslate.length} keys via LLM...`);
    const translations = await translateBatch(valuesToTranslate, "English", "Chinese");
    for (let i = 0; i < keysToTranslate.length; i++) {
      const key = keysToTranslate[i];
      const translated = translations[i] || keyValueMap.get(key);
      if (translated) {
        outputData[key] = translated;
        result.translatedCount++;
      }
    }
  }

  writeJsonFile(targetPath, outputData);
  result.skippedCount = Object.keys(outputData).length - result.translatedCount;
}

/**
 * 翻译普通 JSON 文件
 * 基于原始文件重新生成，通过 translation.json 记录已翻译的 key
 */
async function translateJsonFile(
  baseDir: string,
  originPath: string,
  targetPath: string,
  entry: FileEntry,
  result: TranslationResult
): Promise<void> {
  const originData = readJsonFile<Record<string, unknown>>(originPath);
  const manifest = loadTranslationManifest(baseDir);

  if (!originData) {
    result.errors?.push(`Origin file not found: ${originPath}`);
    result.success = false;
    return;
  }

  // 获取已翻译的 keys
  const translatedKeys = getTranslatedKeysFromManifest(manifest, entry.file);

  // 读取 zh/ 目录下已有的翻译文件（如果存在）
  const existingTarget = readJsonFile<Record<string, unknown> | null>(targetPath);

  // 基于原始文件生成新输出
  const outputData = deepClone(originData);

  if (entry.translateKeys && entry.translateKeys.length > 0) {
    // 收集需要翻译的文本
    const pathsToTranslate: string[] = [];
    const valuesToTranslate: string[] = [];
    const pathValueMap = new Map<string, string>();

    // 按指定 keys 翻译
    for (const keyPattern of entry.translateKeys) {
      const values = extractValuesByPath(originData, keyPattern);
      for (const [fullPath, value] of Object.entries(values)) {
        if (typeof value === "string" && value.trim()) {
          // 先尝试从已翻译文件中获取
          if (existingTarget) {
            const translatedValue = getValueByPath(existingTarget, fullPath);
            if (translatedValue !== undefined) {
              setValueByPath(outputData, fullPath, translatedValue);
              result.translatedCount++;
              continue;
            }
          }
          // 需要 LLM 翻译
          pathsToTranslate.push(fullPath);
          valuesToTranslate.push(value);
          pathValueMap.set(fullPath, value);
        }
      }
    }

    // 批量调用 LLM 翻译
    if (valuesToTranslate.length > 0) {
      const translations = await translateBatch(valuesToTranslate, "English", "Chinese");
      for (let i = 0; i < pathsToTranslate.length; i++) {
        const fullPath = pathsToTranslate[i];
        const translated = translations[i] || pathValueMap.get(fullPath);
        if (translated) {
          setValueByPath(outputData, fullPath, translated);
          result.translatedCount++;
        }
      }
    }
  }

  writeJsonFile(targetPath, outputData);
}

/**
 * 翻译 TMX 文件
 * 使用正则直接替换 XML 中的 property 值
 */
function translateTmxFile(
  baseDir: string,
  originPath: string,
  targetPath: string,
  entry: FileEntry,
  result: TranslationResult
): void {
  const originContent = readTmxFile(originPath);
  const existingTargetContent = readTmxFile(targetPath);

  if (!originContent) {
    result.errors?.push(`Origin file not found: ${originPath}`);
    result.success = false;
    return;
  }

  let outputContent = originContent;

  if (entry.translateKeys && entry.translateKeys.length > 0 && existingTargetContent) {
    // 从目标文件提取已翻译的内容
    for (const keyPattern of entry.translateKeys) {
      const translatedItems = extractFromTmx(existingTargetContent, keyPattern);

      for (const item of translatedItems) {
        // 在原始文件中替换对应 name 的 property 的 value
        outputContent = replaceTmxPropertyValue(outputContent, item.name, item.value);
        result.translatedCount++;
      }
    }
  } else {
    result.skippedCount++;
  }

  // 输出翻译后的 TMX
  writeTmxFile(targetPath, outputContent);
}

/**
 * 根据 path pattern 提取值
 * 支持 (*), (*) 等通配符
 */
function extractValuesByPath(
  obj: unknown,
  pattern: string
): Record<string, unknown> {
  const results: Record<string, unknown> = {};
  const normalizedPattern = pattern.replace(/\(\*\)/g, "*");

  extractRecursive(obj, normalizedPattern, results, "");
  return results;
}

function extractRecursive(
  obj: unknown,
  pattern: string,
  results: Record<string, unknown>,
  currentPath: string
): void {
  if (obj === null || obj === undefined) return;

  if (Array.isArray(obj)) {
    for (let i = 0; i < obj.length; i++) {
      const itemPath = currentPath ? `${currentPath}[${i}]` : `[${i}]`;
      extractRecursive(obj[i], pattern, results, itemPath);
    }
    return;
  }

  if (typeof obj === "object") {
    for (const [key, value] of Object.entries(obj)) {
      const keyPath = currentPath ? `${currentPath}.${key}` : key;

      // 检查是否匹配模式
      if (pattern.includes("*")) {
        const regex = new RegExp(
          "^" + pattern.replace(/\*/g, "[^.]+") + "$"
        );
        if (regex.test(keyPath)) {
          if (typeof value === "string") {
            results[keyPath] = value;
          } else if (typeof value === "object" && value !== null) {
            results[keyPath] = value;
          }
        }
      } else if (keyPath === pattern) {
        if (typeof value === "string") {
          results[keyPath] = value;
        }
      }

      if (typeof value === "object" && value !== null) {
        extractRecursive(value, pattern, results, keyPath);
      }
    }
  }
}

/**
 * 按路径获取值
 */
function getValueByPath(obj: unknown, path: string): unknown {
  const parts = path.split(".").filter(Boolean);
  let current = obj;

  for (const part of parts) {
    if (current === null || current === undefined) {
      return undefined;
    }

    // 处理数组索引 [0] 或 [name] 等
    const arrayMatch = part.match(/^(.+?)\[(\d+|\w+)\]$/);
    if (arrayMatch) {
      const [, key, index] = arrayMatch;
      if (key) {
        current = (current as Record<string, unknown>)[key];
      }
      if (current && typeof current === "object" && !Array.isArray(current)) {
        current = (current as Record<string, unknown>)[index];
      } else if (current && Array.isArray(current)) {
        current = current[parseInt(index)];
      } else {
        return undefined;
      }
    } else if (typeof current === "object") {
      current = (current as Record<string, unknown>)[part];
    } else {
      return undefined;
    }
  }

  return current;
}

/**
 * 按路径设置值
 */
function setValueByPath(obj: unknown, path: string, value: unknown): void {
  const parts = path.split(".").filter(Boolean);
  const target = obj as Record<string, unknown>;
  let current = target;

  for (let i = 0; i < parts.length - 1; i++) {
    const part = parts[i];
    if (current[part] === undefined) {
      current[part] = {};
    }
    current = current[part] as Record<string, unknown>;
  }

  const lastPart = parts[parts.length - 1];
  current[lastPart] = value;
}

/**
 * 深拷贝
 */
function deepClone<T>(obj: T): T {
  return JSON.parse(JSON.stringify(obj));
}

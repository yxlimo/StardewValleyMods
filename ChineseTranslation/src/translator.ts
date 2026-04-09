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
import type { ModConfig, FileEntry, FileType, TranslationResult } from "./types";

/**
 * 翻译单个文件
 */
export function translateFile(
  baseDir: string,
  entry: FileEntry
): TranslationResult {
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
        translateI18nFile(originPath, targetPath, result);
        break;
      case FileType.Json:
        translateJsonFile(
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
 */
function translateI18nFile(
  originPath: string,
  targetPath: string,
  result: TranslationResult
): void {
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

  for (const key of diffKeys) {
    const value = getValueByPath(originData, key);
    if (value !== undefined) {
      setValueByPath(outputData, key, value);
      result.translatedCount++;
    }
  }

  // 如果有新增 key 或修改的 key，需要翻译
  // 目前是直接复制原始值（假设原始文件已经是目标语言）
  // TODO: 调用 LLM 翻译

  writeJsonFile(targetPath, outputData);
  result.skippedCount = Object.keys(outputData).length - result.translatedCount;
}

/**
 * 翻译普通 JSON 文件
 * 基于原始文件重新生成，通过 translation.json 记录已翻译的 key
 */
function translateJsonFile(
  baseDir: string,
  originPath: string,
  targetPath: string,
  entry: FileEntry,
  result: TranslationResult
): void {
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
    // 按指定 keys 翻译
    for (const keyPattern of entry.translateKeys) {
      const values = extractValuesByPath(originData, keyPattern);
      for (const [fullPath, value] of Object.entries(values)) {
        if (typeof value === "string" && value.trim()) {
          // TODO: 调用 LLM 翻译
          // 目前直接从 zh/ 中获取已翻译的值
          if (existingTarget) {
            const translatedValue = getValueByPath(existingTarget, fullPath);
            if (translatedValue !== undefined) {
              setValueByPath(outputData, fullPath, translatedValue);
              result.translatedCount++;
            } else {
              result.skippedCount++;
            }
          } else {
            result.skippedCount++;
          }
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

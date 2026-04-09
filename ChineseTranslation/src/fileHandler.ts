import { readFileSync, writeFileSync, existsSync, mkdirSync } from "node:fs";
import { resolve } from "node:path";
import type { FileType } from "./types";

/**
 * 读取 JSON 文件
 */
export function readJsonFile<T>(filePath: string): T | null {
  if (!existsSync(filePath)) {
    return null;
  }
  const content = readFileSync(filePath, "utf-8");
  return JSON.parse(content) as T;
}

/**
 * 写入 JSON 文件
 */
export function writeJsonFile<T>(filePath: string, data: T): void {
  const dir = resolve(filePath, "..");
  mkdirSync(dir, { recursive: true });
  writeFileSync(filePath, JSON.stringify(data, null, 2), "utf-8");
}

/**
 * 读取 TMX 文件（XML 原始内容）
 */
export function readTmxFile(filePath: string): string | null {
  if (!existsSync(filePath)) {
    return null;
  }
  return readFileSync(filePath, "utf-8");
}

/**
 * 写入 TMX 文件
 */
export function writeTmxFile(filePath: string, content: string): void {
  const dir = resolve(filePath, "..");
  mkdirSync(dir, { recursive: true });
  writeFileSync(filePath, content, "utf-8");
}

/**
 * 根据文件类型读取文件
 */
export function readFileByType<T>(
  filePath: string,
  fileType: FileType
): T | null {
  switch (fileType) {
    case FileType.I18nDefault:
    case FileType.Json:
      return readJsonFile<T>(filePath);
    case FileType.Tmx:
      return readTmxFile(filePath) as T;
    default:
      return null;
  }
}

/**
 * 根据文件类型写入文件
 */
export function writeFileByType(
  filePath: string,
  data: unknown,
  fileType: FileType
): void {
  switch (fileType) {
    case FileType.I18nDefault:
    case FileType.Json:
      writeJsonFile(filePath, data);
      break;
    case FileType.Tmx:
      writeTmxFile(filePath, data as string);
      break;
    default:
      break;
  }
}

// ==================== TMX 内容处理 ====================

/**
 * 从 TMX 内容中提取需要翻译的内容
 * 返回所有包含指定模式的 property
 */
export function extractFromTmx(
  tmxContent: string,
  pathPattern: string,
  originContent?: string
): Array<{ name: string; value: string; oldValue: string }> {
  const results: Array<{ name: string; value: string; oldValue: string }> = [];

  // 规范化模式，将 (*) 转换为 *
  const normalizedPattern = pathPattern.replace(/\(\*\)/g, "*");

  // 匹配 <property name="..." value="..."/> 格式，同时捕获前一个property的结束位置
  const propertyRegex = /<property\s+name="([^"]*)"\s+value="([^"]*)"[^/]*\/>/gi;
  let match;
  let lastMatchEnd = 0;

  while ((match = propertyRegex.exec(tmxContent)) !== null) {
    const name = match[1];
    const value = decodeXmlEntities(match[2]);

    // 检查 name 是否匹配模式
    if (matchPattern(name, normalizedPattern)) {
      // 如果提供了 originContent，查找对应的原始值
      // 通过在 originContent 中找到相同位置的 property
      let oldValue = value;
      if (originContent) {
        // 获取当前匹配在 tmxContent 中的位置
        const matchStart = match.index;
        const beforeMatch = tmxContent.substring(0, matchStart);
        const beforeInOrigin = originContent.substring(0, Math.min(matchStart, originContent.length));

        // 在 originContent 中找到对应的 property
        // 使用 value 作为辅助判断（确保是同一个位置）
        const originRegex = /<property\s+name="([^"]*)"\s+value="([^"]*)"[^/]*\/>/gi;
        let originMatch;
        let originLastEnd = 0;

        while ((originMatch = originRegex.exec(originContent)) !== null) {
          if (originMatch.index >= beforeInOrigin.length) {
            // 这是 origin 中第一个在对应位置之后的 match
            const originName = originMatch[1];
            if (originName === name) {
              oldValue = decodeXmlEntities(originMatch[2]);
            }
            break;
          }
          originLastEnd = originRegex.lastIndex;
        }
      }
      results.push({ name, value, oldValue });
    }
    lastMatchEnd = match.index + match[0].length;
  }

  return results;
}

/**
 * 转义正则特殊字符
 */
function escapeRegex(str: string): string {
  return str.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/**
 * 简单的模式匹配
 * 支持 * 通配符
 */
function matchPattern(name: string, pattern: string): boolean {
  if (pattern === "*" || pattern === "**") {
    return true;
  }

  const regex = new RegExp(
    "^" + pattern.replace(/\*/g, ".*").replace(/\?/g, ".") + "$",
    "i"
  );
  return regex.test(name);
}

/**
 * 解码 XML 实体
 */
function decodeXmlEntities(str: string): string {
  return str
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&amp;/g, "&")
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'");
}

/**
 * 替换 TMX 中的指定 property 值
 * @param tmxContent 目标 TMX 内容
 * @param propertyName 属性名
 * @param newValue 新的属性值
 * @param oldValue 原始属性值（用于精确匹配同名的不同 property）
 */
export function replaceTmxPropertyValue(
  tmxContent: string,
  propertyName: string,
  newValue: string,
  oldValue?: string
): string {
  const escapedName = propertyName.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const escapedNewValue = encodeXmlEntities(newValue);
  const escapedOldValue = oldValue ? encodeXmlEntities(oldValue) : null;

  // 如果提供了 oldValue，精确匹配替换
  if (escapedOldValue) {
    // 匹配特定的 property：name 相同，且 value 是 oldValue
    const regex = new RegExp(
      `(<property\\s+name="${escapedName}"\\s+value="${escapedOldValue}"[^/]*\\/>)`,
      "gi"
    );
    return tmxContent.replace(
      regex,
      `<property name="${escapedName}" value="${escapedNewValue}"/>`
    );
  }

  // 否则替换第一个匹配的
  const regex = new RegExp(
    `(<property\\s+name="${escapedName}"\\s+value=")[^"]*(")`,
    "i"
  );
  return tmxContent.replace(regex, `$1${escapedNewValue}$2`);
}

/**
 * 编码 XML 属性值中的特殊字符
 */
function encodeXmlEntities(str: string): string {
  return str
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&apos;");
}

import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import type { ModConfig, TranslationManifest } from "./types";
import { FileType } from "./types";

const CONFIG_DIR = resolve("config");
const ORIGIN_DIR = resolve("origin");
const ZH_DIR = resolve("zh");

/**
 * 加载配置文件
 */
export function loadConfig(configPath: string): ModConfig {
  const content = readFileSync(configPath, "utf-8");
  return JSON.parse(content) as ModConfig;
}

/**
 * 判断文件类型
 */
export function getFileType(filePath: string): FileType {
  if (filePath.endsWith("i18n/default.json")) {
    return FileType.I18nDefault;
  }
  if (filePath.endsWith(".json")) {
    return FileType.Json;
  }
  if (filePath.endsWith(".tmx")) {
    return FileType.Tmx;
  }
  return FileType.Unknown;
}

/**
 * 获取原始文件完整路径
 */
export function getOriginPath(baseDir: string, file: string): string {
  return resolve(ORIGIN_DIR, baseDir, file);
}

/**
 * 获取翻译后文件完整路径
 */
export function getTargetPath(baseDir: string, target: string): string {
  return resolve(ZH_DIR, baseDir, target);
}

/**
 * 获取 translation.json 路径
 */
export function getTranslationManifestPath(baseDir: string): string {
  return resolve(ZH_DIR, baseDir, "translation.json");
}

/**
 * 加载 translation.json
 */
export function loadTranslationManifest(
  baseDir: string
): TranslationManifest | null {
  const path = getTranslationManifestPath(baseDir);
  try {
    const content = readFileSync(path, "utf-8");
    return JSON.parse(content) as TranslationManifest;
  } catch {
    return null;
  }
}

export { CONFIG_DIR, ORIGIN_DIR, ZH_DIR };

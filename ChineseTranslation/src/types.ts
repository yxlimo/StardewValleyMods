/**
 * 配置文件格式
 */
export interface ModConfig {
  baseDir: string;
  files: FileEntry[];
}

/**
 * 文件条目
 */
export interface FileEntry {
  /** 原始文件路径（相对于 origin/{baseDir}/） */
  file: string;
  /** 翻译后文件路径（相对于 zh/{baseDir}/） */
  target: string;
  /** 是否全量翻译 */
  translateAll?: boolean;
  /** 使用 jsonpath 匹配的 key 列表 */
  translateKeys?: string[];
}

/**
 * 文件类型枚举
 */
export enum FileType {
  /** i18n/default.json - 全量翻译 */
  I18nDefault = "i18n/default.json",
  /** JSON 文件 - 按 key 翻译 */
  Json = "json",
  /** TMX 地图文件 - XML 格式 */
  Tmx = "tmx",
  /** 其他文件 */
  Unknown = "unknown",
}

/**
 * translation.json 格式
 */
export interface TranslationManifest {
  version: string;
  files: TranslationFileRecord[];
}

/**
 * 已翻译文件记录
 */
export interface TranslationFileRecord {
  file: string;
  keys: string[];
}

/**
 * 翻译结果
 */
export interface TranslationResult {
  success: boolean;
  file: string;
  translatedCount: number;
  skippedCount: number;
  errors?: string[];
}

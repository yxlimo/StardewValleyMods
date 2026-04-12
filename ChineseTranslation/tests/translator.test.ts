import { describe, test, expect, beforeAll, beforeEach, afterEach, afterAll } from "bun:test";
import { resolve } from "node:path";
import { readJsonFile, writeJsonFile, readTmxFile, writeTmxFile, extractFromTmx, replaceTmxPropertyValue } from "../src/fileHandler";
import { computeJsonDiff, getTranslatedKeysFromManifest, getNewTranslationKeys } from "../src/diff";
import { getFileType, loadConfig, loadTranslationManifest } from "../src/config";
import { translateFile, translateI18nFile } from "../src/translator";
import type { TranslationManifest, TranslationResult } from "../src/types";
import { FileType } from "../src/types";
import {
  setMockMode,
  setMockTranslation,
  clearMockTranslations,
  translateWithLLM,
  translateBatch,
} from "../src/llm";
import { existsSync, unlinkSync } from "node:fs";

const RESOURCES = resolve("tests/resources");
const ORIGIN_DIR = resolve(RESOURCES, "origin");
const ZH_DIR = resolve(RESOURCES, "zh");
const CONFIG_DIR = resolve(RESOURCES, "config");

describe("fileHandler", () => {
  test("readJsonFile reads valid JSON", () => {
    const testData = { key: "value", nested: { key: 123 } };
    const path = "/tmp/test_translator.json";
    writeJsonFile(path, testData);

    const result = readJsonFile<typeof testData>(path);
    expect(result).toEqual(testData);
  });

  test("readJsonFile returns null for non-existent file", () => {
    const result = readJsonFile("/tmp/nonexistent_file.json");
    expect(result).toBeNull();
  });

  test("writeJsonFile creates directory if not exists", () => {
    const testData = { key: "value" };
    const path = "/tmp/nested/test/dir.json";
    writeJsonFile(path, testData);

    const result = readJsonFile<typeof testData>(path);
    expect(result).toEqual(testData);
  });

  test("readTmxFile reads TMX XML content", () => {
    const tmxContent = `<?xml version="1.0" encoding="UTF-8"?>
<map>
  <objectgroup name="Objects">
    <object id="1" name="TestObject">
      <properties>
        <property name="Dialogue" value="Hello World"/>
      </properties>
    </object>
  </objectgroup>
</map>`;
    const path = "/tmp/test_translator.tmx";
    writeTmxFile(path, tmxContent);

    const result = readTmxFile(path);
    expect(result).not.toBeNull();
    expect(result).toContain("Hello World");
  });

  test("extractFromTmx extracts Dialogue properties", () => {
    const tmxContent = `<?xml version="1.0" encoding="UTF-8"?>
<map>
  <objectgroup name="Objects">
    <object id="1" name="NPC1">
      <properties>
        <property name="Dialogue" value="Hello player"/>
        <property name="OtherProp" value="Other value"/>
      </properties>
    </object>
  </objectgroup>
</map>`;

    const results = extractFromTmx(tmxContent, "Dialogue");
    expect(results.length).toBe(1);
    expect(results[0].name).toBe("Dialogue");
    expect(results[0].value).toBe("Hello player");
  });

  test("extractFromTmx with wildcard pattern", () => {
    const tmxContent = `<?xml version="1.0" encoding="UTF-8"?>
<map>
  <objectgroup name="Objects">
    <object id="1" name="NPC1">
      <properties>
        <property name="Dialogue" value="Hello"/>
        <property name="Dialogue2" value="World"/>
      </properties>
    </object>
  </objectgroup>
</map>`;

    const results = extractFromTmx(tmxContent, "Dialogue*");
    expect(results.length).toBe(2);
  });

  test("replaceTmxPropertyValue replaces property value", () => {
    const tmxContent = `<?xml version="1.0" encoding="UTF-8"?>
<map>
  <properties>
    <property name="Dialogue" value="Original"/>
  </properties>
</map>`;

    const result = replaceTmxPropertyValue(tmxContent, "Dialogue", "Translated", "Original");
    expect(result).toContain('value="Translated"');
    expect(result).not.toContain('value="Original"');
  });
});

describe("config", () => {
  test("getFileType identifies i18n/default.json", () => {
    expect(getFileType("Cape Stardew/i18n/default.json")).toBe(FileType.I18nDefault);
  });

  test("getFileType identifies JSON files", () => {
    expect(getFileType("content.json")).toBe(FileType.Json);
    expect(getFileType("Data/CapeShops.json")).toBe(FileType.Json);
  });

  test("getFileType identifies TMX files", () => {
    expect(getFileType("assets/CapeHouse2ndroom.tmx")).toBe(FileType.Tmx);
  });

  test("loadConfig loads test config", () => {
    const config = loadConfig(resolve(CONFIG_DIR, "TestMod.json"));
    expect(config.baseDir).toBe("TestMod");
    expect(config.files.length).toBe(3);
  });
});

describe("diff", () => {
  test("computeJsonDiff finds new keys", () => {
    const oldData = { a: 1, b: 2 };
    const newData = { a: 1, b: 2, c: 3 };

    const diff = computeJsonDiff(newData, oldData);
    expect(diff.has("c")).toBe(true);
    expect(diff.has("a")).toBe(false);
    expect(diff.has("b")).toBe(false);
  });

  test("computeJsonDiff handles null oldData", () => {
    const newData = { a: 1, b: 2 };
    const diff = computeJsonDiff(newData, null);

    expect(diff.has("a")).toBe(true);
    expect(diff.has("b")).toBe(true);
  });

  test("computeJsonDiff finds nested keys", () => {
    const oldData = { nested: { a: 1 } };
    const newData = { nested: { a: 1, b: 2 }, other: "value" };

    const diff = computeJsonDiff(newData, oldData);
    expect(diff.has("nested.b")).toBe(true);
    expect(diff.has("other")).toBe(true);
  });

  test("getTranslatedKeysFromManifest returns empty set for null manifest", () => {
    const result = getTranslatedKeysFromManifest(null, "anyfile.json");
    expect(result.size).toBe(0);
  });

  test("getTranslatedKeysFromManifest finds keys for file", () => {
    const manifest: TranslationManifest = {
      version: "1.0",
      files: [
        {
          file: "test.json",
          keys: ["key1", "key2", "key3"],
        },
      ],
    };

    const result = getTranslatedKeysFromManifest(manifest, "test.json");
    expect(result.size).toBe(3);
    expect(result.has("key1")).toBe(true);
    expect(result.has("key2")).toBe(true);
    expect(result.has("key3")).toBe(true);
  });

  test("getTranslatedKeysFromManifest returns empty for unknown file", () => {
    const manifest: TranslationManifest = {
      version: "1.0",
      files: [
        {
          file: "test.json",
          keys: ["key1"],
        },
      ],
    };

    const result = getTranslatedKeysFromManifest(manifest, "unknown.json");
    expect(result.size).toBe(0);
  });

  test("getNewTranslationKeys filters already translated keys", () => {
    const manifest: TranslationManifest = {
      version: "1.0",
      files: [
        {
          file: "test.json",
          keys: ["key1"],
        },
      ],
    };

    const allKeys = ["key1", "key2", "key3"];
    const newKeys = getNewTranslationKeys("test.json", allKeys, manifest);
    expect(newKeys).toEqual(["key2", "key3"]);
  });
});

describe("llm", () => {
  beforeEach(() => {
    setMockMode(true);
    clearMockTranslations();
  });

  afterEach(() => {
    setMockMode(false);
    clearMockTranslations();
  });

  test("translateWithLLM returns mock translation in mock mode", async () => {
    const result = await translateWithLLM("Hello", "English", "Chinese");
    expect(result).toBe("[MOCK] Hello -> 中文翻译");
  });

  test("translateBatch returns mock translations in mock mode", async () => {
    const texts = ["Hello", "World", "Test"];
    const results = await translateBatch(texts, "English", "Chinese");
    expect(results).toEqual([
      "[MOCK] Hello -> 中文",
      "[MOCK] World -> 中文",
      "[MOCK] Test -> 中文",
    ]);
  });

  test("translateBatch with custom mock translations", async () => {
    setMockTranslation("Hello", "你好");
    setMockTranslation("World", "世界");
    setMockTranslation("Test", "测试");

    const texts = ["Hello", "World", "Test"];
    const results = await translateBatch(texts, "English", "Chinese");
    expect(results).toEqual(["你好", "世界", "测试"]);
  });

  test("translateBatch returns empty array for empty input", async () => {
    const results = await translateBatch([], "English", "Chinese");
    expect(results).toEqual([]);
  });

  test("translateBatch uses fallback when no custom mock translation", async () => {
    clearMockTranslations();
    // mockTranslations is now empty, so it uses the default fallback
    const texts = ["Hello", "Unknown"];
    const results = await translateBatch(texts, "English", "Chinese");
    expect(results).toEqual(["[MOCK] Hello -> 中文", "[MOCK] Unknown -> 中文"]);
  });
});

describe("integration", () => {
  test("loadTranslationManifest loads manifest from test resources", () => {
    const manifest = loadTranslationManifest(resolve(ZH_DIR, "TestMod"));
    expect(manifest).not.toBeNull();
    expect(manifest!.version).toBe("1.0.0");
  });

  test("i18n translation adds new keys from origin", () => {
    const originPath = resolve(ORIGIN_DIR, "TestMod/i18n/default.json");
    const zhPath = resolve(ZH_DIR, "TestMod/i18n/zh.json");

    const originData = readJsonFile<Record<string, unknown>>(originPath);
    const zhData = readJsonFile<Record<string, unknown> | null>(zhPath);

    expect(originData).not.toBeNull();
    expect(zhData).not.toBeNull();

    // key4 存在于 origin 但不存在于 zh（新增）
    expect(Object.prototype.hasOwnProperty.call(originData, "key4")).toBe(true);
    expect(Object.prototype.hasOwnProperty.call(zhData, "key4")).toBe(false);
  });

  test("TMX translation extracts and replaces dialogue", () => {
    const originContent = readTmxFile(resolve(ORIGIN_DIR, "TestMod/assets/map.tmx"));
    const zhContent = readTmxFile(resolve(ZH_DIR, "TestMod/assets/map.tmx"));

    expect(originContent).not.toBeNull();
    expect(zhContent).not.toBeNull();

    // 从 zh 提取翻译后的 Dialogue，同时获取 origin 中对应的旧值
    const translatedItems = extractFromTmx(zhContent!, "Dialogue", originContent);
    expect(translatedItems.length).toBe(2);

    // 替换 origin 中的值
    let output = originContent!;
    for (const item of translatedItems) {
      output = replaceTmxPropertyValue(output, item.name, item.value, item.oldValue);
    }

    expect(output).toContain("你好玩家");
    expect(output).toContain("再见玩家");
    expect(output).not.toContain("Hello player");
    expect(output).not.toContain("Goodbye player");
  });
});

describe("translator with LLM", () => {
  const TEST_ORIGIN = "/tmp/translator_llm_origin.json";
  const TEST_ZH = "/tmp/translator_llm_zh.json";

  beforeEach(() => {
    setMockMode(true);
    clearMockTranslations();
    setMockTranslation("New key in updated version", "新项目");
    setMockTranslation("Test string", "测试字符串");
    setMockTranslation("Hello", "你好");
    // 删除可能存在的目标文件，确保测试干净
    if (existsSync(TEST_ZH)) {
      unlinkSync(TEST_ZH);
    }
  });

  afterEach(() => {
    setMockMode(false);
    clearMockTranslations();
  });

  test("translateI18nFile translates new keys with mock LLM", async () => {
    const originData = {
      "key1": "Value 1",
      "key2": "Value 2",
      "key3": "Value 3",
      "key4": "New key in updated version",
    };

    // 不创建 zh 文件，模拟全新翻译场景 - 所有 key 都被视为新 key
    writeJsonFile(TEST_ORIGIN, originData);

    const result: TranslationResult = {
      success: true,
      file: "test",
      translatedCount: 0,
      skippedCount: 0,
      errors: [],
    };

    await translateI18nFile(TEST_ORIGIN, TEST_ZH, result);

    expect(result.success).toBe(true);
    // 所有 4 个 key 都是新的（没有现有翻译文件）
    expect(result.translatedCount).toBe(4);

    const translatedData = readJsonFile<Record<string, string>>(TEST_ZH);
    expect(translatedData).not.toBeNull();
    // key4 有特定翻译映射
    expect(translatedData!["key4"]).toBe("新项目");
    // 其他 key 使用 fallback 翻译
    expect(translatedData!["key1"]).toBe("[MOCK] Value 1 -> 中文");
  });

  test("translateI18nFile updates existing file with new keys", async () => {
    const originData = {
      "key1": "Value 1",
      "key2": "Value 2",
      "key3": "Value 3",
      "key4": "New key in updated version",
    };
    const existingZhData = {
      "key1": "值 1",
      "key2": "值 2",
    };

    writeJsonFile(TEST_ORIGIN, originData);
    writeJsonFile(TEST_ZH, existingZhData);

    const result: TranslationResult = {
      success: true,
      file: "test",
      translatedCount: 0,
      skippedCount: 0,
      errors: [],
    };

    await translateI18nFile(TEST_ORIGIN, TEST_ZH, result);

    expect(result.success).toBe(true);
    // key3 和 key4 是新 key
    expect(result.translatedCount).toBe(2);

    const translatedData = readJsonFile<Record<string, string>>(TEST_ZH);
    expect(translatedData).not.toBeNull();
    // 保留已有翻译
    expect(translatedData!["key1"]).toBe("值 1");
    expect(translatedData!["key2"]).toBe("值 2");
  });

  test("translateI18nFile handles non-string values", async () => {
    const originData = {
      "greeting": "Hello",
      "count": 42,
      "enabled": true,
    };

    writeJsonFile(TEST_ORIGIN, originData);

    const result: TranslationResult = {
      success: true,
      file: "test",
      translatedCount: 0,
      skippedCount: 0,
      errors: [],
    };

    await translateI18nFile(TEST_ORIGIN, TEST_ZH, result);

    expect(result.success).toBe(true);
    expect(result.translatedCount).toBe(1);

    const translatedData = readJsonFile<Record<string, unknown>>(TEST_ZH);
    expect(translatedData).not.toBeNull();
    expect(translatedData!["greeting"]).toBe("你好");
    expect(translatedData!["count"]).toBe(42);
    expect(translatedData!["enabled"]).toBe(true);
  });
});

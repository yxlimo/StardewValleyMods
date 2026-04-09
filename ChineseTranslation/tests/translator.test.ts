import { describe, test, expect, beforeAll } from "bun:test";
import { resolve } from "node:path";
import { readJsonFile, writeJsonFile, readTmxFile, writeTmxFile, extractFromTmx, replaceTmxPropertyValue } from "../src/fileHandler";
import { computeJsonDiff, getTranslatedKeysFromManifest, getNewTranslationKeys } from "../src/diff";
import { getFileType, loadConfig, loadTranslationManifest } from "../src/config";
import { translateFile } from "../src/translator";
import type { TranslationManifest } from "../src/types";
import { FileType } from "../src/types";

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

import { resolve } from "node:path";
import { loadConfig } from "./config";
import { translateFile } from "./translator";
import type { ModConfig, TranslationResult } from "./types";

const CONFIG_DIR = resolve("config");

/**
 * CLI 入口
 *
 * 用法:
 *   bun run src/index.ts                    # 翻译所有配置
 *   bun run src/index.ts config/CapeStardew.json  # 翻译指定配置
 */
async function main(): Promise<void> {
  const args = process.argv.slice(2);
  const configPath = args[0]
    ? resolve(args[0])
    : resolve(CONFIG_DIR, "CapeStardew.json");

  console.log(`Loading config: ${configPath}`);

  const config = loadConfig(configPath);
  console.log(`Processing mod: ${config.baseDir}`);
  console.log(`Files to translate: ${config.files.length}`);
  console.log("");

  const results: TranslationResult[] = [];

  for (const entry of config.files) {
    const result = translateFile(config.baseDir, entry);
    results.push(result);

    const status = result.success ? "✓" : "✗";
    console.log(
      `${status} ${entry.file}: ${result.translatedCount} translated, ${result.skippedCount} skipped`
    );

    if (result.errors && result.errors.length > 0) {
      for (const error of result.errors) {
        console.log(`  Error: ${error}`);
      }
    }
  }

  console.log("");
  console.log("Summary:");
  const totalTranslated = results.reduce((sum, r) => sum + r.translatedCount, 0);
  const totalSkipped = results.reduce((sum, r) => sum + r.skippedCount, 0);
  const totalErrors = results.reduce(
    (sum, r) => sum + (r.errors?.length || 0),
    0
  );
  console.log(`  Total translated: ${totalTranslated}`);
  console.log(`  Total skipped: ${totalSkipped}`);
  console.log(`  Total errors: ${totalErrors}`);
}

main().catch(console.error);

import { resolve } from "node:path";
import { loadConfig } from "./config";
import { translateFile } from "./translator";
import type { ModConfig, TranslationResult } from "./types";

const CONFIG_DIR = resolve("mods", "config");

/**
 * CLI 入口
 *
 * 用法:
 *   bun run translate DeluxeGrabberFix              # 使用模组名翻译
 *   bun run translate -c mods/config/xxx.json      # 指定配置文件
 */
async function main(): Promise<void> {
  const args = process.argv.slice(2);
  let configPath: string;

  if (args[0] === "-c" && args[1]) {
    configPath = resolve(args[1]);
    args.splice(0, 2);
  } else if (args[0]) {
    configPath = resolve(CONFIG_DIR, `${args[0]}.json`);
  } else {
    console.error("Usage: bun run translate <mod-name> [-c <config-path>]");
    process.exit(1);
  }

  console.log(`Loading config: ${configPath}`);

  const config = loadConfig(configPath);
  console.log(`Processing mod: ${config.baseDir}`);
  console.log(`Files to translate: ${config.files.length}`);
  console.log("");

  const results: TranslationResult[] = [];

  for (const entry of config.files) {
    const result = await translateFile(config.baseDir, entry);
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

using System;
using System.Runtime.InteropServices;
using SmartFilteredHopper.Interfaces;
using StardewModdingAPI;

namespace SmartFilteredHopper {
  internal class ModConfig {

    public int LogLevel { get; set; }
    public bool CompareQuality { get; set; }
    public int TransferInterval { get; set; }
    public bool GrabAutomateChestGroup { get; set; }

    private IGenericModConfigMenuApi configMenu;

    public ModConfig() {
      this.Reset();
    }

    public void Reset() {
      this.LogLevel = 0;
      this.CompareQuality = false;
      this.TransferInterval = 360;
      this.GrabAutomateChestGroup = false;
    }

    public void RegisterConfigMenu(IGenericModConfigMenuApi configMenuApi, IManifest manifest, IModHelper helper, Action onSave) {
      this.configMenu = configMenuApi;

      this.configMenu.Register(
          mod: manifest,
          reset: () => this.Reset(),
          save: () => onSave?.Invoke()
      );

      this.configMenu.AddBoolOption(
          mod: manifest,
          name: () => helper.Translation.Get("config.enable-debug"),
          tooltip: () => helper.Translation.Get("config.enable-debug.tooltip"),
          getValue: () => this.LogLevel <= 1,
          setValue: value => {
            if (value)
              this.LogLevel = 0;
            else
              this.LogLevel = 2;
          }
      );

      this.configMenu.AddBoolOption(
          mod: manifest,
          name: () => helper.Translation.Get("config.compare-quality"),
          tooltip: () => helper.Translation.Get("config.compare-quality.tooltip"),
          getValue: () => this.CompareQuality,
          setValue: value => this.CompareQuality = value
      );

      this.configMenu.AddNumberOption(
          mod: manifest,
          name: () => helper.Translation.Get("config.transfer-interval"),
          tooltip: () => helper.Translation.Get("config.transfer-interval.tooltip"),
          getValue: () => this.TransferInterval,
          setValue: value => this.TransferInterval = value,
          min: 1,
          max: 600
      );

      this.configMenu.AddBoolOption(
          mod: manifest,
          name: () => helper.Translation.Get("config.grab-automate-chest-group"),
          tooltip: () => helper.Translation.Get("config.grab-automate-chest-group.tooltip"),
          getValue: () => this.GrabAutomateChestGroup,
          setValue: value => this.GrabAutomateChestGroup = value
      );
    }
  }
}

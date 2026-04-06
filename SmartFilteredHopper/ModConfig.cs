using System;
using System.Reflection;
using SmartFilteredHopper.Interfaces;
using StardewModdingAPI;
using xTile.Format;

namespace SmartFilteredHopper {
  internal class ModConfig {

    public int LogLevel { get; set; }
    public bool CompareQuality { get; set; }
    public int TransferInterval { get; set; }
    public bool GrabAutomateChestGroup { get; set; }

    private IGenericModConfigMenuApi configMenu;
    private IManifest manifest;

    public ModConfig() {
      this.Reset();
    }

    public void Reset() {
      this.LogLevel = 4;
      this.CompareQuality = false;
      this.TransferInterval = 60;
      this.GrabAutomateChestGroup = false;
    }

    public void RegisterConfigMenu(IGenericModConfigMenuApi configMenuApi, IManifest manifest, Action onSave) {
      this.configMenu = configMenuApi;
      this.manifest = manifest;

      this.configMenu.Register(
          mod: manifest,
          reset: () => this.Reset(),
          save: () => onSave?.Invoke()
      );

      this.configMenu.AddNumberOption(
          mod: manifest,
          name: () => "Log Level",
          tooltip: () => "lower is more verbose,only show error log in default",
          getValue: () => this.LogLevel,
          setValue: value => this.LogLevel = value,
          min: 0,
          max: 5
      );

      this.configMenu.AddBoolOption(
          mod: manifest,
          name: () => "Compare Quality",
          tooltip: () => "If true the filters will check the qualities of items as well as the item id",
          getValue: () => this.CompareQuality,
          setValue: value => this.CompareQuality = value
      );

      this.configMenu.AddNumberOption(
          mod: manifest,
          name: () => "Tranfer Interval",
          tooltip: () => "How often the item transfer logic runs in frames, ie. 1 is every frame, 60 every 60 frames which should be about every second",
          getValue: () => this.TransferInterval,
          setValue: value => this.TransferInterval = value,
          min: 1,
          max: 600
      );

      this.configMenu.AddBoolOption(
          mod: manifest,
          name: () => "Connect Automate Chest Group",
          tooltip: () => "Grab all chests in Automate Group if the chest above in an Automate Group(just work when you have automate mod)",
          getValue: () => this.GrabAutomateChestGroup,
          setValue: value => this.GrabAutomateChestGroup = value
      );
    }
  }
}

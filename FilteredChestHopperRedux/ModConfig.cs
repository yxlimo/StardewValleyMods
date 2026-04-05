using System;
using GenericModConfigMenu;
using StardewModdingAPI;

namespace FilteredChestHopperRedux {
  internal class ModConfig {

    public int LogLevel { get; set; }
    public bool CompareQuality { get; set; }
    public int TransferInterval { get; set; }
    public bool GrabAutomateChestGroup { get; set; }

    public ModConfig() {
      this.Reset();
    }

    public void Reset() {
      this.LogLevel = 4;
      this.CompareQuality = false;
      this.TransferInterval = 60;
      this.GrabAutomateChestGroup = false;
    }

    public void RegisterConfigMenu(IModHelper helper, IManifest manifest) {
      var configMenu = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
      if (configMenu is null) {
        return;
      }

      configMenu.Register(
          mod: manifest,
          reset: () => this.Reset(),
          save: () => { }
      );

      configMenu.AddNumberOption(
          mod: manifest,
          name: () => "Log Level",
          tooltip: () => "lower is more verbose,only show error log in default",
          getValue: () => this.LogLevel,
          setValue: value => this.LogLevel = value,
          min: 0,
          max: 5
      );

      configMenu.AddBoolOption(
          mod: manifest,
          name: () => "Compare Quality",
          tooltip: () => "If true the filters will check the qualities of items as well as the item id",
          getValue: () => this.CompareQuality,
          setValue: value => this.CompareQuality = value
      );

      configMenu.AddNumberOption(
          mod: manifest,
          name: () => "Tranfer Interval",
          tooltip: () => "How often the item transfer logic runs in frames, ie. 1 is every frame, 60 every 60 frames which should be about every second",
          getValue: () => this.TransferInterval,
          setValue: value => this.TransferInterval = value,
          min: 1,
          max: 600
      );

      configMenu.AddBoolOption(
          mod: manifest,
          name: () => "Grab all chests in Automate Group if the chest above in an Automate Group",
          tooltip: () => "TODO",
          getValue: () => this.GrabAutomateChestGroup,
          setValue: value => this.GrabAutomateChestGroup = value
      );

    }
  }
}

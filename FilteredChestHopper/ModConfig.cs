
using GenericModConfigMenu;
using StardewModdingAPI;

namespace FilteredChestHopper {
  internal class ModConfig {
    public bool CompareQuality { get; set; } = false;
    public int TransferInterval { get; set; } = 60;

    public bool AutomateRespect { get; set; } = false;

    public void RegisterOptions(IGenericModConfigMenuApi configMenu, IManifest manifest) {
      // register mod
      configMenu.Register(
          mod: manifest,
          reset: () => this.CompareQuality = false,
          save: () => { }
      );

      // add some config options
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
          name: () => "Automate Respect",
          tooltip: () => "TODO",
          getValue: () => this.AutomateRespect,
          setValue: value => this.AutomateRespect = value
      );
    }
  }

}

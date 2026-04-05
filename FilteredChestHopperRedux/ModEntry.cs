using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;
using StardewValley;
using StardewValley.Objects;

namespace FilteredChestHopperRedux {

  internal class ModEntry : StardewModdingAPI.Mod {
    private Context ctx;
    public int AutomateCountdown;

    //Active LocationManagers by location
    private Dictionary<GameLocation, LocationManager> pipelines;

    //Applying this flag gets automate to ignore the hopper, so I hijack it
    public const string ModDataFlag = "spacechase0.SuperHopper";

    public override void Entry(IModHelper helper) {
      helper.Events.GameLoop.UpdateTicked += this.UpdateTicked;
      helper.Events.GameLoop.SaveLoaded += this.SaveLoaded;
      helper.Events.GameLoop.GameLaunched += this.GameLaunched;
      helper.Events.World.ObjectListChanged += this.ObjectListChanged;

      this.ctx = new Context(helper.ReadConfig<ModConfig>(), this.Monitor);
      this.pipelines = new Dictionary<GameLocation, LocationManager>();
    }

    private void GameLaunched(object sender, GameLaunchedEventArgs e) {
      this.ctx.Info("GameLaunched, registered mod config menu if exist");
      this.ctx.Config.RegisterConfigMenu(this.Helper, this.ModManifest);
    }

    private void SaveLoaded(object sender, SaveLoadedEventArgs e) {
      this.ctx.Info("SaveLoaded, try regenerating pipelines");
      this.RegeneratePipelines();
    }

    public void RegeneratePipelines() {
      this.ctx.Trace("Regenerating all pipelines");
      Utility.ForEachLocation(location => {
        this.BuildLocationManager(location);
        return true;
      });
      this.ctx.Trace($"Pipeline regeneration complete. Total locations: {this.pipelines.Count}");
    }

    private void BuildLocationManager(GameLocation location) {
      LocationManager manager = new LocationManager(this.ctx);

      foreach (var stardewObject in location.objects.Pairs) {
        if (Utill.TryExtractHopper(stardewObject.Value, out var hopper)) {
          manager.Add(hopper);
        }
      }

      this.pipelines[location] = manager;
      this.ctx.Trace($"Built LocationManager for {location.Name} with {manager.IOGroups.Count} IOGroups");
    }

    private void ObjectListChanged(object sender, ObjectListChangedEventArgs e) {
      foreach (var pair in e.Removed) {
        if (Utill.TryExtractHopper(pair.Value, out var hopper)) {
          this.HandleHopperRemoved(hopper, e.Location);
        } else if (pair.Value is Chest) {
          this.HandleChestChanged(e.Location);
        }
      }

      foreach (var pair in e.Added) {
        if (Utill.TryExtractHopper(pair.Value, out var hopper)) {
          this.HandleHopperAdded(hopper, e.Location);
        } else if (pair.Value is Chest) {
          this.HandleChestChanged(e.Location);
        }
      }
    }

    private void HandleHopperRemoved(Chest hopper, GameLocation location) {
      this.ctx.Trace($"HandleHopperRemoved: hopper at {hopper.TileLocation}");

      if (this.pipelines.TryGetValue(location, out var pipeline)) {
        pipeline.RemoveGroupByHopper(hopper);
        if (pipeline.IOGroups.Count == 0) {
          this.pipelines.Remove(location);
        }
      }
    }

    private void HandleHopperAdded(Chest hopper, GameLocation location) {
      this.ctx.Trace($"HandleHopperAdded: hopper at {hopper.TileLocation}");

      if (!this.pipelines.TryGetValue(location, out var manager)) {
        manager = new LocationManager(this.ctx);
        this.pipelines[location] = manager;
      }
      manager.Add(hopper);
    }

    private void HandleChestChanged(GameLocation location) {
      this.ctx.Trace($"HandleChestChanged at {location.Name}");
      this.BuildLocationManager(location);
    }

    private void UpdateTicked(object sender, UpdateTickedEventArgs e) {
      this.AutomateCountdown--;
      if (this.AutomateCountdown > 0)
        return;

      this.AutomateCountdown = this.ctx.Config.TransferInterval;

      foreach (var pipeline in this.pipelines.Values) {
        pipeline.AttemptTransfer();
      }
    }
  }
}

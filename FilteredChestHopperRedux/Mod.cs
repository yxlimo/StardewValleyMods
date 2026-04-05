using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;
using StardewValley;
using StardewValley.Objects;

//To-Do
//Custom Item
//Custom Filter Interface

namespace FilteredChestHopperRedux {

  internal class Mod : StardewModdingAPI.Mod {
    private Context ctx;
    public int AutomateCountdown;

    //Active Pipelines
    private List<Pipeline> pipelines;

    //Applying this flag gets automate to ignore the hopper, so I hijack it
    public const string ModDataFlag = "spacechase0.SuperHopper";

    public override void Entry(IModHelper helper) {
      helper.Events.GameLoop.UpdateTicked += this.UpdateTicked;
      helper.Events.GameLoop.SaveLoaded += this.SaveLoaded;
      helper.Events.GameLoop.GameLaunched += this.GameLaunched;
      helper.Events.World.ObjectListChanged += this.ObjectListChanged;

      this.ctx = new Context(helper.ReadConfig<ModConfig>(), this.Monitor);
      this.pipelines = new List<Pipeline>();
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
        foreach (var stardewObject in location.objects.Pairs) {
          if (TryGetHopper(stardewObject.Value, out var hopper)) {
            this.ctx.Trace($"Found hopper at {hopper.TileLocation}, creating pipeline");
            this.addPipeline(hopper);
          }
        }
        return true;
      });
      if (this.pipelines == null) {
        this.ctx.Trace("pipelines is null, no hopper found when regenerating pipelines");
        this.pipelines = new List<Pipeline>();
      }
      this.ctx.Trace($"Pipeline regeneration complete. Total pipelines: {this.pipelines?.Count ?? 0}");
    }


    private void ObjectListChanged(object sender, ObjectListChangedEventArgs e) {
      if (e.Removed != null) {
        this.ctx.Trace($"ObjectListChanged: {e.Removed.Count()} objects removed at {e.Location.Name}");
        foreach (var ro in e.Removed) {
          string specialChestType = ro.Value is Chest c ? c.SpecialChestType.ToString() : "not a chest";
          this.ctx.Trace($"RO pos={ro.Key}, type={ro.Value?.GetType().Name ?? "null"}, name={ro.Value?.Name ?? "null"}, specialChestType={specialChestType}");
          if (TryGetHopper(ro.Value, out Chest hopper)) {
            this.ctx.Trace($"Removed hopper at {ro.Key}, removing from pipelines");
            this.pipelines.RemoveAll(pipeline => pipeline.Hoppers.Contains(hopper));

            Chest chestLeft = GetChestAt(e.Location, ro.Key - new Vector2(1, 0));
            if (chestLeft != null && TryGetHopper(chestLeft, out var hopperLeft)) {
              this.ctx.Trace($"Left neighbor is hopper at {chestLeft.TileLocation}, adding new pipeline");
              this.addPipeline(hopperLeft);
            }

            Chest chestRight = GetChestAt(e.Location, ro.Key + new Vector2(1, 0));
            if (chestRight != null && TryGetHopper(chestRight, out var hopperRight)) {
              this.ctx.Trace($"Right neighbor is hopper at {chestRight.TileLocation}, adding new pipeline");
              this.addPipeline(hopperRight);
            }
          }
        }
      }

      if (e.Added != null) {
        this.ctx.Trace($"ObjectListChanged: {e.Added.Count()} objects added at {e.Location.Name}");
        foreach (var AddedObject in e.Added) {
          string specialChestType = AddedObject.Value is Chest c ? c.SpecialChestType.ToString() : "not a chest";
          this.ctx.Trace($"AO pos={AddedObject.Key}, type={AddedObject.Value?.GetType().Name ?? "null"}, name={AddedObject.Value?.Name ?? "null"}, specialChestType={specialChestType}");
          if (TryGetHopper(AddedObject.Value, out Chest hopper)) {
            this.ctx.Trace($"find hopper at {AddedObject.Key}, rebuilding adjacent pipelines");
            this.pipelines.RemoveAll(pipeline => pipeline.Hoppers.Count < 1 || AddedObject.Key == pipeline.Hoppers[0].TileLocation - new Vector2(1, 0) || AddedObject.Key == pipeline.Hoppers[pipeline.Hoppers.Count - 1].TileLocation + new Vector2(1, 0));
            this.addPipeline(hopper);
          }
        }
      }
    }

    private void UpdateTicked(object sender, UpdateTickedEventArgs e) {
      this.AutomateCountdown--;
      if (this.AutomateCountdown > 0)
        return;

      this.AutomateCountdown = this.ctx.Config.TransferInterval;

      if (this.pipelines != null) {
        foreach (var p in this.pipelines) {
          p.AttemptTransfer();
        }
      }
    }

    /// <summary>Get the hopper instance if the object is a hopper.</summary>
    /// <param name="obj">The object to check.</param>
    /// <param name="hopper">The hopper instance.</param>
    /// <returns>Returns whether the object is a hopper.</returns>
    public static bool TryGetHopper(StardewValley.Object obj, out Chest hopper) {
      if (obj is Chest { SpecialChestType: Chest.SpecialChestTypes.AutoLoader } chest) {
        hopper = chest;
        return true;
      }

      hopper = null;
      return false;
    }

    public static Chest GetChestAt(GameLocation location, Vector2 position) {
      if (location.objects.TryGetValue(position, out StardewValley.Object obj) && obj != null && obj is Chest chest) {
        return chest;
      }
      return null;
    }

    private void addPipeline(Chest hopper) {
      this.pipelines.Add(new Pipeline(this.ctx, hopper));
    }
  }
}

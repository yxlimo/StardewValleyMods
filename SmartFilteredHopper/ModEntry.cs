using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using SmartFilteredHopper.Interfaces;
using SmartFilteredHopper.LocationManager;

namespace SmartFilteredHopper {

  internal class ModEntry : StardewModdingAPI.Mod {
    private Context ctx;
    public int AutomateCountdown;

    private Dictionary<GameLocation, LocationManager.Manager> managers;
    private Harmony harmony;

    private const string CapacityModDataKey = "SmartFilteredHopper/Capacity";

    public override void Entry(IModHelper helper) {
      helper.Events.GameLoop.UpdateTicked += this.UpdateTicked;
      helper.Events.GameLoop.SaveLoaded += this.SaveLoaded;
      helper.Events.GameLoop.DayStarted += this.DayStarted;
      helper.Events.GameLoop.GameLaunched += this.GameLaunched;
      helper.Events.World.ObjectListChanged += this.ObjectListChanged;
      helper.Events.World.TerrainFeatureListChanged += this.TerrainFeatureListChanged;

      this.ctx = new Context(helper.ReadConfig<ModConfig>(), this.Monitor);
      this.managers = new Dictionary<GameLocation, LocationManager.Manager>();

      this.harmony = new Harmony(this.ModManifest.UniqueID);
      this.harmony.PatchAll();
    }

    private void GameLaunched(object sender, GameLaunchedEventArgs e) {
    
      var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
      if (configMenu != null) {
        this.ctx.Config.RegisterConfigMenu(configMenu, this.ModManifest, this.Helper, this.onConfigSave);
        this.ctx.Info("Mod Config Menu detected");
      }

      var automateApi = this.Helper.ModRegistry.GetApi<IAutomateAPI>("Pathoschild.Automate");
      if (automateApi != null) {
        this.ctx.RegisterAutomateAPI(automateApi);
        this.ctx.Info("Automate detected, Group support enabled");
      }
    }

    private void SaveLoaded(object sender, SaveLoadedEventArgs e) {
      this.ctx.Info("SaveLoaded, try regenerating LocationManagers");
      this.stampHopperCapacity();
      this.rebuildAllLocationManagers();
    }

    private void DayStarted(object sender, DayStartedEventArgs e) {
      this.ctx.Info("DayStarted, rebuilding all LocationManagers");
      this.rebuildAllLocationManagers();
    }

    private void ObjectListChanged(object sender, ObjectListChangedEventArgs e) {
      foreach (var pair in e.Removed) {
        if (Utill.TryExtractHopper(pair.Value, out var hopper)) {
          this.handleHopperRemoved(hopper, e.Location);
        } else if (pair.Value is Chest) {
          this.handleChestChanged(e.Location);
        }
      }

      foreach (var pair in e.Added) {
        if (Utill.TryExtractHopper(pair.Value, out var hopper)) {
          this.handleHopperAdded(hopper, e.Location);
        } else if (pair.Value is Chest) {
          this.handleChestChanged(e.Location);
        }
      }
    }

    private void TerrainFeatureListChanged(object sender, TerrainFeatureListChangedEventArgs e) {
      if (!this.ctx.Config.FlooringAsInput || !this.ctx.AutomateEnabled()) {
        return;
      }
      bool hasFlooringChange = false;
      foreach (var pair in e.Removed) {
        if (pair.Value is StardewValley.TerrainFeatures.Flooring) {
          hasFlooringChange = true;
          break;
        }
      }
      if (!hasFlooringChange) {
        foreach (var pair in e.Added) {
          if (pair.Value is StardewValley.TerrainFeatures.Flooring) {
            hasFlooringChange = true;
            break;
          }
        }
      }
      if (hasFlooringChange) {
        this.buildLocationManager(e.Location);
      }
    }

    private void UpdateTicked(object sender, UpdateTickedEventArgs e) {
      this.AutomateCountdown--;
      if (this.AutomateCountdown > 0)
        return;

      this.AutomateCountdown = this.ctx.Config.TransferInterval;

      foreach (var pipeline in this.managers.Values) {
        pipeline.AttemptTransfer();
      }
    }

    private void handleHopperRemoved(Chest hopper, GameLocation location) {
      this.ctx.Trace($"HandleHopperRemoved: hopper at {hopper.TileLocation}");
      if (this.managers.TryGetValue(location, out var mg)) {
        mg.RemoveGroupByHopper(hopper);
        if (mg.IOGroups.Count == 0) {
          this.managers.Remove(location);
        }
      }
    }

    private void handleHopperAdded(Chest hopper, GameLocation location) {
      this.ctx.Trace($"HandleHopperAdded: hopper at {hopper.TileLocation}");

      if (!this.managers.TryGetValue(location, out var manager)) {
        manager = new LocationManager.Manager(this.ctx, location);
        this.managers[location] = manager;
      }
      manager.Add(hopper);
    }

    private void handleChestChanged(GameLocation location) {
      this.ctx.Trace($"HandleChestChanged at {location.Name}");
      this.buildLocationManager(location);
    }

    private void onConfigSave() {
      this.Helper.WriteConfig(this.ctx.Config);

      this.ctx.Info("Option saved, rebuilding all managers");
      this.stampHopperCapacity();
      foreach (var manager in this.managers.Values) {
        manager.RebuildIOGroups();
      }
    }

    private void buildLocationManager(GameLocation location) {
      var manager = new LocationManager.Manager(this.ctx, location);

      foreach (var stardewObject in location.objects.Pairs) {
        if (Utill.TryExtractHopper(stardewObject.Value, out var hopper)) {
          manager.Add(hopper);
        }
      }

      this.managers[location] = manager;
    }

    private void rebuildAllLocationManagers() {
      this.managers.Clear();
      Utility.ForEachLocation(location => {
        this.buildLocationManager(location);
        return true;
      });
    }

    /// <summary>
    /// Stamp all hoppers with the configured capacity in their modData.
    /// </summary>
    private void stampHopperCapacity() {
      int capacity = this.ctx.Config.HopperCapacity;
      Utility.ForEachLocation(location => {
        foreach (var pair in location.objects.Pairs) {
          if (Utill.TryExtractHopper(pair.Value, out var hopper)) {
            hopper.modData[CapacityModDataKey] = capacity.ToString();
          }
        }
        return true;
      });
    }

    [HarmonyPatch]
    [HarmonyPatch(typeof(Chest), nameof(Chest.GetActualCapacity))]
    private static class ChestGetActualCapacityPatch {
      [HarmonyPostfix]
      private static void GetActualCapacity_Postfix(Chest __instance, ref int __result) {
        if (__instance.modData.TryGetValue(CapacityModDataKey, out string val)
            && int.TryParse(val, out int cap)
            && cap > 0) {
          __result = cap;
        }
      }
    }

  }
}

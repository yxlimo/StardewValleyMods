using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using SmartFilteredHopper.Interfaces;

namespace SmartFilteredHopper {
	internal class Logger {

	}
	internal class Context {
		public ModConfig Config { get; }
		private readonly StardewModdingAPI.IMonitor monitor;
		private IAutomateAPI automateApi;

		public Context(ModConfig config, StardewModdingAPI.IMonitor monitor) {
			this.Config = config;
			this.monitor = monitor;
			this.automateApi = null;
		}

		public Context(ModConfig config, StardewModdingAPI.IMonitor monitor, IAutomateAPI automateApi) {
			this.Config = config;
			this.monitor = monitor;
			this.automateApi = automateApi;
		}

		public void RegisterAutomateAPI(IAutomateAPI automateApi) {
			this.automateApi = automateApi;
		}

		public bool AutomateEnabled() {
			return this.automateApi != null && this.Config.GrabAutomateChestGroup;
		}

		public void Log(string message, StardewModdingAPI.LogLevel level) {
			if (level < (StardewModdingAPI.LogLevel)this.Config.LogLevel)
				return;
			this.monitor.Log(message, level);
		}

		public void Error(string message) {
			this.Log(message, StardewModdingAPI.LogLevel.Error);
		}

		public void Warn(string message) {
			this.Log(message, StardewModdingAPI.LogLevel.Warn);
		}
		public void Info(string message) {
			this.Log(message, StardewModdingAPI.LogLevel.Info);
		}

		public void Debug(string message) {
			this.Log(message, StardewModdingAPI.LogLevel.Debug);
		}

		public void Trace(string message) {
			this.Log(message, StardewModdingAPI.LogLevel.Trace);
		}

		/// <summary>
		/// 获取 Location 中所有 Machine 的状态
		/// </summary>
		public IDictionary<Vector2, int> GetAutomateMachineStates(GameLocation location) {
			if (this.automateApi == null) {
				return new Dictionary<Vector2, int>();
			}

			var map = location.Map;
			if (map == null) {
				return new Dictionary<Vector2, int>();
			}

			var area = new Rectangle(0, 0, map.Layers[0].LayerWidth, map.Layers[0].LayerHeight);
			return this.automateApi.GetMachineStates(location, area);
		}
	}
}

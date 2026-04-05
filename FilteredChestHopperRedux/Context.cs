namespace FilteredChestHopperRedux {
  internal class Context {
    public ModConfig Config { get; }
    private readonly StardewModdingAPI.IMonitor monitor;
    public Context(ModConfig config, StardewModdingAPI.IMonitor monitor) {
      this.Config = config;
      this.monitor = monitor;
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
  }
}

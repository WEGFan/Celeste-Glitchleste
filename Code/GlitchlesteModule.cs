using System;
using Celeste.Mod.Glitchleste.Modules;

namespace Celeste.Mod.Glitchleste;

public class GlitchlesteModule : EverestModule {

    public GlitchlesteModule() {
        Instance = this;
    }

    public static GlitchlesteModule Instance { get; private set; }

    public override Type SettingsType => typeof(GlitchlesteSettings);

    public static GlitchlesteSettings Settings => Instance._Settings as GlitchlesteSettings;

    public static bool Loaded = false;

    public override void Load() {
        if (Loaded || !Settings.Enabled) {
            return;
        }

        SimulateFloatingPointPrecisionLossRender.Load();

        Loaded = true;
    }

    public override void Unload() {
        if (!Loaded) {
            return;
        }

        SimulateFloatingPointPrecisionLossRender.Unload();

        Loaded = false;
    }

}

using Celeste.Mod.Glitchleste.Modules;
using Monocle;

namespace Celeste.Mod.Glitchleste;

public class GlitchlesteSettings : EverestModuleSettings {

    public GlitchlesteSettings() {
        Instance = this;
    }

    public static GlitchlesteSettings Instance { get; private set; }

    [SettingIgnore]
    public int SettingsVersion { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    private int horizontalGlitchLevel = 1;

    [SettingRange(0, 4)]
    public int HorizontalGlitchLevel {
        get => horizontalGlitchLevel;
        set {
            horizontalGlitchLevel = Calc.Clamp(value, 0, 4);
            SimulateFloatingPointPrecisionLossRender.UpdateOffset();
        }
    }

    private int verticalGlitchLevel = 1;

    [SettingRange(0, 4)]
    public int VerticalGlitchLevel {
        get => verticalGlitchLevel;
        set {
            verticalGlitchLevel = Calc.Clamp(value, 0, 4);
            SimulateFloatingPointPrecisionLossRender.UpdateOffset();
        }
    }

#if SHOWCASE
    public bool ShowcaseVideoModifications { get; set; } = false;
#endif

    public void CreateEnabledEntry(TextMenu textMenu, bool inGame) {
        TextMenu.Item item = new TextMenu.OnOff("Enabled", Enabled)
            .Change(value => {
                Enabled = value;
                if (value) {
                    GlitchlesteModule.Instance.Load();
                } else {
                    GlitchlesteModule.Instance.Unload();
                }
            });
        textMenu.Add(item);
    }

#if SHOWCASE
    public void CreateShowcaseVideoModificationsEntry(TextMenu textMenu, bool inGame) {
        TextMenu.Item item = new TextMenu.OnOff("Showcase Video Modifications", ShowcaseVideoModifications)
            .Change(value => {
                ShowcaseVideoModifications = value;
                if (value) {
                    ShowcaseVideo.Load();
                } else {
                    ShowcaseVideo.Unload();
                }
            });
        textMenu.Add(item);
    }
#endif

}

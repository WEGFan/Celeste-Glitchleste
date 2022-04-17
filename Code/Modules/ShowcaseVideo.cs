#if SHOWCASE

using System;
using System.Collections;
using Celeste.Mod.WEGFanCommons.Utils;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Cil;

namespace Celeste.Mod.Glitchleste.Modules;


public static class ShowcaseVideo {

    private static readonly GlitchlesteSettings ModSettings = GlitchlesteSettings.Instance;
    public static bool Loaded = false;

    private static bool hasDelayedForCurrentNode;
    private static float textboxDelayTime = 0.2f;

    public static void Load() {
        if (Loaded || !ModSettings.ShowcaseVideoModifications) {
            return;
        }

        // applies some modifications to 6A for the showcase video
        On.Celeste.Textbox.RunRoutine += TextboxOnRunRoutine;
        IL.Celeste.NPC06_Theo_Plateau.Awake += NPC06_Theo_PlateauOnAwake;
        On.Celeste.NPC06_Granny.OnTalk += NPC06_GrannyOnOnTalk;
    }

    public static void Unload() {
        On.Celeste.Textbox.RunRoutine -= TextboxOnRunRoutine;
        IL.Celeste.NPC06_Theo_Plateau.Awake -= NPC06_Theo_PlateauOnAwake;
        On.Celeste.NPC06_Granny.OnTalk -= NPC06_GrannyOnOnTalk;

        Loaded = false;
    }

    private static IEnumerator TextboxOnRunRoutine(On.Celeste.Textbox.orig_RunRoutine orig, Textbox self) {
        hasDelayedForCurrentNode = false;
        IEnumerator origEnumerator = orig(self);
        while (origEnumerator.MoveNext()) {
            if (SaveData.Instance.CurrentSession?.Area is {ID: 6, Mode: AreaMode.Normal}) {
                // delay for a short time before the textbox closes
                bool waitingForInput = self.GetFieldValue<bool>("waitingForInput");
                int index = self.GetFieldValue<int>("index");
                if ((waitingForInput || index == self.Nodes.Count - 1) && !hasDelayedForCurrentNode) {
                    yield return textboxDelayTime;
                    hasDelayedForCurrentNode = true;
                }
                if (!waitingForInput) {
                    hasDelayedForCurrentNode = false;
                }
            }
            yield return origEnumerator.Current;
        }
    }

    private static void NPC06_Theo_PlateauOnAwake(ILContext il) {
        ILCursor cursor = new ILCursor(il);
        cursor.GotoNext(instr => instr.MatchNewobj<CS06_Campfire>());
        cursor.Remove();
        cursor.EmitDelegate<Func<NPC06_Theo_Plateau, Player, object>>((npc, player) => {
            if (SaveData.Instance.CurrentSession?.Area is {ID: 6, Mode: AreaMode.Normal}) {
                return new CS06_Campfire_Glitchleste(npc, player);
            } else {
                return new CS06_Campfire(npc, player);
            }
        });
    }

    private static void NPC06_GrannyOnOnTalk(On.Celeste.NPC06_Granny.orig_OnTalk orig, NPC06_Granny self, Player player) {
        if (SaveData.Instance.CurrentSession?.Area is not {ID: 6, Mode: AreaMode.Normal}) {
            orig(self, player);
            return;
        }

        self.Scene.Add(new CS06_Granny_Glitchleste(self, player));
        self.Talker.Enabled = false;
        self.SetFieldValue("cutsceneIndex", 3);
    }

}

public class CS06_Campfire_Glitchleste : CutsceneEntity {

    private NPC theo;
    private Player player;
    private Bonfire bonfire;
    private Vector2 cameraStart;
    private Vector2 playerCampfirePosition;
    private Vector2 theoCampfirePosition;
    private Selfie selfie;

    public CS06_Campfire_Glitchleste(NPC theo, Player player) {
        Tag = Tags.HUD;
        this.theo = theo;
        this.player = player;
    }

    public override void OnBegin(Level level) {
        Audio.SetMusic(null, startPlaying: false, allowFadeOut: false);
        level.SnapColorGrade(null);
        level.Bloom.Base = 0f;
        level.Session.SetFlag("duskbg");
        bonfire = Scene.Tracker.GetEntity<Bonfire>();
        level.Camera.Position = new Vector2(level.Bounds.Left, bonfire.Y - 144f);
        level.ZoomSnap(new Vector2(80f, 120f), 2f);
        cameraStart = level.Camera.Position;
        theo.X = level.Camera.X - 48f;
        theoCampfirePosition = new Vector2(bonfire.X - 16f, bonfire.Y);
        player.Light.Alpha = 0f;
        player.X = level.Bounds.Left - 40;
        player.StateMachine.State = 11;
        player.StateMachine.Locked = true;
        playerCampfirePosition = new Vector2(bonfire.X + 20f, bonfire.Y);
        if (level.Session.GetFlag("campfire_chat")) {
            WasSkipped = true;
            level.ResetZoom();
            level.EndCutscene();
            EndCutscene(level);
        } else {
            Add(new Coroutine(Cutscene(level)));
        }
    }

    private IEnumerator PlayerLightApproach() {
        while (player.Light.Alpha < 1f) {
            player.Light.Alpha = Calc.Approach(player.Light.Alpha, 1f, Engine.DeltaTime * 2f);
            yield return null;
        }
    }

    private IEnumerator Cutscene(Level level) {
        yield return 0.1f;
        Add(new Coroutine(PlayerLightApproach()));
        CS06_Campfire_Glitchleste cS06CampfireGlitchleste = this;
        Coroutine component;
        Coroutine camTo = (component = new Coroutine(CameraTo(new Vector2(level.Camera.X + 90f, level.Camera.Y), 6f, Ease.CubeIn)));
        cS06CampfireGlitchleste.Add(component);
        player.DummyAutoAnimate = false;
        player.Sprite.Play("carryTheoWalk");
        for (float p = 0f; p < 3.5f; p += Engine.DeltaTime) {
            SpotlightWipe.FocusPoint = new Vector2(40f, 120f);
            player.NaiveMove(new Vector2(32f * Engine.DeltaTime, 0f));
            yield return null;
        }
        player.Sprite.Play("carryTheoCollapse");
        Audio.Play("event:/char/madeline/theo_collapse", player.Position);
        yield return 0.3f;
        Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
        Vector2 position = player.Position + new Vector2(16f, 1f);
        Level.ParticlesFG.Emit(Payphone.P_Snow, 2, position, Vector2.UnitX * 4f);
        Level.ParticlesFG.Emit(Payphone.P_SnowB, 12, position, Vector2.UnitX * 10f);
        yield return 0.7f;
        FadeWipe fade = new FadeWipe(level, wipeIn: false) {
            Duration = 1.5f,
            EndTimer = 2.5f
        };
        yield return fade.Wait();
        bonfire.SetMode(Bonfire.Mode.Lit);
        yield return 2.45f;
        camTo.Cancel();
        theo.Position = theoCampfirePosition;
        theo.Sprite.Play("sleep");
        theo.Sprite.SetAnimationFrame(theo.Sprite.CurrentAnimationTotalFrames - 1);
        player.Position = playerCampfirePosition;
        player.Facing = Facings.Left;
        player.Sprite.Play("asleep");
        level.Session.SetFlag("starsbg");
        level.Session.SetFlag("duskbg", setTo: false);
        fade.EndTimer = 0f;
        new FadeWipe(level, wipeIn: true);
        yield return null;
        level.ResetZoom();
        level.Camera.Position = new Vector2(bonfire.X - 160f, bonfire.Y - 140f);
        yield return 3f;
        Audio.SetMusic("event:/music/lvl6/madeline_and_theo");
        yield return 1.5f;
        Add(Wiggler.Create(0.6f, 3f, delegate(float v) {
            theo.Sprite.Scale = Vector2.One * (1f + 0.1f * v);
        }, start: true, removeSelfOnFinish: true));
        Level.Particles.Emit(NPC01_Theo.P_YOLO, 4, theo.Position + new Vector2(-4f, -14f), Vector2.One * 3f);
        yield return 0.5f;
        theo.Sprite.Play("wakeup");
        yield return 1f;
        player.Sprite.Play("halfWakeUp");
        yield return 0.25f;
        yield return Textbox.Say("Glitchleste_Showcase_CH6_Campfire", SelfieSequence);
        FadeWipe fadeWipe = new FadeWipe(level, wipeIn: false) {
            Duration = 3f
        };
        yield return fadeWipe.Wait();
        EndCutscene(level);
    }

    private IEnumerator SelfieSequence() {
        Add(new Coroutine(Level.ZoomTo(new Vector2(160f, 105f), 2f, 0.5f)));
        yield return 0.1f;
        theo.Sprite.Play("idle");
        Add(Alarm.Create(Alarm.AlarmMode.Oneshot, delegate {
            theo.Sprite.Scale.X = -1f;
        }, 0.25f, start: true));
        player.DummyAutoAnimate = true;
        yield return player.DummyWalkToExact((int)(theo.X + 5f), walkBackwards: false, 0.7f);
        yield return 0.2f;
        Audio.Play("event:/game/02_old_site/theoselfie_foley", theo.Position);
        theo.Sprite.Play("takeSelfie");
        yield return 1f;
        selfie = new Selfie(SceneAs<Level>());
        Scene.Add(selfie);
        yield return selfie.PictureRoutine("selfieCampfire");
        selfie = null;
        yield return 0.5f;
        yield return Level.ZoomBack(0.5f);
        yield return 0.2f;
        theo.Sprite.Scale.X = 1f;
        yield return player.DummyWalkToExact((int)playerCampfirePosition.X, walkBackwards: false, 0.7f);
        theo.Sprite.Play("wakeup");
        yield return 0.1;
        player.DummyAutoAnimate = false;
        player.Facing = Facings.Left;
        player.Sprite.Play("sleep");
        yield return 2f;
        player.Sprite.Play("halfWakeUp");
    }

    public override void OnEnd(Level level) {
        if (!WasSkipped) {
            level.ZoomSnap(new Vector2(160f, 120f), 2f);
            FadeWipe fadeWipe = new FadeWipe(level, wipeIn: true) {
                Duration = 3f
            };
            Coroutine zoom = new Coroutine(level.ZoomBack(fadeWipe.Duration));
            fadeWipe.OnUpdate = delegate {
                zoom.Update();
            };
        }
        selfie?.RemoveSelf();
        level.Session.SetFlag("campfire_chat");
        level.Session.SetFlag("starsbg", setTo: false);
        level.Session.SetFlag("duskbg", setTo: false);
        level.Session.Dreaming = true;
        level.Add(new StarJumpController());
        level.Add(new CS06_StarJumpEnd(theo, player, playerCampfirePosition, cameraStart));
        level.Add(new FlyFeather(level.LevelOffset + new Vector2(272f, 2616f), shielded: false, singleUse: false));
        SetBloom(1f);
        bonfire.Activated = false;
        bonfire.SetMode(Bonfire.Mode.Lit);
        theo.Sprite.Play("sleep");
        theo.Sprite.SetAnimationFrame(theo.Sprite.CurrentAnimationTotalFrames - 1);
        theo.Sprite.Scale.X = 1f;
        theo.Position = theoCampfirePosition;
        player.Sprite.Play("asleep");
        player.Position = playerCampfirePosition;
        player.StateMachine.Locked = false;
        player.StateMachine.State = 15;
        player.Speed = Vector2.Zero;
        player.Facing = Facings.Left;
        level.Camera.Position = player.CameraTarget;
        if (WasSkipped) {
            player.StateMachine.State = 0;
        }
        RemoveSelf();
    }

    private void SetBloom(float add) {
        Level.Session.BloomBaseAdd = add;
        Level.Bloom.Base = AreaData.Get(Level).BloomBase + add;
    }

}

public class CS06_Granny_Glitchleste : CutsceneEntity {

    private NPC06_Granny granny;
    private Player player;
    private bool firstLaugh;

    public CS06_Granny_Glitchleste(NPC06_Granny granny, Player player) {
        this.granny = granny;
        this.player = player;
    }

    public override void OnBegin(Level level) {
        Add(new Coroutine(Cutscene(level)));
    }

    private IEnumerator Cutscene(Level level) {
        player.StateMachine.State = 11;
        player.StateMachine.Locked = true;
        player.ForceCameraUpdate = true;
        yield return player.DummyWalkTo(granny.X - 40f);
        player.Facing = Facings.Right;
        firstLaugh = true;
        yield return Textbox.Say("ch6_oldlady", ZoomIn, Laughs, StopLaughing, MaddyWalksRight, MaddyWalksLeft, WaitABit, MaddyTurnsRight);
        yield return Textbox.Say("ch6_oldlady_b");
        yield return Level.ZoomBack(0.5f);
        EndCutscene(level);
    }

    private IEnumerator ZoomIn() {
        Vector2 screenSpaceFocusPoint = Vector2.Lerp(granny.Position, player.Position, 0.5f) - Level.Camera.Position + new Vector2(0f, -20f);
        yield return Level.ZoomTo(screenSpaceFocusPoint, 2f, 0.5f);
    }

    private IEnumerator Laughs() {
        if (firstLaugh) {
            firstLaugh = false;
            yield return 0.5f;
        }
        granny.Sprite.Play("laugh");
        yield return 1f;
    }

    private IEnumerator StopLaughing() {
        granny.Sprite.Play("idle");
        yield return 0.25f;
    }

    private IEnumerator MaddyWalksLeft() {
        yield return 0.1f;
        player.Facing = Facings.Left;
        yield return player.DummyWalkToExact((int)player.X - 8);
        yield return 0.1f;
    }

    private IEnumerator MaddyWalksRight() {
        yield return 0.1f;
        player.Facing = Facings.Right;
        yield return player.DummyWalkToExact((int)player.X + 8);
        yield return 0.1f;
    }

    private IEnumerator WaitABit() {
        yield return 0.8f;
    }

    private IEnumerator MaddyTurnsRight() {
        yield return 0.1f;
        player.Facing = Facings.Right;
        yield return 0.1f;
    }

    public override void OnEnd(Level level) {
        player.StateMachine.Locked = false;
        player.StateMachine.State = 0;
        player.ForceCameraUpdate = false;
        granny.Sprite.Play("idle");
    }

}

#endif

using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.WEGFanCommons.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mod.Glitchleste.Modules;

public static class SimulateFloatingPointPrecisionLossRender {

    private static readonly GlitchlesteSettings ModSettings = GlitchlesteSettings.Instance;
    public static bool Loaded = false;

    private static readonly List<IDetour> hooks = new List<IDetour>();

    private static readonly HashSet<WeakReference> cameraReferences = new HashSet<WeakReference>(new WeakReferenceComparer());
    private static Vector2 offset;
    private static bool shouldOffsetPositionsInSpriteBatch;

    public static void Load() {
        if (Loaded) {
            return;
        }

        hooks.Add(new Hook(typeof(SpriteBatch).FindMethod("System.Void Microsoft.Xna.Framework.Graphics.SpriteBatch::Begin(Microsoft.Xna.Framework.Graphics.SpriteSortMode,Microsoft.Xna.Framework.Graphics.BlendState,Microsoft.Xna.Framework.Graphics.SamplerState,Microsoft.Xna.Framework.Graphics.DepthStencilState,Microsoft.Xna.Framework.Graphics.RasterizerState,Microsoft.Xna.Framework.Graphics.Effect,Microsoft.Xna.Framework.Matrix)", simple: false), On_SpriteBatch_Begin));
        if (Everest.Flags.IsXNA) {
            hooks.Add(new Hook(typeof(SpriteBatch).FindMethod("InternalDraw"), On_SpriteBatch_InternalDraw));
        } else {
            hooks.Add(new Hook(typeof(SpriteBatch).FindMethod("PushSprite"), On_SpriteBatch_PushSprite));
        }

        On.Monocle.Camera.UpdateMatrices += On_Camera_UpdateMatrices;
        On.Celeste.Level._GCCollect += On_Level_GCCollect;

        List<Type> types = new List<Type> {
            typeof(VertexPositionColor),
            typeof(VertexPositionTexture),
            typeof(VertexPositionColorTexture),
            typeof(VertexPositionNormalTexture),
            Type.GetType("Celeste.LightingRenderer+VertexPositionColorMaskTexture, Celeste")
        };
        foreach (Type type in types) {
            hooks.Add(new Hook(typeof(GFX).FindMethod("DrawVertices").MakeGenericMethod(type),
                typeof(SimulateFloatingPointPrecisionLossRender).FindMethod(nameof(On_GFX_DrawVertices)).MakeGenericMethod(type)));
            hooks.Add(new Hook(typeof(GFX).FindMethod("DrawIndexedVertices").MakeGenericMethod(type),
                typeof(SimulateFloatingPointPrecisionLossRender).FindMethod(nameof(On_GFX_DrawIndexedVertices)).MakeGenericMethod(type)));
        }
    }

    public static void Unload() {
        hooks.ForEach(hook => hook.Dispose());
        hooks.Clear();

        On.Monocle.Camera.UpdateMatrices -= On_Camera_UpdateMatrices;
        On.Celeste.Level._GCCollect -= On_Level_GCCollect;

        // force all cameras we modified update matrices again to render properly
        foreach (WeakReference cameraReference in cameraReferences) {
            if (cameraReference.SafeGetIsAlive()) {
                (cameraReference.SafeGetTarget() as Camera)?.InvokeMethod("UpdateMatrices");
            }
        }
        cameraReferences.Clear();

        Loaded = false;
    }

    private static void On_Camera_UpdateMatrices(On.Monocle.Camera.orig_UpdateMatrices orig, Camera self) {
        if (Engine.Scene is not Level) {
            orig(self);
            return;
        }

        orig(self);

        // we need to know if the transform matrix passed to SpriteBatch.Begin and GFX.Draw[Indexed]Vertices is from level's camera,
        // so we apply a dirty hack: since Celeste is a 2D game, we can offset Z position a bit and offset back in those methods
        Matrix matrix = self.Matrix;
        Matrix inverse = self.Inverse;
        matrix.M43 = 0.001f;
        inverse.M43 = -0.001f;
        self.SetFieldValue("matrix", matrix);
        self.SetFieldValue("inverse", inverse);

        cameraReferences.Add(new WeakReference(self));
    }

    private static void On_Level_GCCollect(On.Celeste.Level.orig__GCCollect orig) {
        orig();
        cameraReferences.RemoveWhere(i => !i.SafeGetIsAlive());
    }

    private delegate void SpriteBatch_orig_Begin(SpriteBatch self, SpriteSortMode sortMode, BlendState blendState, SamplerState samplerState, DepthStencilState depthStencilState, RasterizerState rasterizerState, Effect effect, Matrix transformMatrix);

    private static void On_SpriteBatch_Begin(SpriteBatch_orig_Begin orig, SpriteBatch self, SpriteSortMode sortMode, BlendState blendState, SamplerState samplerState, DepthStencilState depthStencilState, RasterizerState rasterizerState, Effect effect, Matrix transformMatrix) {
        if (Engine.Scene is not Level || (ModSettings.HorizontalGlitchLevel == 0 && ModSettings.VerticalGlitchLevel == 0)) {
            shouldOffsetPositionsInSpriteBatch = false;
            transformMatrix.M43 = 0f;
            orig(self, sortMode, blendState, samplerState, depthStencilState, rasterizerState, effect, transformMatrix);
            return;
        }

        shouldOffsetPositionsInSpriteBatch = MatrixIsFromLevelCamera(transformMatrix);
        if (shouldOffsetPositionsInSpriteBatch) {
            transformMatrix.Translation -= new Vector3(offset, 0);
            transformMatrix.M43 = 0f;
        }

        orig(self, sortMode, blendState, samplerState, depthStencilState, rasterizerState, effect, transformMatrix);
    }

    private delegate void SpriteBatch_orig_PushSprite(SpriteBatch self, Texture2D texture, float sourceX, float sourceY, float sourceW, float sourceH, float destinationX, float destinationY, float destinationW, float destinationH, Color color, float originX, float originY, float rotationSin, float rotationCos, float depth, byte effects);

    private static void On_SpriteBatch_PushSprite(SpriteBatch_orig_PushSprite orig, SpriteBatch self, Texture2D texture, float sourceX, float sourceY, float sourceW, float sourceH, float destinationX, float destinationY, float destinationW, float destinationH, Color color, float originX, float originY, float rotationSin, float rotationCos, float depth, byte effects) {
        if (shouldOffsetPositionsInSpriteBatch) {
            destinationX += offset.X;
            destinationY += offset.Y;
        }

        orig(self, texture, sourceX, sourceY, sourceW, sourceH, destinationX, destinationY, destinationW, destinationH, color, originX, originY, rotationSin, rotationCos, depth, effects);
    }

    private delegate void SpriteBatch_orig_InternalDraw(SpriteBatch self, Texture2D texture, ref Vector4 destination, bool scaleDestination, ref Rectangle? sourceRectangle, Color color, float rotation, ref Vector2 origin, SpriteEffects effects, float depth);

    private static void On_SpriteBatch_InternalDraw(SpriteBatch_orig_InternalDraw orig, SpriteBatch self, Texture2D texture, ref Vector4 destination, bool scaleDestination, ref Rectangle? sourceRectangle, Color color, float rotation, ref Vector2 origin, SpriteEffects effects, float depth) {
        if (shouldOffsetPositionsInSpriteBatch) {
            destination.X += offset.X;
            destination.Y += offset.Y;
        }

        orig(self, texture, ref destination, scaleDestination, ref sourceRectangle, color, rotation, ref origin, effects, depth);
    }

    private delegate void GFX_orig_DrawVertices<in T>(Matrix matrix, T[] vertices, int vertexCount, Effect effect, BlendState blendState) where T : struct, IVertexType;

    private static void On_GFX_DrawVertices<T>(GFX_orig_DrawVertices<T> orig, Matrix matrix, T[] vertices, int vertexCount, Effect effect, BlendState blendState) where T : struct, IVertexType {
        if (MatrixIsFromLevelCamera(matrix)) {
            matrix.Translation -= new Vector3(offset, 0);
            vertices = GetOffsetVertices(vertices, vertexCount);
        }

        orig(matrix, vertices, vertexCount, effect, blendState);
    }

    private delegate void GFX_orig_DrawIndexedVertices<in T>(Matrix matrix, T[] vertices, int vertexCount, int[] indices, int primitiveCount, Effect effect, BlendState blendState) where T : struct, IVertexType;

    private static void On_GFX_DrawIndexedVertices<T>(GFX_orig_DrawIndexedVertices<T> orig, Matrix matrix, T[] vertices, int vertexCount, int[] indices, int primitiveCount, Effect effect, BlendState blendState) where T : struct, IVertexType {
        if (MatrixIsFromLevelCamera(matrix)) {
            matrix.Translation -= new Vector3(offset, 0);
            vertices = GetOffsetVertices(vertices, vertexCount);
        }

        orig(matrix, vertices, vertexCount, indices, primitiveCount, effect, blendState);
    }

    private static T[] GetOffsetVertices<T>(T[] vertices, int vertexCount) where T : struct, IVertexType {
        Type elementType = vertices.GetType().GetElementType();
        ReflectionUtils.GetDelegate<Vector3> getDelegate = elementType.GetFieldGetDelegate<Vector3>("Position");
        ReflectionUtils.SetDelegateRef<T, Vector3> setDelegate = elementType.GetFieldSetDelegateRef<T, Vector3>("Position");

        return vertices.Take(vertexCount)
            .Select(vertex => {
                Vector3 position = getDelegate(vertex);
                position += new Vector3(offset, 0);
                setDelegate(ref vertex, position);
                return vertex;
            }).ToArray();
    }

    public static void UpdateOffset() {
        // delta to next/previous representable float value after offset is 2 ** (glitch_level - 1) when glitch_level > 0
        offset = new Vector2(
            ModSettings.HorizontalGlitchLevel == 0 ? 0 : (1 << (22 + ModSettings.HorizontalGlitchLevel)) + (1 << (21 + ModSettings.HorizontalGlitchLevel)),
            ModSettings.VerticalGlitchLevel == 0 ? 0 : (1 << (22 + ModSettings.VerticalGlitchLevel)) + (1 << (21 + ModSettings.VerticalGlitchLevel))
        );
    }

    private static bool MatrixIsFromLevelCamera(Matrix matrix) {
        // assume the matrix is from level camera as long as it has Z transformation
        // since other transforms may be applied to it, we can't really check for the value we offset
        return matrix.M43 != 0f;
    }

}

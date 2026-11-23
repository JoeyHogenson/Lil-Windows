using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP renderer feature that draws the <see cref="RetroStylize"/> volume override, in up to three full-screen passes:
///   1. Before transparents: cel bands, outlines and the world's PSX pixel grid. Outlines come from opaque
///      depth/normals, so running here keeps them from being drawn over UI.
///   2. After transparents, on the base camera (only when the sprite and UI resolutions differ): the sprite grid
///      over world sprites, particles and other transparents, before the UI camera draws on top.
///   3. After everything (when "Stylize UI And Sprites" is on): the UI grid, color depth and dither over the
///      final image. Each grid is a whole multiple of the previous one so they line up and later passes
///      don't re-blur earlier blocks.
/// Screen Space - Overlay canvases are drawn after the camera, out of reach of any camera effect;
/// <see cref="RetroCanvasRouter"/> moves them onto the camera so pass 2 covers them too.
/// </summary>
[DisallowMultipleRendererFeature("Retro Stylize")]
public class RetroStylizeFeature : ScriptableRendererFeature
{
    public const string ShaderName = "Hidden/Lil Windows/Retro Stylize";

    [Tooltip("Assigned automatically; kept as a reference so builds include the shader.")]
    [SerializeField] private Shader shader;
    [Tooltip("Run the PSX pixelation/color/dither again at the very end so sprites, particles and UI are stylized too. "
        + "Cel bands and outlines always stay off the UI.")]
    [SerializeField] private bool stylizeUIAndSprites = true;
    [SerializeField] private bool applyInSceneView = true;

    private Material opaqueMaterial;
    private Material spriteMaterial;
    private Material finalMaterial;
    private RetroStylizePass opaquePass;
    private RetroStylizePass spritePass;
    private RetroStylizePass finalPass;

    public override void Create()
    {
#if UNITY_EDITOR
        if(shader == null) shader = Shader.Find(ShaderName);
#endif
        if(shader != null)
        {
            //one material per pass: both passes run in the same frame with different settings
            if(opaqueMaterial == null) opaqueMaterial = CoreUtils.CreateEngineMaterial(shader);
            if(spriteMaterial == null) spriteMaterial = CoreUtils.CreateEngineMaterial(shader);
            if(finalMaterial == null) finalMaterial = CoreUtils.CreateEngineMaterial(shader);
        }
        opaquePass = new RetroStylizePass("Retro Stylize (Opaque)") { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
        spritePass = new RetroStylizePass("Retro Stylize (Sprites)") { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        finalPass = new RetroStylizePass("Retro Stylize (UI + Sprites)") { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        CameraType cameraType = renderingData.cameraData.cameraType;
        if(cameraType == CameraType.Preview || cameraType == CameraType.Reflection) return;
        if(cameraType == CameraType.SceneView && !applyInSceneView) return;
        if(opaqueMaterial == null || spriteMaterial == null || finalMaterial == null) return;

        RetroStylize settings = VolumeManager.instance.stack.GetComponent<RetroStylize>();
        if(settings == null || !settings.IsActive()) return;

        //camera stacks (the UI overlay camera from RetroCanvasRouter): the world passes belong to the base camera,
        //the final pass to whichever camera finishes the frame, so nothing is stylized twice
        bool isBase = renderingData.cameraData.renderType == CameraRenderType.Base;
        bool isLast = renderingData.cameraData.resolveFinalTarget;

        if(!stylizeUIAndSprites)
        {
            if(!isBase) return;
            opaquePass.Setup(opaqueMaterial, settings, Stage.Everything);
            renderer.EnqueuePass(opaquePass);
            return;
        }

        if(isBase && (settings.CelActive || settings.OutlinesActive || settings.PixelateActive || settings.QuantizeActive))
        {
            opaquePass.Setup(opaqueMaterial, settings, Stage.Opaque);
            renderer.EnqueuePass(opaquePass);
        }
        //equal grids: the final pass already gives sprites the UI grid, no extra blit needed
        settings.GridMultiples(out int spriteMultiple, out int uiMultiple);
        if(isBase && settings.PixelateActive && spriteMultiple != uiMultiple)
        {
            spritePass.Setup(spriteMaterial, settings, Stage.Sprites);
            renderer.EnqueuePass(spritePass);
        }
        if(isLast && (settings.PixelateActive || settings.QuantizeActive))
        {
            finalPass.Setup(finalMaterial, settings, Stage.Final);
            renderer.EnqueuePass(finalPass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(opaqueMaterial);
        CoreUtils.Destroy(spriteMaterial);
        CoreUtils.Destroy(finalMaterial);
        opaqueMaterial = null;
        spriteMaterial = null;
        finalMaterial = null;
    }

    private enum Stage
    {
        /// <summary>Everything in one pass before transparents (UI and sprites untouched).</summary>
        Everything,
        /// <summary>Cel, outlines, world pixel grid; color depth left for the final pass.</summary>
        Opaque,
        /// <summary>Sprite pixel grid over world transparents, before the UI camera; color depth left for the final pass.</summary>
        Sprites,
        /// <summary>UI pixel grid + color depth + dither over the finished frame.</summary>
        Final,
    }

    private class RetroStylizePass : ScriptableRenderPass
    {
        private static readonly int SourceSizeId = Shader.PropertyToID("_Retro_SourceSize");
        private static readonly int CelId = Shader.PropertyToID("_Retro_Cel");
        private static readonly int OutlineId = Shader.PropertyToID("_Retro_Outline");
        private static readonly int OutlineColorId = Shader.PropertyToID("_Retro_OutlineColor");
        private static readonly int OutlineFadeId = Shader.PropertyToID("_Retro_OutlineFade");
        private static readonly int PsxId = Shader.PropertyToID("_Retro_Psx");
        private static readonly int SampleId = Shader.PropertyToID("_Retro_Sample");

        private class PassData
        {
            public TextureHandle source;
            public Material material;
        }

        private readonly string passName;
        private Material material;
        private Stage stage;
        private bool needsDepthNormals;
        private int worldHeight;
        private int spriteMultiple;
        private int uiMultiple;
        private int spriteRequest;
        private int uiRequest;
        private float colorLevels;
        private float dither;

        public RetroStylizePass(string name)
        {
            passName = name;
            profilingSampler = new ProfilingSampler(name);
            //the pass reads the camera color, so it can't be the backbuffer
            requiresIntermediateTexture = true;
        }

        public void Setup(Material passMaterial, RetroStylize settings, Stage passStage)
        {
            material = passMaterial;
            stage = passStage;

            bool celAndOutlines = stage == Stage.Everything || stage == Stage.Opaque;
            needsDepthNormals = celAndOutlines && settings.OutlinesActive;
            ConfigureInput(needsDepthNormals ? ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal : ScriptableRenderPassInput.None);

            material.SetVector(CelId, new Vector4(celAndOutlines ? settings.celStrength.value : 0f, settings.celBands.value, settings.celSoftness.value, 0f));
            material.SetVector(OutlineId, new Vector4(celAndOutlines ? settings.outlineStrength.value : 0f, settings.outlineThickness.value,
                settings.depthSensitivity.value, settings.normalSensitivity.value));
            material.SetColor(OutlineColorId, settings.outlineColor.value);
            Vector2 fade = settings.outlineFade.value;
            material.SetVector(OutlineFadeId, new Vector4(fade.x, fade.y, 0f, 0f));

            worldHeight = settings.psxVerticalResolution.value;
            settings.GridMultiples(out spriteMultiple, out uiMultiple);
            spriteRequest = settings.psxSpriteVerticalResolution.value;
            uiRequest = settings.psxUIVerticalResolution.value;
            //every pass quantizes, so each layer is dithered on its own grid (world, sprites, UI); the shader leaves
            //colors an earlier pass already put on a level alone
            colorLevels = settings.QuantizeActive ? Mathf.Pow(2f, settings.psxColorBits.value) : 0f;
            dither = settings.psxDither.value;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            if(resources.isActiveTargetBackBuffer || material == null) return;

            TextureHandle source = resources.activeColorTexture;
            TextureDesc desc = renderGraph.GetTextureDesc(source);
            //before transparents the color target is still multisampled when MSAA is on, and a plain
            //texture read can't sample it (every quality level currently has MSAA off)
            if(desc.msaaSamples != MSAASamples.None) return;
            desc.name = "_RetroStylizeColor";
            desc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(desc);

            Vector2Int grid = PixelGrid(desc.width, desc.height);
            material.SetVector(SourceSizeId, new Vector4(desc.width, desc.height, 1f / desc.width, 1f / desc.height));
            material.SetVector(PsxId, new Vector4(grid.y, colorLevels, dither, grid.x));
            //a finer grid than the world's lies over blocks an earlier pass made: blurring would smear their edges
            bool refines = grid.y > 0 && grid.y > worldHeight;
            material.SetVector(SampleId, new Vector4(refines ? 1f : 0f, 0f, 0f, 0f));

            using(IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(passName, out PassData data, profilingSampler))
            {
                data.source = source;
                data.material = material;
                builder.UseTexture(source, AccessFlags.Read);
                if(needsDepthNormals)
                {
                    if(resources.cameraDepthTexture.IsValid()) builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    if(resources.cameraNormalsTexture.IsValid()) builder.UseTexture(resources.cameraNormalsTexture, AccessFlags.Read);
                }
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData passData, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, passData.source, new Vector4(1f, 1f, 0f, 0f), passData.material, 0);
                });
            }

            //the last camera can just switch to the new texture. Earlier cameras in a stack (the base camera under
            //RetroCanvasRouter's UI camera) can't: the texture is freed when this camera's graph ends, and the next
            //camera would draw into it. Copy the result back into the camera's own color target instead.
            if(frameData.Get<UniversalCameraData>().resolveFinalTarget)
            {
                resources.cameraColor = destination;
                return;
            }
            using(IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(passName + " (Copy Back)", out PassData data, profilingSampler))
            {
                data.source = destination;
                builder.UseTexture(destination, AccessFlags.Read);
                builder.SetRenderAttachment(source, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData passData, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, passData.source, new Vector4(1f, 1f, 0f, 0f), 0f, false);
                });
            }
        }

        /// <summary>
        /// Virtual pixel grid (width, height) for this pass; (0, 0) = native resolution. The sprite and UI grids are
        /// the world grid times a whole number, so every earlier block splits into whole later blocks and the passes line up.
        /// </summary>
        private Vector2Int PixelGrid(int width, int height)
        {
            if(worldHeight <= 0 || worldHeight >= height) return Vector2Int.zero;
            //asking for the screen's height or more means native (rounding to a multiple of the world would land just under it)
            int request = stage == Stage.Sprites ? spriteRequest : stage == Stage.Final ? uiRequest : 0;
            if(request >= height) return Vector2Int.zero;

            int worldWidth = Mathf.Max(1, Mathf.RoundToInt(worldHeight * (float)width / height));
            int multiple = stage == Stage.Sprites ? spriteMultiple
                : stage == Stage.Final ? uiMultiple
                : 1;
            if(worldHeight * multiple >= height) return Vector2Int.zero;
            return new Vector2Int(worldWidth * multiple, worldHeight * multiple);
        }
    }
}

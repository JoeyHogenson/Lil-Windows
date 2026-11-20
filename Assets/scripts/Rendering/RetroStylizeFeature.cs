using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP renderer feature that draws the <see cref="RetroStylize"/> volume override, in two full-screen passes:
///   1. Before transparents: cel bands, outlines and the world's PSX pixel grid. Outlines come from opaque
///      depth/normals, so running here keeps them from being drawn over UI.
///   2. After everything (when "Stylize UI And Sprites" is on): PSX pixelation, color depth and dither over the
///      final image, so sprites, particles and UI get the same retro treatment. Their grid is a whole multiple
///      of the world's so the two line up.
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
    private Material finalMaterial;
    private RetroStylizePass opaquePass;
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
            if(finalMaterial == null) finalMaterial = CoreUtils.CreateEngineMaterial(shader);
        }
        opaquePass = new RetroStylizePass("Retro Stylize (Opaque)") { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
        finalPass = new RetroStylizePass("Retro Stylize (UI + Sprites)") { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        CameraType cameraType = renderingData.cameraData.cameraType;
        if(cameraType == CameraType.Preview || cameraType == CameraType.Reflection) return;
        if(cameraType == CameraType.SceneView && !applyInSceneView) return;
        if(opaqueMaterial == null || finalMaterial == null) return;

        RetroStylize settings = VolumeManager.instance.stack.GetComponent<RetroStylize>();
        if(settings == null || !settings.IsActive()) return;

        if(!stylizeUIAndSprites)
        {
            opaquePass.Setup(opaqueMaterial, settings, Stage.Everything);
            renderer.EnqueuePass(opaquePass);
            return;
        }

        if(settings.CelActive || settings.OutlinesActive || settings.PixelateActive)
        {
            opaquePass.Setup(opaqueMaterial, settings, Stage.Opaque);
            renderer.EnqueuePass(opaquePass);
        }
        if(settings.PixelateActive || settings.QuantizeActive)
        {
            finalPass.Setup(finalMaterial, settings, Stage.Final);
            renderer.EnqueuePass(finalPass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(opaqueMaterial);
        CoreUtils.Destroy(finalMaterial);
        opaqueMaterial = null;
        finalMaterial = null;
    }

    private enum Stage
    {
        /// <summary>Everything in one pass before transparents (UI and sprites untouched).</summary>
        Everything,
        /// <summary>Cel, outlines, world pixel grid; color depth left for the final pass.</summary>
        Opaque,
        /// <summary>Pixel grid + color depth + dither over the finished frame, UI included.</summary>
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
        private int uiHeight;
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

            bool celAndOutlines = stage != Stage.Final;
            needsDepthNormals = celAndOutlines && settings.OutlinesActive;
            ConfigureInput(needsDepthNormals ? ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal : ScriptableRenderPassInput.None);

            material.SetVector(CelId, new Vector4(celAndOutlines ? settings.celStrength.value : 0f, settings.celBands.value, settings.celSoftness.value, 0f));
            material.SetVector(OutlineId, new Vector4(celAndOutlines ? settings.outlineStrength.value : 0f, settings.outlineThickness.value,
                settings.depthSensitivity.value, settings.normalSensitivity.value));
            material.SetColor(OutlineColorId, settings.outlineColor.value);
            Vector2 fade = settings.outlineFade.value;
            material.SetVector(OutlineFadeId, new Vector4(fade.x, fade.y, 0f, 0f));

            worldHeight = settings.psxVerticalResolution.value;
            uiHeight = settings.psxUIVerticalResolution.value;
            //levels per channel: 5 bits -> 32 levels; 0 skips quantizing (the final pass does it once for everything)
            bool quantizeHere = stage != Stage.Opaque && settings.QuantizeActive;
            colorLevels = quantizeHere ? Mathf.Pow(2f, settings.psxColorBits.value) : 0f;
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

            resources.cameraColor = destination;
        }

        /// <summary>
        /// Virtual pixel grid (width, height) for this pass; (0, 0) = native resolution. The UI/sprite grid is the
        /// world grid times a whole number, so every world block splits into whole UI blocks and the passes line up.
        /// </summary>
        private Vector2Int PixelGrid(int width, int height)
        {
            if(worldHeight <= 0 || worldHeight >= height) return Vector2Int.zero;

            int worldWidth = Mathf.Max(1, Mathf.RoundToInt(worldHeight * (float)width / height));
            int multiple = 1;
            if(stage == Stage.Final && uiHeight > 0)
            {
                multiple = Mathf.Max(1, Mathf.RoundToInt((float)uiHeight / worldHeight));
            }
            if(worldHeight * multiple >= height) return Vector2Int.zero;
            return new Vector2Int(worldWidth * multiple, worldHeight * multiple);
        }
    }
}

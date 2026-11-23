using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Volume override for the whole-screen look that ties mismatched assets together: toon banding,
/// ink outlines, and a PS1-style low-res, low-color-depth, dithered output. Every option is applied
/// to the final image, so it affects every mesh/material in view without touching their shaders.
/// Needs <see cref="RetroStylizeFeature"/> on the active URP renderer (Lil Windows > Set Up Retro Visuals).
/// </summary>
[Serializable, VolumeComponentMenu("Lil Windows/Retro Stylize")]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class RetroStylize : VolumeComponent, IPostProcessComponent
{
    [Header("Cel Shading (posterized light)")]
    [Tooltip("0 = off. Blends the image toward flat light bands.")]
    public ClampedFloatParameter celStrength = new ClampedFloatParameter(0f, 0f, 1f);
    [Tooltip("Number of brightness bands.")]
    public ClampedIntParameter celBands = new ClampedIntParameter(4, 2, 10);
    [Tooltip("How soft the edge between two bands is.")]
    public ClampedFloatParameter celSoftness = new ClampedFloatParameter(0.1f, 0f, 1f);

    [Header("Outlines (from depth + normals)")]
    [Tooltip("0 = off.")]
    public ClampedFloatParameter outlineStrength = new ClampedFloatParameter(0f, 0f, 1f);
    public ColorParameter outlineColor = new ColorParameter(new Color(0.06f, 0.05f, 0.07f), false, false, true);
    [Tooltip("Line width in (virtual) pixels.")]
    public ClampedFloatParameter outlineThickness = new ClampedFloatParameter(1f, 0.5f, 4f);
    [Tooltip("Relative depth jump that counts as a silhouette edge. Lower = more lines.")]
    public ClampedFloatParameter depthSensitivity = new ClampedFloatParameter(0.08f, 0.005f, 1f);
    [Tooltip("Normal change that counts as a crease. Lower = more lines.")]
    public ClampedFloatParameter normalSensitivity = new ClampedFloatParameter(0.5f, 0.05f, 2f);
    [Tooltip("Lines fade out between these view distances (meters). End 0 = never fade.")]
    public FloatRangeParameter outlineFade = new FloatRangeParameter(new Vector2(60f, 200f), 0f, 1000f);

    [Header("PSX")]
    [Tooltip("Renders at this many vertical pixels and scales up blocky. 0 = native resolution. PS1 ran at 240.")]
    public ClampedIntParameter psxVerticalResolution = new ClampedIntParameter(0, 0, 1080);
    [Tooltip("Resolution for world sprites, particles and other transparents (not UI). Needs \"Stylize UI And Sprites\" on the renderer feature. "
        + "Rounded to a whole multiple of the world resolution so the pixel grids line up (set it equal to the world for matching chunky sprites). "
        + "0 = same as the UI.")]
    public ClampedIntParameter psxSpriteVerticalResolution = new ClampedIntParameter(0, 0, 1080);
    [Tooltip("Resolution for screen UI (needs \"Stylize UI And Sprites\" on the renderer feature). "
        + "Rounded to a whole multiple of the sprite resolution (or the world's, when sprites is 0) so the pixel grids line up; "
        + "it can't be coarser than the sprites, since UI is pixelated over the finished frame. 0 = same as the sprites/world. "
        + "Small UI text gets hard to read below ~480.")]
    public ClampedIntParameter psxUIVerticalResolution = new ClampedIntParameter(480, 0, 1080);
    [Tooltip("Bits per color channel. 8 = off. PS1 framebuffer was 5 (15-bit color).")]
    public ClampedIntParameter psxColorBits = new ClampedIntParameter(8, 2, 8);
    [Tooltip("Ordered (Bayer) dither strength hiding the color banding, like the PS1's own dither.")]
    public ClampedFloatParameter psxDither = new ClampedFloatParameter(1f, 0f, 1f);

    public bool CelActive => celStrength.value > 0f;
    public bool OutlinesActive => outlineStrength.value > 0f;
    public bool PixelateActive => psxVerticalResolution.value > 0;
    public bool QuantizeActive => psxColorBits.value < 8;

    /// <summary>
    /// Sprite and UI pixel grids as whole multiples of the world grid, so each later pass splits every earlier block
    /// into whole blocks and leaves it untouched. The UI multiple is also a multiple of the sprite one.
    /// </summary>
    public void GridMultiples(out int sprite, out int ui)
    {
        int world = psxVerticalResolution.value;
        sprite = 1;
        ui = 1;
        if(world <= 0) return;

        int spriteHeight = psxSpriteVerticalResolution.value;
        int uiHeight = psxUIVerticalResolution.value;
        if(spriteHeight <= 0)
        {
            //sprites follow the UI grid: one pass at the end covers both
            sprite = uiHeight > 0 ? Mathf.Max(1, Mathf.RoundToInt((float)uiHeight / world)) : 1;
            ui = sprite;
            return;
        }
        sprite = Mathf.Max(1, Mathf.RoundToInt((float)spriteHeight / world));
        int uiOverSprite = uiHeight > 0 ? Mathf.Max(1, Mathf.RoundToInt((float)uiHeight / (world * sprite))) : 1;
        ui = sprite * uiOverSprite;
    }

    public bool IsActive()
    {
        return active && (CelActive || OutlinesActive || PixelateActive || QuantizeActive);
    }
}

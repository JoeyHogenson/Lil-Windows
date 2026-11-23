// Full-screen pass for the RetroStylize volume override (see RetroStylizeFeature.cs).
// Order inside the pass: snap to the virtual PSX pixel grid -> cel banding -> outlines -> color-depth quantize + dither.
Shader "Hidden/Lil Windows/Retro Stylize"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "RetroStylize"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 _Retro_SourceSize;    // w, h, 1/w, 1/h of the camera color target
            float4 _Retro_Cel;           // x strength, y bands, z softness
            float4 _Retro_Outline;       // x strength, y thickness (px), z depth sensitivity, w normal sensitivity
            float4 _Retro_OutlineColor;
            float4 _Retro_OutlineFade;   // x start, y end (0 = no fade)
            float4 _Retro_Psx;           // x virtual height (0 = native), y levels per channel (0 = off), z dither, w virtual width
            float4 _Retro_Sample;        // x 1 = one point tap per cell: this pass refines a grid an earlier pass already
                                         // pixelated, and blurring would smear those blocks' edges

            static const float Bayer4x4[16] =
            {
                 0.0,  8.0,  2.0, 10.0,
                12.0,  4.0, 14.0,  6.0,
                 3.0, 11.0,  1.0,  9.0,
                15.0,  7.0, 13.0,  5.0
            };

            float Luma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

            // Size of the grid everything snaps to: the PSX resolution if set, else the real one.
            // Width/height come from C# so the UI pass's grid is an exact multiple of the world pass's grid.
            float2 VirtualSize()
            {
                if (_Retro_Psx.x <= 0.0) return _Retro_SourceSize.xy;
                return _Retro_Psx.wx;
            }

            float3 SampleColor(float2 uv, float2 virtualSize)
            {
                if (_Retro_Psx.x <= 0.0 || _Retro_Sample.x > 0.5)
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb;

                // 4 bilinear taps spread over the big pixel: an average of ~16 source pixels, so the
                // low-res image doesn't crawl/shimmer as the camera moves (single point taps would)
                float2 quarter = 0.25 / virtualSize;
                float3 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-quarter.x, -quarter.y)).rgb;
                c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( quarter.x, -quarter.y)).rgb;
                c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-quarter.x,  quarter.y)).rgb;
                c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( quarter.x,  quarter.y)).rgb;
                return c * 0.25;
            }

            float3 CelBands(float3 color)
            {
                float strength = _Retro_Cel.x;
                if (strength <= 0.0) return color;

                float bands = _Retro_Cel.y;
                float lum = Luma(color);
                // band in perceptual (sqrt) space so dark interiors keep more steps than highlights
                float x = sqrt(max(lum, 0.0)) * bands;
                float band = floor(x);
                float soft = max(_Retro_Cel.z, 0.001) * 0.5;
                float stepped = (band + smoothstep(0.5 - soft, 0.5 + soft, x - band)) / bands;
                float target = stepped * stepped;
                // scale the color rather than replacing it, so hue/saturation (and textures' detail) survive
                float3 banded = color * (target / max(lum, 1e-4));
                return lerp(color, banded, strength);
            }

            float Edge(float2 uv, float2 virtualSize)
            {
                float strength = _Retro_Outline.x;
                if (strength <= 0.0) return 0.0;

                float2 o = _Retro_Outline.y / virtualSize;
                float2 uvA = uv + float2(-o.x, -o.y);
                float2 uvB = uv + float2( o.x,  o.y);
                float2 uvC = uv + float2( o.x, -o.y);
                float2 uvD = uv + float2(-o.x,  o.y);

                float center = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float dA = LinearEyeDepth(SampleSceneDepth(uvA), _ZBufferParams);
                float dB = LinearEyeDepth(SampleSceneDepth(uvB), _ZBufferParams);
                float dC = LinearEyeDepth(SampleSceneDepth(uvC), _ZBufferParams);
                float dD = LinearEyeDepth(SampleSceneDepth(uvD), _ZBufferParams);
                // Roberts cross, relative to distance so far walls don't turn solid black
                float depthEdge = sqrt((dA - dB) * (dA - dB) + (dC - dD) * (dC - dD)) / max(center, 0.01);
                depthEdge = smoothstep(_Retro_Outline.z, _Retro_Outline.z * 2.0, depthEdge);

                float3 nA = SampleSceneNormals(uvA);
                float3 nB = SampleSceneNormals(uvB);
                float3 nC = SampleSceneNormals(uvC);
                float3 nD = SampleSceneNormals(uvD);
                float normalEdge = sqrt(dot(nA - nB, nA - nB) + dot(nC - nD, nC - nD));
                normalEdge = smoothstep(_Retro_Outline.w, _Retro_Outline.w * 1.5, normalEdge);

                float edge = max(depthEdge, normalEdge) * strength;
                if (_Retro_OutlineFade.y > 0.0)
                    edge *= 1.0 - smoothstep(_Retro_OutlineFade.x, _Retro_OutlineFade.y, center);
                return edge;
            }

            float3 Quantize(float3 color, float2 virtualPixel)
            {
                float levels = _Retro_Psx.y;
                if (levels <= 0.0) return color;

                #if !defined(UNITY_COLORSPACE_GAMMA)
                color = LinearToSRGB(saturate(color));
                #endif

                uint2 p = (uint2)virtualPixel % 4u;
                float threshold = (Bayer4x4[p.y * 4u + p.x] + 0.5) / 16.0;
                float steps = levels - 1.0;
                // dither 0 = plain rounding, 1 = full ordered dither
                float offset = lerp(0.5, threshold, _Retro_Psx.z);
                float3 x = saturate(color) * steps;
                float3 level = round(x);
                // already on a level (an earlier pass quantized it, then the 8-bit target rounded it): keep it, so each
                // layer keeps the dither of its own grid instead of being re-dithered at this pass's
                float tolerance = 0.01 + steps / 400.0;
                float3 onLevel = step(abs(x - level), tolerance);
                color = lerp(floor(x + offset), level, onLevel) / steps;

                #if !defined(UNITY_COLORSPACE_GAMMA)
                color = SRGBToLinear(color);
                #endif
                return color;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 virtualSize = VirtualSize();
                float2 virtualPixel = floor(input.texcoord * virtualSize);
                // everything below samples at the center of the big pixel, so lines and dither are chunky too
                float2 uv = _Retro_Psx.x > 0.0 ? (virtualPixel + 0.5) / virtualSize : input.texcoord;

                float3 color = SampleColor(uv, virtualSize);
                color = CelBands(color);
                color = lerp(color, _Retro_OutlineColor.rgb, Edge(uv, virtualSize));
                color = Quantize(color, virtualPixel);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}

// Lit, world-space triplanar painted brick/cinderblock wall for URP.
// Replaces SG_Triplanarwall (unlit texture x tint) with:
//   - surface normals from the brick texture (mortar recessed) plus every procedural layer below
//   - paint chipping: 3D fbm noise, biased toward mortar lines and cracks, revealing the block underneath
//   - cracks: a jittered Voronoi edge network, only in noise-driven patches
//   - grime blotches
// Everything is procedural in world space, so it needs no UVs and never repeats with the brick texture.
// Noise scales are relative to Texture Scale, so they follow the bricks if the wall is rescaled.
Shader "Lil Windows/Brick Wall"
{
    Properties
    {
        [Header(Base)]
        [MainTexture][NoScaleOffset] _WallTexture ("Wall Texture", 2D) = "white" {}
        _TextureScale ("Texture Scale (tiles per meter)", Float) = 0.055
        [MainColor] _BrickTint ("Paint Tint", Color) = (0.434, 0.434, 0.434, 1)
        _Smoothness ("Paint Smoothness", Range(0, 1)) = 0.25
        _SelfIllumination ("Self Illumination", Range(0, 1)) = 0.15

        [Header(Surface Relief)]
        _BumpStrength ("Bump Strength", Range(0, 4)) = 1
        [Toggle] _InvertHeight ("Mortar Is Brighter (invert height)", Float) = 1
        _HeightMidpoint ("Height Midpoint (texture luma)", Range(0, 1)) = 0.92
        _HeightContrast ("Height Contrast", Range(0, 20)) = 6
        _CavityOcclusion ("Mortar Occlusion", Range(0, 1)) = 0.35

        [Header(Paint Chipping)]
        _ChipAmount ("Chip Amount", Range(0, 1)) = 0.3
        _ChipScale ("Chip Scale (per brick tile)", Range(0.5, 40)) = 9
        _ChipSharpness ("Chip Edge Sharpness", Range(1, 60)) = 18
        _ChipMortarBias ("Chips Favor Mortar", Range(0, 1)) = 0.45
        _ChipColor ("Exposed Block Color", Color) = (0.43, 0.41, 0.38, 1)
        _ChipDepth ("Paint Thickness (bump)", Range(0, 2)) = 0.8
        _ChipRimDarken ("Chip Edge Darken", Range(0, 1)) = 0.35

        [Header(Cracks)]
        _CrackAmount ("Crack Coverage", Range(0, 1)) = 0.35
        _CrackScale ("Crack Cell Scale (per brick tile)", Range(0.5, 20)) = 3.5
        _CrackWidth ("Crack Width", Range(0.001, 0.1)) = 0.018
        _CrackJitter ("Crack Jaggedness", Range(0, 1)) = 0.35
        _CrackColor ("Crack Color", Color) = (0.08, 0.07, 0.07, 1)
        _CrackDepth ("Crack Depth (bump)", Range(0, 3)) = 1.5

        [Header(Grime)]
        _GrimeAmount ("Grime Amount", Range(0, 1)) = 0.3
        _GrimeScale ("Grime Scale (per brick tile)", Range(0.25, 10)) = 1.5
        _GrimeColor ("Grime Color", Color) = (0.32, 0.29, 0.24, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "Queue" = "Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_WallTexture);
        SAMPLER(sampler_WallTexture);

        CBUFFER_START(UnityPerMaterial)
            float _TextureScale;
            half4 _BrickTint;
            half _Smoothness;
            half _SelfIllumination;
            half _BumpStrength;
            half _InvertHeight;
            half _HeightMidpoint;
            half _HeightContrast;
            half _CavityOcclusion;
            half _ChipAmount;
            float _ChipScale;
            half _ChipSharpness;
            half _ChipMortarBias;
            half4 _ChipColor;
            half _ChipDepth;
            half _ChipRimDarken;
            half _CrackAmount;
            float _CrackScale;
            half _CrackWidth;
            half _CrackJitter;
            half4 _CrackColor;
            half _CrackDepth;
            half _GrimeAmount;
            float _GrimeScale;
            half4 _GrimeColor;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile_fog
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 3);
            #ifdef USE_APV_PROBE_OCCLUSION
                float4 probeOcclusion : TEXCOORD4;
            #endif
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half3 vertexLight : TEXCOORD5;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ---------- Noise ----------

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);
                float n000 = Hash13(i);
                float n100 = Hash13(i + float3(1, 0, 0));
                float n010 = Hash13(i + float3(0, 1, 0));
                float n110 = Hash13(i + float3(1, 1, 0));
                float n001 = Hash13(i + float3(0, 0, 1));
                float n101 = Hash13(i + float3(1, 0, 1));
                float n011 = Hash13(i + float3(0, 1, 1));
                float n111 = Hash13(i + float3(1, 1, 1));
                return lerp(lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y),
                            lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y), u.z);
            }

            // 0..1, 4 octaves
            float Fbm(float3 p)
            {
                float sum = 0.0;
                float amplitude = 0.5;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    sum += ValueNoise(p) * amplitude;
                    p = p * 2.03 + 17.1;
                    amplitude *= 0.5;
                }
                return sum / 0.9375;
            }

            // Distance to the nearest Voronoi cell border (F2 - F1 approximation).
            float VoronoiBorder(float2 x)
            {
                float2 cell = floor(x);
                float2 f = frac(x);
                float f1 = 8.0;
                float f2 = 8.0;
                [unroll] for (int j = -1; j <= 1; j++)
                [unroll] for (int i = -1; i <= 1; i++)
                {
                    float2 g = float2(i, j);
                    float2 r = g + Hash22(cell + g) - f;
                    float d = dot(r, r);
                    if (d < f1) { f2 = f1; f1 = d; }
                    else if (d < f2) { f2 = d; }
                }
                return sqrt(f2) - sqrt(f1);
            }

            // ---------- Wall layers ----------

            struct WallLayers
            {
                float brickHeight;  // 0 = mortar, 1 = brick face
                float chip;         // 1 = paint chipped off
                float crack;        // 1 = inside a crack
                float height;       // combined relief
            };

            // planeUV/planeP: position projected on the dominant triplanar plane, in brick-tile units
            WallLayers EvaluateLayers(float3 tileP, float2 planeUV)
            {
                WallLayers o;

                float luma = dot(SAMPLE_TEXTURE2D(_WallTexture, sampler_WallTexture, planeUV).rgb, float3(0.299, 0.587, 0.114));
                float h = saturate((luma - _HeightMidpoint) * _HeightContrast + 0.5);
                o.brickHeight = _InvertHeight > 0.5 ? 1.0 - h : h;

                // cracks: Voronoi borders, warped for jaggedness, kept only where a low-frequency patch mask allows
                float2 crackUV = planeUV * _CrackScale;
                float2 warp = float2(ValueNoise(float3(crackUV * 2.5, 3.7)), ValueNoise(float3(crackUV * 2.5, 9.2))) - 0.5;
                float border = VoronoiBorder(crackUV + warp * _CrackJitter * 1.5);
                float widthNoise = ValueNoise(float3(crackUV * 4.0, 1.3));
                float width = _CrackWidth * (0.35 + 1.3 * widthNoise);
                float crackLine = 1.0 - smoothstep(width * 0.4, width, border);
                float patch = ValueNoise(tileP * (_CrackScale * 0.35) + 5.1);
                float patchMask = smoothstep(1.0 - _CrackAmount, 1.0 - _CrackAmount + 0.15, patch);
                o.crack = crackLine * patchMask * step(0.001, _CrackAmount);

                // chips: fbm noise thresholded; mortar lines and cracks chip first
                float n = Fbm(tileP * _ChipScale);
                n += (1.0 - o.brickHeight) * _ChipMortarBias * 0.35;
                n += o.crack * 0.3;
                float threshold = 1.0 - _ChipAmount * 0.75;
                o.chip = saturate((n - threshold) * _ChipSharpness + 0.5) * step(0.001, _ChipAmount);

                // paint sits on top (raised); chips and cracks go down
                o.height = o.brickHeight * _BumpStrength
                         + (1.0 - o.chip) * _ChipDepth
                         - o.crack * _CrackDepth;
                return o;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                output.vertexLight = VertexLighting(positionInputs.positionWS, output.normalWS);
            #endif

                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH4(positionInputs.positionWS, output.normalWS, GetWorldSpaceNormalizeViewDir(positionInputs.positionWS), output.vertexSH, output.probeOcclusion);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalGeo = normalize(input.normalWS);
                float3 absPos = GetAbsolutePositionWS(input.positionWS);
                float3 tileP = absPos * _TextureScale;

                // ---- albedo: same triplanar mapping as the old Shader Graph (Triplanar node, blend 1)
                float3 blend = abs(normalGeo);
                blend /= max(dot(blend, 1.0), 1e-4);
                half3 texX = SAMPLE_TEXTURE2D(_WallTexture, sampler_WallTexture, tileP.zy).rgb;
                half3 texY = SAMPLE_TEXTURE2D(_WallTexture, sampler_WallTexture, tileP.xz).rgb;
                half3 texZ = SAMPLE_TEXTURE2D(_WallTexture, sampler_WallTexture, tileP.xy).rgb;
                half3 wallTex = texX * blend.x + texY * blend.y + texZ * blend.z;

                // ---- relief: evaluated on the dominant plane, normals by finite differences along it
                float3 axisU, axisV;
                if (blend.x >= blend.y && blend.x >= blend.z) { axisU = float3(0, 0, 1); axisV = float3(0, 1, 0); }
                else if (blend.y >= blend.z)                  { axisU = float3(1, 0, 0); axisV = float3(0, 0, 1); }
                else                                          { axisU = float3(1, 0, 0); axisV = float3(0, 1, 0); }
                float2 planeUV = float2(dot(tileP, axisU), dot(tileP, axisV));

                // one texel step, widened at distance so the bumps don't sparkle
                float texel = 1.0 / 1024.0;
                float footprint = max(length(fwidth(planeUV)), texel);
                WallLayers center = EvaluateLayers(tileP, planeUV);
                WallLayers stepU = EvaluateLayers(tileP + axisU * footprint, planeUV + float2(footprint, 0));
                WallLayers stepV = EvaluateLayers(tileP + axisV * footprint, planeUV + float2(0, footprint));
                // slope per texel, so Bump Strength reads the same at any distance
                float slopeScale = texel / footprint * 4.0;
                float dhdu = (stepU.height - center.height) * slopeScale;
                float dhdv = (stepV.height - center.height) * slopeScale;

                float3 tangentU = normalize(axisU - normalGeo * dot(normalGeo, axisU));
                float3 tangentV = normalize(axisV - normalGeo * dot(normalGeo, axisV));
                float3 normalWS = normalize(normalGeo - (dhdu * tangentU + dhdv * tangentV));

                // ---- color layers
                half3 paint = wallTex * _BrickTint.rgb;
                half blockNoise = (half)ValueNoise(tileP * _ChipScale * 3.0 + 11.0);
                half3 block = _ChipColor.rgb * (0.8 + 0.4 * blockNoise) * lerp(1.0, wallTex.g, 0.5);
                half3 albedo = lerp(paint, block, center.chip);
                // a dark rim right where the paint breaks
                half rim = saturate(center.chip * (1.0 - center.chip) * 4.0);
                albedo *= 1.0 - rim * _ChipRimDarken;
                albedo = lerp(albedo, _CrackColor.rgb, center.crack);
                half grime = (half)smoothstep(0.45, 0.85, Fbm(tileP * _GrimeScale + 23.0)) * _GrimeAmount;
                albedo = lerp(albedo, albedo * _GrimeColor.rgb, grime);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.alpha = 1.0;
                surface.metallic = 0.0;
                surface.specular = half3(0.0, 0.0, 0.0);
                surface.smoothness = _Smoothness * (1.0 - center.chip) * (1.0 - center.crack) * (1.0 - grime * 0.5);
                surface.occlusion = lerp(1.0, center.brickHeight, _CavityOcclusion) * (1.0 - center.crack * 0.6);
                surface.emission = albedo * _SelfIllumination;
                surface.normalTS = half3(0, 0, 1);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                inputData.vertexLighting = input.vertexLight;
            #endif
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            #if !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
                inputData.bakedGI = SAMPLE_GI(input.vertexSH, GetAbsolutePositionWS(inputData.positionWS), inputData.normalWS,
                    inputData.viewDirectionWS, input.positionCS.xy, input.probeOcclusion, inputData.shadowMask);
            #else
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
            #endif

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            // Shadows.hlsl needs LerpWhiteTo; Lit gets this include through LitInput/SurfaceInput
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                return ApplyShadowClamping(positionCS);
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }

        // Geometric normals only: the retro outline pass should ink the wall's silhouette and corners,
        // not every mortar line.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

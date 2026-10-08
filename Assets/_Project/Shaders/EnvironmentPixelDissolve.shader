// Shader d'environnement : éclairage cartoon (cel-shading) + UV monde + dissolution "pixel"
// pilotée par _Dissolve (FadeableWall). Utilisé pour le sol, les murs et les props.
Shader "TaintlessGlade/Environment Pixel Dissolve"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _SpecColor("Specular", Color) = (0, 0, 0, 0)
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        [HideInInspector] _Cutoff("Alpha Clipping", Range(0.0, 1.0)) = 0.5

        [Header(Toon)]
        _ShadowColor("Shadow Tint", Color) = (0.5, 0.45, 0.72, 1)
        _ShadeThreshold("Shade Threshold", Range(-1, 1)) = 0.1
        _ShadeSoftness("Shade Edge Softness", Range(0.001, 0.3)) = 0.02
        _AmbientStrength("Ambient Strength", Range(0, 2)) = 0.5
        _LightBands("Point Light Bands", Range(1, 8)) = 3
        _TextureDetail("Texture Detail", Range(0, 1)) = 0.5
        _Saturation("Saturation", Range(0, 2)) = 1.3
        _ShadowPixelSize("Shadow Pixel Size (m, 0 = off)", Float) = 0.125

        [Header(Pixel Dissolve)]
        _Dissolve("Dissolve", Range(0, 1)) = 0
        _PixelSize("Pixel Size (m)", Float) = 0.2
        _TopBias("Top To Bottom Bias", Range(0, 1)) = 0.35
        _KeepHeight("Keep Bottom Height (m)", Float) = 0.15
        _WallHeight("Wall Height (m)", Float) = 3
        [HDR] _EdgeColor("Edge Color", Color) = (0.6, 2.0, 1.2, 1)
        _EdgeWidth("Edge Width", Range(0, 0.3)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "SimpleLit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertexSimple
            #pragma fragment DissolveForwardFragment

            #pragma shader_feature_local_fragment _EMISSION

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_instancing

            #define BUMP_SCALE_NOT_SUPPORTED 1

            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitForwardPass.hlsl"
            #include "PixelDissolve.hlsl"
            #include "ToonLighting.hlsl"

            void DissolveForwardFragment(
                Varyings input
                , out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 edge = PixelDissolve(input.positionWS);
                float2 uv = PD_WorldUV(input.positionWS, input.normalWS.xyz) * _BaseMap_ST.xy + _BaseMap_ST.zw;

                InputData inputData;
                InitializeInputData(input, half3(0, 0, 1), inputData);
                InitializeBakedGIData(input, inputData);

                half3 color = ToonLighting(inputData, ToonAlbedo(uv));
            #if defined(_EMISSION)
                color += _EmissionColor.rgb;
            #endif
                color = MixFog(color, inputData.fogCoord);

                outColor = half4(color + edge, 1.0);
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }

        // Les murs continuent de projeter leur ombre même dissous : la pièce reste lisible.
        UsePass "Universal Render Pipeline/Simple Lit/ShadowCaster"

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DissolveDepthVertex
            #pragma fragment DissolveDepthOnlyFragment
            #pragma multi_compile_instancing
            #include "PixelDissolveDepth.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DissolveDepthVertex
            #pragma fragment DissolveDepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include "PixelDissolveDepth.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

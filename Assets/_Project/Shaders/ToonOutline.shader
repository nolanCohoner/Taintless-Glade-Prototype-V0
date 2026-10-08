// Contour cartoon plein écran : détecte les bords via la profondeur et les normales de la scène.
// Utilisé par un "Full Screen Pass Renderer Feature" injecté avant les objets transparents,
// donc les sprites et particules passent par-dessus et ne sont jamais recouverts par le trait.
Shader "TaintlessGlade/Toon Outline"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (0.1, 0.06, 0.14, 1)
        _Thickness("Thickness (px)", Range(0.5, 4)) = 1.5
        _DepthThreshold("Depth Sensitivity", Range(0.005, 0.5)) = 0.06
        _NormalThreshold("Normal Sensitivity", Range(0.05, 2)) = 0.5
        _FadeDistance("Fade Distance", Float) = 60
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "ToonOutline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            half4 _OutlineColor;
            float _Thickness;
            float _DepthThreshold;
            float _NormalThreshold;
            float _FadeDistance;

            float EyeDepth(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float2 offset = _ScreenSize.zw * _Thickness;
                float2 uvs[4] = { uv + float2(offset.x, 0), uv - float2(offset.x, 0), uv + float2(0, offset.y), uv - float2(0, offset.y) };

                float depth = EyeDepth(uv);
                float3 normal = SampleSceneNormals(uv);

                // Laplacien de profondeur : nul sur une surface plane (pas de faux traits sur le sol incliné).
                float depthSum = 0;
                float normalDiff = 0;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    depthSum += EyeDepth(uvs[i]);
                    normalDiff += 1.0 - saturate(dot(normal, SampleSceneNormals(uvs[i])));
                }

                float depthEdge = step(_DepthThreshold, abs(depthSum - 4.0 * depth) / max(depth, 0.001));
                float normalEdge = step(_NormalThreshold, normalDiff);
                float edge = max(depthEdge, normalEdge) * saturate(1.0 - depth / _FadeDistance);

                return half4(lerp(color.rgb, _OutlineColor.rgb, edge * _OutlineColor.a), color.a);
            }
            ENDHLSL
        }
    }
}

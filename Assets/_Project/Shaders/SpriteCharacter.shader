// Sprite de personnage (non éclairé) qui PROJETTE une ombre sur le décor.
// Compatible avec les sprites riggés du package 2D Animation (SKINNED_SPRITE).
Shader "TaintlessGlade/Sprite Character"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Cutoff("Shadow Alpha Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _Color("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1, 1, 1, 1)
        // Masque d'oeil : la boule d'oeil écrit le stencil (_AlphaClip = 1), l'iris ne se dessine que dedans.
        [HideInInspector] _StencilRef("Stencil Ref", Float) = 0
        [HideInInspector] _StencilComp("Stencil Comp", Float) = 8
        [HideInInspector] _StencilPass("Stencil Pass", Float) = 0
        [HideInInspector] _AlphaClip("Alpha Clip", Float) = 0
        // 4 = LEqual (normal) ; 8 = Always : le sprite se dessine par-dessus le décor (arc qui descend sous le sol).
        [HideInInspector] _ZTest("ZTest", Float) = 4
        // Reflets dynamiques des lumières ponctuelles (torches) : relief factice déduit de la forme du sprite.
        _TorchStrength("Torch Highlight Strength", Range(0, 3)) = 1.2
        _EdgeNormal("Edge Relief (silhouette)", Range(0, 12)) = 5
        _Bulge("Bulge (dôme)", Range(0, 2)) = 0.9
        _NormalRadius("Relief Radius (UV)", Range(0.002, 0.05)) = 0.014
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Cutoff;
            half _AlphaClip;
            half _TorchStrength;
            half _EdgeNormal;
            half _Bulge;
            half _NormalRadius;
        CBUFFER_END

        struct Attributes
        {
            float3 positionOS : POSITION;
            float2 uv         : TEXCOORD0;
            half4  color      : COLOR;
            UNITY_SKINNED_VERTEX_INPUTS
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv         : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            half4  color      : COLOR;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float3 SpritePositionOS(Attributes input)
        {
            UNITY_SKINNED_VERTEX_COMPUTE(input);
            SetUpSpriteInstanceProperties();
            return UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
        }

        half4 SpriteColor(Varyings input)
        {
            return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
        }
        ENDHLSL

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off
            Stencil
            {
                Ref [_StencilRef]
                Comp [_StencilComp]
                Pass [_StencilPass]
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float TorchAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            // Reflet d'une lumière : bande nette (style cartoon) sur le côté du relief qui regarde la lumière.
            half3 TorchHighlight(Light light, float3 normalWS, half3 albedo)
            {
                half facing = saturate(dot(normalWS, light.direction) * 0.5h + 0.5h);
                half band = smoothstep(0.66h, 0.72h, facing) * 0.55h + smoothstep(0.84h, 0.9h, facing) * 0.45h;
                half amount = band * saturate(light.distanceAttenuation * 2.5h);
                // Reflet teinté par la lumière, proportionnel à la couleur du dessin (les contours noirs restent noirs).
                return albedo * light.color * amount * _TorchStrength;
            }

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionOS = SpritePositionOS(input);
                o.positionCS = TransformObjectToHClip(positionOS);
                o.positionWS = TransformObjectToWorld(positionOS);
                o.uv = input.uv;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 c = SpriteColor(input);
                if (_AlphaClip > 0.5h) clip(c.a - 0.5h);

            #if defined(_ADDITIONAL_LIGHTS)
                // Normale factice : relief aux bords de la silhouette (gradient de l'alpha) + dôme, dans le plan du sprite.
                float r = _NormalRadius;
                float2 gradient = float2(
                    TorchAlpha(input.uv + float2(r, 0)) - TorchAlpha(input.uv - float2(r, 0)),
                    TorchAlpha(input.uv + float2(0, r)) - TorchAlpha(input.uv - float2(0, r)));
                float2 tilt = -gradient * _EdgeNormal + (input.uv - 0.5) * 2.0 * _Bulge;
                float3 axisX = normalize(UNITY_MATRIX_M._m00_m10_m20);
                float3 axisY = normalize(UNITY_MATRIX_M._m01_m11_m21);
                float3 towardCamera = UNITY_MATRIX_V[2].xyz;
                float3 normalWS = normalize(axisX * tilt.x + axisY * tilt.y + towardCamera);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                half3 highlight = 0;
                uint pixelLightCount = GetAdditionalLightsCount();

                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    highlight += TorchHighlight(GetAdditionalLight(lightIndex, input.positionWS), normalWS, c.rgb);
                }
                #endif

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    highlight += TorchHighlight(GetAdditionalLight(lightIndex, input.positionWS), normalWS, c.rgb);
                LIGHT_LOOP_END

                c.rgb += min(highlight, 0.8h);
            #endif
                return c;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = TransformObjectToWorld(SpritePositionOS(input));
                float3 normalWS = TransformObjectToWorldNormal(float3(0, 0, -1));
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = positionCS;
                o.uv = input.uv;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(SpriteColor(input).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}

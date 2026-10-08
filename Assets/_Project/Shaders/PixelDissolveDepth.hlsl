#ifndef TAINTLESS_PIXEL_DISSOLVE_DEPTH_INCLUDED
#define TAINTLESS_PIXEL_DISSOLVE_DEPTH_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "PixelDissolve.hlsl"

struct DissolveDepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct DissolveDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    UNITY_VERTEX_OUTPUT_STEREO
};

DissolveDepthVaryings DissolveDepthVertex(DissolveDepthAttributes input)
{
    DissolveDepthVaryings output = (DissolveDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

half DissolveDepthOnlyFragment(DissolveDepthVaryings input) : SV_TARGET
{
    PixelDissolve(input.positionWS);
    return input.positionCS.z;
}

half4 DissolveDepthNormalsFragment(DissolveDepthVaryings input) : SV_TARGET
{
    PixelDissolve(input.positionWS);
    float3 normalWS = normalize(input.normalWS);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
    return half4(PackFloat2To888(remappedOctNormalWS), 0.0);
#else
    return half4(normalWS, 0.0);
#endif
}

#endif

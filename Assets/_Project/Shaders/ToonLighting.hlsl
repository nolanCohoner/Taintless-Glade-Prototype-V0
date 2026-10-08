#ifndef TAINTLESS_TOON_LIGHTING_INCLUDED
#define TAINTLESS_TOON_LIGHTING_INCLUDED

// À inclure après SimpleLitInput.hlsl / SimpleLitForwardPass.hlsl (utilise _BaseMap, _BaseColor, InputData, Light...).

half4 _ShadowColor;
half  _ShadeThreshold;
half  _ShadeSoftness;
half  _AmbientStrength;
half  _LightBands;
half  _TextureDetail;
half  _Saturation;
half  _ShadowPixelSize;

// Aligne la position sur une grille monde, en restant SUR la surface (on retire la composante normale)
// pour éviter que la surface ne s'ombre elle-même. Donne des ombres et des halos de lumière "pixel".
float3 PixelSnapOnSurface(float3 positionWS, float3 normalWS)
{
    if (_ShadowPixelSize <= 0.001) return positionWS;
    float3 snapped = (floor(positionWS / _ShadowPixelSize) + 0.5) * _ShadowPixelSize;
    float3 offset = snapped - positionWS;
    offset -= normalWS * dot(offset, normalWS);
    return positionWS + offset;
}

half ToonStep(half x, half threshold)
{
    return smoothstep(threshold - _ShadeSoftness, threshold + _ShadeSoftness, x);
}

// Texture "aplatie" : mélange vers sa couleur moyenne (dernier mip) pour un rendu peint, puis saturation.
half3 ToonAlbedo(float2 uv)
{
    half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
    half3 average = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 8).rgb;
    half3 color = lerp(average, tex, _TextureDetail) * _BaseColor.rgb;
    half luminance = dot(color, half3(0.299h, 0.587h, 0.114h));
    return max(0, lerp(luminance.xxx, color, _Saturation));
}

// Lumières ponctuelles (torches...) découpées en anneaux nets.
half3 ToonAdditionalLight(Light light, half3 normalWS, half3 albedo)
{
    half amount = saturate(dot(normalWS, light.direction)) * light.distanceAttenuation * light.shadowAttenuation;
    amount = saturate(amount * 1.5h);
    amount = floor(amount * _LightBands + 0.5h) / max(_LightBands, 1.0h);
    return albedo * light.color * amount;
}

half3 ToonLighting(InputData inputData, half3 albedo)
{
    half4 shadowMask = CalculateShadowMask(inputData);
    float3 pixelPositionWS = PixelSnapOnSurface(inputData.positionWS, inputData.normalWS);

    float4 shadowCoord = inputData.shadowCoord;
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && !defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    shadowCoord = TransformWorldToShadowCoord(pixelPositionWS);
#endif

    // Lumière principale : 2 tons nets (lumière / ombre teintée), ombres portées pixélisées.
    Light mainLight = GetMainLight(shadowCoord, pixelPositionWS, shadowMask);
    half ndotl = dot(inputData.normalWS, mainLight.direction);
    half lit = ToonStep(ndotl, _ShadeThreshold) * ToonStep(mainLight.shadowAttenuation, 0.5h);
    half3 color = albedo * lerp(_ShadowColor.rgb, mainLight.color, lit);
    color += albedo * inputData.bakedGI * _AmbientStrength;

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, pixelPositionWS, shadowMask);
        color += ToonAdditionalLight(light, inputData.normalWS, albedo);
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, pixelPositionWS, shadowMask);
        color += ToonAdditionalLight(light, inputData.normalWS, albedo);
    LIGHT_LOOP_END
#endif

#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    color += inputData.vertexLighting * albedo;
#endif

    return color;
}

#endif

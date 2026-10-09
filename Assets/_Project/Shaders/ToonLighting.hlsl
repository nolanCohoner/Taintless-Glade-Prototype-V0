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

// 1 : aligne les ombres/halos sur une grille de pixels monde (rendu pixel d'origine).
// 0 : pas de pixélisation (les halos de torche utilisent des polygones, voir ToonPolygonLayers).
#define TOON_PIXEL_SNAP 0

// Aligne la position sur une grille monde, en restant SUR la surface (on retire la composante normale)
// pour éviter que la surface ne s'ombre elle-même. Donne des ombres et des halos de lumière "pixel".
float3 PixelSnapOnSurface(float3 positionWS, float3 normalWS)
{
#if TOON_PIXEL_SNAP
    if (_ShadowPixelSize <= 0.001) return positionWS;
    float3 snapped = (floor(positionWS / _ShadowPixelSize) + 0.5) * _ShadowPixelSize;
    float3 offset = snapped - positionWS;
    offset -= normalWS * dot(offset, normalWS);
    return positionWS + offset;
#else
    return positionWS;
#endif
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

// 1 : halos de lumière en couches de polygones irréguliers, dont la taille suit la "Range" de la lumière.
// 0 : atténuation URP d'origine (1/d², la Range ne fait que couper au bord).
#define TOON_POLYGON_LIGHTS 1
// Nombre de sommets de chaque polygone (7 = heptagone irrégulier).
#define TOON_POLYGON_SIDES 7
// Rayon (en fraction de la Range) de la couche la plus externe et de la plus interne.
#define TOON_POLYGON_OUTER 0.92
#define TOON_POLYGON_INNER 0.38
// Irrégularité : variation du rayon des sommets (0 = polygone régulier) et de leur angle.
#define TOON_POLYGON_RADIUS_JITTER 0.22
#define TOON_POLYGON_ANGLE_JITTER 0.45

float ToonHash(float n)
{
    return frac(sin(n * 127.1) * 43758.5453);
}

float ToonCross2(float2 a, float2 b)
{
    return a.x * b.y - a.y * b.x;
}

float2 ToonPolygonVertex(int i, float seed)
{
    float angle = (i + (ToonHash(seed + i * 7.3) - 0.5) * TOON_POLYGON_ANGLE_JITTER * 2.0) * (6.2831853 / TOON_POLYGON_SIDES);
    float radius = 1.0 + (ToonHash(seed + i * 3.1 + 11.7) - 0.5) * TOON_POLYGON_RADIUS_JITTER * 2.0;
    return radius * float2(cos(angle), sin(angle));
}

// Distance (rayon unité) entre le centre et le bord du polygone dans la direction dir (normalisée).
float ToonPolygonRadius(float2 dir, float seed)
{
    float2 first = ToonPolygonVertex(0, seed);
    float2 previous = first;
    float result = 1.0;
    [unroll] for (int i = 1; i <= TOON_POLYGON_SIDES; i++)
    {
        float2 current = (i == TOON_POLYGON_SIDES) ? first : ToonPolygonVertex(i, seed);
        float2 edge = current - previous;
        float denom = ToonCross2(dir, edge);
        if (abs(denom) > 1e-5)
        {
            float t = ToonCross2(previous, edge) / denom;
            float u = ToonCross2(previous, dir) / denom;
            if (t > 0.0 && u >= 0.0 && u <= 1.0) result = t;
        }
        previous = current;
    }
    return result;
}

// Part (0..1, par paliers) des couches polygonales de la lumière qui contiennent ce point.
// Chaque couche a sa propre forme (graine différente) ; la forme dépend de la Range de la lumière, pas de sa position,
// donc elle reste stable quand la lumière se déplace (seule la direction autour de la lumière varie, de façon continue).
half ToonPolygonLayers(uint i, float3 positionWS, float3 normalWS, half fallback)
{
#if USE_CLUSTER_LIGHT_LOOP
    int perObjectIndex = i;
#else
    int perObjectIndex = GetPerObjectLightIndex(i);
#endif

#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    float4 lightPositionWS = _AdditionalLightsBuffer[perObjectIndex].position;
    half4 distanceAndSpotAttenuation = _AdditionalLightsBuffer[perObjectIndex].attenuation;
    half4 spotDirection = _AdditionalLightsBuffer[perObjectIndex].spotDirection;
#else
    float4 lightPositionWS = _AdditionalLightsPosition[perObjectIndex];
    half4 distanceAndSpotAttenuation = _AdditionalLightsAttenuation[perObjectIndex];
    half4 spotDirection = _AdditionalLightsSpotDir[perObjectIndex];
#endif

    if (lightPositionWS.w < 0.5) return fallback; // lumière directionnelle : pas de portée

    float3 toPoint = positionWS - lightPositionWS.xyz;
    float distanceSqr = max(dot(toPoint, toPoint), HALF_MIN);
    // distanceAndSpotAttenuation.x = 1 / range²  =>  t = distance / range
    float t = sqrt(distanceSqr * distanceAndSpotAttenuation.x);
    if (t >= 1.0) return 0.0h;

    // Direction autour de la lumière dans le plan de la surface (axes monde : sol, murs...).
    float3 a = abs(normalWS);
    float2 planar = (a.y >= a.x && a.y >= a.z) ? toPoint.xz : ((a.x >= a.z) ? toPoint.zy : toPoint.xy);
    float planarLength = length(planar);
    float2 dir = planarLength > 1e-4 ? planar / planarLength : float2(1.0, 0.0);

    int layers = clamp((int)round(_LightBands), 1, 6);
    // Graine tirée de la Range (stable quand la lumière bouge ; la couleur/intensité vacille avec LightFlicker, la position change).
    float baseSeed = floor(rsqrt(max(distanceAndSpotAttenuation.x, 1e-6)) * 10.0 + 0.5) * 1.713;
    int inside = 0;
    for (int k = 0; k < layers; k++)
    {
        float layerT = layers > 1 ? (float)k / (layers - 1) : 0.0;
        float radius = lerp(TOON_POLYGON_OUTER, TOON_POLYGON_INNER, layerT);
        float shape = ToonPolygonRadius(dir, baseSeed + k * 13.37);
        if (t < radius * shape) inside++;
    }

    half3 lightDirection = half3(-toPoint * rsqrt(distanceSqr));
    return (half)inside / (half)layers * AngleAttenuation(spotDirection.xyz, lightDirection, distanceAndSpotAttenuation.zw);
}

// Lumières ponctuelles (torches...) en couches nettes (polygones) : l'éclairage est constant dans chaque couche.
half3 ToonAdditionalLight(Light light, half3 normalWS, half3 albedo, half layerAmount)
{
#if TOON_POLYGON_LIGHTS
    half facing = saturate(dot(normalWS, light.direction) * 8.0h);
    // Ombre nette (toon) : on seuille l'ombre portée au lieu de la laisser en dégradé.
    half amount = facing * layerAmount * ToonStep(light.shadowAttenuation, 0.5h);
#else
    half amount = saturate(dot(normalWS, light.direction)) * layerAmount * light.shadowAttenuation;
    amount = saturate(amount * 1.5h);
    amount = floor(amount * _LightBands + 0.5h) / max(_LightBands, 1.0h);
#endif
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
        half attenuation = light.distanceAttenuation;
        #if TOON_POLYGON_LIGHTS
        attenuation = ToonPolygonLayers(lightIndex, pixelPositionWS, inputData.normalWS, attenuation);
        #endif
        color += ToonAdditionalLight(light, inputData.normalWS, albedo, attenuation);
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, pixelPositionWS, shadowMask);
        half attenuation = light.distanceAttenuation;
        #if TOON_POLYGON_LIGHTS
        attenuation = ToonPolygonLayers(lightIndex, pixelPositionWS, inputData.normalWS, attenuation);
        #endif
        color += ToonAdditionalLight(light, inputData.normalWS, albedo, attenuation);
    LIGHT_LOOP_END
#endif

#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    color += inputData.vertexLighting * albedo;
#endif

    return color;
}

#endif

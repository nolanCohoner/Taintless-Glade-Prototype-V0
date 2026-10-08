#ifndef TAINTLESS_PIXEL_DISSOLVE_INCLUDED
#define TAINTLESS_PIXEL_DISSOLVE_INCLUDED

// Pilotées par FadeableWall via MaterialPropertyBlock (hors CBUFFER volontairement).
float  _Dissolve;
float  _PixelSize;
float  _TopBias;
float  _KeepHeight;
float  _WallHeight;
float4 _EdgeColor;
float  _EdgeWidth;

float PD_Hash13(float3 p3)
{
    p3 = frac(p3 * 0.1031);
    p3 += dot(p3, p3.zyx + 31.32);
    return frac((p3.x + p3.y) * p3.z);
}

// Découpe le mur en "pixels" (cellules cubiques en espace monde) qui disparaissent
// selon un bruit + un biais du haut vers le bas. Retourne la couleur de bordure (émissive).
float3 PixelDissolve(float3 positionWS)
{
    if (_Dissolve <= 0.0001 || positionWS.y < _KeepHeight)
        return 0;

    float  pixelSize = max(_PixelSize, 0.01);
    float3 cell      = floor(positionWS / pixelSize);
    float  noise     = PD_Hash13(cell);
    float  height01  = saturate(((cell.y + 0.5) * pixelSize) / max(_WallHeight, 0.01));
    float  key       = lerp(noise, 1.0 - height01, _TopBias);

    float threshold = _Dissolve * (1.0 + _EdgeWidth);
    clip(key - (threshold - _EdgeWidth));
    return key < threshold ? _EdgeColor.rgb : 0;
}

// Coordonnées UV en espace monde (box mapping) pour que la texture ne s'étire pas sur des cubes mis à l'échelle.
float2 PD_WorldUV(float3 positionWS, float3 normalWS)
{
    float3 n = abs(normalWS);
    if (n.y > 0.5) return positionWS.xz;
    if (n.x > 0.5) return positionWS.zy;
    return positionWS.xy;
}

#endif

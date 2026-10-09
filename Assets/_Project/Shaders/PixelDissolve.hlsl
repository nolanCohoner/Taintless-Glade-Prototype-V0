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

// 1 : le mur se découpe en polygones irréguliers (cellules de Voronoï fixes dans le monde).
// 0 : cellules cubiques ("pixels") d'origine.
#define PD_POLYGON_CELLS 1
// Taille moyenne d'un polygone = _PixelSize * ce facteur.
#define PD_CELL_SCALE 1.6
// Irrégularité des polygones (0 = cellules régulières en grille, 1 = très irrégulières).
#define PD_CELL_JITTER 0.9

float3 PD_Hash33(float3 p3)
{
    p3 = frac(p3 * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yxz + 33.33);
    return frac((p3.xxy + p3.yxx) * p3.zyx);
}

// Cellule de Voronoï 3D contenant p (en unités de cellule) : renvoie son identifiant et son point central.
void PD_Voronoi(float3 p, out float3 cellId, out float3 cellCenter)
{
    float3 baseCell = floor(p);
    float  best     = 1e9;
    cellId = baseCell;
    cellCenter = baseCell + 0.5;

    [unroll] for (int z = -1; z <= 1; z++)
    {
        [unroll] for (int y = -1; y <= 1; y++)
        {
            [unroll] for (int x = -1; x <= 1; x++)
            {
                float3 id    = baseCell + float3(x, y, z);
                float3 seedPoint = id + 0.5 + (PD_Hash33(id) - 0.5) * PD_CELL_JITTER;
                float3 delta = p - seedPoint;
                float  d     = dot(delta, delta);
                if (d < best)
                {
                    best = d;
                    cellId = id;
                    cellCenter = seedPoint;
                }
            }
        }
    }
}

// Découpe le mur en polygones (ou en "pixels" cubiques si PD_POLYGON_CELLS = 0) qui disparaissent
// selon un bruit + un biais du haut vers le bas. Retourne la couleur de bordure (émissive).
float3 PixelDissolve(float3 positionWS)
{
    if (_Dissolve <= 0.0001 || positionWS.y < _KeepHeight)
        return 0;

#if PD_POLYGON_CELLS
    float  cellSize = max(_PixelSize, 0.01) * PD_CELL_SCALE;
    float3 cellId, cellCenter;
    PD_Voronoi(positionWS / cellSize, cellId, cellCenter);
    float  noise    = PD_Hash13(cellId);
    float  height01 = saturate((cellCenter.y * cellSize) / max(_WallHeight, 0.01));
#else
    float  pixelSize = max(_PixelSize, 0.01);
    float3 cell      = floor(positionWS / pixelSize);
    float  noise     = PD_Hash13(cell);
    float  height01  = saturate(((cell.y + 0.5) * pixelSize) / max(_WallHeight, 0.01));
#endif
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

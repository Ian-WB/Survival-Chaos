// The island's rock (environment roadmap, item 43). The one Custom Function node of
// IslandRock.shadergraph calls IslandRock_float, and everything the rock is, is here.
//
// Until 8 October the island was four flat colours: the base, its darker rock, the cone
// and the cone's tip. They are still its colours - each material keeps its own as Tint -
// and this adds what a flat colour cannot say:
//
// - Fine relief, from a detail sheet laid on from three sides by the world's own axes, so
//   the meshes need no UV work and a pattern never stretches down a cliff. The faces stay
//   flat: the relief only tilts the light within a face.
// - Colour by how a face lies. Level ground is dusted with ash, cliffs go towards dark
//   basalt, and whatever looks down is darkest.
// - Cracks, which are dark when cold and glow near the lava. How near is read from a map
//   of the island seen from above (Heat), painted from the lava's own mesh. The glow
//   grows with the eruption: Eruption sets _IslandEruption, 0 at rest to 1.
//
// The detail sheet is not a normal map as Unity imports one, so that all four channels
// are free: red and green are the relief's slope, blue is how deep in a crack a point is,
// alpha is a slow light-and-dark. IslandRockBuilder paints it and the heat map.

#ifndef ISLAND_ROCK_INCLUDED
#define ISLAND_ROCK_INCLUDED

// How far the eruption has got. Eruption sets it each frame of a run; left unset it is 0.
float _IslandEruption;

// How much the relief tilts the light.
#define ISLAND_RELIEF 0.9

// Ash, and how much of it lies on level ground.
#define ISLAND_ASH float3(0.36, 0.31, 0.30)
#define ISLAND_ASH_COVER 0.55

// What a cliff's colour is multiplied by, and an underside's.
#define ISLAND_BASALT float3(0.82, 0.80, 0.86)
#define ISLAND_UNDERSIDE float3(0.6, 0.59, 0.66)

// The colour a crack glows, the lava's own hot colour.
#define ISLAND_HOT float3(1.0, 0.38, 0.08)

void IslandRock_float(
    float3 PositionWS, float3 NormalWS, float3 TangentWS, float3 BitangentWS, float3 Tint,
    UnityTexture2D Detail, UnityTexture2D Heat, float3 HeatBox, float Tile, float Glow,
    out float3 BaseColor, out float3 NormalTS, out float3 Emission, out float Smoothness)
{
    float3 normal = normalize(NormalWS);

    // How much of each of the three sides a face shows.
    float3 side = pow(abs(normal), 4.0);
    side /= side.x + side.y + side.z;

    float4 fromX = SAMPLE_TEXTURE2D(Detail.tex, Detail.samplerstate, PositionWS.zy * Tile);
    float4 fromY = SAMPLE_TEXTURE2D(Detail.tex, Detail.samplerstate, PositionWS.xz * Tile);
    float4 fromZ = SAMPLE_TEXTURE2D(Detail.tex, Detail.samplerstate, PositionWS.xy * Tile);

    float2 slopeX = fromX.xy * 2.0 - 1.0;
    float2 slopeY = fromY.xy * 2.0 - 1.0;
    float2 slopeZ = fromZ.xy * 2.0 - 1.0;

    float3 tilt = side.x * float3(0.0, slopeX.y, slopeX.x)
                + side.y * float3(slopeY.x, 0.0, slopeY.y)
                + side.z * float3(slopeZ.x, slopeZ.y, 0.0);
    float3 relief = normalize(normal + ISLAND_RELIEF * tilt);

    NormalTS = normalize(float3(dot(relief, normalize(TangentWS)), dot(relief, normalize(BitangentWS)), dot(relief, normal)));

    float crack = dot(side, float3(fromX.b, fromY.b, fromZ.b));
    float tone = dot(side, float3(fromX.a, fromY.a, fromZ.a));

    float level = smoothstep(0.78, 0.96, normal.y);
    float cliff = 1.0 - smoothstep(0.25, 0.7, abs(normal.y));
    float under = smoothstep(0.25, 0.7, -normal.y);

    float3 rock = Tint * lerp(0.8, 1.15, tone);
    rock *= lerp(float3(1.0, 1.0, 1.0), ISLAND_BASALT, cliff);
    rock *= lerp(float3(1.0, 1.0, 1.0), ISLAND_UNDERSIDE, under);
    rock = lerp(rock, ISLAND_ASH * lerp(0.85, 1.1, tone), level * ISLAND_ASH_COVER);
    rock *= lerp(1.0, 0.5, crack);
    BaseColor = rock;

    // The heat map is the island from above: red is how near the lava a point is.
    float2 over = (PositionWS.xz - HeatBox.xy) / HeatBox.z + 0.5;
    float heat = SAMPLE_TEXTURE2D(Heat.tex, Heat.samplerstate, over).r;
    // Not on what looks down: the map cannot tell the rock under the lava from the rock beside it.
    heat *= smoothstep(-0.35, 0.1, normal.y);

    // Only the heart of a wide crack glows, and not every crack: the slow light-and-dark
    // picks which. Lit edge to edge and all of them, the cone wore a net of light.
    float vein = smoothstep(0.7, 1.0, crack) * smoothstep(0.35, 0.75, tone);

    float erupting = saturate(_IslandEruption);
    // The eruption both brightens the cracks and lights ones further from the lava.
    float reach = saturate(heat * lerp(1.0, 1.5, erupting));
    Emission = ISLAND_HOT * (Glow * lerp(1.0, 2.5, erupting) * vein * reach * reach * reach);

    Smoothness = lerp(0.3, 0.12, level) * (1.0 - 0.6 * crack);
}

#endif

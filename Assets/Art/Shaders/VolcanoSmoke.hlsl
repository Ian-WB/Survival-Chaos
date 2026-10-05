// The volcano's smoke, for VolcanoSmoke.shadergraph on a Local Volumetric Fog.
//
// UV is the fog box, 0 to 1 along each of its sides, with y up from the crater.
// The plume is a cone that opens as it climbs and bends downwind; its edge is
// pushed in and out by billows that rise, and that grow with the cone, so they
// spread as they climb the way smoke does.
//
// Only big billows. The fog is drawn into a coarse grid and blended over
// several frames, and anything finer than a few of its cells does not show as
// detail: it shows as flicker, which is what the first version of this, a
// scrolling texture with fine noise in it, did.

#ifndef VOLCANO_SMOKE_INCLUDED
#define VOLCANO_SMOKE_INCLUDED

float VolcanoSmokeLattice(float3 p)
{
    return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
}

// Value noise, 0 to 1.
float VolcanoSmokeNoise(float3 p)
{
    float3 cell = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float near = lerp(
        lerp(VolcanoSmokeLattice(cell), VolcanoSmokeLattice(cell + float3(1, 0, 0)), f.x),
        lerp(VolcanoSmokeLattice(cell + float3(0, 1, 0)), VolcanoSmokeLattice(cell + float3(1, 1, 0)), f.x), f.y);
    float far = lerp(
        lerp(VolcanoSmokeLattice(cell + float3(0, 0, 1)), VolcanoSmokeLattice(cell + float3(1, 0, 1)), f.x),
        lerp(VolcanoSmokeLattice(cell + float3(0, 1, 1)), VolcanoSmokeLattice(cell + float3(1, 1, 1)), f.x), f.y);
    return lerp(near, far, f.z);
}

void VolcanoSmoke_float(float3 UV, float T, out float3 Color, out float Alpha)
{
    float h = saturate(UV.y);

    // The middle of the plume, carried downwind as it climbs.
    float2 middle = float2(0.5 + 0.20 * h * h, 0.5);

    // Half its width, as a share of the box: narrow at the mouth, opening fast
    // and then slower.
    float halfWidth = lerp(0.055, 0.21, pow(h, 0.75));

    float2 across = (UV.xz - middle) / halfWidth;
    float distance = length(across);

    // Billows, in the plume's own shape: across it they are measured in
    // plume-widths, so they open with it, and they climb.
    float3 at = float3(across.x * 1.1, h * 5.0 - T * 0.22, across.y * 1.1);
    float billow = 0.65 * VolcanoSmokeNoise(at) + 0.35 * VolcanoSmokeNoise(at * 2.0 + 17.0);

    // A soft edge, moved by the billows: half a plume-width of fade, so the
    // edge is wide enough for the fog's grid to hold still.
    float body = 1.0 - smoothstep(0.5, 1.0, distance + (billow - 0.5) * 1.2);

    // Thicker inside a billow than between two, so they show in the middle
    // of the plume as well as along its edge.
    body *= lerp(0.55, 1.25, billow);

    // Thick at the mouth, thinning as it spreads, and gone before the box ends.
    float thinning = lerp(1.0, 0.40, h);
    float ends = smoothstep(0.0, 0.03, h) * (1.0 - smoothstep(0.72, 1.0, h));

    Color = float3(1.0, 1.0, 1.0);
    Alpha = saturate(body * thinning * ends);
}

void VolcanoSmoke_half(half3 UV, half T, out half3 Color, out half Alpha)
{
    float3 colour;
    float alpha;
    VolcanoSmoke_float(UV, T, colour, alpha);
    Color = colour;
    Alpha = alpha;
}

#endif

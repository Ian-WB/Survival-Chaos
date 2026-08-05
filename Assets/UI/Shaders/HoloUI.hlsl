#ifndef SURVIVAL_CHAOS_HOLO_UI_INCLUDED
#define SURVIVAL_CHAOS_HOLO_UI_INCLUDED

// Shared drawing for the holographic interface.
//
// Everything here works in pixels, not UV. A UI element's mesh carries its own
// size in TEXCOORD1 (written by HoloRectData), so a wide health bar and a small
// square skill slot get identical corner cuts and border widths. Doing this in
// UV space is the usual reason procedural UI looks stretched on wide elements.
//
// Nothing samples a texture. The interface has no source art, so it cannot go
// soft on a high resolution display - which is what was wrong with the old one.

// Antialiased fill for a signed distance, negative inside.
// fwidth gives the distance covered by one screen pixel, so the edge stays one
// pixel wide however far the canvas has been scaled up.
float HoloFill(float distance)
{
    float aa = max(fwidth(distance), 1e-5);
    return saturate(0.5 - distance / aa);
}

// Antialiased band centred on distance == 0, "thickness" wide.
float HoloBand(float distance, float thickness)
{
    return HoloFill(abs(distance) - thickness * 0.5);
}

// Signed distance to a rectangle with its corners cut off at 45 degrees.
// The angular cut is what reads as machined hardware rather than a soft app
// panel; a rounded box here made the whole thing look like a phone dialog.
float HoloChamferBox(float2 p, float2 halfSize, float chamfer)
{
    p = abs(p);
    float2 q = p - halfSize;
    float box = max(q.x, q.y);
    // Diagonal half-plane through both corner cuts. 0.7071 = 1/sqrt(2), which
    // keeps the returned value a true distance rather than a skewed one.
    float cut = (p.x + p.y - (halfSize.x + halfSize.y - chamfer)) * 0.70710678;
    return max(box, cut);
}

// L-shaped marks at the four corners, drawn only near the frame.
// Returns coverage, not a distance, because the two arms are combined.
float HoloCornerBrackets(float2 p, float2 halfSize, float arm, float thickness)
{
    // Distance from this pixel to the nearest edge on each axis.
    float2 toEdge = halfSize - abs(p);

    // Horizontal arm: hugging the top or bottom edge, and within "arm" of a side.
    float across = HoloFill(toEdge.y - thickness) * HoloFill(toEdge.x - arm);
    // Vertical arm: the same rotated a quarter turn.
    float down = HoloFill(toEdge.x - thickness) * HoloFill(toEdge.y - arm);

    // Inside the frame only, so the arms do not bleed outside the panel.
    float inside = HoloFill(max(-toEdge.x, -toEdge.y));
    return saturate(across + down) * inside;
}

// Horizontal scanlines drifting upward. Cheap, and the thing that most sells a
// projected image - a static gradient reads as flat plastic.
float HoloScanlines(float2 p, float density, float speed, float time)
{
    float lines = sin((p.y * density - time * speed) * 6.2831853);
    // Bias toward dark so the lines sit under the artwork rather than over it.
    return saturate(lines * 0.5 + 0.5);
}

// Cheap value noise, used for the irregular part of the flicker.
float HoloNoise(float2 p)
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

// Brightness wobble. Two out-of-phase sines plus a little noise, so the pattern
// does not visibly repeat the way a single sine does.
float HoloFlicker(float time, float amount)
{
    float wobble = sin(time * 11.0) * 0.5 + sin(time * 27.3) * 0.3;
    float grain = HoloNoise(float2(floor(time * 24.0), 0.0)) - 0.5;
    return 1.0 - amount * saturate(wobble * 0.25 + grain * 0.5 + 0.35);
}

// A bright line sweeping up the element once per cycle, used when a panel
// appears. Returns 0 outside the sweep.
float HoloSweep(float2 p, float2 halfSize, float progress, float width)
{
    float y = (p.y + halfSize.y) / max(halfSize.y * 2.0, 1e-5);
    return HoloFill(abs(y - progress) * max(halfSize.y * 2.0, 1e-5) - width);
}

#endif

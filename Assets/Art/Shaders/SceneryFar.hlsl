#ifndef SCENERY_FAR_INCLUDED
#define SCENERY_FAR_INCLUDED

// The far trees and ruins (environment roadmap, item 47, 8 October 2026).
//
// On High and Ultra the trees and ruins on the far side of the island crawled as the view
// turned: twigs and window bars under a pixel wide, drawn in some frames and not in others,
// against bright cloud. Widening them by a pixel was tried on 26 September and changed
// nothing that could be seen. This leaves them out instead.
//
// Every vertex of the scenery carries how thick the mesh is there, in the fourth UV set,
// written at import (SceneryThinParts, from ThinParts). A vertex whose thickness is under
// Pixels pixels at its distance from the camera is given no position at all, and a triangle
// with such a corner is not drawn. So a tree loses its twigs from the tips inward as it gets
// further away, and what is left is at least a pixel wide.
//
// Shadows and the bake keep every triangle: a shadow is cast from the light's place, not the
// camera's, and the lightmap must not depend on where the camera stood.
void SceneryFar_float(float3 PositionOS, float4 Thickness, float Pixels, out float3 Position)
{
    Position = PositionOS;

#if !defined(SHADERGRAPH_PREVIEW)
#if !(defined(SHADERPASS) && (SHADERPASS == SHADERPASS_SHADOWS || SHADERPASS == SHADERPASS_LIGHT_TRANSPORT))
    // A mesh that was never measured has no thickness, and is drawn whole.
    if (Thickness.x > 0.0 && Pixels > 0.0)
    {
        float3 fromCamera = TransformObjectToWorld(PositionOS);
#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING == 0)
        fromCamera -= _WorldSpaceCameraPos;
#endif
        // The size of a pixel at this distance: the screen's height in the world there, over
        // its height in pixels. The screen size is the one being rendered at, before an
        // upscaler, which is the size the crawl happens at.
        float pixel = length(fromCamera) * 2.0 / (abs(UNITY_MATRIX_P._m11) * _ScreenSize.y);

        if (Thickness.x < Pixels * pixel)
        {
            float none = asfloat(0x7fc00000);
            Position = float3(none, none, none);
        }
    }
#endif
#endif
}

#endif

// A soft glow hugging the screen's edges, for the low-hull vignette and the
// hit-direction flare (17 November 2026 HUD redesign). Drawn on a full-screen
// UI Image: colour and strength come from the Image's colour, so the script
// driving it only ever sets one value.
//
// _Sides weights the four edges (x left, y right, z bottom, w top). _Reach is
// how far in the glow comes, as a fraction of the screen on that axis. _Centre
// and _Spread narrow it along the edge to a tapered band around one point
// (0 to 1 along the edge); a spread of 0 keeps the whole edge lit.
Shader "Survival Chaos/Edge Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Sides ("Edges (left, right, bottom, top)", Vector) = (1,1,1,1)
        _Reach ("Reach (fraction of screen)", Range(0.01, 0.5)) = 0.08
        _Falloff ("Falloff power", Range(0.5, 4)) = 1.6
        _Centre ("Band centre along the edge", Range(0, 1)) = 0.5
        _Spread ("Band half-length (0 = whole edge)", Range(0, 1)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        ColorMask [_ColorMask]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float4 _Sides;
            float _Reach;
            float _Falloff;
            float _Centre;
            float _Spread;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float Edge(float distance, float along)
            {
                // Smooth at both ends: no hard inner line where the glow stops.
                float glow = pow(1.0 - smoothstep(0.0, _Reach, distance), _Falloff);
                if (_Spread > 0.0)
                {
                    float t = saturate(abs(along - _Centre) / _Spread);
                    glow *= 1.0 - t * t * (3.0 - 2.0 * t);
                }
                return glow;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // Edges combined as light through four filters rather than by
                // the strongest one, so corners blend instead of meeting in a
                // diagonal seam.
                float clear = 1.0;
                clear *= 1.0 - _Sides.x * Edge(uv.x, uv.y);
                clear *= 1.0 - _Sides.y * Edge(1.0 - uv.x, uv.y);
                clear *= 1.0 - _Sides.z * Edge(uv.y, uv.x);
                clear *= 1.0 - _Sides.w * Edge(1.0 - uv.y, uv.x);

                fixed4 colour = i.color;
                colour.a *= 1.0 - clear;
                return colour;
            }
            ENDCG
        }
    }
}

Shader "Survival Chaos/Holo Panel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Holo)]
        _FillColor ("Fill", Color) = (0.05, 0.5, 0.65, 0.35)
        _EdgeColor ("Edge", Color) = (0.5, 0.95, 1.0, 1.0)
        _Chamfer ("Corner cut (px)", Float) = 14
        _Border ("Border width (px)", Float) = 2
        _BracketArm ("Bracket length (px)", Float) = 26
        _BracketWidth ("Bracket width (px)", Float) = 4
        _Inset ("Inset (px)", Float) = 1

        [Header(Projection)]
        _ScanDensity ("Scanline density", Float) = 0.35
        _ScanSpeed ("Scanline speed", Float) = 1.5
        _ScanStrength ("Scanline strength", Range(0,1)) = 0.18
        _FlickerAmount ("Flicker", Range(0,1)) = 0.05
        _Glow ("Edge glow", Range(0,4)) = 1.6

        // Set from script when a panel appears; 1 means fully settled.
        _Sweep ("Boot sweep", Range(0,1)) = 1
        _SweepWidth ("Boot sweep width (px)", Float) = 40

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
            "CanUseSpriteAtlas" = "True"
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

        // Premultiplied alpha. Lets one pass carry both a solid translucent fill
        // and edges that add light on top of the scene, which is what makes the
        // frame read as projected rather than painted on.
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "HoloUI.hlsl"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 size : TEXCOORD1;   // element size in pixels, from HoloRectData
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 size : TEXCOORD1;
                float4 world : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _FillColor;
            fixed4 _EdgeColor;
            float _Chamfer;
            float _Border;
            float _BracketArm;
            float _BracketWidth;
            float _Inset;
            float _ScanDensity;
            float _ScanSpeed;
            float _ScanStrength;
            float _FlickerAmount;
            float _Glow;
            float _Sweep;
            float _SweepWidth;
            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.world = v.vertex;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.size = v.size;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Fall back to something sane if the canvas is not supplying
                // TEXCOORD1, so a misconfigured panel is still visible.
                float2 size = max(i.size, float2(1.0, 1.0));
                float2 halfSize = size * 0.5;

                // Pixel-space position relative to the element's centre.
                float2 p = (i.uv - 0.5) * size;

                float2 inner = max(halfSize - _Inset, 1.0);
                float d = HoloChamferBox(p, inner, _Chamfer);

                float body = HoloFill(d);
                float edge = HoloBand(d, _Border);
                float brackets = HoloCornerBrackets(p, inner, _BracketArm, _BracketWidth);

                float scan = HoloScanlines(p, _ScanDensity, _ScanSpeed, _Time.y);
                float flicker = HoloFlicker(_Time.y, _FlickerAmount);

                // Fill: dimmed by the scanlines so the surface has structure.
                float3 fill = _FillColor.rgb * lerp(1.0 - _ScanStrength, 1.0, scan);
                float fillAlpha = _FillColor.a * body;

                // Line work: bracket marks ride at full strength, the frame is
                // slightly softer so the corners read as the accent.
                float lines = saturate(max(edge * 0.85, brackets));

                // A panel that has just appeared gets a bright line running up it.
                float sweep = _Sweep < 1.0
                    ? HoloSweep(p, inner, _Sweep, _SweepWidth) * body
                    : 0.0;

                float3 lit = _EdgeColor.rgb * (lines + sweep) * _Glow * _EdgeColor.a;

                float alpha = saturate(fillAlpha + lines * _EdgeColor.a + sweep);
                alpha *= i.color.a * flicker;

                // Premultiplied: colour is already scaled by coverage, so the
                // edges add light instead of just tinting what is behind them.
                float3 rgb = (fill * fillAlpha + lit) * i.color.rgb * flicker;

                #ifdef UNITY_UI_CLIP_RECT
                float clip = UnityGet2DClipping(i.world.xy, _ClipRect);
                rgb *= clip;
                alpha *= clip;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}

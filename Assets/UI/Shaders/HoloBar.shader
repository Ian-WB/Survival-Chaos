Shader "Survival Chaos/Holo Bar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Values)]
        // Both are driven from script. _Ghost lags behind _Fill after damage so
        // the amount lost is visible for a moment instead of vanishing.
        _Fill ("Fill", Range(0,1)) = 1
        _Ghost ("Ghost", Range(0,1)) = 1

        [Header(Holo)]
        _TrackColor ("Track", Color) = (0.04, 0.18, 0.24, 0.55)
        _FillColor ("Fill colour", Color) = (0.35, 0.9, 1.0, 1.0)
        _GhostColor ("Ghost colour", Color) = (1.0, 0.35, 0.35, 1.0)
        _EdgeColor ("Edge", Color) = (0.6, 0.97, 1.0, 1.0)
        _Chamfer ("Corner cut (px)", Float) = 8
        _Border ("Border width (px)", Float) = 2
        _Inset ("Fill inset (px)", Float) = 4
        _Segments ("Segments", Float) = 12
        _TickWidth ("Tick width (px)", Float) = 2
        _HeadWidth ("Leading edge (px)", Float) = 3

        [Header(Projection)]
        _ScanDensity ("Scanline density", Float) = 0.5
        _ScanSpeed ("Scanline speed", Float) = 2
        _ScanStrength ("Scanline strength", Range(0,1)) = 0.22
        _Glow ("Glow", Range(0,4)) = 1.8
        _Pulse ("Low pulse", Range(0,1)) = 0

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
                float2 size : TEXCOORD1;
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
            fixed4 _TrackColor;
            fixed4 _FillColor;
            fixed4 _GhostColor;
            fixed4 _EdgeColor;
            float _Fill;
            float _Ghost;
            float _Chamfer;
            float _Border;
            float _Inset;
            float _Segments;
            float _TickWidth;
            float _HeadWidth;
            float _ScanDensity;
            float _ScanSpeed;
            float _ScanStrength;
            float _Glow;
            float _Pulse;
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
                float2 size = max(i.size, float2(1.0, 1.0));
                float2 halfSize = size * 0.5;
                float2 p = (i.uv - 0.5) * size;

                // Outer frame, and the recessed channel the fill sits in.
                float dFrame = HoloChamferBox(p, halfSize - 1.0, _Chamfer);
                float2 innerHalf = max(halfSize - _Inset, 1.0);
                float dTrack = HoloChamferBox(p, innerHalf, max(_Chamfer - _Inset, 0.0));

                float frame = HoloBand(dFrame, _Border);
                float track = HoloFill(dTrack);

                // Fill measured along the channel, not the whole element, so the
                // bar reaches its ends exactly rather than under the frame.
                float channel = innerHalf.x * 2.0;
                float x = (p.x + innerHalf.x) / max(channel, 1e-5);

                float fillMask = HoloFill((x - _Fill) * channel) * track;
                float ghostMask = HoloFill((x - _Ghost) * channel) * track;
                // Only the stretch between the two, so it shows what was lost.
                float lostMask = saturate(ghostMask - fillMask);

                // Ticks divide the channel so a length reads as a quantity.
                float ticks = 0.0;
                if (_Segments >= 1.0)
                {
                    float slot = frac(x * _Segments);
                    float toTick = min(slot, 1.0 - slot) * channel / _Segments;
                    ticks = HoloFill(toTick - _TickWidth * 0.5) * track;
                }

                // Bright head where the fill currently ends.
                float head = HoloBand((x - _Fill) * channel, _HeadWidth) * track;

                float scan = HoloScanlines(p, _ScanDensity, _ScanSpeed, _Time.y);
                float scanned = lerp(1.0 - _ScanStrength, 1.0, scan);

                // Urgency pulse, driven from script when the value is low.
                float pulse = 1.0 + _Pulse * (sin(_Time.y * 8.0) * 0.5 + 0.5);

                float3 rgb = 0.0;
                float alpha = 0.0;

                // Empty channel.
                float trackAlpha = _TrackColor.a * track * (1.0 - ghostMask);
                rgb += _TrackColor.rgb * trackAlpha;
                alpha += trackAlpha;

                // What was just lost.
                float lostAlpha = _GhostColor.a * lostMask;
                rgb += _GhostColor.rgb * lostAlpha * _Glow;
                alpha += lostAlpha;

                // Current value.
                float valueAlpha = _FillColor.a * fillMask;
                rgb += _FillColor.rgb * valueAlpha * scanned * pulse;
                alpha += valueAlpha;

                // Line work sits over everything.
                float lineAlpha = _EdgeColor.a * saturate(max(frame, max(head, ticks * 0.45)));
                rgb += _EdgeColor.rgb * lineAlpha * _Glow * pulse;
                alpha += lineAlpha;

                rgb *= i.color.rgb;
                alpha = saturate(alpha) * i.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                float clipping = UnityGet2DClipping(i.world.xy, _ClipRect);
                rgb *= clipping;
                alpha *= clipping;
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

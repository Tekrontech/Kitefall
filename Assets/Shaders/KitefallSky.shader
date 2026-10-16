// Kitefall gradient sky (upgrade spec 0.1 / 12): zenith → horizon gradient, sun disc with glow,
// soft animated clouds and a warm golden-hour band. Original shader, works as a skybox in URP and Built-in.
Shader "Kitefall/GradientSky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.24, 0.47, 0.85, 1)
        _HorizonColor ("Horizon", Color) = (0.86, 0.9, 0.95, 1)
        _GroundColor ("Below horizon", Color) = (0.55, 0.52, 0.48, 1)
        _GoldenColor ("Golden band", Color) = (1, 0.7, 0.4, 1)
        _Golden ("Golden hour (0-1)", Range(0, 1)) = 0.3
        _SunDir ("Sun direction (world, towards sun)", Vector) = (0.3, 0.5, 0.8, 0)
        _SunColor ("Sun colour", Color) = (1, 0.95, 0.85, 1)
        _SunSize ("Sun size", Range(0.0005, 0.02)) = 0.004
        _SunGlow ("Sun glow", Range(0, 4)) = 1.2
        _CloudColor ("Cloud colour", Color) = (1, 1, 1, 1)
        _CloudCover ("Cloud cover", Range(0, 1)) = 0.45
        _CloudSpeed ("Cloud speed", Range(0, 0.1)) = 0.008
        _CloudScale ("Cloud scale", Range(0.5, 8)) = 2.6
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ZenithColor, _HorizonColor, _GroundColor, _GoldenColor, _SunColor, _CloudColor;
            float4 _SunDir;
            float _Golden, _SunSize, _SunGlow, _CloudCover, _CloudSpeed, _CloudScale;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash (float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float noise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i), b = hash(i + float2(1, 0)), c = hash(i + float2(0, 1)), d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm (float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 4; k++) { v += a * noise(p); p *= 2.03; a *= 0.5; }
                return v;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                // Sky gradient with a slightly wider horizon band.
                float up = saturate(pow(saturate(h), 0.45));
                float3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, up);
                float3 sun = normalize(_SunDir.xyz);
                float sunDot = saturate(dot(d, sun));
                // Golden-hour warmth near the horizon and around the sun.
                float band = exp(-abs(h) * 6.0) * _Golden;
                col = lerp(col, _GoldenColor.rgb, band * (0.55 + 0.45 * pow(sunDot, 4.0)));
                if (h < 0.0) col = lerp(col, _GroundColor.rgb, saturate(-h * 6.0));

                // Clouds on a virtual dome.
                if (h > 0.01)
                {
                    float2 uv = d.xz / (h + 0.15) * _CloudScale + _Time.y * _CloudSpeed * float2(1.0, 0.35);
                    float n = fbm(uv);
                    float c = smoothstep(1.0 - _CloudCover, 1.0 - _CloudCover + 0.35, n);
                    float lit = 0.75 + 0.25 * saturate(fbm(uv + sun.xz * 0.4) * 1.4);
                    float3 cloud = _CloudColor.rgb * lit + _GoldenColor.rgb * band * 0.6 + _SunColor.rgb * pow(sunDot, 12.0) * 0.4;
                    col = lerp(col, cloud, c * saturate(h * 8.0) * 0.92);
                }

                // Sun disc + glow.
                float disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.6, sunDot);
                col += _SunColor.rgb * (disc * 4.0 + pow(sunDot, 64.0) * 0.6 * _SunGlow + pow(sunDot, 8.0) * 0.15 * _SunGlow);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}

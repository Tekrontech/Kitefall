// Kitefall kite paper / fabric (upgrade spec 0.1): soft lambert light, translucency when the sun is behind the sail,
// gentle vertex flutter from the wind and a thin rim. Unlit pass (no pipeline dependencies) – light comes from
// globals set by the game (_KF_SunDir, _KF_SunColor, _KF_Ambient), so it looks the same in URP and Built-in.
Shader "Kitefall/KitePaper"
{
    Properties
    {
        _MainTex ("Pattern", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Translucency ("Translucency", Range(0, 2)) = 0.9
        _Flutter ("Flutter", Range(0, 0.3)) = 0.06
        _FlutterSpeed ("Flutter speed", Range(0, 30)) = 11
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _BaseColor;
            float _Translucency, _Flutter, _FlutterSpeed;
            float4 _KF_SunDir, _KF_SunColor, _KF_Ambient;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            v2f vert (appdata v)
            {
                v2f o;
                // Flutter grows toward the sail edges (distance from the spine).
                float edge = saturate(abs(v.vertex.x) * 1.5 + max(0.0, -v.vertex.y) * 0.6);
                float w = sin(_Time.y * _FlutterSpeed + v.vertex.y * 6.0 + v.vertex.x * 4.0) * _Flutter * edge;
                v.vertex.xyz += v.normal * w;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _BaseColor;
                float3 n = normalize(i.normal) * (facing > 0 ? 1.0 : -1.0);
                float3 sun = normalize(_KF_SunDir.xyz + float3(0, 0.0001, 0));
                float3 sunCol = _KF_SunColor.a > 0 ? _KF_SunColor.rgb : float3(1, 0.96, 0.9);
                float3 amb = _KF_Ambient.a > 0 ? _KF_Ambient.rgb : float3(0.55, 0.6, 0.7);
                float3 view = normalize(_WorldSpaceCameraPos - i.worldPos);
                float lambert = saturate(dot(n, sun));
                // Light through the paper: strongest when the sun is behind the sail as seen by the camera.
                float through = saturate(dot(-view, sun)) * saturate(dot(-n, sun) + 0.35);
                float rim = pow(1.0 - saturate(dot(n, view)), 3.0) * 0.25;
                float3 col = albedo.rgb * (amb + sunCol * lambert) + albedo.rgb * sunCol * through * _Translucency + rim * sunCol;
                fixed4 o = fixed4(col, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, o);
                return o;
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}

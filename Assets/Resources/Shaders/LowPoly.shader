// Flat-shaded low-poly look: colour comes from mesh vertex colours, so one material serves every model
// and GPU instancing can batch all copies of a mesh. Vertex alpha below 1 makes that part glow
// (emission = rgb * (1 - alpha)), used for lights, tracers and missile tips.
// Stylised lighting: wrapped diffuse (soft terminator), a cool rim light, a per-instance hit flash
// (_Flash, set through a MaterialPropertyBlock) and scene fog for depth.
Shader "Corehold/LowPoly"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Wrap addshadow fullforwardshadows
        #pragma multi_compile_instancing
        #pragma target 3.0

        struct Input
        {
            float4 color : COLOR;
            float3 viewDir;
        };

        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float, _Flash)
        UNITY_INSTANCING_BUFFER_END(Props)

        half4 LightingWrap(SurfaceOutput s, half3 lightDir, half atten)
        {
            half ndl = dot(s.Normal, lightDir);
            half diff = saturate(ndl * 0.7 + 0.3);
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * diff * atten;
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo = IN.color.rgb;
            half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            half flash = UNITY_ACCESS_INSTANCED_PROP(Props, _Flash);
            o.Emission = IN.color.rgb * (1.0 - IN.color.a)
                       + half3(0.55, 0.65, 0.8) * (rim * rim * rim * 0.22)
                       + flash.xxx * 0.7;
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

// Flat-shaded low-poly look: colour comes from mesh vertex colours, so one material serves every model
// and GPU instancing can batch all copies of a mesh. Vertex alpha below 1 makes that part glow
// (emission = rgb * (1 - alpha)), used for lights, tracers and missile tips.
Shader "Corehold/LowPoly"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert addshadow fullforwardshadows
        #pragma multi_compile_instancing
        #pragma target 3.0

        struct Input
        {
            float4 color : COLOR;
        };

        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo = IN.color.rgb;
            o.Emission = IN.color.rgb * (1.0 - IN.color.a);
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

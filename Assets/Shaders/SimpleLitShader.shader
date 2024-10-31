Shader "Custom/SimpleLitShader"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1) // Base color
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL; // Normal for lighting
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed3 worldNormal : TEXCOORD0; // Transformed normal
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                v2f o;
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                // Transform vertex position to clip space
                o.pos = UnityObjectToClipPos(v.vertex);

                // Transform normal to world space
                o.worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));

                // Access the per-instance color
                fixed4 instanceColor = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                o.color = instanceColor;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // Calculate lighting based on the world normal and the light source
                fixed3 lightDir = fixed3(0, 0, -1);
                fixed3 normal = normalize(i.worldNormal);

                // Diffuse Lambertian shading
                float diff = max(dot(normal, lightDir), 0);

                // Apply lighting to the base color
                fixed4 shadedColor = i.color * diff;

                // Add ambient lighting to ensure visibility
                float ambient = 0.2;
                shadedColor.rgb += i.color.rgb * ambient;

                // Debug: Show normal direction in color (to visualize)
                // Uncomment the line below to visualize normals
                //shadedColor.rgb = (normal * 0.5) + 0.5;

                return shadedColor;
            }
            ENDCG
        }
    }
}

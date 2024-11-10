Shader "Custom/FunctionShader"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1) // Default color for instances
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            // Enable instancing
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Include instance ID for instancing
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Include instance ID for the fragment shader
            };

            // Declare the per-instance color property
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color) // Define color as an instanced property
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v); // Set up instance ID for this vertex

                v2f o;
                UNITY_TRANSFER_INSTANCE_ID(v, o); // Transfer instance ID for fragment shader access

                // Transform the vertex position
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; // Pass UV coordinates to the fragment shader

                // Access the per-instance color property
                o.color = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);

                return o;
            }

            // Define the function that takes both x and y as inputs
            float ExampleFunction(float2 pos)
            {
                return cos(pos.y - 3 + sin(pos.x));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Map UV coordinates to a range from -1 to 1
                float2 pos = 5.0*i.uv * 2.0 - 1.0;

                // Calculate the function's value at the pixel's position
                float functionValue = ExampleFunction(pos);

                // Map the function value to a grayscale color for display
                // Scale and clamp the result to be in the range [0, 1]
                float intensity = saturate(functionValue * 0.5 + 0.5);

                // Apply the intensity to the instance color
                fixed4 color = i.color * intensity;

                return color;
            }
            ENDCG
        }
    }
}

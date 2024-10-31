Shader "Custom/SimpleVertexShader"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1) // Base color
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
                UNITY_VERTEX_INPUT_INSTANCE_ID // Include instance ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Include instance ID for per-instance properties
            };

            // Declare the per-instance color property
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                v2f o;
                UNITY_TRANSFER_INSTANCE_ID(v, o); // Transfer instance ID to v2f

                // Transform vertex position using the transformation matrix
                o.pos = UnityObjectToClipPos(v.vertex);

                // Access the per-instance color
                fixed4 instanceColor = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                o.color = instanceColor;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i); // Set up instance ID in fragment shader if needed

                return i.color;
            }
            ENDCG
        }
    }
}

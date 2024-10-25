Shader "Custom/SimpleVertexShader"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1) // Color of the object
        _Radius ("Radius", Float) = 1.0        // Radius to scale the object
        _Position ("Position Offset", Vector) = (0, 0, 0, 0) // Position offset
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
            };

            fixed4 _Color;
            float _Radius;
            float4 _Position;

            v2f vert (appdata v)
            {
                v2f o;

                // Scale the vertex position by the radius and apply position offset
                v.vertex.xy *= _Radius;
                v.vertex.xyz += _Position.xyz;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}
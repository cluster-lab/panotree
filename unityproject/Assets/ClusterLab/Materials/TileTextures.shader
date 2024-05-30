// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Unlit/TileTextures"
{
    Properties
    {
        [MainTexture]_TexArray ("Tex", 2DArray) = "" {}
        _RowCol("RowCol", Float) = 2
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma require 2darray
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };


            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            float _RowCol;
            UNITY_DECLARE_TEX2DARRAY(_TexArray);

            half4 frag (v2f i) : SV_Target
            {
                float axisLength = _RowCol;
                float2 uvOffset;
                float3 uv = float3(i.uv.xy, 0);
                float ix = floor(uv.x * axisLength);
                float iy = _RowCol - 1 - floor(uv.y * axisLength);
                float textureIndex = iy * axisLength + ix;
                uv.xy = uv.xy;
                uv.z = textureIndex;
                return UNITY_SAMPLE_TEX2DARRAY(_TexArray, uv);
            }
            ENDCG
        }
    }
}
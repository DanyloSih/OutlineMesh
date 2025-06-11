Shader "Custom/OutlineMesh"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _DotThreshold("Dot Threshold", Range(0,1)) = 0.5
        _NormalMultiplier("Normal Multiplier", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #define _USE_UNITY_INSTANCING
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos    : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _DotThreshold;
                float _NormalMultiplier;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float3 posWS = TransformObjectToWorld(IN.positionOS);
                OUT.worldPos = posWS;
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.worldNormal = normalWS * _NormalMultiplier;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - IN.worldPos);
                float d = (dot(viewDir, normalize(IN.worldNormal)) + 1) / 2;
                half4 col = half4(_Color.rgb, d);

                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
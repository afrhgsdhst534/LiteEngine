Shader "Custom/SaneToon"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _ShadowColor("Shadow Color", Color) = (0.5, 0.5, 0.6, 1)
        _ShadowThreshold("Shadow Threshold", Range(-1, 1)) = 0.0
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("Outline Width", Float) = 0.02
    }

        SubShader
        {
            Tags
            {
                "RenderType" = "Opaque"
                "RenderPipeline" = "UniversalPipeline"
                "Queue" = "Geometry"
            }

            // PASS 1: OUTLINE
            Pass
            {
                Name "Outline"
                Tags { "LightMode" = "SRPDefaultUnlit" }
                Cull Front
                ZWrite On

                HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma multi_compile_instancing

                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

                CBUFFER_START(UnityPerMaterial)
                    float4 _MainTex_ST;
                    half4 _BaseColor;
                    half4 _ShadowColor;
                    half _ShadowThreshold;
                    half4 _OutlineColor;
                    float _OutlineWidth;
                CBUFFER_END

                struct Attributes
                {
                    float4 positionOS : POSITION;
                    float3 normalOS   : NORMAL;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };

                struct Varyings
                {
                    float4 positionHCS : SV_POSITION;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };

                Varyings vert(Attributes IN)
                {
                    Varyings OUT;
                    UNITY_SETUP_INSTANCE_ID(IN);
                    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                    float3 pos = IN.positionOS.xyz + (IN.normalOS * _OutlineWidth);
                    OUT.positionHCS = TransformObjectToHClip(pos);
                    return OUT;
                }

                half4 frag(Varyings IN) : SV_Target
                {
                    return _OutlineColor;
                }
                ENDHLSL
            }

            // PASS 2: FORWARD LIT WITH SHADOWS
            Pass
            {
                Name "ForwardLit"
                Tags { "LightMode" = "UniversalForward" }
                Cull Back
                ZWrite On

                HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma multi_compile_instancing

                #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
                #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
                #pragma multi_compile _ _SHADOWS_SOFT

                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

                CBUFFER_START(UnityPerMaterial)
                    float4 _MainTex_ST;
                    half4 _BaseColor;
                    half4 _ShadowColor;
                    half _ShadowThreshold;
                    half4 _OutlineColor;
                    float _OutlineWidth;
                CBUFFER_END

                TEXTURE2D(_MainTex);
                SAMPLER(sampler_MainTex);

                struct Attributes
                {
                    float4 positionOS : POSITION;
                    float3 normalOS   : NORMAL;
                    float2 uv         : TEXCOORD0;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };

                struct Varyings
                {
                    float4 positionHCS : SV_POSITION;
                    float3 normalWS    : TEXCOORD0;
                    float2 uv          : TEXCOORD1;
                    float4 shadowCoord : TEXCOORD2;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };

                Varyings vert(Attributes IN)
                {
                    Varyings OUT;
                    UNITY_SETUP_INSTANCE_ID(IN);
                    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                    VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);

                    OUT.positionHCS = posInputs.positionCS;
                    OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                    OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                    OUT.shadowCoord = GetShadowCoord(posInputs);

                    return OUT;
                }

                half4 frag(Varyings IN) : SV_Target
                {
                    half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _BaseColor;

                    Light mainLight = GetMainLight(IN.shadowCoord);

                    half3 normalWS = normalize(IN.normalWS);
                    half NdotL = dot(normalWS, mainLight.direction);

                    half lit = step(_ShadowThreshold, NdotL);

                    // shadowAttenuation = 0 в тени, 1 на свету
                    half lightAmount = lit * mainLight.shadowAttenuation;

                    half3 shadowCol = texColor.rgb * _ShadowColor.rgb;
                    half3 lightCol = texColor.rgb * mainLight.color;

                    half3 finalColor = lerp(shadowCol, lightCol, lightAmount);

                    return half4(finalColor, texColor.a);
                }
                ENDHLSL
            }

                    // PASS 3: CAST SHADOWS
                    UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        }
}
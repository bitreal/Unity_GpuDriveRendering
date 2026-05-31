Shader "Unlit/GDD_Shader"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM

            #include "GDD.hlsl"
            
            #pragma target 4.5
            
            #pragma vertex Vertex
            #pragma fragment Fragment
            
            float4x4 unity_MatrixVP;

            struct Attributes
            {
                uint vertexId : SV_VertexID;
                uint instanceId : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                half3 color : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;

                VertexData vertexData = _VertexBuffer[_IndexBuffer[input.vertexId]];
                InstanceData instanceData = _InstanceBuffer[_InstanceMappingBuffer[_InstanceOffset + input.instanceId]];
                float3 positionWS = vertexData.PositionOS.xyz * 50.0 + instanceData.PositionWS.xyz;
                output.positionCS = mul(unity_MatrixVP, float4(positionWS, 1.0));
                output.color = instanceData.Color.rgb * (dot(vertexData.NormalOS.xyz, half3(0.0, 1.0, 0.0)) * 0.5 + 0.5);
                
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                return half4(input.color, 1.0);
            }
            
            ENDHLSL
        }
    }
}

#ifndef GDD_INCLUDED
#define GDD_INCLUDED

struct VertexData
{
    float4 PositionOS;
    float4 NormalOS;
    //float2 Uv;
};
StructuredBuffer<VertexData> _VertexBuffer;
StructuredBuffer<uint> _IndexBuffer;

struct InstanceData
{
    float4 PositionWS;
    float4 Color;
};
StructuredBuffer<InstanceData> _InstanceBuffer;
StructuredBuffer<uint> _InstanceMappingBuffer;

struct DrawCall
{
    uint vertexCount;
    uint instanceCount;
    uint firstVertex;
    uint instanceLocation;
};
RWStructuredBuffer<DrawCall> _DrawCalls;

#endif
using Unity.Mathematics;
using UnityEngine;

public struct MeshInfo
{
    public uint FirstIndex;
    public uint Indexes;
    public uint FirstVertex;
    public uint Vertexes;

    public const int Size = 4 * 4;
}

public struct Obj
{
    public ushort PositionX;
    public ushort PositionY;
    public ushort PositionZ;
    public ushort MeshIdx;
}

public struct RawObject
{
    // 0 (4)
    public uint PositionXZ;
    // 4 (4)
    public uint PositionY_MeshIdx;

    public const int Size = 4 + 4;

    public static RawObject Pack(Obj obj)
    {
        RawObject rawObject = new RawObject();
        rawObject.PositionXZ = obj.PositionX | (uint)(obj.PositionZ << 16);
        rawObject.PositionY_MeshIdx = obj.PositionY | (uint)(obj.MeshIdx << 16);
        return rawObject;
    }

    public static uint ColorToUint(Color color)
    {
        uint uintColor = 0;
        uintColor = ((uint)(color.r * 255f) << 0) | ((uint)(color.g * 255f) << 8) | ((uint)(color.b * 255f) << 16) | ((uint)(color.a * 255f) << 24);
        return uintColor;
    }

    public static uint TwoUshortsToUint(ushort ush1, ushort ush2)
    {
        return ush1 | ((uint)ush2 << 16);
    }

    public static ushort FloatToUshort(float f, float min, float max)
    {
        return (ushort)(Mathf.InverseLerp(min, max, f) * ushort.MaxValue);
    }
}

public struct VertexData
{
    public float4 PositionOS;
    public float4 NormalOS;
    //public float2 Uv;

    public const int Size = 4 * 4 + 4 * 4;// + 4 * 2;
}

public struct DrawCall
{
    public uint VertexCount;
    public uint InstanceCount;
    public uint FirstVertex;
    public uint InstanceLocation;

    public const int Size = 4 * 4;
}

public struct InstanceData
{
    public float4 PositionWS;
    public float4 Color;

    public const int Size = 4 * 4 + 4 * 4;
}

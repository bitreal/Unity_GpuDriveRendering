using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
public class GpuDriverRenderingSettings
{
    [SerializeField]
    private ComputeShader _computeShader;
    [SerializeField]
    private Material _material;

    public ComputeShader ComputeShader => _computeShader;
    public Material Material => _material;
}

public class GpuDriverRenderingFeature : ScriptableRendererFeature
{
    [SerializeField]
    private GpuDriverRenderingSettings _settings;

    private GpuDriverRenderingPass _pass;
    
    public override void Create()
    {
        _pass = new GpuDriverRenderingPass(_settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        _pass.Init();
        renderer.EnqueuePass(_pass);
    }

    private void OnDisable()
    {
        if(_pass != null)
        {
            _pass.Deinit();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if(_pass != null)
        {
            _pass.Deinit();
        }
    }

    protected void OnDestroy()
    {
        if(_pass != null)
        {
            _pass.Deinit();
        }
    }
}

public class GpuDriverRenderingPass : ScriptableRenderPass
{
    public class GpuDriverRenderingPassStats
    {
        // buffers' sizes
        public int MeshInfoBufferBytes;
        public int VertexBufferBytes;
        public int IndexBufferBytes;
        public int ObjectsBufferBytes;
        public int CameraPlanesBufferBytes;
        public int InstanceDataBufferBytes;
        public int InstanceMappingBufferBytes;
        public int DrawCallBufferBytes;
    
        // meshes
        public int Meshes;
        public int Vertexes;
        public int Indexes;
        
        // scene
        public int Objects;
        public int VertexesOnScene;
    }
    
    private readonly GpuDriverRenderingSettings _settings;

    private int _cleanKernel;
    private int _cullKernel;

    private ComputeBuffer _meshInfoBuffer;
    private ComputeBuffer _vertexBuffer;
    private ComputeBuffer _indexBuffer;
    
    private Plane[] _cameraPlanes;
    private float4[] _cameraPlanesF4;
    private ComputeBuffer _cameraPlanesBuffer;

    private ComputeBuffer _objectsBuffer;
    private ComputeBuffer _instanceDataBuffer;
    private ComputeBuffer _instanceMappingBuffer;
    
    private ComputeBuffer _drawCallBuffer;

    private Material _material;

    private const int MAX_MESHES = 1000;
    private const int MAX_VERTEXES = 5_000;
    private const int MAX_INDEXES = MAX_VERTEXES * 3;
    private const int MAX_VERTEXES_PER_MESH = 10_000;
    private const int MAX_INDEXES_PER_MESH = MAX_VERTEXES_PER_MESH * 3;
    private const int MAX_OBJECTS = 1_500_000;
    private const int MAX_INSTANCES_PER_DRAW_CALL = 1023;  // unity can't more

    public static GpuDriverRenderingPass Instance { get; private set; }
    public GpuDriverRenderingPassStats Stats { get; } = new GpuDriverRenderingPassStats();

    public GpuDriverRenderingPass(GpuDriverRenderingSettings settings)
    {
        renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        _settings = settings;
    }

    private bool _inited;
    public bool Init()
    {
        if(_inited)
        {
            return true;
        }

        if(_settings == null || _settings.ComputeShader == null || _settings.Material == null)
        {
            return false;
        }
            
        // --- MESH buffers ---
        
        _meshInfoBuffer = new ComputeBuffer(MAX_MESHES, MeshInfo.Size);
        _vertexBuffer = new ComputeBuffer(MAX_VERTEXES, VertexData.Size);
        _indexBuffer = new ComputeBuffer(MAX_INDEXES, 4);
        
        // --- OBJECT and INSTANCE buffers ---

        _objectsBuffer = new ComputeBuffer(MAX_OBJECTS, RawObject.Size);
        _instanceDataBuffer = new ComputeBuffer(MAX_OBJECTS, InstanceData.Size);
        _instanceMappingBuffer = new ComputeBuffer(MAX_MESHES * MAX_INSTANCES_PER_DRAW_CALL, 4);
        
        // --- DRAW CALL buffer ---

        _drawCallBuffer = new ComputeBuffer(MAX_MESHES, DrawCall.Size, ComputeBufferType.IndirectArguments);

        // --- CAMERA PLANES buffer ---

        var planes = 6;
        _cameraPlanes = new Plane[planes];
        _cameraPlanesF4 = new float4[planes];
        _cameraPlanesBuffer = new ComputeBuffer(planes, 4 * 4);
        
        // --- kernels ---

        _cleanKernel = _settings.ComputeShader.FindKernel("Clean");
        _cullKernel = _settings.ComputeShader.FindKernel("Cull");
        
        // --- compute shader globals ---
        
        _settings.ComputeShader.SetInt("_MaxInstancesPerDrawCall", MAX_INSTANCES_PER_DRAW_CALL);
        
        _settings.ComputeShader.SetBuffer(_cleanKernel, "_MeshInfoBuffer", _meshInfoBuffer);
        _settings.ComputeShader.SetInt("_MeshCount", _meshCount);
        _settings.ComputeShader.SetBuffer(_cleanKernel, "_DrawCalls", _drawCallBuffer);
        
        _settings.ComputeShader.SetBuffer(_cullKernel, "_Objects", _objectsBuffer);
        _settings.ComputeShader.SetInt("_ObjectCount", _lastObjectIdx + 1);
        _settings.ComputeShader.SetBuffer(_cullKernel, "_DrawCalls", _drawCallBuffer);
        _settings.ComputeShader.SetBuffer(_cullKernel, "_CameraPlanes", _cameraPlanesBuffer);
        _settings.ComputeShader.SetBuffer(_cullKernel, "_InstanceMappingBuffer", _instanceMappingBuffer);
        
        // --- vertex/fragment shader globals ---
        
        //Shader.SetGlobalBuffer("_InstanceBuffer", _instanceDataBuffer);
        //Shader.SetGlobalBuffer("_InstanceMappingBuffer", _instanceMappingBuffer);
        //Shader.SetGlobalBuffer("_VertexBuffer", _vertexBuffer);
        //Shader.SetGlobalBuffer("_IndexBuffer", _indexBuffer);
        _material = new Material(_settings.Material);
        _material.SetBuffer("_InstanceBuffer", _instanceDataBuffer);
        _material.SetBuffer("_InstanceMappingBuffer", _instanceMappingBuffer);
        _material.SetBuffer("_VertexBuffer", _vertexBuffer);
        _material.SetBuffer("_IndexBuffer", _indexBuffer);
        //_material.SetBuffer("_DrawCallBuffer", _drawCallBuffer);
        
        //
        
        _inited = true;
        Debug.Log("Inited!");

        Instance = this;

        return true;
    }

    public void Deinit()
    {
        if (!_inited)
            return;

        _inited = false;
        Debug.Log("Deinited!");

        if (Instance == this)
            Instance = null;
        
        _meshInfoBuffer.Dispose();
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
        _objectsBuffer.Dispose();
        _instanceDataBuffer.Dispose();
        _instanceMappingBuffer.Dispose();
        _drawCallBuffer.Dispose();
        _cameraPlanesBuffer.Dispose();
    }

    private MeshInfo[] _meshesInfo = new MeshInfo[MAX_MESHES];
    private int _meshCount;
    
    private MeshInfo[] _newMeshesInfo = new MeshInfo[MAX_MESHES];
    private int _newMeshCount;

    private List<Vector3> _tmpMeshVertexes = new List<Vector3>(MAX_VERTEXES_PER_MESH);
    private List<Vector3> _tmpMeshNormals = new List<Vector3>(MAX_VERTEXES_PER_MESH);
    private List<Vector2> _tmpMeshUv = new List<Vector2>(MAX_VERTEXES_PER_MESH);
    private List<int> _tmpMeshTriangles = new List<int>(MAX_INDEXES_PER_MESH);

    private int _vertexesCount;
    private VertexData[] _newVertexes = new VertexData[MAX_VERTEXES];
    private int _newVertexesCount;

    private int _indexesCount;
    private uint[] _newIndexes = new uint[MAX_INDEXES];
    private int _newIndexesCount;

    public ushort AddMesh(Mesh mesh)
    {
        mesh.GetVertices(_tmpMeshVertexes);
        mesh.GetNormals(_tmpMeshNormals);
        mesh.GetUVs(0, _tmpMeshUv);
        mesh.GetTriangles(_tmpMeshTriangles, 0);

        // add mesh info
        MeshInfo lastMeshInfo = default(MeshInfo);
        if(_newMeshCount > 0)
        {
            lastMeshInfo = _newMeshesInfo[_newMeshCount - 1];
        }
        else if(_meshCount > 0)
        {
            lastMeshInfo = _meshesInfo[_meshCount - 1];
        }
        else
        {
            // it's a first mesh
        }
        var firstIndex = lastMeshInfo.FirstIndex + lastMeshInfo.Indexes;
        var firstVertex = lastMeshInfo.FirstVertex + lastMeshInfo.Vertexes;
        _newMeshesInfo[_newMeshCount] = new MeshInfo()
        {
            Indexes = (uint)_tmpMeshTriangles.Count,
            Vertexes = (uint)_tmpMeshVertexes.Count,
            FirstIndex = firstIndex,
            FirstVertex = firstVertex,
        };
        var meshIdx = (ushort)(_meshCount + _newMeshCount);
        _newMeshCount++;

        // add vertexes
        for(var i = 0; i < _tmpMeshVertexes.Count; i++)
        {
            _newVertexes[_newVertexesCount + i] = new VertexData()
            {
                PositionOS = V3ConvertToF4(_tmpMeshVertexes[i], 1.0f),
                NormalOS = V3ConvertToF4(_tmpMeshNormals[i], 0f),
                //Uv = _tmpMeshUv[i],
            };
        }
        _newVertexesCount += _tmpMeshVertexes.Count;

        // add indexes
        for(var i = 0; i < _tmpMeshTriangles.Count; i++)
        {
            _newIndexes[_newIndexesCount + i] = (uint)(firstVertex + _tmpMeshTriangles[i]);
        }
        _newIndexesCount += _tmpMeshTriangles.Count;

        return meshIdx;
    }

    private RawObject[] _objects = new RawObject[MAX_OBJECTS];
    private InstanceData[] _instances = new InstanceData[MAX_OBJECTS];
    private int _objectsCount;
    private int _firstEmptyObjectIdx;
    private bool _firstEmptyObjectInvalid;
    private int _lastObjectIdx = -1;
    private bool _lastObjectIdxInvalid;
    
    private int[] _dirtyObjects = new int[MAX_OBJECTS];
    private int _dirtyObjectsCount;

    public uint AddObject(ushort meshIdx, InstanceData instanceData)
    {
        if(_firstEmptyObjectInvalid)
        {
            _firstEmptyObjectIdx = FindFirstEmptyObject();
            _firstEmptyObjectInvalid = false;
        }
        var objIdx = _firstEmptyObjectIdx;
        
        int3 positionForCulling = new int3(
            (int)instanceData.PositionWS.x, 
            (int)instanceData.PositionWS.y, 
            (int)instanceData.PositionWS.z);
        var newObj = new Obj()
        {
            PositionX = (ushort)positionForCulling.x,
            PositionY = (ushort)positionForCulling.y,
            PositionZ = (ushort)positionForCulling.z,
            MeshIdx = (ushort)(meshIdx + 1),
        };
        _objects[objIdx] = RawObject.Pack(newObj);
        _instances[objIdx] = instanceData;

        _dirtyObjects[_dirtyObjectsCount] = objIdx;
        _dirtyObjectsCount++;

        if(objIdx <= _firstEmptyObjectIdx)
        {
            _firstEmptyObjectInvalid = true;
        }

        if(objIdx > _lastObjectIdx)
        {
            _lastObjectIdx = objIdx;
            _lastObjectIdxInvalid = false;
        }

        _objectsCount++;

        return (uint)objIdx;
    }
    
    private static readonly RawObject _emptyRawObj = RawObject.Pack(new Obj() { MeshIdx = 0});

    public void RemoveObject(uint objIdx)
    {
        _objects[objIdx] = _emptyRawObj;

        _dirtyObjects[_dirtyObjectsCount] = (int)objIdx;
        _dirtyObjectsCount++;

        if(objIdx < _firstEmptyObjectIdx)
        {
            _firstEmptyObjectIdx = (int)objIdx;
            _firstEmptyObjectInvalid = false;
        }

        if(objIdx >= _lastObjectIdx)
        {
            _lastObjectIdxInvalid = true;
        }
        
        _objectsCount--;
    }

    public void Reset()
    {
        _meshCount = 0;
        _newMeshCount = 0;
        _vertexesCount = 0;
        _newVertexesCount = 0;
        _indexesCount = 0;
        _newIndexesCount = 0;
        _settings.ComputeShader.SetInt("_MeshCount", _meshCount);

        for(var i = 0; i < MAX_OBJECTS; i++)
        {
            _objects[i].PositionY_MeshIdx = 0;
        }
        _objectsCount = 0;
        _firstEmptyObjectIdx = 0;
        _firstEmptyObjectInvalid = false;
        _lastObjectIdx = -1;
        _lastObjectIdxInvalid = false;
        _dirtyObjectsCount = 0;
        _settings.ComputeShader.SetInt("_ObjectCount", _lastObjectIdx + 1);
    }

    private int FindLastNotEmptyObject()
    {
        if(_objectsCount <= 0)
            return -1;
        
        for(var i = _lastObjectIdx - 1; i >= 0; i--)
        {
            if((_objects[i].PositionY_MeshIdx & 0xFFFF0000) != 0)
                return i;
        }

        return -1;  // impossible
    }

    private int FindFirstEmptyObject()
    {
        for(var i = _firstEmptyObjectIdx + 1; i < MAX_OBJECTS; i++)
        {
            if((_objects[i].PositionY_MeshIdx & 0xFFFF0000) == 0)
                return i;
        }

        return -1;
    }
    
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (!_inited)
            return;

        new Plane(Vector3.zero, Vector3.zero, Vector3.zero);
        
        var cameraType = renderingData.cameraData.cameraType;
        if(cameraType != CameraType.Game && cameraType != CameraType.SceneView)
        {
            return;
        }
        
        // add meshes
        
        var cmdBuffer = CommandBufferPool.Get();

        if(_newMeshCount > 0)
        {
            cmdBuffer.SetComputeBufferData(_meshInfoBuffer, _newMeshesInfo, 0, _meshCount, _newMeshCount);
            Array.Copy(_newMeshesInfo, 0, _meshesInfo, _meshCount, _newMeshCount);
            _meshCount += _newMeshCount;
            cmdBuffer.SetComputeIntParam(_settings.ComputeShader, "_MeshCount", _meshCount);
            _newMeshCount = 0;
        
            cmdBuffer.SetComputeBufferData(_vertexBuffer, _newVertexes, 0, _vertexesCount, _newVertexesCount);
            _vertexesCount += _newVertexesCount;
            _newVertexesCount = 0;
        
            cmdBuffer.SetComputeBufferData(_indexBuffer, _newIndexes, 0, _indexesCount, _newIndexesCount);
            _indexesCount += _newIndexesCount;
            _newIndexesCount = 0;
        }
        
        // add objects

        if(_dirtyObjectsCount > 0)
        {
            Profiler.BeginSample("_dirtyObjects");
            Profiler.BeginSample("Sort");
            Array.Sort(_dirtyObjects, 0, _dirtyObjectsCount);
            Profiler.EndSample();
            var firstObjIdx = _dirtyObjects[0];
            var objCount = 1;
            for(var i = 0; i < _dirtyObjectsCount; i++)
            {
                var idx = _dirtyObjects[i];
                var nextIdx = -1;
                var isItLast = (i + 1) >= _dirtyObjectsCount;
                if(!isItLast)
                {
                    nextIdx = _dirtyObjects[i + 1];
                }
                if(nextIdx == idx)
                {
                    continue;
                }
                var isThenGap = (idx + 1) != nextIdx;
                if(isItLast || isThenGap)
                {
                    Profiler.BeginSample("SetComputeBufferData");
                    cmdBuffer.SetComputeBufferData(_objectsBuffer, _objects, firstObjIdx, firstObjIdx, objCount);
                    cmdBuffer.SetComputeBufferData(_instanceDataBuffer, _instances, firstObjIdx, firstObjIdx, objCount);
                    Profiler.EndSample();
                    firstObjIdx = nextIdx;
                }
                else
                {
                    objCount++;
                }
            }
            _dirtyObjectsCount = 0;

            if(_lastObjectIdxInvalid)
            {
                _lastObjectIdx = FindLastNotEmptyObject();
                _lastObjectIdxInvalid = false;
            }
            
            cmdBuffer.SetComputeIntParam(_settings.ComputeShader, "_ObjectCount", _lastObjectIdx + 1);
            
            Profiler.EndSample();
        }
        
        // render

        if(_lastObjectIdx >= 0)
        {
            // clean
            {
                int xGroups = Mathf.CeilToInt(_meshCount / 64f);
                cmdBuffer.DispatchCompute(_settings.ComputeShader, _cleanKernel, xGroups, 1, 1);
                //_drawCallBuffer.GetData(_drawCalls);
            }
        
            // frustum culling
            {
                GeometryUtility.CalculateFrustumPlanes(renderingData.cameraData.camera, _cameraPlanes);
                for(var i = 0; i < 6; i++)
                {
                    _cameraPlanesF4[i] = PlaneToF4(_cameraPlanes[i]);
                }
                cmdBuffer.SetComputeBufferData(_cameraPlanesBuffer, _cameraPlanesF4);
            
                int xGroups = Mathf.CeilToInt((_lastObjectIdx + 1) / 64f);
                cmdBuffer.DispatchCompute(_settings.ComputeShader, _cullKernel, xGroups, 1, 1);
                //_drawCallBuffer.GetData(_drawCalls);
                //_instanceDataBuffer.GetData(_instanceData);
            }

            // render
            for(var i = 0; i < _meshCount; i++)
            {
                var argsOffset = i * DrawCall.Size;
                cmdBuffer.DrawProceduralIndirect(Matrix4x4.identity, _material, 0, MeshTopology.Triangles, _drawCallBuffer, argsOffset);
            }
        }
        
        context.ExecuteCommandBuffer(cmdBuffer);
        CommandBufferPool.Release(cmdBuffer);
        
        // ===

        if(cameraType == CameraType.Game)
        {
            Stats.MeshInfoBufferBytes = _meshInfoBuffer.count * _meshInfoBuffer.stride;
            Stats.VertexBufferBytes = _vertexBuffer.count * _vertexBuffer.stride;
            Stats.IndexBufferBytes = _indexBuffer.count * _indexBuffer.stride;
            Stats.ObjectsBufferBytes = _objectsBuffer.count * _objectsBuffer.stride;
            Stats.CameraPlanesBufferBytes = _cameraPlanesBuffer.count * _cameraPlanesBuffer.stride;
            Stats.InstanceDataBufferBytes = _instanceDataBuffer.count * _instanceDataBuffer.stride;
            Stats.InstanceMappingBufferBytes = _instanceMappingBuffer.count * _instanceMappingBuffer.stride;
            Stats.DrawCallBufferBytes = _drawCallBuffer.count * _drawCallBuffer.stride;
            
            Stats.Meshes = _meshCount;
            Stats.Vertexes = _vertexesCount;
            Stats.Indexes = _indexesCount;
            
            Stats.Objects = _objectsCount;
            if(_meshCount > 0)
            {
                Stats.VertexesOnScene = (int)_meshesInfo[0].Vertexes * _objectsCount;
            }
            else
            {
                Stats.VertexesOnScene = 0;
            }
        }
    }

    private DrawCall[] _drawCalls = new DrawCall[MAX_MESHES];

    private static float4 V3ConvertToF4(Vector3 v3, float w)
    {
        return new float4(v3.x, v3.y, v3.z, w);
    }

    private static float4 ColorToF4(Color c)
    {
        return new float4(c.r, c.g, c.b, c.a);
    }

    private static float4 PlaneToF4(Plane plane)
    {
        return new float4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
    }
}

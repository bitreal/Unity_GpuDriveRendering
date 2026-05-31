using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class Chunks : MonoBehaviour
{
    [SerializeField]
    private Transform _player;
    [SerializeField]
    private int2 _chunksAround;
    [SerializeField]
    private int2 _chunkSize;
    private int2 _playerChunk;
    private int2 _chunksCount;
    private int2[,] _chunks;
    private List<uint>[,] _chunkObjects;

    [SerializeField]
    private Mesh _mesh;
    [SerializeField]
    private int _meshes;
    [SerializeField]
    private int _height;

    private IEnumerator Start()
    {
        for(var i = 0; i < 3; i++)
        {
            yield return null;
        }
        Init();
    }

    private bool _inited;
    private void Init()
    {
        LoadMeshes();
        
        _chunksCount = _chunksAround * 2 + new int2(1, 1);
        _chunks = new int2[_chunksCount.x, _chunksCount.y];
        _chunkObjects = new List<uint>[_chunksCount.x, _chunksCount.y];
        for(var x = 0; x < _chunksCount.x; x++)
        {
            for(var y = 0; y < _chunksCount.y; y++)
            {
                _chunkObjects[x, y] = new List<uint>();
            }
        }
        
        _playerChunk = CalcPlayerChunk();
        var min = _playerChunk - _chunksAround;
        var max = _playerChunk + _chunksAround;
        for(var x = min.x; x <= max.x; x++)
        {
            for(var y = min.y; y <= max.y; y++)
            {
                var pos = new int2(x, y);
                var idx = GetChunkIdx(pos);
                _chunks[idx.x, idx.y] = pos;
                LoadChunk(pos, idx);
            }
        }

        _inited = true;
    }

    private void LoadMeshes()
    {
        var gdd = GpuDriverRenderingPass.Instance;
        for(var i = 0; i < _meshes; i++)
        {
            gdd.AddMesh(_mesh);
        }
    }

    private int2 GetChunkIdx(int2 chunkPos)
    {
        var idx = chunkPos % _chunksCount;
        if(idx.x < 0)
            idx.x += _chunksCount.x;
        if(idx.y < 0)
            idx.y += _chunksCount.y;
        return idx;
    }

    private int2 CalcPlayerChunk()
    {
        var pos = _player.position;
        return new int2((int)pos.x / _chunkSize.x, (int)pos.z / _chunkSize.y);
    }

    private void Update()
    {
        if (!_inited)
            return;
        
        var playerChunk = CalcPlayerChunk();
        if(playerChunk.x == _playerChunk.x && playerChunk.y == _playerChunk.y)
        {
            return;
        }
        
        _playerChunk = CalcPlayerChunk();
        var min = _playerChunk - _chunksAround;
        var max = _playerChunk + _chunksAround;
        for(var x = min.x; x <= max.x; x++)
        {
            for(var y = min.y; y <= max.y; y++)
            {
                var pos = new int2(x, y);
                var idx = GetChunkIdx(pos);
                var prevPos = _chunks[idx.x, idx.y];
                if(prevPos.x == pos.x && prevPos.y == pos.y)
                {
                    continue;
                }
                ReleaseChunk(prevPos, idx);
                _chunks[idx.x, idx.y] = pos;
                LoadChunk(pos, idx);
            }
        }
    }

    private void LoadChunk(int2 pos, int2 idx)
    {
        var gdd = GpuDriverRenderingPass.Instance;

        var posWs = pos * _chunkSize;

        for(var x = 0; x < _chunkSize.x; x++)
        {
            for(var z = 0; z < _chunkSize.y; z++)
            {
                var h = Random.Range(1, _height);
                for(var y = 0; y < h; y++)
                {
                    var positionWS = new int3(posWs.x + x, y, posWs.y + z);
                    var color = new float4(Random.value, Random.value, Random.value, 1.0f);
                    var meshIdx = (ushort)Random.Range(0, _meshes);
                    var objIdx = gdd.AddObject(meshIdx, new InstanceData()
                    {
                        Color = color,
                        PositionWS = new float4(positionWS.x, positionWS.y, positionWS.z, 1f),
                    });
                    _chunkObjects[idx.x, idx.y].Add(objIdx);
                }
            }
        }
    }

    private void ReleaseChunk(int2 pos, int2 idx)
    {
        var gdd = GpuDriverRenderingPass.Instance;
        
        foreach(var objIdx in _chunkObjects[idx.x, idx.y])
        {
            gdd.RemoveObject(objIdx);
        }
        _chunkObjects[idx.x, idx.y].Clear();
    }
    
    //
}

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class Test : MonoBehaviour
{
    [SerializeField]
    private Mesh _mesh;
    [SerializeField]
    private int _meshes;
    [SerializeField]
    private int3 _gridSize;
    [SerializeField]
    private Transform _cameraRig;

    private IEnumerator Start()
    {
        for(var i = 0; i < 3; i++)
        {
            yield return null;
        }
        LoadMeshes();
        AddObjects();
    }

    [ContextMenu("LoadZeroChunk")]
    private void LoadZeroChunk()
    {
        ResetGDD();
        LoadMeshes();
        AddObjects();
    }

    [ContextMenu("ResetGDD")]
    private void ResetGDD()
    {
        var gdd = GpuDriverRenderingPass.Instance;
        gdd.Reset();
    }

    private void LoadMeshes()
    {
        var gdd = GpuDriverRenderingPass.Instance;
        for(var i = 0; i < _meshes; i++)
        {
            gdd.AddMesh(_mesh);
        }
    }

    private void AddObjects()
    {
        var gdd = GpuDriverRenderingPass.Instance;

        for(var x = 0; x < _gridSize.x; x++)
        {
            for(var z = 0; z < _gridSize.z; z++)
            {
                var h = Random.Range(1, _gridSize.y);
                for(var y = 0; y < h; y++)
                {
                    var positionWS = new float4(x, y, z, 1.0f);
                    var color = new float4(Random.value, Random.value, Random.value, 1.0f);
                    var meshIdx = (ushort)Random.Range(0, _meshes);
                    var objIdx = gdd.AddObject(meshIdx, new InstanceData()
                    {
                        Color = color,
                        PositionWS = positionWS,
                    });
                }
            }
        }

        var center = new Vector3(_gridSize.x, 0f, _gridSize.z) * 0.5f;
        _cameraRig.position = center;
    }
}

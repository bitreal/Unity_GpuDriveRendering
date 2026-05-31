using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugGUI : MonoBehaviour
{
    private void Start()
    {
        #if !UNITY_EDITOR
        Application.targetFrameRate = 60;
        #endif
    }

    private bool _dbgGui = true;
    private void OnGUI()
    {
        _dbgGui = GUILayout.Toggle(_dbgGui, "debug gui");
        GUILayout.Button($"frameMs:{_frameMs:0.00}ms");
        if(_dbgGui)
        {
            var stats = GpuDriverRenderingPass.Instance.Stats;
            
            GUILayout.Label("buffers' sizes:");
            GUILayout.Button($"!!! _meshInfoBuffer:{GetBufferSize(stats.MeshInfoBufferBytes)}");
            GUILayout.Button($"_vertexBuffer:{GetBufferSize(stats.VertexBufferBytes)}");
            GUILayout.Button($"_indexBuffer:{GetBufferSize(stats.IndexBufferBytes)}");
            GUILayout.Button($"!!! _objectsBuffer:{GetBufferSize(stats.ObjectsBufferBytes)}");
            GUILayout.Button($"!!! _cameraPlanesBuffer:{GetBufferSize(stats.CameraPlanesBufferBytes)}");
            GUILayout.Button($"_instanceDataBuffer:{GetBufferSize(stats.InstanceDataBufferBytes)}");
            GUILayout.Button($"_instanceMappingBuffer:{GetBufferSize(stats.InstanceMappingBufferBytes)}");
            GUILayout.Button($"!!! _drawCallBuffer:{GetBufferSize(stats.DrawCallBufferBytes)}");
            
            GUILayout.Label("meshes:");
            GUILayout.Button($"meshes (draw calls):{stats.Meshes}");
            GUILayout.Button($"vertexes:{stats.Vertexes}");
            GUILayout.Button($"indexes:{stats.Indexes}");
            
            GUILayout.Label("scene:");
            GUILayout.Button($"objects on scene (instances):{stats.Objects}");
            GUILayout.Button($"vertexes on scene:~{(stats.VertexesOnScene / 1_000_000f):0.00} M");
            if (stats.Meshes > 0)
                GUILayout.Button($"instances per mesh:~{stats.Objects/stats.Meshes}");
        }
    }

    private string GetBufferSize(int sizeInBytes)
    {
        return $"{(sizeInBytes / 1024f / 1024f):0.00} Mb";
    }

    private int _frames;
    private float _lastFrameMsTime;
    private float _frameMs;
    private void Update()
    {
        _frames++;
        var time = Time.time;
        var dt = (time - _lastFrameMsTime);
        if(dt > 1f)
        {
            _frameMs = dt / _frames * 1000f;
            _frames = 0;
            _lastFrameMsTime = time;
        }
    }
}

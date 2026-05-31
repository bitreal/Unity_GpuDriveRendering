# GPU Driven Rendering — Unity experiment

Usually in a game, the CPU tells the GPU what to draw, one object at a time.
When there are thousands of objects on screen, this takes a lot of CPU time.

This project tries a different approach: the CPU uploads all object data to the GPU once,
and then the GPU decides what to draw by itself. The CPU is almost not involved every frame.

The result: up to 1.5 million objects on screen with a single draw call per mesh type.

## How it works

The system is built as a custom URP `ScriptableRendererFeature`.
Each frame, a compute shader runs two passes:

- **Clean** — resets indirect draw arguments for each mesh type.
- **Cull** — iterates over all objects in parallel, performs frustum culling,
  and fills an instance mapping buffer for visible objects.

After that, `DrawProceduralIndirect` issues one draw call per mesh type.
The vertex shader reads vertex and instance data directly from `ComputeBuffer`
using the instance mapping built by the Cull kernel.

Object positions are packed into two `uint` fields (`PositionXZ` and `PositionY_MeshIdx`)
to minimize GPU memory bandwidth.

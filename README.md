# 10,000 Balls Against a Signed Distance Field

A Unity 6.3 (URP) project that drops 10,000 balls onto a static scene (a
ground plane and a cylinder) and lets them settle against a **signed
distance field** baked from that scene offline. The simulation runs
entirely on the C# Job System with Burst — no GameObjects, Rigidbodies,
Colliders, or MonoBehaviours per ball — and all balls are drawn in a
single GPU-instanced draw call.

## Demo

A short screen recording of the simulation running is included in the
project root: [`recording.mp4`](recording.mp4).

<video src="recording.mp4" controls width="100%"></video>

<!-- The tag above inline-plays the video on renderers that support local
     HTML5 video in Markdown (e.g. GitHub); otherwise use the link above it. -->

## How to run

1. Open the project in Unity 6.3 (URP).
2. Open the main scene and press Play.
3. The scene ships with a pre-baked SDF (`Assets/SDF/SceneField.asset` and
   `Assets/SDF/SceneFieldVolume.asset`), so no manual step is required
   unless the static geometry changes (see below).

An on-screen overlay in the top-left corner shows the live ball count,
frame time (ms) and FPS.

## Re-baking the SDF

The field is baked **offline, in the editor**, from analytic distance
functions for the plane and the capped cylinder (combined with a CSG
`min()` union) — it does not read the scene at runtime and does not react
to geometry moved or resized while playing.

The process is **not hardcoded to fixed shape values**: the plane's
height and the cylinder's center/radius/height are read at bake time from
the actual `Transform`s of the objects in the scene, via two marker
components (`SdfPlaneMarker`, `SdfCylinderMarker`) — the baker finds
whichever objects carry these components rather than assuming fixed
numbers or a fixed name. Moving, raising/lowering, or resizing the plane
or the cylinder in the editor and re-baking picks up the new shape
correctly. What *is* still a fixed, manually-chosen constant is the bake
volume itself (bounds and resolution, see below) and the cylinder's
rotation is not accounted for (the formula assumes it stands upright, as
in the given scene).

- Menu: **Tools > SDF > Bake Scene** (`Assets/Scripts/Editor/SdfBaker.cs`).
- Both the plane and the cylinder object in the scene must have their
  respective marker component attached (`SdfPlaneMarker` /
  `SdfCylinderMarker`) — if either is missing, the bake logs an error and
  aborts rather than silently baking wrong or stale geometry.
- Re-run the bake whenever the plane or cylinder's position or scale
  changes, or whenever the bake bounds/resolution constants below
  change. Editing the transform or the scene does **not** trigger a
  re-bake automatically — the SDF only updates when the menu item is run
  again.
- The bake is idempotent: it deletes and recreates both output assets
  each time, so it's safe to run repeatedly.
- Current bake volume settings: bounds `(-5.3, -0.3, -5.3)` to
  `(5.3, 2.3, 5.3)`, resolution `106 x 26 x 106`, cell size `0.1`. These
  are still fixed constants in `SdfBaker.cs`, not derived from the scene —
  the bounds wrap the 10x10 plane and the cylinder with roughly half a
  ball-radius of margin on every side, but if the cylinder is moved or
  resized enough to fall outside this fixed volume, the bounds need to be
  widened by hand.

## Approach

### Baking (`Editor/SdfBaker.cs`, `SdfVolume.cs`)

The bake writes two assets: a `Texture3D` (`SceneField.asset`, one float
per voxel) holding the actual distance values, and an `SdfVolume`
`ScriptableObject` (`SceneFieldVolume.asset`) holding the metadata needed
to interpret it — `boundsMin`/`boundsMax`, resolution per axis, and a
computed `cellSize` property. Keeping metadata and data in separate
assets means the metadata can be inspected/tweaked without touching the
(large) texture asset.

The plane and cylinder's shape parameters are not hardcoded constants:
`SdfBaker.Bake()` looks up `SdfPlaneMarker`/`SdfCylinderMarker` in the
scene via `Object.FindFirstObjectByType`, then reads the plane's height
and the cylinder's center/radius/height straight from their `Transform`s
(`localScale.x * 0.5f` for radius and `localScale.y * 2f` for height,
matching Unity's default primitive dimensions at scale 1). The one
subtlety: the cylinder's analytic distance function expects `center` to
be the cylinder's *base* (the point where it touches the ground), while a
Unity primitive's pivot sits at its geometric middle — so the pivot's
world position is shifted down by half the (current, scaled) height
before being passed in. Only the bake *volume* (bounds/resolution/cell
size) remains a fixed, hand-tuned constant, and the cylinder's rotation
is not read (assumed upright).

### Bridging into Burst (`BallSimulation.cs`, `SdfSampler.cs`)

`Texture.GetPixelData<float>(0)` returns a `NativeArray<float>` **view**
directly onto the `Texture3D`'s CPU-side buffer — no copy, and it must
**not** be disposed (its owner is the texture asset). This is what lets a
Burst job read baked SDF data without any managed-code bridge at
simulation time.

Sampling (`SdfSampler.Sample`) does trilinear interpolation over the 8
surrounding voxels, clamping the query point to the volume's bounds so
that queries outside the baked region still return a sensible (if
extrapolated) distance rather than reading out of range. The surface
normal at a contact point (`SdfSampler.Gradient`) is estimated with a
central finite difference (6 extra samples, `epsilon = cellSize * 0.5`).

### Simulation (`SimulateBallsJob.cs`)

Ball state lives in two persistent, structure-of-arrays `NativeArray<float3>`
(`positions`, `velocities`) — no per-ball object, no per-ball allocation.
`SimulateBallsJob` is an `IJobParallelFor` with `[BurstCompile]`,
scheduled once per frame over all 10,000 balls (batch size 64) and
completed synchronously before rendering. Per ball, each frame:

1. Integrate gravity and velocity into position (semi-implicit Euler).
2. Sample the SDF at the new position.
3. If the ball has penetrated the surface (`distance < ballRadius`):
   - Push it back out along the surface normal by the penetration depth.
   - Reflect the velocity component **along the normal** using a
     restitution coefficient (`0.5`).
   - Damp the **tangential** velocity component by a friction factor
     (`0.9` per contact frame) — without this, a ball deflected
     sideways (e.g. off the cylinder's cap edge) would slide forever
     along the ground plane, since the ground's analytic distance
     function (`distance = p.y`) is valid for any X/Z and never
     naturally decelerates horizontal motion.
   - Zero the velocity once its magnitude drops below a sleep threshold,
     to prevent it oscillating indefinitely around rest due to
     restitution's asymptotic decay.

### Rendering (`ProceduralMesh.cs`, `Shaders/InstancedBall.shader`)

A low-poly UV sphere (6 rings x 8 segments, 96 triangles / 63 vertices)
is generated procedurally once at startup. All 10,000 balls are drawn in
a single `Graphics.DrawMeshInstancedIndirect` call using GPU **procedural
instancing** (`#pragma instancing_options procedural:ConfigureProcedural`):
the shader reads each instance's position out of a `StructuredBuffer<float3>`
and writes `unity_ObjectToWorld` itself, so there is no per-ball Transform
or GameObject anywhere. Positions are pushed to the GPU once per frame via
`GraphicsBuffer.SetData(NativeArray<float3>)`, which avoids a managed-array
copy of the simulation's native buffer. Per-frame GPU traffic is therefore
one `float3` (12 bytes) per ball for positions — 120 KB/frame at 10,000
balls — plus the mesh itself, uploaded once.

### On-screen readout (`BallSimulation.OnGUI`)

An `OnGUI` overlay shows ball count, frame time, and FPS. Frame time is
smoothed with an exponential moving average (`smoothedDeltaTime += (Time.deltaTime
- smoothedDeltaTime) * 0.1f`) so the number is readable instead of
flickering every frame; it still reflects a sustained change within a
handful of frames.

## Known issues / limitations

- Ball-ball collisions are intentionally not implemented (explicitly out
  of scope for this task).
- The SDF is fully static: moving or resizing the plane/cylinder at
  runtime does nothing until the scene is re-baked from the editor menu.
- `renderBounds` (the shared `Bounds` passed to
  `DrawMeshInstancedIndirect`) uses a generous fixed margin (±60 units in
  X/Z around the plate) rather than a tightly computed box. This is a
  deliberate safety margin: since the draw call is culled against one
  shared `Bounds` for *all* instances, if any single ball drifted outside
  it, the entire batch of 10,000 would disappear, not just that one ball.
  A tighter box computed from the actual min/max ball extents each frame
  would be more correct but wasn't necessary at this scale.
- The simulation is scheduled and immediately completed on the main
  thread every frame (`Schedule(...).Complete()`) — there's no
  multi-frame job pipelining (e.g. scheduling frame N+1 before waiting on
  frame N), so there's one hard sync point per frame.
- Contact friction is a simple linear damping of the tangential velocity
  component, not a physically derived (e.g. normal-force-proportional
  Coulomb) friction model. It satisfies the required behavior — balls
  rest without sinking, tunneling, or jittering or sliding forever — but
  isn't a physically rigorous friction implementation.
- No compute-shader simulation, no dynamic geometry, no art/UI polish —
  all explicitly out of scope per the task brief.

## What I'd do with more time

- Pipeline the simulation across frames (schedule frame N+1's job while
  frame N's GPU upload/draw is still in flight) instead of completing
  the job synchronously every frame.
- Replace the fixed `renderBounds` margin with a tight bounds computed
  from the actual ball positions each frame (a small parallel min/max
  reduction job).
- Add a uniform spatial grid, as a stepping stone toward ball-ball
  collisions if that were ever brought into scope.
- Expose simulation parameters (restitution, friction, sleep threshold,
  bake resolution) as serialized fields instead of inline constants.
- Add simple LOD/impostors for balls far from the camera, since every
  instance currently renders the full 96-triangle mesh regardless of
  distance.

## Performance

Measured at 10,000 balls: **~9.0 ms/frame (~110 FPS)**.

Test machine: Intel Core i7-11850H (8C/16T) @ 2.5 GHz, 32 GB RAM,
NVIDIA RTX A3000 Laptop GPU, Windows 11 Pro.

## Packages

- `com.unity.render-pipelines.universal` 17.3.0 — added directly (URP
  project template).
- `com.unity.burst` 1.8.28, `com.unity.collections` 2.6.2,
  `com.unity.mathematics` 1.3.3 — pulled in transitively (as dependencies
  of other installed packages), not added manually; used directly by the
  simulation code (`[BurstCompile]`, `NativeArray<T>`, `float3`/`math.*`).
- The C# Job System itself (`Unity.Jobs`) is part of Unity's core engine
  API, not a separate package.

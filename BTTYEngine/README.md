# BTTYEngine Developer Guide

BTTYEngine is a MonoGame voxel game engine. VoxelShooter is its first game, but the engine is designed to be dropped into any MonoGame project via a `<ProjectReference>`, that points to a sibling directory.

> **Namespace note:** Everything currently lives in the `VoxelShooter` namespace. A rename to `BTTYEngine` is planned when the engine moves to its own repo.

---

## Getting started

Add a project reference from your game project to `BTTYEngine.csproj`:

```xml
<ProjectReference Include="..\BTTYEngine\BTTYEngine.csproj" />
```

---

## Camera system

### The interface

Every camera implements `ICamera`, which gives you:

| Member | What it's for |
|--------|---------------|
| `ViewMatrix` / `ProjectionMatrix` | Drop straight into a `BasicEffect` or custom shader |
| `BoundingFrustum` | Ready for frustum culling (call `.Intersects(sphere)`) |
| `Position` / `Target` | Drive where the camera sits and what it tracks |
| `Update(GameTime, VoxelWorld)` | Call once per frame |
| `TriggerShake(float)` | Kick off a screen-shake of the given amplitude (world units) |

### Concrete cameras

All four extend `BaseCamera`, which handles screen-shake and the shared matrix/frustum state.

| Class | Projection | Good for |
|-------|-----------|----------|
| `SideScrollingCamera` | Orthographic | Classic 2.5-D side-scroller |
| `IsometricCamera` | Orthographic | 45° isometric view |
| `FirstPersonCamera` | Perspective | First-person or chase-cam |
| `TopDownCamera` | Orthographic | Top-down / minimap style |

Construct any of them with the `GraphicsDevice` and starting `Viewport`, set `Position` and `Target`, then call `Update` each frame.

```csharp
var camera = new SideScrollingCamera(GraphicsDevice, GraphicsDevice.Viewport);

// Position is the scene focus point that the camera tracks. The eye sits at Position + Offset (default Z+95).
// Set Target = Position on startup so the camera doesn't drift before the first Update().
camera.Position = new Vector3(scrollX, worldCentreY, 0f);
camera.Target   = camera.Position;
```

### Smooth transitions with CameraTransitionManager

Pass your cameras to a `CameraTransitionManager` and the rest of your code never needs to know a transition is happening:

```csharp
var manager = new CameraTransitionManager(sideCamera);

// Switch cameras with a 1.5-second smoothstep blend
manager.TransitionTo(isoCamera, durationSeconds: 1.5f);

// Each frame, update and read matrices as normal
manager.Update(gameTime, world);
drawEffect.View       = manager.ViewMatrix;
drawEffect.Projection = manager.ProjectionMatrix;
```

`manager.IsTransitioning` is `true` while the blend is playing. `manager.ActiveCamera` returns the camera that's currently in full control.

### Screen-shake

Call `TriggerShake` on either a camera directly or the manager:

```csharp
cameraManager.TriggerShake(5f); // 5 world-units peak amplitude
```

The shake decays at 15% per frame and won't cancel an in-progress shake. It takes the maximum of the two. Screen-shake works through transitions too.

---

## Input

`InputManager` captures keyboard and gamepad state once per frame and exposes game-level queries so your game logic stays clean.

```csharp
var input = new InputManager();

// In Update():
input.BeginInputProcessing();

Vector2 move = input.MoveDirection(); // WASD / arrows / left stick
bool firing  = input.IsFiring();      // Z / RT / A
bool quit    = input.IsExiting();     // Esc / Back

input.EndInputProcessing();
```

There are also lower-level helpers such as `IsKeyDown`, `IsKeyPressed`, `IsButtonDown`, `IsButtonPressed`, if you need to check specific keys not covered by the built-in queries.

For camera switching specifically:

```csharp
input.IsCameraSelectPressed(1..4)  // keyboard 1-4
input.IsCameraNextPressed()        // RB / R1
input.IsCameraPrevPressed()        // LB / L1
```

---

## Physics

BTTYEngine wraps [BepuPhysics 2.4.0](https://github.com/bepu/bepuphysics2) behind `PhysicsManager`, which runs single-threaded, which is plenty for a small side-scroller and straightforward to reason about.

```csharp
// Startup
var physics = new PhysicsManager();
physics.Initialize();   // sets PhysicsManager.Instance

// In Update() pass your real delta time
physics.Step((float)gameTime.ElapsedGameTime.TotalSeconds);

// Cleanup
physics.Dispose();
```

`PhysicsManager.Instance` is available statically anywhere you need to add or query bodies directly via `PhysicsManager.Instance.Simulation`.

### Collision events

Bepu callbacks run on its own threads. BTTYEngine queues collision pairs and lets you process them safely on the main thread via `CollisionEventHandler`:

```csharp
CollisionEventHandler.Instance.ProcessPending(evt =>
{
    var a = EntityRegistry.Instance.FindByHandle(evt.A);
    var b = EntityRegistry.Instance.FindByHandle(evt.B);
    a?.OnCollision(b);
    b?.OnCollision(a);
});
```

---

## Voxel world

`VoxelWorld` is a 3-D grid of `Chunk` objects. Each chunk builds its own vertex/index buffer; the game loop asks for `BoundingFrustum.Intersects(chunk.boundingSphere)` before drawing, so off-screen chunks cost nothing.

```csharp
var world = new VoxelWorld(widthInChunks, heightInChunks, depthInChunks);

// Copy a VoxelSprite into the world at voxel coordinates
world.CopySprite(tileX * Chunk.X_SIZE, tileY * Chunk.Y_SIZE, 0, sprite);

// Rebuild dirty chunk meshes (call after making changes)
world.UpdateWorldMeshes();

// Each frame
world.Update(gameTime, cameraManager);
```

Voxel geometry is stored in `.bvx` files, a custom binary format that converts MagicaVoxel (`.vox`) files into a GPU-friendly layout. Load them with `BvxLoader.LoadSprite`.

---

## Particles

`ParticleController` manages a pool of `ParticleCube` instances that that fly out on impact, fade, and return to the pool automatically.

```csharp
var particles = new ParticleController(GraphicsDevice);

// Spawn a burst (position, velocity, size, colour, lifetime ms, loop)
particles.Spawn(position, velocity, 0.5f, Color.OrangeRed, 800, false);

// Each frame
particles.Update(gameTime, cameraManager, world);
particles.Draw();
```

---

## Coordinate convention

Y-up, right-handed. The camera sits on the +Z axis and looks toward −Z. World units are in voxels; one voxel = `Voxel.SIZE` (check the constant for the current value).

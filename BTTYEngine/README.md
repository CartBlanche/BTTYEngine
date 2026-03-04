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

`InputManager<TAction>` is a fully game-agnostic input system. The engine never hardcodes a single key, your game defines its own action enum and builds a binding table. Any combination of keyboard keys, gamepad buttons, gamepad analogue axes, mouse buttons, mouse analogue axes, and touch gestures can be bound to the same action.

### Step 1:Define your action enum

```csharp
// In your game project (not in BTTYEngine)
public enum MyAction
{
    MoveLeft, MoveRight, MoveUp, MoveDown,
    Fire, Quit,
    ZoomIn, ZoomOut,
}
```

### Step 2:Create the manager

```csharp
InputManager<MyAction> input = new InputManager<MyAction>();
```

### Step 3:Bind physical inputs in `LoadContent`

**Keyboard keys** one or more keys per call, all map to the same action:

```csharp
input.Bind(MyAction.MoveUp,    Keys.W, Keys.Up);
input.Bind(MyAction.MoveDown,  Keys.S, Keys.Down);
input.Bind(MyAction.MoveLeft,  Keys.A, Keys.Left);
input.Bind(MyAction.MoveRight, Keys.D, Keys.Right);
input.Bind(MyAction.Fire,      Keys.Space);
input.Bind(MyAction.Quit,      Keys.Escape);
```

**Gamepad buttons:**

```csharp
input.Bind(MyAction.Fire, Buttons.A);
input.Bind(MyAction.Quit, Buttons.Back);
```

**Gamepad analogue axes** (`sign` is `+1` for positive direction, `-1` for negative):

```csharp
// Left stick horizontal → MoveLeft / MoveRight
input.BindAxis(MyAction.MoveRight, GamePadAxis.LeftStickX,  1f);
input.BindAxis(MyAction.MoveLeft,  GamePadAxis.LeftStickX, -1f);

// Left stick vertical → MoveUp / MoveDown
input.BindAxis(MyAction.MoveUp,   GamePadAxis.LeftStickY,  1f);
input.BindAxis(MyAction.MoveDown, GamePadAxis.LeftStickY, -1f);

// Right trigger → Fire (with a 0.1 deadzone)
input.BindAxis(MyAction.Fire, GamePadAxis.RightTrigger, 1f, 0.1f);
```

Available `GamePadAxis` values: `LeftStickX`, `LeftStickY`, `RightStickX`, `RightStickY`, `LeftTrigger`, `RightTrigger`.

**Mouse buttons:**

```csharp
input.Bind(MyAction.Fire,    MouseButton.Left);
input.Bind(MyAction.ZoomIn,  MouseButton.X1);
```

Available `MouseButton` values: `Left`, `Right`, `Middle`, `X1`, `X2`.

**Mouse analogue axes** (frame-relative deltas, not cumulative):

```csharp
// Scroll wheel → zoom
input.BindMouseAxis(MyAction.ZoomIn,  MouseAxis.ScrollWheel,  1f);
input.BindMouseAxis(MyAction.ZoomOut, MouseAxis.ScrollWheel, -1f);
```

Available `MouseAxis` values: `DeltaX`, `DeltaY`, `ScrollWheel`.

**Touch gestures:**

```csharp
// Automatically enables the gesture on TouchPanel
input.BindGesture(MyAction.Fire, GestureType.Tap);
```

### Step 4: Frame lifecycle

Call these at the top and bottom of `Update()`:

```csharp
input.BeginInputProcessing();   // capture this frame's state
// ... your Update() logic ...
input.EndInputProcessing();     // roll current into last (for edge detection)
```

### Step 5: Query actions

**Digital held / pressed:**

```csharp
if (input.IsHeld(MyAction.Fire))
    projectile.Fire();

if (input.IsPressed(MyAction.Quit))   // rising-edge only: true for one frame
    Exit();
```

`IsHeld` is true for every frame the binding is active. `IsPressed` is true only on the first frame it becomes active.

**Analogue value (0 – 1 for sticks/triggers; unbounded for mouse delta):**

```csharp
float throttle = input.GetValue(MyAction.Fire);   // 0..1 from trigger or 1.0 from button
```

**2D vector from four directional actions:**

```csharp
// Works seamlessly whether the actions are bound to keys, buttons, or analogue sticks
Vector2 move = input.GetAxis2D(MyAction.MoveLeft, MyAction.MoveRight,
                                MyAction.MoveDown, MyAction.MoveUp);
```

### Direct positional queries (mouse & touch)

These are not mapped to actions, instead use them for UI, camera look, virtual joysticks, etc.

```csharp
Point   cursor = input.GetMousePosition();     // screen-space pixel position
Vector2 delta  = input.GetMouseDelta();        // pixels moved this frame (+X right, +Y down)
int     scroll = input.GetMouseScrollDelta();  // +ve = scrolled up, -ve = scrolled down

IReadOnlyList<TouchPoint> touches = input.GetTouches();
foreach (var t in touches)
{
    // t.Id        — unique contact identifier
    // t.Position  — screen-space position
    // t.Delta     — movement since last frame
    // t.State     — Pressed / Moved / Released / Invalid
}
```

### Escape hatch

If you need the raw MonoGame state beyond what the API exposes:

```csharp
KeyboardState  kb  = input.CurrentState.KeyState;
GamePadState   pad = input.CurrentState.PadState;
MouseState     ms  = input.CurrentState.MouseState;
TouchCollection tc = input.CurrentState.Touches;
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

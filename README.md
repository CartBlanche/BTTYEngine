# BTTYEngine

BTTYEngine is a simple voxel game engine that is built on [MonoGame](https://monogame.net/) . 

The engine lives in `BTTYEngine/` and is intentionally game-agnostic (as engines should be).  [`VoxelShooter/README.md`](VoxelShooter/README.md) is its first demo to use it, but the plan is for future games to reference it directly via `<ProjectReference>` from their own sibling repos.

The initials stand for `Better Today Than Yesterday` (Betty for short :) ) as I've always believed that voxels are the future of video games, it's just taken 20+ years to slowly crawl towards that future, and for me to get off my arse and actually do something about it.


What's in there:

- **Camera system** : `ICamera` interface, `BaseCamera` with built-in screen-shake, and four ready-to-use implementations: `SideScrollingCamera`, `IsometricCamera`, `FirstPersonCamera`, `TopDownCamera`. `CameraTransitionManager` wraps any two cameras and blends between them using a smoothstep curve, so switching camera modes looks polished rather than instant.
- **Voxel engine** : `VoxelWorld`, `Chunk`, mesh building, frustum culling, and an explosion system for breaking up the voxel terrain
- **Physics** : BepuPhysics 2.4.0 integration via `PhysicsManager`. Entity-entity collisions are dispatched back to the game loop via `CollisionEventHandler`.
- **Particles** : pooled `ParticleController` and `ParticleCube` for voxel-style debris
- **Input** : `InputManager` Input agnostics system, inspired by SDL3's system.
- **Utilities** : `Helper` (random, geometry helpers)

See [`BTTYEngine/README.md`](BTTYEngine/README.md) for a developer guide on using the engine in your own game.

---

## Tech Notes

- Voxel geometry is stored in `.bvx` files (our new custom binary format that takes MagicaVoxel files, *.vox, and converts them to *.bvx, which is optimised for GPUs) and loaded at runtime.
- The content pipeline builds `.tmx` → `.xnb` via the `TiledContentPipeline` extension; `.bvx` files are copied as-is
- The level layout comes from a [Tiled](https://www.mapeditor.org/) `.tmx` file (`Core/Content/1.tmx`)
- Enemy spawn positions and wave configurations are defined as object layers inside the same Tiled map
- Coordinate convention: Y-up, right-handed. Camera sits on +Z, looks in −Z.
- BEPU 2.x Physics now handles the physics side of things.

## History

As you can see, this repo was originally forked from 
[GarethIW's repo](https://github.com/GarethIW/VoxelShooter) about 13 years ago. But I never (like so many of my other forks) did anything with it until this year. This fork brought the old MonoGame project up to date, by making sure it built with MonoGame 3.8.x release and added all the supported platforms. After that I separated out the Voxel engine out, which became BTTYEngine, hence the repo name change.

With that separation I integrated the BEPU 2.x Physics engine, added 3 built in cameras, namely SideScroller, Isometric, First Person and Top-Down (but you can add your own) and inspired by the SDL3 agnostic input system, added that.

The [`VoxelShooter/README.md`](VoxelShooter/README.md) demo shows how all these addition have been integrated into a playable "game".

## Eye Candy

VoxelShooter

Android:
![VoxelShooter - Android](screenshot-android.png)

Desktop:
![VoxelShooter - Desktop](screenshot-desktop.png)

Read more about the demo here: [`VoxelShooter/README.md`](VoxelShooter/README.md)

## Building & Running

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [MonoGame 3.8.*](https://www.monogame.net/) (packages restore automatically via NuGet)

---

### Visual Studio Code

The repo includes a `.vscode` folder with build tasks and launch configurations already set up.

**Desktop (OpenGL, Windows, Mac, Linux):**

1. Open the `Run and Debug` panel (`Ctrl+Shift+D`)
2. Select **Desktop Debug** from the dropdown
3. Press `F5`

**Windows (DirectX):**

1. Select **Windows Debug** from the dropdown
2. Press `F5`

**Android:**

1. Connect an Android device (API level 23 or higher) and make sure ADB is set up
2. Select **Android Debug** from the dropdown
3. Press `F5`, this builds, deploys, and attaches the debugger over port 10000

**iOS:**

1. Requires a Mac with Xcode. Build using the **Debug iOS Build** task from `Terminal → Run Task`

To run a build task without launching the debugger, use `Terminal → Run Task` and pick the relevant platform.

---

### Visual Studio

Open `VoxelShooter.sln`. The solution is organised into 5 folders:

- **Dependencies**, TiledLib (Tiled map runtime) and TiledContentPipeline (content pipeline extension)
- **Desktop**, Desktop build
- **Windows**, Windows (Direct3D) build
- **Android** and **iOS** folders in the solution root

The content pipeline runs automatically as part of the build, no separate MGCB step needed.

---

## Repo Structure

```
VoxelShooter/
├── BTTYEngine/         - Reusable voxel game engine (cameras, voxel world, particles, input)
│   └── Voxel/          - VoxelWorld, Chunk, LoadSave and supporting types
├── VoxelShooter/ 
│   ├── Core/               - VoxelShooter game code and content
│   │   ├── Enemies/        - Enemy types (Asteroid, Omega, Squid, Turret)
│   │   └── Content/        - .mgcb file and all game assets (.vxs, .png, .tmx, etc.)
│   ├── Desktop/            - DesktopGL entry point (net9.0)
│   ├── Windows/            - WindowsDX entry point (net9.0-windows)
│   ├── Android/            - Android entry point (net9.0-android)
│   ├── iOS/                - iOS entry point (net9.0-ios)
├── Dependencies/
│   ├── TiledLib/               - Tiled map file runtime library
│   └── TiledContentPipeline/   - MGCB pipeline extension for .tmx files
```

`Core` references `BTTYEngine` via a `<ProjectReference>`.
The platform projects (`Desktop`, `Windows`, etc.) also reference
 `Core` via a `<ProjectReference>`.
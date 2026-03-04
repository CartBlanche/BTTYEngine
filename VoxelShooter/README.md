## VoxelShooter

A side-scrolling space shooter where everything is built out of voxels. 

Android:
![VoxelShooter - Android](../screenshot-android.png)

Desktop:
![VoxelShooter - Desktop](../screenshot-desktop.png)

---

## The Game

You're a lone fighter pilot flying through a hostile corridor. The enemies pour in from the right in formations, and your only job is to stay alive long enough to make it through.

It's a short demo, originally written by [GarethIW](https://github.com/GarethIW/VoxelShooter), but this version has been updated to show how to integrate with [`BTTYEngine/README.md`](../BTTYEngine/README.md).

---

## Goal

Survive to the end of the level.

Destroy enemies to collect XP orbs. Pick those up to level your weapons up. Don't get hit too many times. The level auto-scrolls horizontally.

---

## How to Play

Enemies spawn in waves: sometimes in a circle formation that fans out, sometimes in a straight line that sweeps across. Learn the patterns early, later waves come in fast and expect you to already be out of the way.

Your ship collides with the voxel terrain, so keep an eye on where the walls and floors are. Getting pinned against a surface while taking fire is a quick way to lose.

### Weapons & Levelling Up

XP orbs are dropped by destroyed enemies and drift toward you once you get close enough. Collect them to charge the XP bar on the left of the HUD.

There are five weapon upgrades:

| Level | Weapon |
|-------|--------|
| 0 | Single forward laser |
| 1–2 | Dual alternating lasers, faster fire rate |
| 3–5 | Triple spread shot across a wide arc |
| 4+ | Two extra side-firing beams added to the spread |
| 5 | Rockets fire automatically upward every 2 seconds |

At level 2 an orbiting drone/hammer activates around your ship. It spins around you continuously and damages any enemy it touches, useful for anything that tries to get in close.

### The HUD

Two vertical bars on the left side of the screen:

- **Left bar**, health. When it's gone, you're done.
- **Right bar**, XP. The tick marks show each weapon upgrade threshold.

---

## Controls

### Keyboard

| Key | Action |
|-----|--------|
| `W` / `↑` | Move up |
| `S` / `↓` | Move down |
| `A` / `←` | Move left |
| `D` / `→` | Move right |
| `Z` | Fire |
| `1` / `2` / `3` / `4` | Switch camera (Side-Scrolling / Isometric / First-Person / Top-Down) |
| `Escape` | Quit |

### Gamepad

| Input | Action |
|-------|--------|
| Left stick | Move |
| Right trigger / `A` | Fire |
| `LB` / `RB` | Cycle camera backwards / forwards |
| `Back` | Quit |

---

## Enemies

| Enemy | Description |
|-------|-------------|
| **Asteroid** | Drifts in slowly. Not aggressive, but it will happily fly into you. |
| **Omega** | Actively hostile. Shoots at you and manoeuvres to stay on screen. |
| **Turret** | Stationary but fires rapidly. Take it out before it lines up a clean shot. |
| **Squid** | Weaves around and gets in close. The orbiting drone is your friend here. |

---

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
- **Desktop**, DesktopGL build
- **Windows**, WindowsDX (Direct3D) build
- **Android** and **iOS** folders in the solution root

**To run:**

1. Right-click the project you want (`Desktop\VoxelShooter`, `Windows\VoxelShooter`, etc.) and set it as the startup project
2. Press `F5` to build and run in debug mode, or `Ctrl+F5` without the debugger

The content pipeline runs automatically as part of the build, no separate MGCB step needed.

---

## Project Structure

```
VoxelShooter/ 
├── Core/               - VoxelShooter game code and content
│   ├── Enemies/        - Enemy types (Asteroid, Omega, Squid, Turret)
│   └── Content/        - .mgcb file and all game assets (.vxs, .png, .tmx, .bvx, .vox etc.)
├── Desktop/            - DesktopGL entry point (net9.0)
├── Windows/            - WindowsDX entry point (net9.0-windows)
├── Android/            - Android entry point (net9.0-android)
├── iOS/                - iOS entry point (net9.0-ios)
```

`Core` references `BTTYEngine` via a `<ProjectReference>`.
The platform projects (`Desktop`, `Windows`, etc.) also reference `Core` via a `<ProjectReference>`.
# Attack of the Radioactive Things

A top-down **stealth-action-tower-defense** game in Unity.

This repository is a Unity project with some local caches missing. When you open it in Unity, it will generate the missing files automatically. This may take a few minutes.

## Prerequisites

- **Unity Editor** `6000.3.6f1`.
- **Windows 11** is the primary development target.

## Setup

### Play the Game

You can go to our itch.io link to directly play the game in the browser or download the available builds.

| Build | Notes |
|--------|--------|
| **Windows** | Build for Windows PCs |
| **Linux** | Build for Linux computers |
| **Web** | WebGL (HTML5) build for browsers |

### Running the Editor

1. Clone or download **this repository**.
2. Open **Unity Hub** → **Add project from disk** → choose the **root folder** that directly contains:
   - `Assets`
   - `Packages`
   - `ProjectSettings`
3. Open the project with editor **6000.3.6f1** (or closest available).
4. On your first time opening, Unity will generate folders like `Library/` and other local caches.

You can now press **Play** in the Editor to run the project.

#### Running Scenes

1. Open any scene under `Assets/Scenes/` (e.g. **MainMenu**)
2. Use **File → Build Settings** to add scenes to your build if they don't open
3. Press **Play**

#### Scenes

- **Stealth:** Hub-style level at `Assets/Scenes/StealthLevels/StealthOpen.unity`.
- **Tower defense:** Levels under `Assets/Scenes/TDLevels/`.
- **Menus:** Main menu, settings, credits, etc. menus under `Assets/Scenes/Menus`.
- **Extras:** Other scenes used during development for testing are also included.

## What’s in this repository

| Path | Purpose |
|------|---------|
| `Assets/` | Scenes, scripts, prefabs, art, audio, UI |
| `Packages/` | Unity package manifest etc. |
| `ProjectSettings/` | Editor version, input, graphics (URP), build scene list, etc. |
| `Assembly-CSharp.csproj` | Generated C# project maintained by Unity |

## Authors

- Hariharan Vallath
- Mishaal Patel
ty |

## Authors

- Hariharan Vallath
- Mishaal Patel
- Kego Wigwas

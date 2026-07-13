# Arrow Bridge

A hybrid-casual puzzle game concept and playable prototype, built in Unity as a case study for a Game & Level Designer application at **Roon Games** (Turkish subsidiary of Hypercell Games).

## Core mechanic

The board is filled with directional arrows. Tapping a *valid* arrow (one whose path out of the grid is currently clear) removes it and adds one segment to a bridge connecting two riverbanks. Tapping a *blocked* arrow — one whose path is obstructed by other arrows — is a wrong move and costs one of the player's three lives. Clear every arrow before running out of lives, and the character walks across the finished bridge to complete the level.

Every arrow is equal — order and arrow size don't matter, only the count of arrows cleared. The bridge grows procedurally: each cleared arrow adds a deck plank and side rails, and every *N*th segment raises a full Warren-truss post with diagonal bracing, so the bridge always spans exactly from bank to bank once the board is clear.

## What's in this repo

- `Assets/Scripts/Game/GameManager.cs` — progress, lives, win/fail state
- `Assets/Scripts/Bridge/BridgeBuilder.cs` — procedural 3D bridge construction (Warren-truss geometry)
- `Assets/Scripts/Arrows/` — grid management, arrow direction/path logic, level layout tooling
- `Assets/Scripts/Player/`, `Assets/Scripts/Camera/` — character and camera behavior
- `Assets/Scripts/Common/` — procedural shape/decor factories, shared game palette
- `Assets/Editor/` — custom in-editor tooling, including a **level solvability verifier** and automated tests (`BridgeBuilderTests`, `GameManagerTests`, `ShapeSpriteFactoryTests`)
- `Blender/` — environment generation scripts for supporting 3D scenery

## Tech stack

- Unity (C#)
- Blender (for environment asset generation, scripted via Python)
- Custom Editor tooling for level design and automated solvability verification

## Status

Case-study prototype, built for a design portfolio/job application. Core loop, procedural bridge-building, and level tooling are implemented and tested.

# Project Guidelines: Top-Down Roguelike Shooter

## Project Overview
We are developing a 2D top-down roguelike shooter in **Unity (C#)**, inspired by *Soul Knight*, but set in a post-apocalyptic world where the player fights zombies. The project is in its early stages. 
*Note: All detailed implementation steps, specific algorithms, and granular mechanics are documented in `PLAN.md`. This file serves as a high-level directive.*

## Tech Stack
- **Engine:** Unity
- **Language:** C#
- **Core Systems:** Unity 2D Physics, Particle System, Animation, Navigation.

## Core Game Mechanics (High-Level)
- **Perspective & Controls:** Top-down view, omnidirectional joystick movement, and auto-aim (targeting the nearest enemy).
- **Procedural Generation:** Levels generate dynamically per run, structured by locations and stages. 
  - *Example (Location: "city")*: Stage 1 (post-apocalyptic streets), Stage 2 (supermarket interior), Stage 3 (5-floor skyscraper with vertical progression via stairs, ending in a rooftop boss fight). Boss encounters conclude each stage.
- **Combat System:** Bullet-hell style with high projectile density. Diverse weapon arsenal. Optimized collision detection (e.g., Circle Colliders). Stats include damage, armor, critical hits, and elemental effects (fire, poison, electricity).
- **AI & Enemies:** Distinct behavior patterns per enemy type (using FSM or Behavior Tree). Bosses feature complex, multi-phase attack patterns.
- **Meta-Progression:** Currency system, unlocking new characters and weapons, and persistent cross-session saves.
- **Multiplayer (Architectural Consideration):** The codebase should be structured with future co-op in mind, anticipating network synchronization for positions, actions, and projectiles (e.g., Unity Netcode, Photon, or Mirror).

## Core Directives & Safety Rules
- **Directory Restriction:** You must ONLY read, write, modify, and execute files within the current project directory. Do not access parent directories, system paths, or external folders.
- **Destructive Actions:** NEVER delete files, folders, or overwrite existing assets without explicit permission from the user. If you believe a file should be removed or replaced, stop and ask for confirmation first.

## ExecPlans
When writing complex features or significant refactors, use an ExecPlan (as described in .agent/PLANS.md) from design to implementation.

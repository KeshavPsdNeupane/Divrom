# Divrom

A feature-rich 2D Unity game project built with modular architecture, featuring advanced AI systems, character statistics, modular sprite animation, and a flexible entity-component framework.

---

## Table of Contents

- [Overview](#overview)
- [Project Structure](#project-structure)
- [Core Systems](#core-systems)
  - [Entity Component System](#entity-component-system-ecr)
  - [Initialization System](#initialization-system)
  - [Service Locator Pattern](#service-locator-pattern)
- [Features](#features)
  - [AI System](#ai-system)
  - [Character Stats System](#character-stats-system)
  - [Modular Sprite Animation System](#modular-sprite-animation-system)
  - [Inventory System](#inventory-system)
  - [State Machine](#state-machine)
  - [Movement & Combat](#movement--combat)
- [Custom Assemblies](#custom-assemblies)
- [Dependencies](#dependencies)
- [Getting Started](#getting-started)
- [Editor Tools](#editor-tools)
- [Architecture Patterns](#architecture-patterns)
- [Contributing](#contributing)

---

## Overview

**Divrom** is a 2D Unity game project showcasing professional game development practices with custom frameworks and systems. The project emphasizes:

- **Modularity**: Clean separation of concerns with custom assembly definitions
- **Scalability**: Built-in support for complex AI behaviors and character customization
- **Maintainability**: Well-documented code with extensive use of interfaces and abstract patterns
- **Performance**: Optimized entity component registry and efficient update loops

---

## Project Structure

```
Divrom/
├── Assets/
│   └── _Project/
│       ├── Graphics/           # Sprites, animations, tiles
│       ├── Prefabs/            # Reusable game objects
│       ├── ScriptableObjects/  # Data-driven configurations
│       ├── Scripts/
│       │   ├── Cores/          # Core systems and utilities
│       │   │   ├── ECS/        # Entity Component System
│       │   │   ├── Init/       # Initialization framework
│       │   │   ├── ServiceLocator/  # Service management
│       │   │   ├── Extension/  # Extension methods
│       │   │   └── CompilerServices/  # Custom logging
│       │   ├── Features/       # Game features
│       │   │   ├── AI/         # AI decision-making
│       │   │   ├── Actors/     # Player/NPC controllers
│       │   │   ├── Animation/  # Modular sprite animation
│       │   │   ├── Component/  # Movement, Attack components
│       │   │   ├── EntityStat/ # Character stats
│       │   │   └── Inventory/  # Item management
│       │   └── Managers/       # Global managers
│       ├── UI/                 # User interface
│       └── _BootStrap/         # Scene initialization
├── Packages/                   # Unity packages
└── ProjectSettings/            # Unity project configuration
```

---

## Core Systems

### Entity Component System (ECS)

A custom lightweight ECS-like architecture that provides centralized component management and initialization.

#### Key Classes

- **`EntityComponentStore`**: Container for entity components with automatic initialization
- **`EntityComponentRegistry`**: Runtime registry for fast component lookup
- **`EntityManager`**: Manages entity identity and component store references

#### Features

- Type-safe component registration and retrieval
- Automatic component initialization through `InitCallerManager`
- Optional behavioral component optimization
- Configurable type exclusion for registry optimization

#### Usage Example

```csharp
[SerializeField] private EntityComponentStore ecr;

if (ecr.ComponentRegistry.TryGetComponent<CharacterStatsSystem>(out var statsSystem))
{
    float health = statsSystem.GetStatValue(CharacterStatType.HP);
}
```

---

### Initialization System

A robust initialization framework ensuring proper component setup and lifecycle management.

#### Key Classes

- **`InitializableBase`**: Base class for all initializable components
- **`InitCallerManager`**: Orchestrates initialization order
- **`IInitializable`**: Interface for initialization contract

#### Lifecycle Flow

```
OnInit() → OnEnable() → OnUpdate() / OnFixedUpdate() → OnDisable()
```

---

### Service Locator Pattern

Provides global and scene-based service management without tight coupling.

#### Components

- **`GlobalServiceLocator`**: Singleton for game-wide services
- **`SceneServiceLocator`**: Scene-specific services
- **`ServiceBase`**: Base class for all services

---

## Features

### AI System

A layered, extensible AI framework with a clean separation between **execution** and **decision-making**. The `AIBrain` drives action execution and lifecycle; the `AIBrainAlgorithm` is a swappable planner that decides what to do next. This means alternative planners (GOAP, Behavior Trees) can be dropped in without touching action implementations.

#### Architecture

```
AIBrain (Executor)
    ├── AIBrainAlgorithm (Planner — swappable)
    │   ├── UtilityAiAlgorithm
    │   └── [Future: GOAP, Behavior Trees]
    ├── Context (World State)
    ├── Sensor (Perception)
    └── BaseActionSO (Actions)
```

#### Action Layers

Actions are split across two base types to keep execution and decision-making concerns separate:

- **`BaseActionSO`** — execution contract only. Knows nothing about utility scoring. Implements `Initialize`, `TickUpdate`, `TickFixedUpdate`, `EndOrAbort`, and `MarkCompleted`. Any planner can use these.
- **`ActionSO`** — extends `BaseActionSO` with utility-specific data: considerations, decay, momentum bias, weight regeneration, and cooldown. Only relevant to the Utility AI planner.

This means if you swap the planner for GOAP tomorrow, all your `BaseActionSO` implementations remain untouched.

#### Action Lifecycle

```
Initialize() → TickUpdate() / TickFixedUpdate() → MarkCompleted() / EndOrAbort()
```

Status flows through `ExecutionActionStatus`:

```
NotInitialized → Running → Success
                         → NotInitialized (via EndOrAbort)
```

#### Interrupt System

`AIBrain` supports priority-based interrupts via `IInterruptOther` components:

| Priority | Behavior |
|----------|----------|
| `Soft` | Clears plan; stops current action only if it is interruptible |
| `Hard` | Forces stop of current action unconditionally |
| `Death` | Forces stop and disables the brain entirely |

---

### Utility AI Algorithm

The `UtilityAiAlgorithm` is the primary planner. Rather than picking the highest-scoring action every tick naively, it maintains a **short-term memory** of recently selected actions and uses **bias weights** to shape behavior over time — producing natural variety without hardcoded state machines.

#### How It Works

Every action carries a `biasWeight` (starts at 1.0) that modifies its raw utility score at evaluation time:

```
effectiveScore = action.Evaluate(ctx) * biasWeight  [+ momentumBias if currently active]
```

The algorithm then applies three mechanisms to keep behavior varied and healthy:

**1. Decay** — when an action is selected, its `biasWeight` is multiplied by `DecayRate` (< 1.0). Repeated selections progressively lower the effective score, creating pressure to try other actions. Weight cannot fall below `minActionWeight` to prevent permanent suppression.

**2. Compound Regeneration** — while an action sits idle in memory, its weight recovers using a compound growth formula:

```
recovered = biasWeight * ((1 + WeightRegenRate)^ticks - 1)
```

Recovery accelerates over time — a long-ignored action bounces back faster than a recently suppressed one, which produces natural behavioral cycles.

**3. Momentum Bias** — a small flat bonus added to the currently active action's score. This discourages jittery switching when two actions score similarly.

#### Short-Term Memory

A bounded `Memory` structure (priority queue + hash set) tracks recently used actions:

- New actions enter memory and immediately receive their first decay
- Actions already in memory receive further decay each time they are selected
- When memory is full, the lowest-weight action is evicted and its weight is reset — anti-starvation
- When idle wins, the most-suppressed action in memory is rescued and its weight reset, giving it a fair second chance

Memory capacity is configurable via `shortTermMemorySize` in the inspector.

#### Cooldown

For actions where decay alone is insufficient to prevent immediate re-selection (e.g. a ranged attack that scores persistently high while the player is in range), a time-based cooldown is available:

```csharp
// On the ActionSO asset — set in the inspector
[SerializeField, Min(0f)] private float cooldownDuration = 0f;
```

When an action deactivates, `cooldownUntil = Time.time + cooldownDuration`. The action is skipped entirely during evaluation until the timer expires. The idle action is always exempt from cooldown to guarantee a safe fallback.

`CooldownDuration` maps directly to wall-clock seconds regardless of how frequently the external caller drives evaluation — making it reliable even with irregular tick rates.

#### Utility Scoring — Considerations

Each `ActionSO` holds a list of `ConsiderationSO` assets. Scoring uses **multiplicative evaluation with compensated utility** to balance actions fairly across different numbers of considerations:

```
FinalScore = (C1 × C2 × ... × CN) ^ (1/N)
```

The `^(1/N)` normalization (compensated utility) prevents actions with many considerations from being unfairly penalized compared to simpler ones.

**ConsiderationSO** is abstract — implement `Evaluate(IReadOnlyContext)` returning a `(float score, int multiplicationCount)` tuple. The multiplication count drives the compensated utility normalization correctly.

**CompositeConsideration** allows nesting multiple considerations under a single slot on an action — useful for compound conditions like "is in range AND has line of sight AND has ammo":

```
ActionSO
├── CompositeConsideration  ("Combat Ready")
│   ├── InRangeConsideration
│   ├── HasLineOfSightConsideration
│   └── HasAmmoConsideration
└── HealthThresholdConsideration
```

Composite considerations propagate their internal multiplication counts correctly so compensated utility remains accurate regardless of nesting depth.

#### Evaluation is Event-Driven

`AIBrain` drives evaluation — not a per-frame loop. A new plan is fetched either when the current action completes or when the optional `refreshInterval` timer fires. This means the AI ticks at a controlled cadence rather than every frame, and `CooldownDuration` values should be tuned relative to your `refreshInterval`.

#### Inspector Reference

| Field | Location | Purpose |
|-------|----------|---------|
| `shortTermMemorySize` | `UtilityAiAlgorithm` | How many recent actions to track (1–20) |
| `minActionWeight` | `UtilityAiAlgorithm` | Floor weight an action can decay to (0.05–1.0) |
| `refreshInterval` | `AIBrain` | Seconds between forced re-evaluations (0 = disabled) |
| `decayRate` | `ActionSO` | Per-selection weight multiplier (0.05–1.0) |
| `weightRegenRate` | `ActionSO` | Compound recovery rate per idle tick (0.0–1.0) |
| `momentumBias` | `ActionSO` | Flat score bonus while action is active (0.01–0.1) |
| `cooldownDuration` | `ActionSO` | Seconds before action can be re-selected (0 = none) |

#### Tuning Guide

- **Patrol / Idle** — low decay, no cooldown. These are filler behaviors; keep them freely available.
- **Reposition / Take Cover** — higher decay, no cooldown. Should happen occasionally, not on loop.
- **Ranged Attack** — moderate decay + cooldown. Decay prevents tunnel vision; cooldown enforces fire rate. Set `cooldownDuration = 1f / attacksPerSecond`.

#### Debug

In Play Mode, an `OnDrawGizmos` overlay renders above each agent showing the currently active action name and its evaluated score — useful for rapid tuning without opening the Profiler.

---

### Character Stats System

Flexible, event-driven stat management with modifiers and scaling.

#### Stat Types

```csharp
public enum CharacterStatType { HP, ATK, DEF, MATK, SPD, CRATE, CDMG }
public enum DamageType { Physical, Fire, Ice, Lightning, Poison }
```

#### Stat Composition

```
FinalValue = (BaseValue + PointStats) × (1 + Multiplier) + PerkStats
```

#### Features

- Event-driven subscription model for reactive UI updates
- Status effects with duration
- Level scaling with configurable growth
- Damage type-specific resistances

---

### Modular Sprite Animation System

Advanced 2D character customization supporting multiple body parts, genders, races, and color variations.

#### Generic Type System

```csharp
SpriteAnimationLibraryAssetDefinition<TGender, TRace, TColorPermutation, TPart>
```

#### Features

- Multi-part system supporting 100+ unique body and equipment parts
- Runtime library switching for dynamic customization
- Editor preview with real-time visualization

---

### Inventory System

Component-based inventory management with UI integration.

- Object pooling for UI elements
- Drag-and-drop item management
- Flexible slot-based system
- Entity component integration

---

### State Machine

Hierarchical state machine for player and AI entities.

```
EntityStateManager
    ├── EntityStateController
    └── EntityStates (Idle, Move, Attack, ...)
```

---

### Movement & Combat

Intent-based movement with stats-modulated speed and physics integration. Combat supports critical hits, damage types, and resistance lookups against `CharacterStatsSystem`.

```csharp
public enum MovementIntentType { Move, Dash, Knockback }
```

---

## Custom Assemblies

| Assembly | Purpose |
|----------|---------|
| `Kope.Core.Runtime` | Core systems (ECS, Init, Service Locator) |
| `Kope.Character.Stats.Runtime` | Character stat management |
| `Kope.ModularSpriteAnimation.Runtime` | Sprite animation system |
| `Kope.ModularSpriteAnimation.Editor` | Animation editor tools |
| `Assembly-CSharp` | Main game scripts |

---

## Dependencies

### Unity Packages

- Unity 2D packages (Animation, Tilemap, Sprite utilities)
- Unity Input System (1.14.2)
- Cinemachine (3.1.5)
- Universal Render Pipeline (17.2.0)
- TextMeshPro

### Third-Party Libraries

- **ZLinq**: Performance-optimized LINQ alternative

---

## Getting Started

### Prerequisites

- Unity 6000.3.10f1 (or closest compatible LTS)
- Git LFS for large binary assets

### Installation

1. Clone the repository:
   ```bash
   git clone [repository-url]
   ```
2. Open the folder from Unity Hub
3. Open the bootstrap scene in `Assets/_Project/_BootStrap/`
4. Press Play

### Controls

- **WASD / Arrow Keys**: Move
- **Attack Button**: Configured via Input System
- **Tab / I**: Open inventory
- **Escape**: Menu

---

## Editor Tools

### Sprite Tools

- **Grid Auto Slicer** (`Tools → Grid Auto Slicer`): Batch-slices sprite sheets with auto-naming and transparent frame detection
- **Sprite Library Populator** (`Tools → Populate Library From Dummy`): Creates sprite library instances from template structures

### Animation Tools

- **Static Animation Library Resolver Editor**: Custom inspector with category/label preview and manual sprite snapping

---

## Architecture Patterns

| Pattern | Where Used |
|---------|-----------|
| Service Locator | Global and scene service access |
| Strategy | Swappable `AIBrainAlgorithm` implementations |
| Observer | Event-driven stat and UI updates |
| Component | Modular entity behaviors via ECS registry |
| Command | Input action encapsulation |
| Object Pool | UI element recycling in inventory |
| State | Entity behavior state machine |

### Design Principles

- **Separation of Concerns**: Execution layer (`BaseActionSO`) is fully decoupled from decision layer (`ActionSO`, planners)
- **Open/Closed**: New actions, considerations, and planners extend without modifying core
- **Dependency Inversion**: Systems depend on `IReadOnlyContext`, not concrete world state
- **Single Responsibility**: Each class has one clearly defined purpose

---

## Code Style

### Naming Conventions

- **Classes / Structs**: PascalCase (`CharacterStatsSystem`)
- **Methods**: PascalCase (`GetStatValue()`)
- **Private Fields**: camelCase with `this.` prefix (`this.statsSystem`)
- **Public Properties**: PascalCase (`CurrentStats`)
- **Constants**: UPPER_CASE (`DEFAULT_INITIAL_WEIGHT`)

### Documentation

All public APIs use XML documentation:

```csharp
/// <summary>
/// Retrieves the current value of a character stat.
/// </summary>
/// <param name="type">The type of stat to retrieve.</param>
/// <returns>The current stat value, or 0 if not found.</returns>
public float GetStatValue(CharacterStatType type) { }
```

---

## Performance Considerations

- **Component Caching**: Components retrieved once during initialization, stored as references
- **Dictionary Lookups**: O(1) component retrieval via `ComponentRegistry`
- **Priority Queue**: O(log n) memory updates in `UtilityAiAlgorithm`
- **Object Pooling**: Reduced GC pressure in inventory UI
- **Event-Based Updates**: Stat UI only redraws on change
- **Controlled AI Tick Rate**: Evaluation driven by action completion and refresh timer, not every frame

---

## Known Issues & Limitations

1. **AI System**: Only Utility AI implemented (GOAP and Behavior Trees planned)
2. **Multiplayer**: Not yet implemented
3. **Save System**: Entity state persistence not implemented
4. **Animation**: Limited to 2D sprite-based animation

---

## Future Roadmap

- [ ] GOAP (Goal-Oriented Action Planning) AI algorithm
- [ ] Behavior Tree AI algorithm
- [ ] Save/Load system with JSON serialization
- [ ] Quest system
- [ ] Dialogue system
- [ ] Network multiplayer support
- [ ] Sound system integration
- [ ] Localization support

---

## Changelog

**Features**:
- ✅ Entity Component System
- ✅ Utility AI with short-term memory, decay, compound regen, momentum, and cooldown
- ✅ Character stats with modifiers and status effects
- ✅ Modular sprite animation
- ✅ Basic inventory system
- ✅ Player movement and combat
- ✅ State machine architecture
- ✅ Service locator pattern
- ✅ Custom editor tools

---

## License

This repository uses MIT for original code authored in this project (see `LICENSE`).

Third-party libraries, external assets, and Unity/Mono runtime components retain their own licenses.

---

**Last Updated**: 2026-03-16
**Unity Version**: 6000.3.10f1
**Render Pipeline**: Universal Render Pipeline (URP)
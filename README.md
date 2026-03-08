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
// Register components in EntityComponentStore
[SerializeField] private EntityComponentStore ecr;

// Retrieve components at runtime
if (ecr.ComponentRegistry.TryGetComponent<CharacterStatsSystem>(out var statsSystem))
{
    // Use the component
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

#### Features

- Guaranteed initialization order
- Lifecycle hooks: `OnInit()`, `OnUpdate()`, `OnFixedUpdate()`
- Debug tracking with stack trace information
- Prevention of duplicate initialization

#### Lifecycle Flow

```
OnInit() → OnEnable() → OnUpdate()/OnFixedUpdate() → OnDisable()
```

---

### Service Locator Pattern

Provides global and scene-based service management without tight coupling.

#### Components

- **`GlobalServiceLocator`**: Singleton for game-wide services (InputManager, etc.)
- **`SceneServiceLocator`**: Scene-specific services
- **`ServiceBase`**: Base class for all services

#### Registered Services

- `InputManager`: Centralized input handling
- `ItemDragDropManager`: UI drag-and-drop operations

---

## Features

### AI System

A powerful, extensible AI framework supporting multiple decision-making algorithms.

#### Architecture

```
AIBrain (Executor)
    ├── AIBrainAlgorithm (Planner)
    │   ├── UtilityAiAlgorithm
    │   └── [Future: GOAP, Behavior Trees]
    ├── Context (World State)
    ├── Sensor (Perception)
    └── BaseActionSO (Actions)
```

#### Utility AI Algorithm

- **Considerations**: Evaluate world state conditions
- **Actions**: Executable behaviors with utility scores
- **Compensated Utility**: Balances multi-factor scoring
- **Loop Prevention**: Automatic fallback to idle action after repeated selections

#### Key Features

1. **Modular Actions**: ScriptableObject-based action definitions
2. **Context System**: Centralized world state management
3. **Sensor Integration**: Circle-based entity detection
4. **Interrupt System**: Priority-based action interruption
5. **Refresh Timer**: Periodic plan reevaluation

#### Action Lifecycle

```csharp
Initialize() → TickUpdate() → TickFixedUpdate() → EndOrAbort()
```

#### Utility Scoring

Uses multiplicative scoring with compensated utility:
```
FinalScore = (C1 × C2 × C3 × ...)^(1/N)
```
Where N is the number of considerations.

---

### Character Stats System

Flexible, event-driven stat management with modifiers and scaling.

#### Stat Types

```csharp
public enum CharacterStatType
{
    HP,      // Health Points
    ATK,     // Physical Attack
    DEF,     // Defense
    MATK,    // Magic Attack
    SPD,     // Movement Speed
    CRATE,   // Critical Rate
    CDMG     // Critical Damage
}

public enum DamageType
{
    Physical, Fire, Ice, Lightning, Poison
}
```

#### Components

- **`AdvanceStat`**: Complex stat with base, multiplier, point, and perk modifiers
- **`StatBase`**: Simple stat with basic modifiers
- **`CharacterStatsSystem`**: Central stat management system
- **`CharacterStatsSO`**: ScriptableObject configuration

#### Stat Composition

```
FinalValue = (BaseValue + PointStats) × (1 + Multiplier) + PerkStats
```

#### Features

- **Event-Driven**: Subscribe to stat changes for reactive UI updates
- **Status Effects**: Temporary stat modifiers with duration
- **Level Scaling**: Automatic stat increases on level-up
- **Resistance System**: Damage type-specific resistances

#### Usage Example

```csharp
// Subscribe to stat changes
statsSystem.StatsSubscribe(CharacterStatType.HP, (newValue) => 
{
    healthBar.UpdateDisplay(newValue);
});

// Modify stats
statsSystem.AddStatModifier(new StatusEffect 
{
    statType = CharacterStatType.ATK,
    modifier = 10f,
    duration = 5f,
    isPercentage = false
});

// Level up
statsSystem.TriggerLevelUp();
```

---

### Modular Sprite Animation System

Advanced 2D character customization and animation system supporting multiple body parts, genders, races, and color variations.

#### Architecture

**Generic Type System**:
```csharp
SpriteAnimationLibraryAssetDefinition<TGender, TRace, TColorPermutation, TPart>
```

#### Components

1. **Asset Definitions**:
   - `BodyRegionAnimationLibraryAsset`: Character body parts
   - `EquipmentAnimationLibraryAsset`: Weapons, armor, accessories

2. **Runtime Resolvers**:
   - `StaticBaseCharacterAnimationLibraryResolver`: Manages sprite library overrides

3. **Custom Libraries**:
   - `CustomSpriteLibraryDefinition<TPart>`: Part-specific sprite management

#### Features

- **Multi-Part System**: Support for 100+ unique body/equipment parts
- **Gender & Race Support**: Configurable character variants
- **Color Permutations**: Multiple color schemes per asset
- **Runtime Switching**: Dynamic character customization
- **Editor Preview**: Real-time visualization in Unity Editor

#### Example Enums

```csharp
public enum BodyRegionEnum : short
{
    None = 0,
    Head = 100,
    Torso = 200,
    Legs = 300,
    Arms = 400
}

public enum EquipmentPartEnum : short
{
    None = 0,
    Helmet = 100,
    Chest = 200,
    Weapon = 300
}
```

---

### Inventory System

Component-based inventory management with UI integration.

#### Components

- **`InventorySystem`**: Core inventory logic
- **`InventoryHolder`**: Entity inventory component
- **`PlayerInventoryDisplay`**: UI visualization
- **`ItemSlotUI`**: Individual item slot representation
- **`PlayerItemCollector`**: Automatic item pickup

#### Features

- Object pooling for UI elements
- Drag-and-drop item management
- Flexible slot-based system
- Entity component integration

---

### State Machine

Hierarchical state machine for player and AI entities.

#### Architecture

```
EntityStateManager (State Machine)
    ├── EntityStateController (Context)
    └── EntityStates (State Collection)
        ├── EntityIdle
        ├── EntityMove
        └── EntityAttack
```

#### Features

- Animation state integration
- Command acceptance filtering
- State transition validation
- Physics-aware updates

#### State Interface

```csharp
public interface IStateCanAcceptCommand
{
    bool CanAcceptCommand { get; }
}
```

---

### Movement & Combat

#### Movement System

**`MovementComponentBase`**:
- Input-driven movement intents
- Stats-based speed modulation
- Physics integration (Rigidbody2D)
- Flexible intent system

**Movement Intent Types**:
```csharp
public enum MovementIntentType
{
    Move,      // Standard movement
    Dash,      // Quick burst
    Knockback  // Force-based movement
}
```

#### Combat System

**`AttackComponentBase`**:
- Weapon data integration
- Critical hit system
- Damage calculation with stats
- Animation-driven attacks

---

## Custom Assemblies

The project uses Assembly Definition files for clean code organization:

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

- **Unity 2D Packages**: Animation, Tilemap, Sprite utilities
- **Unity Input System** (1.14.2): Modern input handling
- **Cinemachine** (3.1.5): Camera control
- **Universal Render Pipeline** (17.2.0): 2D rendering
- **TextMeshPro**: Advanced text rendering
- **NuGet for Unity**: External package management

### Third-Party Libraries

- **ZLinq**: Custom LINQ utilities for performance

---

## Getting Started

### Prerequisites

- **Unity 2022.3 LTS** or newer
- **Git LFS**: For large binary assets
- **.NET SDK**: For C# compilation

### Installation

1. Clone the repository:
   ```bash
   git clone [repository-url]
   cd Divrom
   ```

2. Open the project in Unity Hub

3. Let Unity import packages (may take 5-10 minutes on first load)

4. Open the bootstrap scene: `Assets/_Project/_BootStrap/Bootstrap.unity`

### Running the Project

1. Press **Play** in Unity Editor
2. Use WASD or arrow keys to move
3. Press designated attack button (configured in Input System)
4. Open inventory with Tab/I key
5. Access menu with Escape

---

## Editor Tools

### Sprite Tools

#### Grid Auto Slicer
**Menu**: `Tools → Grid Auto Slicer`

Automatically slices sprite sheets into animation frames with:
- Grid-based slicing
- Auto-naming based on `SpriteRowNamingData`
- Subcategory support
- Transparent frame detection

#### Sprite Library Populator
**Menu**: `Tools → Populate Library From Dummy`

Creates sprite library instances from template structures:
- Category/label matching
- Automatic sprite assignment
- Batch processing

### Animation Tools

#### Static Animation Library Resolver Editor

Custom inspector for resolving sprite libraries with:
- Category/label preview
- Manual sprite snapping
- Multi-object editing support

---

## Architecture Patterns

### Patterns Used

1. **Service Locator**: Global service access without singletons
2. **Component Pattern**: Modular entity behaviors
3. **Observer Pattern**: Event-driven stat and UI updates
4. **Strategy Pattern**: Interchangeable AI algorithms
5. **Command Pattern**: Input action encapsulation
6. **Object Pool**: UI element recycling
7. **State Pattern**: Entity behavior states

### Design Principles

- **Separation of Concerns**: Clear module boundaries
- **Open/Closed Principle**: Extensible without modification
- **Dependency Inversion**: Depend on abstractions
- **Interface Segregation**: Small, focused interfaces
- **Single Responsibility**: Each class has one purpose

---

## Code Style

### Naming Conventions

- **Classes/Structs**: PascalCase (`CharacterStatsSystem`)
- **Methods**: PascalCase (`GetStatValue()`)
- **Private Fields**: camelCase with prefix (`this.statsSystem`)
- **Public Properties**: PascalCase (`CurrentStats`)
- **Constants**: UPPER_CASE (`ATTACK_ANIMATION_THRESHOLD`)

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

### Optimizations

1. **Component Caching**: Components retrieved once during initialization
2. **Dictionary Lookups**: O(1) component retrieval
3. **Object Pooling**: Reduced garbage collection for UI
4. **Event-Based Updates**: Only update when values change
5. **Custom Update Loops**: Avoid MonoBehaviour overhead where possible

### Profiling Tips

- Use Unity Profiler for frame time analysis
- Monitor GC allocations in inventory UI
- Check AI planner update frequency
- Profile sprite library resolution times

---

## Known Issues & Limitations

1. **AI System**: Currently only Utility AI implemented (GOAP planned)
2. **Multiplayer**: Not yet implemented
3. **Save System**: Entity state persistence not implemented
4. **Animation**: Limited to 2D sprite-based animation

---

## Future Roadmap

### Planned Features

- [ ] GOAP (Goal-Oriented Action Planning) AI algorithm
- [ ] Behavior Tree AI algorithm  
- [ ] Save/Load system with JSON serialization
- [ ] Quest system
- [ ] Dialogue system
- [ ] Network multiplayer support
- [ ] Advanced particle effects
- [ ] Sound system integration
- [ ] Localization support

---

## Contributing

### Guidelines

1. Follow existing code style and architecture patterns
2. Write XML documentation for public APIs
3. Add unit tests for new systems
4. Update this README when adding major features
5. Use meaningful commit messages

### Branch Strategy

- `main`: Stable release branch
- `develop`: Integration branch for features
- `feature/*`: Individual feature branches
- `bugfix/*`: Bug fix branches

---

## License

[Specify your license here]

---

## Credits

### Development Team

[Add your team members here]

### Third-Party Assets

- **Unity Technologies**: 2D packages, URP, Input System
- **ZLinq**: Performance-optimized LINQ alternative

---

## Contact

For questions, issues, or contributions:
- **Project Repository**: [Add GitHub/GitLab URL]
- **Issue Tracker**: [Add issue tracker URL]
- **Documentation**: [Add docs URL if available]

---

## Changelog

### Version 0.1.0 (Current)

**Features**:
- ✅ Entity Component System
- ✅ Utility AI with context system
- ✅ Character stats with modifiers
- ✅ Modular sprite animation
- ✅ Basic inventory system
- ✅ Player movement and combat
- ✅ State machine architecture
- ✅ Service locator pattern
- ✅ Custom editor tools

**Systems**:
- Core initialization framework
- Input management system
- UI state management
- Sensor-based entity detection

---

**Last Updated**: February 2026  
**Unity Version**: 2022.3+  
**Render Pipeline**: Universal Render Pipeline (URP)

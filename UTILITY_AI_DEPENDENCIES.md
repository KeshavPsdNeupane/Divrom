# Utility AI System - Complete Script Dependencies

## Overview
This document lists all scripts required for the Utility AI system. Use this as a checklist when creating a separate showcase project.

**Total Scripts**: ~50+ files organized across multiple directories.

---

## 📁 Directory Structure & Scripts

### 1. **Core Utility AI System**
**Location**: `Assets/_Project/Scripts/Features/AI/Algorithm/UtilityAi/`

#### Algorithm Core
- ✅ `UtilityAiAlgorithm.cs` - Main Utility AI implementation with decision algorithm
- ✅ `UtilityAiConfig.cs` - Configuration scriptable object for the algorithm
- ✅ `ActionType.cs` - Enum defining action type categories

#### Actions
**Location**: `Assets/_Project/Scripts/Features/AI/Algorithm/UtilityAi/Action/`
- ✅ `ActionSO.cs` - Base class for all Utility AI actions
- ✅ `IdleAction.cs` - Default idle action implementation
- ✅ `MoveToTargetAction.cs` - Movement towards target action
- ✅ `RandomWanderActionSO.cs` - Random wandering action

#### Considerations (Evaluation System)
**Location**: `Assets/_Project/Scripts/Features/AI/Algorithm/UtilityAi/Consideration/`
- ✅ `ConsiderationSO.cs` - Base class for decision considerations
- ✅ `HealthConsideration.cs` - Health-based evaluation consideration
- ✅ `CompositeConsideration.cs` - Container for multiple considerations
- ✅ `ConstantConsideration/ConstantConsideration.cs` - Constant value consideration
- ✅ `TargerDistanceConsideration/TargetDistanceConsideration.cs` - Distance-based consideration

---

### 2. **AI Framework Base Classes**
**Location**: `Assets/_Project/Scripts/Features/AI/`

- ✅ `AIBrain.cs` - Main AI brain controller component (MonoBehaviour)
- ✅ `AIBrainAlgorithm.cs` - Abstract base class for AI decision algorithms
- ✅ `BaseActionSO.cs` - Base class for all action types (shared with other AI systems)
- ✅ `EntitySensor.cs` - Collider-based sensor for entity detection

#### Editor Tools
**Location**: `Assets/_Project/Scripts/Features/AI/Editor/`
- ✅ `AIBrainEditor.cs` - Custom inspector editor for AIBrain (optional but recommended)

---

### 3. **Context & State Management**
**Location**: `Assets/_Project/Scripts/Features/Actors/`

#### Context System
**Location**: `Assets/_Project/Scripts/Features/Actors/Context/`
- ✅ `IReadOnlyContext.cs` - Read-only context interface
- ✅ `Context.cs` - Mutable context implementation for entity data and targets

#### State Management
- ✅ `EntityStateController.cs` - Controls entity state transitions
- ✅ `EntityStateManager.cs` - Manages entity state machine
- ✅ `EntityStates.cs` - Entity state enumerations
- ✅ `IStateMachineCanAcceptCommand.cs` - State machine command interface
- ✅ `EntityInitCaller.cs` - Entity initialization caller

---

### 4. **Core Framework - Initialization System**
**Location**: `Assets/_Project/Scripts/Cores/Init/`

- ✅ `InitializableBase.cs` - Base class for initializable objects
- ✅ `IInitializable.cs` - Initialization interface
- ✅ `InitCallerManager.cs` - Manages initialization callbacks

---

### 5. **Core Framework - Entity Component System (ECS)**
**Location**: `Assets/_Project/Scripts/Cores/EntityCompoentsRegistry/`

#### Core ECS
- ✅ `EntityManager.cs` - Manages entity components and initialization
- ✅ `EntityComponentsRegistry.cs` - Registry factory for entity components
- ✅ `ComponentRegistry.cs` - Individual component registry per entity
- ✅ `EntityDetail.cs` - Entity metadata and tags

#### ECS Interfaces
**Location**: `Assets/_Project/Scripts/Cores/EntityCompoentsRegistry/Interfaces/`
- ✅ `IReadOnlyComponentRegistry.cs` - Read-only registry interface
- ✅ `IEntityDiedOrPooled.cs` - Entity lifecycle interface

#### ECS Configuration
**Location**: `Assets/_Project/Scripts/Cores/EntityCompoentsRegistry/config/`
- ✅ `EntityComponentStoreConfig.cs` - Component storage configuration
- ✅ `EntityCommonNameConfig.cs` - Common entity name configuration

---

### 6. **Core Framework - Service Locator**
**Location**: `Assets/_Project/Scripts/Cores/ServiceLocator/`

- ✅ `ServiceLocator.cs` - Generic service locator base
- ✅ `GlobalServiceLocator.cs` - Global service locator singleton
- ✅ `SceneServiceLocator.cs` - Scene-level service locator
- ✅ `ServiceBase.cs` - Base class for services

---

### 7. **Core Framework - Sensors**
**Location**: `Assets/_Project/Scripts/Cores/`

- ✅ `SensorBase.cs` - Base class for all sensor systems

---

### 8. **Core Framework - Extensions & Utilities**
**Location**: `Assets/_Project/Scripts/Cores/Extension/`

- ✅ `TypeExtension.cs` - Type reflection helper methods
- ✅ `StringExtension.cs` - String utility methods
- ✅ `FloatExtension.cs` - Float utility methods
- ✅ `EnumExtensions.cs` - Enum utility methods
- ✅ `UnityTypeExtention.cs` - Unity type helper methods

---

### 9. **Core Framework - Compiler Services & Logging**
**Location**: `Assets/_Project/Scripts/Cores/CompilerServices/`

- ✅ `ILogger.cs` - Logger interface
- ✅ `Logger.cs` - Logger implementation
- ✅ `UnityLogger.cs` - Unity-specific logger
- ✅ `FileLogger.cs` - File-based logger (optional)

---

### 10. **Core Framework - Identity System**
**Location**: `Assets/_Project/Scripts/Cores/Identity/`

- ✅ `HashedTag.cs` - Hashed tag system for entity identification
- ✅ `UniqueID.cs` - Unique identifier component

---

### 11. **Core Framework - Attributes**
**Location**: `Assets/_Project/Scripts/Cores/Attribute/`

- ✅ `ReadOnlyAttribute.cs` - Read-only field attribute for Inspector
- ✅ `SelectionBase.cs` - Selection base attribute

---

### 12. **Core Framework - Execution Order**
**Location**: `Assets/_Project/Scripts/Cores/ExecutionOrder/`

- ✅ `SceneExecutionTracker.cs` - Tracks scene execution order
- ✅ `CustomExecutionOrder.cs` - Custom execution order management

---

### 13. **Third-Party Dependencies**
**Location**: `Assets/_Project/_ThirdParty/`

#### Priority Queue (Required for action memory management)
**Location**: `Assets/_Project/_ThirdParty/PriorityQueue/`
- ✅ `PriorityQueueSimple.cs` - Simple priority queue implementation (CRITICAL)
- ✅ `KeyedEquatable.cs` - Key equatable interface for priority queue

#### Utilities
- ✅ `Timer.cs` - Timer utility class
- ✅ `SerializableDictionary.cs` - Serializable dictionary for Inspector

---

## 📋 File Count Summary

| Category | Count |
|----------|-------|
| Utility AI Core | 3 |
| Utility AI Actions | 4 |
| Utility AI Considerations | 5 |
| AI Framework | 5 |
| Context & State | 6 |
| Initialization System | 3 |
| Entity Component System | 8 |
| Service Locator | 4 |
| Sensors | 1 |
| Extensions | 5 |
| Compiler Services | 4 |
| Identity System | 2 |
| Attributes | 2 |
| Execution Order | 2 |
| Third-Party | 4 |
| **TOTAL** | **~60 files** |

---

## 🎯 Essential Dependencies (Minimum Setup)

If you want a **minimal showcase**, these are the absolutely critical files:

### Must Have:
1. **Algorithm**: UtilityAiAlgorithm.cs, UtilityAiConfig.cs, ActionType.cs
2. **Actions**: ActionSO.cs + any action implementations (IdleAction.cs minimum)
3. **Considerations**: ConsiderationSO.cs + at least one consideration
4. **Framework**: 
   - AIBrain.cs, AIBrainAlgorithm.cs, BaseActionSO.cs
   - Context.cs, IReadOnlyContext.cs
   - EntityManager.cs, ComponentRegistry.cs, IReadOnlyComponentRegistry.cs
   - InitializableBase.cs, IInitializable.cs
   - HashedTag.cs
5. **Third-Party**: 
   - PriorityQueueSimple.cs (CRITICAL - core to algorithm)
   - KeyedEquatable.cs

**Minimum Count**: ~25-30 files

---

## 🚀 Recommended for Full Showcase

Include all files listed above for a complete, production-ready showcase with:
- Multiple action types
- Various consideration systems
- Full context management
- Proper logging and debugging
- Editor tools for inspection

---

## 📦 Assembly Definition Files Required

When setting up your showcase project, ensure these .asmdef files are created:

- `Kope.Core.Runtime.asmdef`
- `Kope.AI.Runtime.asmdef` (for AI scripts)
- `Out.ThirdParty.Stuff.asmdef` (for third-party code)
- `Kope.AI.Editor.asmdef` (if including editor tools)

---

## ⚙️ Configuration & Setup Tips

1. **Priority Queue Integration**: The `PriorityQueueSimple.cs` is critical for the algorithm's memory management system. Cannot work without it.

2. **Context System**: The context holds entity references and component registries. Ensure EntityManager is properly initialized before AIBrain.

3. **Considerations**: Each consideration needs to be added to an ActionSO's list to be evaluated. Create consideration assets in your project.

4. **Actions**: Each action needs to be registered in the UtilityAiConfig or manually assigned to the AIBrain component.

5. **Entity Sensor**: The EntitySensor uses a CircleCollider2D to detect entities. Requires EntityManager component on detected entities.

---

## 🔗 Inter-Dependency Graph

```
UtilityAiAlgorithm
├── UtilityAiConfig
├── ActionSO (through ActionEntry)
│   ├── ConsiderationSO
│   └── BaseActionSO
├── PriorityQueueSimple (CRITICAL)
└── AIBrainAlgorithm
    └── InitializableBase

AIBrain
├── AIBrainAlgorithm
├── EntityComponentsRegistry
│   ├── EntityManager
│   ├── ComponentRegistry
│   └── IReadOnlyComponentRegistry
├── Context
│   └── IReadOnlyContext
├── EntityStateController
└── EntitySensor
    └── SensorBase
```

---

## ✅ Verification Checklist

Before deploying your showcase project:

- [ ] All script files copied to correct folder structure
- [ ] Assembly definition files (.asmdef) created and configured
- [ ] No broken namespace references
- [ ] Editor script in Editor folder if included
- [ ] Third-party dependencies included
- [ ] Test scene with AIBrain component on an entity
- [ ] Entity has EntityManager component
- [ ] Actions and Considerations configured as assets


using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Lightweight initialization + reflection-based injection manager.
/// - Registers listed components and the manager itself
/// - Injects fields/properties marked with [Inject]
/// - Calls Init() (parameterless) or method-injection Init(...) when possible
/// - Calls Shutdown() in reverse order on destroy
/// </summary>
public class InitManager : MonoBehaviour, IInitializable
{
    [Tooltip("Order matters: earlier items initialize first.")]
    public List<MonoBehaviour> initializables = new();

    [Tooltip("If true, auto-populate `initializables` from this GameObject (and optionally children) before Init runs.")]
    public bool autoPopulate = false;

    [Tooltip("If true, include child GameObjects when auto-populating.")]
    public bool includeChildren = true;

    [Tooltip("If true, register other Components found on a registered object's GameObject (e.g. Rigidbody2D).")]
    public bool autoRegisterGameObjectComponents = false;

    public enum TraversalMode
    {
        ParentFirst,    // Unity default: parent before children (pre-order)
        ChildrenFirst,  // deepest nodes first (depth-descending)
        SiblingPath,    // deterministic lexicographic sibling-index path order
    }

    [Tooltip("How discovered components are ordered when populating the `initializables` list.")]
    public TraversalMode traversal = TraversalMode.ParentFirst;

    // --- Reflection cache to avoid repeated attribute queries during startup ---
    private class ReflectionCacheEntry
    {
        public FieldInfo[] InjectFields = Array.Empty<FieldInfo>();
        public PropertyInfo[] InjectProperties = Array.Empty<PropertyInfo>();
        public MethodInfo[] InitMethods = Array.Empty<MethodInfo>();
    }

    // Cache per-Type reflection results (populated lazily).
    private static readonly Dictionary<Type, ReflectionCacheEntry> reflectionCache = new();
    private static readonly object reflectionCacheLock = new();
    // Type -> instance cache (registered under concrete type and common interfaces/base types)
    private readonly Dictionary<Type, object> cache = new();
    private readonly List<IInitializable> ordered = new();

    protected virtual void Awake()
    {

        // Optionally auto-populate the inspection `initializables` list before performing registration.
        if (autoPopulate)
            PopulateInitializables();

        // Register manager itself
        RegisterInstance(this);

        // Register listed initializables and build ordered list (preserve order)
        foreach (var mono in initializables)
        {
            if (mono == null) continue;
            if (mono is IInitializable initable)
            {
                RegisterInstance(mono);
                ordered.Add(initable);
            }
            else
            {
                Debug.LogError($"{mono.name} does NOT implement IInitializable.");
            }
        }

        // Inject & Init manager first
        InjectMembers(this);
        CallInit(this);

        // Inject & Init listed components in order
        foreach (var initable in ordered)
        {
            InjectMembers(initable);
            CallInit(initable);
        }
    }

    // Register an instance under its concrete type, interfaces, and base classes (up to MonoBehaviour).
    private void RegisterInstance(object instance)
    {
        if (instance == null) return;
        var instType = instance.GetType();

        // concrete type
        cache[instType] = instance;

        // interfaces
        foreach (var iface in instType.GetInterfaces())
            cache[iface] = instance;

        // base classes (stop before MonoBehaviour and object)
        var baseType = instType.BaseType;
        while (baseType != null && baseType != typeof(MonoBehaviour) && baseType != typeof(object))
        {
            cache[baseType] = instance;
            baseType = baseType.BaseType;
        }

        // Optionally register other Components that live on the same GameObject.
        // This makes Unity engine components (Rigidbody2D, Collider2D, etc.) available for DI
        // without manually adding them to `initializables` or calling RegisterInstance explicitly.
        if (autoRegisterGameObjectComponents && instance is Component compInstance)
        {
            var comps = compInstance.GetComponents<Component>();
            foreach (var c in comps)
            {
                if (c == null || c == compInstance) continue;
                var cType = c.GetType();

                // concrete type
                if (!cache.ContainsKey(cType))
                    cache[cType] = c;

                // interfaces
                foreach (var iface in cType.GetInterfaces())
                    cache[iface] = c;

                // register base classes up to Component (but not Component itself)
                var b = cType.BaseType;
                while (b != null && b != typeof(Component) && b != typeof(object))
                {
                    cache[b] = c;
                    b = b.BaseType;
                }
            }
        }
    }

    /// <summary>
    /// Retrieve or build the reflection cache entry for a type.
    /// </summary>
    private ReflectionCacheEntry GetReflectionCache(Type type)
    {
        lock (reflectionCacheLock)
        {
            if (reflectionCache.TryGetValue(type, out var entry))
                return entry;

            entry = new ReflectionCacheEntry();

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            entry.InjectFields = fields.Where(f => f.GetCustomAttribute<InjectAttribute>() != null).ToArray();

            var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            entry.InjectProperties = props.Where(p => p.GetCustomAttribute<InjectAttribute>() != null && p.CanWrite).ToArray();

            entry.InitMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                    .Where(m => m.Name == "Init").ToArray();

            reflectionCache[type] = entry;
            return entry;
        }
    }
    // Try to resolve a dependency by type. Uses exact lookup then assignability fallback.
    private bool TryResolve(Type requestedType, out object instance)
    {
        if (requestedType == null)
        {
            instance = null;
            return false;
        }

        if (cache.TryGetValue(requestedType, out instance))
            return true;

        // fallback: find first registered instance whose registered type is assignable to requestedType
        foreach (var kv in cache)
        {
            var registeredType = kv.Key;
            if (requestedType.IsAssignableFrom(registeredType))
            {
                instance = kv.Value;
                return true;
            }
        }

        instance = null;
        return false;
    }

    // Inject fields and properties marked with [Inject]
    private void InjectMembers(object obj)
    {
        if (obj == null) return;
        var type = obj.GetType();
        var cacheEntry = GetReflectionCache(type);

        // Inject fields
        foreach (var field in cacheEntry.InjectFields)
        {
            var attr = field.GetCustomAttribute<InjectAttribute>();
            if (!TryResolve(field.FieldType, out var dependency))
            {
                if (attr != null && attr.Optional) continue;
                Debug.LogError($"InitManager: {type.Name} requires {field.FieldType.Name} for field {field.Name}, but it is not registered.");
                continue;
            }
            field.SetValue(obj, dependency);
        }

        // Inject properties
        foreach (var prop in cacheEntry.InjectProperties)
        {
            var attr = prop.GetCustomAttribute<InjectAttribute>();
            if (!TryResolve(prop.PropertyType, out var dependency))
            {
                if (attr != null && attr.Optional) continue;
                Debug.LogError($"InitManager: {type.Name} requires {prop.PropertyType.Name} for property {prop.Name}, but it is not registered.");
                continue;
            }
            prop.SetValue(obj, dependency);
        }
    }

    // Call Init: prefer parameterless Init(), otherwise try method-injection Init(...)
    private void CallInit(object obj)
    {
        if (obj == null) return;
        var type = obj.GetType();
        var cacheEntry = GetReflectionCache(type);

        // First, prefer parameterized Init methods: try to find the most-specific overload
        // (largest parameter count) whose parameters can all be resolved from the cache.
        var paramMethods = cacheEntry.InitMethods.Where(m => m.GetParameters().Length > 0)
                                                .OrderByDescending(m => m.GetParameters().Length);

        foreach (var method in paramMethods)
        {
            var parameters = method.GetParameters();
            var args = new object[parameters.Length];
            bool ok = true;
            for (int i = 0; i < parameters.Length; i++)
            {
                var pType = parameters[i].ParameterType;
                if (!TryResolve(pType, out var dep))
                {
                    ok = false;
                    break;
                }
                args[i] = dep;
            }

            if (!ok) continue;

            try
            {
                method.Invoke(obj, args);
            }
            catch (Exception ex)
            {
                Debug.LogError($"InitManager: Exception while invoking Init on {type.Name}: {ex}");
            }
            return;
        }

        // If no resolvable parameterized Init was found, fall back to parameterless Init if present
        var paramless = cacheEntry.InitMethods.FirstOrDefault(m => m.GetParameters().Length == 0);
        if (paramless != null)
        {
            try { paramless.Invoke(obj, null); }
            catch (Exception ex) { Debug.LogError($"InitManager: Exception invoking Init() on {type.Name}: {ex}"); }
            return;
        }
    }

    // Shutdown in reverse order; be resilient to exceptions
    // Made protected virtual so subclasses can run cleanup before/after base behavior.
    protected virtual void OnDestroy()
    {
        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            var item = ordered[i];
            try { item.Shutdown(); }
            catch (Exception ex) { Debug.LogError($"InitManager: Exception in Shutdown of {item.GetType().Name}: {ex}"); }
        }

        // Manager shutdown last
        try { (this as IInitializable)?.Shutdown(); }
        catch (Exception ex) { Debug.LogError($"InitManager: Exception in manager Shutdown: {ex}"); }
    }



    // Optional IInitializable implementation for the manager
    public void Init()
    {
        Debug.Log("InitManager.Init() called");
    }

    public void Shutdown() { }

    [ContextMenu("Populate Initializables")]
    public void PopulateInitializables()
    {
        IEnumerable<InitializableBase> found = includeChildren
            ? GetComponentsInChildren<InitializableBase>(false)
            : GetComponents<InitializableBase>();

        // Exclude this manager
        var list = found.Where(c => c != this).ToList();

        // Order according to traversal strategy
        IEnumerable<InitializableBase> ordered;
        switch (traversal)
        {
            case TraversalMode.ChildrenFirst:
                ordered = list.OrderByDescending(m => GetDepth(((MonoBehaviour)m).transform, this.transform));
                break;
            case TraversalMode.SiblingPath:
                ordered = list.OrderBy(m => GetSiblingPathKey(((MonoBehaviour)m).transform, this.transform));
                break;
            case TraversalMode.ParentFirst:
            default:
                // Unity's GetComponentsInChildren already returns parent-first pre-order
                ordered = list;
                break;
        }

        // Populate inspector list so designers can further reorder if needed
        initializables.Clear();
        foreach (var mb in ordered.OfType<MonoBehaviour>())
            initializables.Add(mb);
    }

    // Helper: get depth of transform relative to root
    private int GetDepth(Transform t, Transform root)
    {
        int d = 0;
        while (t != null && t != root)
        {
            d++;
            t = t.parent;
        }
        return d;
    }

    // Helper: build sibling-index path key (e.g. "0.2.1") for deterministic ordering
    private string GetSiblingPathKey(Transform t, Transform root)
    {
        var parts = new List<int>();
        while (t != null && t != root)
        {
            parts.Add(t.GetSiblingIndex());
            t = t.parent;
        }
        parts.Reverse();
        return string.Join(".", parts);
    }
}

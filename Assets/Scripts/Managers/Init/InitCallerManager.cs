using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Simple manager that only calls Init()/Shutdown() on listed IInitializable components.
/// No DI, no injection — just lifecycle ordering by the `initializables` list.
/// </summary>

[CustomExecutionOrder(-30)]
public class InitLifecycleManager : InitializableBase
{
    [Tooltip("Order matters: earlier items initialize first.")]
    public List<InitializableBase> initializables = new();
    [Tooltip("If true, Init() is called in Awake(). Otherwise, Init() must be called manually.")]
    [SerializeField] private bool canCallInAwake = true;

    [Tooltip("If true, auto-populate `initializables` from this GameObject (and optionally children) before Init runs.")]
    [SerializeField] private bool autoPopulate = false;

    [Tooltip("If true, include child GameObjects when auto-populating.")]
    [SerializeField] private bool includeChildren = true;

    public enum TraversalMode
    {
        ParentFirst,
        ChildrenFirst,
        SiblingPath,
    }

    [Tooltip("How discovered components are ordered when populating the `initializables` list.")]
    public TraversalMode traversal = TraversalMode.ParentFirst;

    private readonly List<IInitializable> ordered = new();

    protected virtual void Awake()
    {
        if (this.canCallInAwake)
        {
            Init();
        }
    }

    public override void Init()
    {
        if (this.IsInitialized) return;
        SetInitialized();
        if (this.autoPopulate)
            PopulateInitializables();

        this.ordered.Clear();
        foreach (var mono in this.initializables)
        {
            if (mono == null) continue;
            if (mono is IInitializable initable)
            {
                if (initable.IsInitialized)
                {
                    Logger.LogWarning($"{mono.name} is already initialized " +
                    " and will be skipped by InitCallerManager.");
                    continue;
                }
                if (!this.ordered.Contains(initable))
                {
                    this.ordered.Add(initable);
                }
            }
            else
            {
                Logger.LogWarning($"{mono.name} does not implement IInitializable and will be skipped by InitCallerManager.");
            }
        }

        // Call Init in order
        foreach (var item in this.ordered)
        {
            try { item.Init(); }
            catch (System.Exception ex)
            {
                Logger.LogError($"InitCallerManager: " +
               $"Exception in Init of {item.GetType().Name}: {ex}");
            }
        }
    }

    protected virtual void OnDestroy()
    {
        for (int i = this.ordered.Count - 1; i >= 0; i--)
        {
            var item = this.ordered[i];
            try { item.Shutdown(); }
            catch (System.Exception ex) { Logger.LogError($"InitCallerManager: Exception in Shutdown of {item.GetType().Name}: {ex}"); }
        }
        SetInitialized(false);
    }

    [ContextMenu("Populate Initializables")]
    public void PopulateInitializables()
    {
        IEnumerable<InitializableBase> found = this.includeChildren
            ? GetComponentsInChildren<InitializableBase>(false)
            : GetComponents<InitializableBase>();

        // Exclude this manager if present
        var list = found.Where(c => c != this).ToList();
        IEnumerable<InitializableBase> orderedList = this.traversal switch
        {
            TraversalMode.ChildrenFirst => list.OrderByDescending(m =>
                            GetDepth(((InitializableBase)m).transform, this.transform)),
            TraversalMode.SiblingPath => list.OrderBy(m =>
                            GetSiblingPathKey(((InitializableBase)m).transform, this.transform)),
            _ => list,
        };

        this.initializables.Clear();
        foreach (var mb in orderedList.OfType<InitializableBase>())
            this.initializables.Add(mb);
    }

    [ContextMenu("Debug: Print Init Tree")]
    public void DebugInitTree()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== Init Tree: {this.gameObject.name} ===");
        sb.AppendLine($"CanCallInAwake: {this.canCallInAwake}");
        sb.AppendLine($"Initializables ({this.initializables.Count}):");

        for (int i = 0; i < this.initializables.Count; i++)
        {
            var item = this.initializables[i];
            if (item == null)
            {
                sb.AppendLine($"  [{i}] <null>");
                continue;
            }

            var isManager = item is InitLifecycleManager;
            string marker = isManager ? " [Manager]" : "";
            sb.AppendLine($"  [{i}] {item.GetType().Name} ({item.gameObject.name}){marker}");

            // If it's a nested manager, show its children indented
            if (isManager)
            {
                var nestedManager = (InitLifecycleManager)item;
                for (int j = 0; j < nestedManager.initializables.Count; j++)
                {
                    var child = nestedManager.initializables[j];
                    if (child == null)
                    {
                        sb.AppendLine($"      [{j}] <null>");
                    }
                    else
                    {
                        var isNestedManager = child is InitLifecycleManager;
                        string nestedMarker = isNestedManager ? " [Manager]" : "";
                        sb.AppendLine($"      [{j}] {child.GetType().Name} ({child.gameObject.name}){nestedMarker}");
                    }
                }
            }
        }

        sb.AppendLine("===================");
        Logger.Log(sb.ToString());
    }

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

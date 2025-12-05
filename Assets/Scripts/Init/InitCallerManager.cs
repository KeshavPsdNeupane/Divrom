using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Simple manager that only calls Init()/Shutdown() on listed IInitializable components.
/// No DI, no injection — just lifecycle ordering by the `initializables` list.
/// </summary>
public class InitCallerManager : MonoBehaviour
{
    [Tooltip("Order matters: earlier items initialize first.")]
    public List<MonoBehaviour> initializables = new();

    [Tooltip("If true, auto-populate `initializables` from this GameObject (and optionally children) before Init runs.")]
    public bool autoPopulate = false;

    [Tooltip("If true, include child GameObjects when auto-populating.")]
    public bool includeChildren = true;

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
        if (autoPopulate)
            PopulateInitializables();

        ordered.Clear();
        foreach (var mono in initializables)
        {
            if (mono == null) continue;
            if (mono is IInitializable initable)
            {
                ordered.Add(initable);
            }
            else
            {
                Debug.LogWarning($"{mono.name} does not implement IInitializable and will be skipped by InitCallerManager.");
            }
        }

        // Call Init in order
        foreach (var item in ordered)
        {
            try { item.Init(); }
            catch (System.Exception ex) { Debug.LogError($"InitCallerManager: Exception in Init of {item.GetType().Name}: {ex}"); }
        }
    }

    protected virtual void OnDestroy()
    {
        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            var item = ordered[i];
            try { item.Shutdown(); }
            catch (System.Exception ex) { Debug.LogError($"InitCallerManager: Exception in Shutdown of {item.GetType().Name}: {ex}"); }
        }
    }

    [ContextMenu("Populate Initializables")]
    public void PopulateInitializables()
    {
        IEnumerable<InitializableBase> found = includeChildren
            ? GetComponentsInChildren<InitializableBase>(false)
            : GetComponents<InitializableBase>();

        // Exclude this manager if present
        var list = found.Where(c => c != this).ToList();

        IEnumerable<InitializableBase> orderedList;
        switch (traversal)
        {
            case TraversalMode.ChildrenFirst:
                orderedList = list.OrderByDescending(m => GetDepth(((MonoBehaviour)m).transform, this.transform));
                break;
            case TraversalMode.SiblingPath:
                orderedList = list.OrderBy(m => GetSiblingPathKey(((MonoBehaviour)m).transform, this.transform));
                break;
            case TraversalMode.ParentFirst:
            default:
                orderedList = list;
                break;
        }

        initializables.Clear();
        foreach (var mb in orderedList.OfType<MonoBehaviour>())
            initializables.Add(mb);
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

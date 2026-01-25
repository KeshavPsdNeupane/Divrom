using UnityEngine;

public abstract class ConsiderationSO : ScriptableObject
{
    [SerializeField] private string considerationName;
    public abstract float Evaluate(EntityContext context);
}

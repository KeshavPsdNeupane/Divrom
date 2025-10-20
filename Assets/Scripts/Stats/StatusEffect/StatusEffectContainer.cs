using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class StatusEffectContainer : MonoBehaviour
{
    [SerializeField] private CircleCollider2D circle;
    [SerializeField] public StatusEffect statusEffect;

    private void Awake()
    {
        if (this.circle == null)
            this.circle = this.gameObject.GetComponent<CircleCollider2D>();
        this.circle.isTrigger = true;
    }
}

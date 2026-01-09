using UnityEngine;


[RequireComponent(typeof(CircleCollider2D))]
public class StatusEffectConnector : InitializableBase
{
    [SerializeField] private string StatusObjectTagName = "StatusEffect";
    [SerializeField] private CharacterStatsSystem characterStats;
    [SerializeField] private CircleCollider2D detectionCollider;
    [SerializeField] private float detectionRadius = .5f;
    [SerializeField] bool isTrigger = true;


    public override void Init()
    {
        if (this.detectionCollider == null)
            this.detectionCollider = this.gameObject.GetComponent<CircleCollider2D>();
        this.detectionCollider.isTrigger = this.isTrigger;
        Vector3 parentScale = transform.lossyScale;
        this.detectionCollider.radius = detectionRadius / Mathf.Max(parentScale.x, parentScale.y);
        this.detectionCollider.radius = this.detectionRadius;
        SetInitialized();
    }

    private void OnTriggerEnter2D(Collider2D effectCollidor)
    {
        if (effectCollidor.CompareTag(StatusObjectTagName))
        {
            StatusEffectContainer effect = effectCollidor.GetComponent<StatusEffectContainer>();
            if (effect != null && effect.statusEffect != null && this.characterStats != null)
            {
                if (this.characterStats.AddStatModifier(effect.statusEffect))
                {
                    Destroy(effectCollidor.gameObject);
                }

            }
        }
    }


    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(this.transform.position, this.detectionRadius);
    }
}

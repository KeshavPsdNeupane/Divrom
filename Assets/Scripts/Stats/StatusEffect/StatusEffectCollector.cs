using UnityEngine;


[RequireComponent(typeof(CircleCollider2D))]
public class StatusEffectConnector : MonoBehaviour
{
    [SerializeField] private CharacterStats characterStats;
    [SerializeField] private CircleCollider2D effectArea;
    [SerializeField] private string StatusObjectTagName = "StatusEffect";
    [SerializeField] private float radius = 2f;
    [SerializeField] bool isTrigger = true;


    private void Awake()
    {
        if (this.effectArea == null)
            this.effectArea = this.gameObject.GetComponent<CircleCollider2D>();
        this.effectArea.isTrigger = this.isTrigger;
        this.effectArea.radius = this.radius;
    }

    private void OnTriggerEnter2D(Collider2D effectCollidor)
    {
        if (effectCollidor.CompareTag(StatusObjectTagName))
        {
            StatusEffectContainer effect = effectCollidor.GetComponent<StatusEffectContainer>();
            if (effect != null && effect.statusEffect!=null && this.characterStats != null)
            {
                if (this.characterStats.AddStatModifier(effect.statusEffect))
                {
                    Destroy(effectCollidor.gameObject);
                }

            }
        }
    }
}

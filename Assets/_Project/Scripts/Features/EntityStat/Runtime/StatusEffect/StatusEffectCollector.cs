using UnityEngine;
using Kope.Core.Init;
using Kope.Character.Stats;
using Kope.Core.CompilerServices;
using Kope.Core.EntityComponentSystem;

[RequireComponent(typeof(CircleCollider2D))]
public class StatusEffectConnector : InitializableBase
{
    [SerializeField] private string StatusObjectTagName = "StatusEffect";
    [SerializeField] private CircleCollider2D detectionCollider;
    [SerializeField] private EntityComponentStore ecs;
    [SerializeField] private float detectionRadius = .5f;
    [SerializeField] bool isTrigger = true;
    private CharacterStatsSystem characterStats;


    public override void OnInit()
    {
        base.OnInit();
        if (this.detectionCollider == null)
        {
            MyLogger.Error("No CircleCollider2D assigned to StatusEffectConnector" + GetParentGameObjectStackTraceMessage());
            return;
        }
        if (ecs == null)
        {
            MyLogger.Error("No EntityComponentStore assigned to StatusEffectConnector" + GetParentGameObjectStackTraceMessage());
            return;
        }
        if (ecs.ComponentRegistry.TryGetComponent<CharacterStatsSystem>(out var statsSystem))
        {
            this.characterStats = statsSystem;
        }
        else
        {
            MyLogger.Error("No CharacterStatsSystem found in EntityComponentStoreConfig for StatusEffectConnector" + GetParentGameObjectStackTraceMessage());
            return;
        }


        this.detectionCollider.isTrigger = this.isTrigger;
        Vector3 parentScale = transform.lossyScale;
        this.detectionCollider.radius = detectionRadius / Mathf.Max(parentScale.x, parentScale.y);
        this.detectionCollider.radius = this.detectionRadius;

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

#if UNITY_EDITOR
    [SerializeField] private bool showGizmos = false;
    void OnDrawGizmos()
    {
        if (!this.enabled || !this.showGizmos) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(this.transform.position, this.detectionRadius);
    }
#endif
}

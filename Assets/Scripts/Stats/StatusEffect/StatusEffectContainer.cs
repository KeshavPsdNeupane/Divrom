using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class StatusEffectContainer : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb2d;
    [SerializeField] private CircleCollider2D circle;
    [SerializeField] public StatusEffect statusEffect;

    private void Awake()
    {
        if (this.rb2d == null)
            this.rb2d = this.gameObject.GetComponent<Rigidbody2D>();

        this.rb2d.bodyType = RigidbodyType2D.Static;

        if (this.circle == null)
            this.circle = this.gameObject.GetComponent<CircleCollider2D>();
        this.circle.isTrigger = true;
    }
}

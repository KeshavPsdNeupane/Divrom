using UnityEngine;

public class SetSpriteToPivot : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr;

    private void Awake()
    {
        this.sr.spriteSortPoint = SpriteSortPoint.Pivot;
    }
}

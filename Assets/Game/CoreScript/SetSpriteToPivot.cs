using UnityEngine;

public class SetSpriteToPivot : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr;

    private void Start()
    {
        this.sr.spriteSortPoint = SpriteSortPoint.Pivot;
    }
}


using UnityEngine;
public class SetSpriteToPivot : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr;
    private void Awake() => sr.spriteSortPoint = SpriteSortPoint.Pivot;
}
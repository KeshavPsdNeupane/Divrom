using UnityEngine;



namespace Kope.Core.Attribute
{
    /// <summary>
    /// A marker attribute to designate a GameObject as a selection base in the Unity Editor.
    /// When a child object is clicked in the Scene view, the selection will default to the
    /// GameObject with this attribute.
    /// </summary>
    [SelectionBase]
    public class SelectionBase : MonoBehaviour { }

}
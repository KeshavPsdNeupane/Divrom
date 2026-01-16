using UnityEngine;
namespace Kope.SpriteComposer2D
{
    /// <summary>
    /// Interface for editor library activeable components.
    /// Implement this interface to allow setting active category and label in the editor.
    /// </summary>
    public abstract class IEditorLibraryActiveable : MonoBehaviour
    {

        public abstract void SetActiveCategoryAndLabel(string category, string label);
    }
}
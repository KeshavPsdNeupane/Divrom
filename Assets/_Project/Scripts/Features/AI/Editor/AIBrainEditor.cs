using UnityEditor;
using UnityEngine;
using Kope.AI;
using Kope.Component.Interfaces;


namespace Kope.AI.Editor
{
    [CustomEditor(typeof(AIBrain), true)] // 'true' allows this to work on inherited brains too



    public class AIBrainEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default inspector (all your SerializeFields)
            DrawDefaultInspector();

            AIBrain brain = (AIBrain)target;

            GUILayout.Space(10);
            GUI.enabled = Application.isPlaying; // Only let us click if the game is running

            if (GUILayout.Button("Soft Interrupt AI", GUILayout.Height(30)))
            {
                brain.ForceInterrupt(InterruptPriority.Soft);
            }

            if (GUILayout.Button("Hard Stop AI", GUILayout.Height(30)))
            {
                brain.ForceInterrupt(InterruptPriority.Hard);
            }
            if (GUILayout.Button("Death Interrupt AI", GUILayout.Height(30)))
            {
                brain.ForceInterrupt(InterruptPriority.Death);
            }

            GUI.enabled = true;
        }
    }
}

using System.Collections;
using Kope.AI.Utility;
using UnityEngine;

[CreateAssetMenu(fileName = "IdleAction", menuName = "Scriptable Objects/AI/Utility/Actions/IdleAction")]
public class IdleAction : ActionSO
{
    [Header("Idle Action Settings\n" +
    "Does nothing just waits for a specified duration for Idle behavior.\n" +
    "Since Idle means doing nothing, this action simply waits for a set duration.\n")]
    [SerializeField] private float idleDuration = 1f;

    public override IEnumerator Execute(Context ctx)
    {
        yield return new WaitForSeconds(idleDuration);
        MarkCompleted();
    }
}

using UnityEngine;

public class TestPlayerDirectionGUI : InitializableBase
{
    private GUIStyle bigStyle;
    [SerializeField] private PlayerMovement playerMovement;
    public override void Init()
    {
        if (this.playerMovement == null)
        {
            this.playerMovement = GetComponent<PlayerMovement>();
            Debug.LogWarning($"{this.gameObject.name}: TestPlayerDirectionGUI: PlayerMovement was not assigned, auto-assigned from GameObject.");
            return;
        }
        Debug.Log("TestPlayerDirectionGUI: Init called with PlayerMovement injected.");
    }



    private void OnGUI()
    {
        if (bigStyle == null)
        {
            bigStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 50        // <--- increase size
            };
            bigStyle.normal.textColor = Color.white;
        }

        GUILayout.BeginArea(new Rect(10, 10, 300, 300));

        GUILayout.Label("Test Player Direction GUI", bigStyle);

        Vector2 direction = playerMovement.rb.linearVelocity;
        GUILayout.Label($"Velocity: {direction}", bigStyle);

        GUILayout.EndArea();
    }
}

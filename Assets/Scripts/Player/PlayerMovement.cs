using UnityEngine;

public class PlayerMovement : InitializableBase
{
    [Tooltip("Movement speed in units/sec")]
    public float speed = 5f;
    public Rigidbody2D rb;
    // Exposed name just for logging/demo
    public string movementName = "PlayerMovement";

    // Called by InitManager (parameterless Init preferred)
    public override void Init()
    {
        if (this.rb == null)
        {
            this.rb = GetComponent<Rigidbody2D>();
            Debug.LogWarning($"{this.gameObject.name}:{this.movementName}: Rigidbody2D was not assigned, auto-assigned from GameObject.");
            return;
        }
        Debug.Log($"{movementName}: Init called. speed={speed}");
    }

    // Simple API used by PlayerManager to move the player
    public void Move(Vector2 direction)
    {
        this.rb.linearVelocity = speed * direction.normalized;
    }

    private void FixedUpdate()
    {
        float h = 0f;
        float v = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;
        // print("Direction: " + h + ", " + v);
        var input = new Vector2(h, v);

        Move(input);
    }

    public override void Shutdown()
    {
        Debug.Log($"{movementName}: Shutdown called.");
    }
}

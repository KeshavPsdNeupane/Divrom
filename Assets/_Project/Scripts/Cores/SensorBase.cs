using Kope.Core.Extensions;
using UnityEngine;

// can change to SphereCollider if 3D is needed, but CircleCollider2D is more performant
// for 2D games and sufficient for most use cases.
[RequireComponent(typeof(CircleCollider2D))]
public abstract class SensorBase : MonoBehaviour
{
	[SerializeField] protected CircleCollider2D detectionCollider;
	[SerializeField, Tooltip("Layers that this sensor can detect. By default, detects all layers.")]
	private LayerMask detectableLayers = 1; // by default, set to detect default layer only. Change in inspector to detect other layers or all layers.
	[SerializeField] protected float detectionRadius = 1f;
	[SerializeField] protected bool isTrigger = true;
	[Tooltip("If true, OnDetectExit will be called when colliders leave the sensor area.")]
	[SerializeField] private bool usesExitDetection = false;
	[SerializeField] protected bool visualizeGizmos = true;
	[SerializeField] private Color gizmoColor = Color.cyan;

	private float actualDetectionRadius;
	protected string parentGameObjectStackTraceMessage;
	/// <summary>	
	///  Call this on either Awake or Start or 
	/// any initialization point before the sensor starts detecting. 
	/// It sets up the collider and other necessary configurations for the sensor to function properly.
	/// </summary>
	protected void Start()
	{
		this.parentGameObjectStackTraceMessage = this.ParentGameObjectStackTrace();
		if (this.detectionCollider == null)
		{
			this.detectionCollider = GetComponent<CircleCollider2D>();
			Debug.LogWarning($"[SensorBase] No CircleCollider2D assigned on {gameObject.name}." +
			" Attempting to find one on the same GameObject. So Assign the collider on inpector " +
			"to avoid this warning and ensure correct setup." + this.parentGameObjectStackTraceMessage);
		}

		this.detectionCollider.isTrigger = this.isTrigger;
		this.actualDetectionRadius = FindActualDetectionRadius();
		this.detectionCollider.radius = this.actualDetectionRadius;
		OnStart();
	}
	/// <summary>
	/// This method is called at the end of the Start method after the sensor has been initialized.
	/// It is meant to be overridden by derived classes to perform any additional setup or initialization
	/// that is specific to the sensor's functionality. This allows for a clean separation of the base sensor setup
	/// and the specific behavior of different types of sensors, while ensuring that the necessary collider
	/// configuration is always performed in the base class.
	/// </summary>
	public virtual void OnStart() { }


	void OnTriggerEnter2D(Collider2D other)
	{
		if (((1 << other.gameObject.layer) & this.detectableLayers) != 0)
		{
			OnDetect(other);
		}
	}
	void OnTriggerExit2D(Collider2D other)
	{
		// micro optimization to skip the layer check if exit detection is not used, 
		// since exit detection is not used in most cases and the layer check can be expensive
		// if there are many colliders exiting frequently.
		if (!this.usesExitDetection) return;
		if (((1 << other.gameObject.layer) & this.detectableLayers) != 0)
		{
			OnDetectExit(other);
		}
	}
	/// <summary>
	/// This method is called when another collider enters the sensor's trigger area and passes the layer mask check.
	/// It is meant to be overridden by derived classes to define the specific behavior that should occur 
	/// when an object is detected by the sensor. The Collider2D parameter provides information about the object that
	/// triggered the detection, allowing the sensor to interact with it as needed 
	/// (e.g., registering it in a context, applying effects, etc.).
	/// </summary>
	/// <param name="other"></param>
	public abstract void OnDetect(Collider2D other);


	/// <summary>
	/// This method is called when another collider exits the sensor's trigger area and passes the layer mask 
	/// check (if exit detection is enabled).
	/// It is meant to be overridden by derived classes to define the specific behavior that should occur when 
	/// an object is no longer detected by the sensor. The Collider2D parameter provides information about the object that
	/// triggered the exit detection, allowing the sensor to interact with it as needed 
	/// (e.g., removing it from a context, stopping effects, etc.). This method will
	/// only be called if the usesExitDetection flag is set to true, which allows 
	/// for optimization in cases where exit detection is not needed, as it can be an expensive operation 
	/// if there are many colliders exiting frequently.
	/// </summary>
	/// <param name="other"></param>
	public virtual void OnDetectExit(Collider2D other)
	{
		// By default, do nothing on exit. Derived classes can override this if they need to handle exit detection.
	}


	private void OnDrawGizmos()
	{
		if (!this.enabled || !this.visualizeGizmos) return;
		Gizmos.color = this.gizmoColor;
		// no need to scale the radius here
		// since it shows the actual detection radius in scene view, we needed the lossy scale on the start
		// because we can handle if there is any kind of scale on the parent object,
		// since the scale in parent object also scale the detection radius, 
		// so we need to divide the detection radius by the max scale on parent to get the actual detection radius,
		Gizmos.DrawWireSphere(this.transform.position, this.detectionRadius);
	}
	/// <summary>
	/// This method calculates the actual detection radius of the sensor by taking into 
	/// account the lossy scale of the parent GameObject.
	/// This is necessary because the CircleCollider2D's radius is affected by the scale of the GameObject, 
	/// so we need to adjust it to ensure the sensor detects objects within the intended radius in world space.
	///  The method divides the base detection radius by the maximum scale factor of the parent GameObject to 
	/// get the correct radius for the collider. This allows the sensor to function correctly regardless of any 
	/// scaling applied to the GameObject or its parents.
	/// </summary>
	/// <returns></returns>
	private float FindActualDetectionRadius()
	{
		Vector3 parentScale = this.transform.lossyScale;
		return this.detectionRadius / Mathf.Max(parentScale.x, parentScale.y);
	}
}

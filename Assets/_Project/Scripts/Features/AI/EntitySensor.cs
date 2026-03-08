using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class EntitySensor : SensorBase
{
	private Context context;


	int tempCounter = 0;

	/// <summary>
	/// Pass the context from AIBrain
	/// </summary>
	/// <param name="context"></param>
	public void InitContext(Context context) => this.context = context;

	void Update()
	{
		if (this.tempCounter != context.GetTotalEntityCount())
		{
			this.tempCounter = context.GetTotalEntityCount();
			Debug.Log($"[EntitySensor] Total entities in context: {this.tempCounter}");
			// this is just to verify that the sensor is properly 
			// registering and removing entities from the context, and that the count is accurate.
		}
	}

	public override void OnStart()
	{
		if (this.context == null)
		{
			Debug.LogWarning($"[EntitySensor] Context is not assigned for {gameObject.name}. Please call InitContext with a valid Context instance before the sensor starts detecting." + this.parentGameObjectStackTraceMessage);
		}
	}

	public override void OnDetect(Collider2D other)
	{
		var entityManager = other.GetComponentInParent<EntityManager>();
		if (entityManager == null || this.context == null) return;
		// this is garunteed to be valid for all entity since we check the commonname on the EM itself,
		//  so we can skip the check here and just add it to the context
		// so if entity manager is valid then all other tags and registry should be valid as well,
		//  if not then we have bigger problems and should just let it throw an error
		this.context.RegisterEntityContext(entityManager.EntityDetail);
	}

	public override void OnDetectExit(Collider2D other)
	{
		var entityManager = other.GetComponentInParent<EntityManager>();
		if (entityManager == null || this.context == null) return;
		var ed = entityManager.EntityDetail;
		this.context.RemoveTargetEntityContext(ed.UniqueID, ed.CommonEntityHashedTag);
	}
}

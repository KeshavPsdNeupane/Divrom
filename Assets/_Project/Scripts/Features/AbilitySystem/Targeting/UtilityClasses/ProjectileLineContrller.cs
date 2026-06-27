using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class ProjectileLineController : MonoBehaviour {
	[SerializeField] private LineRenderer lineRenderer;

	[Header("Positions")]
	[SerializeField] private Vector3 startPoint = Vector3.zero;
	[SerializeField] private Vector3 endPoint = new(1f, 2f, 0f);

	[Header("Width Settings")]
	[Range(0f, 5f)][SerializeField] private float startWidth = 0.1f;
	[Range(0f, 5f)][SerializeField] private float endWidth = 0.1f;

	[Header("Color Settings")]
	[SerializeField] private Color startColor = Color.white;
	[SerializeField] private Color endColor = Color.white;

	[Header("Material Settings")]
	[SerializeField] private Material lineMaterial;

	private void OnValidate() {
		if (lineRenderer == null) {
			lineRenderer = GetComponent<LineRenderer>();
		}

		if (startPoint == Vector3.zero && endPoint == Vector3.zero) {
			startPoint = transform.position;
			endPoint = transform.position + Vector3.right;
		}

		SetLinePositions(startPoint, endPoint);
		SetLineWidth(startWidth, endWidth);
		SetLineColors(startColor, endColor);
		SetLineMaterial(lineMaterial);
	}

	public void SetLinePositions(Vector3 start, Vector3 end) {
		this.gameObject.transform.position = start;
		if (lineRenderer != null) {
			lineRenderer.SetPosition(0, start);
			lineRenderer.SetPosition(1, end);
		}
	}

	public void SetLineWidth(float start, float end) {
		if (lineRenderer != null) {
			lineRenderer.startWidth = start;
			lineRenderer.endWidth = end;
		}
	}

	/// <summary>
	/// Updates the start and end colors (including alpha transparency).
	/// </summary>
	public void SetLineColors(Color start, Color end) {
		if (lineRenderer != null) {
			// This replaces the manual gradient generation and natively handles alpha
			lineRenderer.startColor = start;
			lineRenderer.endColor = end;
		}
	}

	public void SetLineMaterial(Material material) {
		if (lineRenderer != null && material != null) {
			lineRenderer.sharedMaterial = material;
		}
	}
}
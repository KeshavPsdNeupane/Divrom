using UnityEngine;
namespace Kope.Core.ObjectPooling {
	[CreateAssetMenu(menuName = "Scriptable Objects/Pooling Object Data", fileName = "PoolData_")]
	public class PoolingObjectData : ScriptableObject {
		[SerializeField] private GameObject prefab;
		[SerializeField] private int initialSize = 10;

		public GameObject Prefab => prefab;
		public int Size => initialSize;
	}

}
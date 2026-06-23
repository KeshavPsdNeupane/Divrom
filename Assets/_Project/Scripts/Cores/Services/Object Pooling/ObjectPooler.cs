using System.Collections;
using System.Collections.Generic;
using Kope.Core.ServiceLocator;
using UnityEngine;

namespace Kope.Core.ObjectPooling {
	internal interface IPoolGroup {
		GameObject Rent();
		void Release(GameObject obj);
		void Clear();
	}

	public class PoolGroup : MonoBehaviour, IPoolGroup {
		private readonly Queue<GameObject> _pool = new();
		private GameObject _prefab;
		private int _currentPoolMaxSize = 0;

		public void Create(GameObject prefab, int size, Transform parent) {
			this.gameObject.name = $"PoolGroup_{prefab.name}";
			this._prefab = prefab;
			this._currentPoolMaxSize = size;
			this.transform.SetParent(parent);
			this.transform.SetLocalPositionAndRotation(parent.position, Quaternion.identity);
			// create at least one object immediately to avoid potential issues 
			// with renting from an empty pool.
			CreateNew();
			// followed Mr.FlaSh.G advice to increase pool size asynchronously to prevent frame drops
			// during pool initialization.
			StartCoroutine(IncreasePoolSizeAsync(size - 1));
		}

		IEnumerator IncreasePoolSizeAsync(int additionalSize) {
			for (int i = 0; i < additionalSize; i++) {
				if (i % 5 == 0) yield return null;
				CreateNew();
			}
		}

		private void CreateNew() {
			var go = Instantiate(this._prefab, this.transform);
			go.SetActive(false);
			this._pool.Enqueue(go);
		}

		public void Release(GameObject obj) {
			obj.transform.SetParent(this.transform);
			this._pool.Enqueue(obj);
		}

		public GameObject Rent() {
			if (this._pool.Count > 0) {
				var obj = this._pool.Dequeue();
				obj.SetActive(true);
				return obj;
			}
			// Following Mr.FlaSh.G's advice to increase pool size  on demand 
			// to balance memory usage and performance also insured 1 object is created 
			// immediately to fulfill the current Rent request without delay. 
			int newObjectsToCreate = Mathf.Max(1, this._currentPoolMaxSize / 2);
			this._currentPoolMaxSize += newObjectsToCreate;
			CreateNew();
			var newObj = this._pool.Dequeue();
			StartCoroutine(IncreasePoolSizeAsync(newObjectsToCreate - 1));
			return newObj;

		}
		public void Clear() {
			if (this.transform) Destroy(this.transform.gameObject);
			this._pool.Clear();
		}
	}

	public class ObjectPooler : GlobalServiceBase {
		private readonly Dictionary<GameObject, IPoolGroup> _poolGroups = new();
		private Transform _poolRoot;
		private const int DEFAULT_POOL_SIZE = 16;
		// 16 or 32, since multiple of 
		// 2^5 = 32 or 2^6 = 64, to optimize memory allocation and reduce fragmentation.


		private void Awake() {
			this._poolRoot = this.gameObject.transform;
			DontDestroyOnLoad(this._poolRoot.gameObject);
		}
		public GameObject Rent(GameObject prefab) {
			if (prefab == null) return null;
			if (!this._poolGroups.TryGetValue(prefab, out var group)) {

				group = new GameObject().AddComponent<PoolGroup>();
				// can cast to PoolGroup because we just created it right now.
				PoolGroup poolGroupComponent = group as PoolGroup;
				poolGroupComponent.Create(prefab, DEFAULT_POOL_SIZE, this._poolRoot);
				// and can use just Add here since, already checked that the key doesn't exist.
				this._poolGroups.Add(prefab, group);
			}
			return group.Rent();
		}

		public void Release(GameObject prefab, GameObject instance) {
			if (prefab != null && this._poolGroups.TryGetValue(prefab, out var group)) {
				group.Release(instance);
			} else {
				if (instance) Destroy(instance);
			}
		}

		public void ClearAllPools() {
			foreach (var group in this._poolGroups.Values) group.Clear();
			this._poolGroups.Clear();
		}
	}
}
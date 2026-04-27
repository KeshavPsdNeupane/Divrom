using System.Collections.Generic;
using ServiceLocatorPattern;
using UnityEngine;

namespace Kope.Core.ObjectPooling {
	/// <summary>
	/// Interface for objects that require state resetting and automatic "Return Home" logic.
	/// </summary>
	public interface IPoolable {
		GameObject OriginPrefab { get; set; }
		void ClearState();
	}

	internal interface IPoolGroup {
		void Enqueue(GameObject obj);
		void Clear();
	}

	public class PoolGroup : IPoolGroup {
		private readonly Queue<GameObject> _pool = new();
		private readonly GameObject _prefab;
		private readonly Transform _groupRoot;

		public PoolGroup(GameObject prefab, int size, Transform parent) {
			this._prefab = prefab;
			this._groupRoot = new GameObject($"Group_{_prefab.name}").transform;
			this._groupRoot.SetParent(parent);

			for (int i = 0; i < size; i++) CreateNew();
		}

		private void CreateNew() {
			var go = Object.Instantiate(this._prefab, this._groupRoot);
			if (go.TryGetComponent<IPoolable>(out var poolable)) {
				poolable.OriginPrefab = this._prefab;
			}
			go.SetActive(false);
			this._pool.Enqueue(go);
		}

		public void Enqueue(GameObject obj) {
			obj.SetActive(false);
			obj.transform.SetParent(this._groupRoot);
			this._pool.Enqueue(obj);
		}

		public bool TryDequeue(out GameObject obj) {
			if (this._pool.Count > 0) {
				obj = this._pool.Dequeue();
				obj.SetActive(true);
				return true;
			}
			obj = null;
			return false;
		}

		public void Clear() {
			if (this._groupRoot) Object.Destroy(this._groupRoot.gameObject);
			this._pool.Clear();
		}
	}

	public class ObjectPooler : GlobalServiceBase {
		private readonly Dictionary<GameObject, IPoolGroup> _poolGroups = new();
		private Transform _poolRoot;

		private void Awake() {
			this._poolRoot = new GameObject("PooledObject").transform;
			DontDestroyOnLoad(this._poolRoot.gameObject);
		}

		public void Preload(PoolingObjectData data) {
			if (data.Prefab == null) return;
			if (!this._poolGroups.ContainsKey(data.Prefab)) {
				Debug.Log($"Preloading pool for {data.Prefab.name} with size {data.Size}");
				this._poolGroups.Add(data.Prefab, new PoolGroup(data.Prefab, data.Size, this._poolRoot));
			}
		}
		public void Preload(PoolingObjectData[] datas) {
			if (datas == null) return;
			foreach (var data in datas) {
				Preload(data);
			}
		}

		public bool TryRent(GameObject prefab, out GameObject instance) {
			if (prefab != null && this._poolGroups.TryGetValue(prefab, out var group)) {
				return ((PoolGroup)group).TryDequeue(out instance);
			}
			instance = null;
			return false;
		}

		public void Release(IPoolable poolable) {
			if (poolable == null) return;
			poolable.ClearState();
			if (poolable is MonoBehaviour mb) {
				Release(poolable.OriginPrefab, mb.gameObject);
			}
		}

		/// <summary>
		/// Releases an instance back to its pool. If the prefab is not recognized, the instance will be destroyed.
		/// what does not recognized mean? If the prefab is not in the dictionary of pool groups, 
		/// which means it was never preloaded or created through the pooler, then 
		/// we consider it "not recognized". This is a safety measure to prevent pooling 
		/// objects that were not intended to be pooled, which could lead to unexpected
		/// behavior or memory leaks if they are returned to the wrong pool. In such 
		/// cases, we simply destroy the instance to ensure it doesn't linger in the scene
		/// without proper management.
		/// </summary>
		/// <param name="prefab"></param>
		/// <param name="instance"></param>
		public void Release(GameObject prefab, GameObject instance) {
			if (prefab != null && this._poolGroups.TryGetValue(prefab, out var group)) {
				group.Enqueue(instance);
			} else {
				if (instance) Object.Destroy(instance);
			}
		}

		public void ClearAllPools() {
			foreach (var group in this._poolGroups.Values) group.Clear();
			this._poolGroups.Clear();
		}
	}
}
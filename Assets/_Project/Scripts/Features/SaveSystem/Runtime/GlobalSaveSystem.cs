using Newtonsoft.Json;
using ServiceLocatorPattern;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Kope.SaveSystem {
	public class GlobalSaveSystem : GlobalServiceBase {
		private string _saveFilePath;

		private readonly JsonSerializerSettings _settings = new() {
			TypeNameHandling = TypeNameHandling.Auto,
			Formatting = Formatting.Indented,
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
			Error = (sender, args) => {
				Debug.LogError($"[JSON ERROR] Path: {args.ErrorContext.Path} | Message: {args.ErrorContext.Error.Message}");
				args.ErrorContext.Handled = true;
			}
		};

		// ONE lock object to protect all access to the dictionary
		private readonly object _dataLock = new object();
		private readonly Dictionary<int, SceneSaveDataAggregate> _sceneSaveDataBySceneIndex = new();

		// Volatile flag to prevent multiple overlapping async commits
		private bool _isSavingAsync = false;

		public void Awake() {
			this._saveFilePath = Path.Combine(Application.persistentDataPath, "world.json");
			GetAllDataFromDisk();
		}

		public bool BufferSceneData(SceneSaveDataAggregate datas) {
			lock (this._dataLock) {
				this._sceneSaveDataBySceneIndex[datas.SceneIndex] = datas;
				return true;
			}

		}

		public bool TryGetBufferedSceneData(int sceneIndex, out SceneSaveDataAggregate dataAggregate) {
			lock (this._dataLock) {
				return this._sceneSaveDataBySceneIndex.TryGetValue(sceneIndex, out dataAggregate);
			}
		}

		/// <summary>
		/// Synchronous commit. Use for debugging or when you absolutely 
		/// need to block until the file is written.
		/// </summary>
		public void CommitAllToDisk() {
			List<SceneSaveDataAggregate> snapshot;

			// We lock just long enough to copy the references. 
			// This is O(N) but very fast since it's just memory pointers.
			lock (this._dataLock) {
				snapshot = this._sceneSaveDataBySceneIndex.Values.ToList();
			}

			InternalWriteToDisk(snapshot);
		}

		public async Task<bool> CommitAsycTODIsk() {
			if (this._isSavingAsync) return false;

			List<SceneSaveDataAggregate> snapshot;

			lock (this._dataLock) {
				// Because SceneSaveDataAggregate is a STRUCT, 
				// snapshot now holds a completely independent copy of the scene metadata.
				snapshot = this._sceneSaveDataBySceneIndex.Values.ToList();
			}

			this._isSavingAsync = true;
			await Task.Run(() => {
				try {
					this.InternalWriteToDisk(snapshot);
				} finally {
					this._isSavingAsync = false;

				}
			});
			return true;
		}

		public void GetAllDataFromDisk() {
			string path = this._saveFilePath;
			if (!File.Exists(path)) {
				Debug.LogWarning($"[GlobalSaveSystem] No save file found at {path}.");
				return;
			}

			try {
				string json = File.ReadAllText(path);
				var dataAggregates = JsonConvert.DeserializeObject<List<SceneSaveDataAggregate>>(json, this._settings);

				lock (this._dataLock) {
					this._sceneSaveDataBySceneIndex.Clear();
					foreach (var aggregate in dataAggregates) {
						this._sceneSaveDataBySceneIndex[aggregate.SceneIndex] = aggregate;
					}
				}
				//Debug.Log($"[GlobalSaveSystem] Loaded {dataAggregates?.Count ?? 0} scene aggregates.");
			} catch (Exception e) {
				Debug.LogError($"[GlobalSaveSystem] Failed to load from disk: {e.Message}");
			}
		}

		/// <summary>
		/// Shared logic for serialization and writing. 
		/// Can be called from Main Thread or Background Thread.
		/// </summary>
		private void InternalWriteToDisk(List<SceneSaveDataAggregate> data) {
			string json = JsonConvert.SerializeObject(data, this._settings);
			Debug.Log("Commited Jasun: " + json);
			File.WriteAllText(this._saveFilePath, json);
		}
	}
}
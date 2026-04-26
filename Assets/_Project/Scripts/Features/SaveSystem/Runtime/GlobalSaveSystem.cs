using Newtonsoft.Json;
using ServiceLocatorPattern;
using System;
using System.Collections.Generic;
using System.IO;
using ZLinq;
using System.Threading.Tasks;
using UnityEngine;

namespace Kope.SaveSystem {
	public class GlobalSaveSystem : GlobalServiceBase {
		/*
			1. SceneLevelSaveData Part Done. ✅
			2. GlobalSaveSystem with in-memory buffering and sync commit. ✅
			3. Async commit method. ✅
			4. Load on Awake. ✅
				- If no file, start with empty buffer. ✅
			5. SaveTypeRegistry and SaveTypeDatabase for polymorphic serialization. ✅
				- Uses TypeNameHandling.Auto and a custom registry to control which types can be serialized. ✅
			6. Error handling in JSON serialization to prevent crashes from bad data. ✅
			7. Thread safety with locks around the in-memory buffer. ✅
				- Only lock for the minimum time needed to copy references for async commit. ✅
				- SceneSaveDataAggregate is a struct, so copying references is enough to get a thread-safe snapshot. ✅
				- Volatile flag to prevent multiple overlapping async commits. ✅

			8. ToDO: Add versioning to the save file format for future compatibility.
				- Could be as simple as adding a "Version" field to the root JSON object. ✅
				- Then we can write upgrade logic in GetAllDataFromDisk to handle old versions.
				
				
			9. Adding the functionality to have multiple savefile for each new game(totally new instance/run of game save,
				 not like skyrim where u can have multiple savefiles but they are all from the same game instance),
				- For this we will have the following system of path as 
					Root/SaveFiles/CharacterName-randomnum/world.json.
				- This class will hande them since Global Mean this thingy is reponsible for them all, 
				so we will have a method like CreateNewSaveFile(string characterName) that will generate 
				a new randomnum and create the directory and file for that save.
				-To load just scan the Root/SaveFiles directory for subdirectories and list them as available saves, 
				then when player select one we load the world.json from that directory.
				- We might also use this class to save/load player setting/pref while the actual editing of those setting 
				is done in a different class, but the saving/loading is done here since 
				this class is responsible for all saving/loading. 
		*/
		private sealed class SaveFileFormat {
			[JsonProperty("ver")]
			public int Version { get; set; }
			[JsonProperty("scenes")]
			public List<SceneSaveDataAggregate> SceneAggregates { get; set; }
			// Future fields for global metadata can go here (e.g. player name, playtime, etc.)
			public SaveFileFormat(int version, List<SceneSaveDataAggregate> sceneAggregates) {
				this.Version = version;
				this.SceneAggregates = sceneAggregates;
			}
		}

		private int _currentVersion = 1; // Increment this if you make breaking changes to the save format

		private string _saveFilePath;
		private const string SaveTypeDatabaseResourcePath = "SaveSystem/SaveTypeDatabase";

		private readonly JsonSerializerSettings _settings = new() {
			TypeNameHandling = TypeNameHandling.Auto,
			SerializationBinder = new StableSaveTypeBinder(),
			Formatting = Formatting.Indented,
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
			Error = (sender, args) => {
				Debug.LogError($"[JSON ERROR] Path: {args.ErrorContext.Path} | Message: {args.ErrorContext.Error.Message}");
				args.ErrorContext.Handled = true;
			}
		};

		// ONE lock object to protect all access to the dictionary
		private readonly object _dataLock = new();
		private readonly Dictionary<int, SceneSaveDataAggregate> _sceneSaveDataBySceneIndex = new();

		// Volatile flag to prevent multiple overlapping async commits
		private bool _isSavingAsync = false;

		public void Awake() {
			this._saveFilePath = Path.Combine(Application.persistentDataPath, "world.json");
			var saveTypeDatabase = Resources.Load<SaveTypeDatabase>(SaveTypeDatabaseResourcePath);
			if (saveTypeDatabase == null) {
				Debug.LogWarning($"[GlobalSaveSystem] Could not load SaveTypeDatabase from Resources/{SaveTypeDatabaseResourcePath}. Falling back to attribute scan.");
			}
			SaveTypeRegistry.SetDatabase(saveTypeDatabase);
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
				snapshot = this._sceneSaveDataBySceneIndex.Values.AsValueEnumerable().ToList();
			}

			InternalWriteToDisk(snapshot);
		}

		public async Task<bool> CommitAsycTODIsk() {
			if (this._isSavingAsync) return false;

			List<SceneSaveDataAggregate> snapshot;

			lock (this._dataLock) {
				// Because SceneSaveDataAggregate is a STRUCT, 
				// snapshot now holds a completely independent copy of the scene metadata.
				snapshot = this._sceneSaveDataBySceneIndex.Values.AsValueEnumerable().ToList();
			}

			this._isSavingAsync = true;
			await Task.Run(() => {
				try {
					InternalWriteToDisk(snapshot);
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
				var saveFile = JsonConvert.DeserializeObject<SaveFileFormat>(json, this._settings);
				var dataAggregates = saveFile?.SceneAggregates ?? new List<SceneSaveDataAggregate>();

				if (saveFile != null && saveFile.Version != this._currentVersion) {
					Debug.LogWarning($"[GlobalSaveSystem] Save file version mismatch. File version: {saveFile.Version}, Current version: {this._currentVersion}.");
				}

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
			var saveFile = new SaveFileFormat(this._currentVersion, data);
			string json = JsonConvert.SerializeObject(saveFile, this._settings);
			// it is a comedic typo that i will keep in the log to make it easier to find when debugging.
			Debug.Log("Commited Jasun: " + json); // Jasun is love, Jasun is life.   
			File.WriteAllText(this._saveFilePath, json);
		}
	}
}
using Newtonsoft.Json;
using ServiceLocatorPattern;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Kope.SaveSystem {
	public class GlobalSaveSystem : GlobalServiceBase {
		/*
		TODO:
		1. Scene Level Save system for Specific Scene. They will be funneled through this GlobalSaveSystem, 
		     they will manage structure of save data for their scene and global will save whole world data, 
			 which includes all the scene data and global data in a json file. This way we can have more 
			 control over the save data structure and we can easily extend it in the future without 
			 breaking the existing save data.
		2. Global will search the save file folder to file all the save file/world that player later can choose 
			to load.
		3. Global will also manage the save file versioning, so that we can have multiple version of save file and 
		   we can easily migrate the old save file to the new version without breaking the existing save data.
		4. Global will read the player setting/config from setting.txt/js so player changed setting of game 
			like(graphics, audio, control) can be saved and loaded across game session.

		above 4 points are the future refinement, for now we will just have a simple implementation that can 
		save and load the world data in a json file.
		*/
		private string SaveFilePath => Application.persistentDataPath + "/world.json";


		private JsonSerializerSettings _settings = new() {
			TypeNameHandling = TypeNameHandling.Auto,
			Formatting = Formatting.Indented,
			// ADD THIS:
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore
		};
		private readonly HashSet<IEntitySavePacketProvider> _registeredEntities = new();

		private readonly Dictionary<int, SceneSaveDataAggregate> _sceneSaveDataBySceneIndex = new();


		public void Awake() {

			GetAllDataFromDisk();
		}


		public void BufferSceneData(SceneSaveDataAggregate datas) {
			// overright or create the buffered data for the scene index, since 
			// we only care about the latest save data for each scene,
			// there will be nothing called caching here, since we always need fresh data for the 
			// and load process, we will not keep multiple version of save data in the memory,
			this._sceneSaveDataBySceneIndex[datas.SceneIndex] = datas;
		}

		public bool TryGetBufferedSceneData(int sceneIndex, out SceneSaveDataAggregate dataAggregate) {
			if (_sceneSaveDataBySceneIndex.TryGetValue(sceneIndex, out var aggregate)) {
				dataAggregate = aggregate;
				return true;
			} else {
				Debug.LogWarning($"No buffered data found for scene index {sceneIndex}. Returning default.");
				dataAggregate = default;
				return false;
			}
		}

		public void CommitAllToDisk() {
			Debug.Log("Committing all buffered scene data to disk...");
			string json = JsonConvert.SerializeObject(_sceneSaveDataBySceneIndex.Values.ToList(), _settings);
			Debug.Log("All scene data serialized to JSON:\n" + json);
			File.WriteAllText(this.SaveFilePath, json);
		}

		public void GetAllDataFromDisk() {
			Debug.Log("Getting all scene data from disk...");
			string path = this.SaveFilePath;

			if (File.Exists(path)) {
				string json = File.ReadAllText(path);
				var dataAggregates = JsonConvert.DeserializeObject<List<SceneSaveDataAggregate>>(json, _settings);
				foreach (var aggregate in dataAggregates) {
					this.BufferSceneData(aggregate);
				}
				Debug.Log($"Loaded {dataAggregates.Count} scene data aggregates from disk.");
			} else {
				Debug.LogWarning($"No save file found at {path}. Starting with empty data.");
			}
		}
	}
}
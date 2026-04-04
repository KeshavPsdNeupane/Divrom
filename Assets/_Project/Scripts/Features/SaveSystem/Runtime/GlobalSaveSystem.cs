using Newtonsoft.Json;
using ServiceLocatorPattern;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Kope.Core.SaveSystem {
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



		private JsonSerializerSettings _settings = new() {
			TypeNameHandling = TypeNameHandling.Auto, // The magic for ISaveData structs
			Formatting = Formatting.Indented
		};
		private readonly HashSet<IEntitySavePacketProvider> _registeredEntities = new();

		public void Print() {
			Debug.Log("Hello from GlobalSaveSystem!"); // Just to verify that the service is initialized and working
		}

		public void RegisterTheEntity(IEntitySavePacketProvider provider) {
			if (!_registeredEntities.Contains(provider)) {
				_registeredEntities.Add(provider);
				provider.RegisterSaveDataChunk();
			}
		}

		public void SaveWorld() {
			// need some refinement here, since we want to have some control over the save data structure, 
			// instead of just dumping all the data into a big json file.
			// for now we will do this simple implementation, but in the future we may want to 
			// have a more structured save data format,
			// which can be easily extended and modified without breaking the existing save data.
			var packets = _registeredEntities.Select(e => e.GetEntitySavePacket()).ToList();
			string json = JsonConvert.SerializeObject(packets, _settings);
			File.WriteAllText(Application.persistentDataPath + "/world.json", json);
		}

		public void LoadWorld() {
			string path = Application.persistentDataPath + "/world.json";
			if (File.Exists(path)) {
				string json = File.ReadAllText(path);
				var packets = JsonConvert.DeserializeObject<List<EntitySavePacket>>(json, _settings);
				foreach (var packet in packets) {
					var provider = _registeredEntities.FirstOrDefault(e => e.GetEntitySavePacket().UniqueID == packet.UniqueID);
					if (provider != null) {
						//provider.LoadFromSavePacket(packet);
					} else {
						Debug.LogWarning($"No provider found for UniqueID: {packet.UniqueID}");
					}
				}
			} else {
				Debug.LogWarning("No save file found at " + path);
			}
		}
	}
}
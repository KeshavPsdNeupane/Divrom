using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThirdParty
{
    /// <summary>
    /// Serializable dictionary for Unity serialization.    
    /// </summary>

    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<TKey> keys = new List<TKey>();

        [SerializeField]
        private List<TValue> values = new List<TValue>();

        // save the dictionary to lists
        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (KeyValuePair<TKey, TValue> pair in this)
            {
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            this.Clear();
            if (keys.Count != values.Count)
            {
                Logger.Warn($"SerializableDictionary<{typeof(TKey)}, {typeof(TValue)}> mismatch: {keys.Count} keys, {values.Count} values. Clearing...");
                keys = new List<TKey>();
                values = new List<TValue>();
                return;
            }
            for (int i = 0; i < keys.Count; i++)
                this.Add(keys[i], values[i]);
        }

    }
}
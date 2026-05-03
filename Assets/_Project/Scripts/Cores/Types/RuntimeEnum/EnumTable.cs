using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kope.Core.Type.EnumAsset {
	[Serializable]
	public class EnumTable<TBinded> {
		public EnumAsset Source;
		public int[] SelectedValue = Array.Empty<int>();
		public TBinded[] BindedValues = Array.Empty<TBinded>();

		private Dictionary<EnumInstance, TBinded> _bindLookup;
		private Dictionary<int, TBinded> _idLookup;

		private void EnsureInitialized() {
			if (this._idLookup != null) return;

			Debug.Assert(Source != null, $"[{nameof(EnumTable<TBinded>)}] Source EnumAsset is not assigned!");

			this._bindLookup = new Dictionary<EnumInstance, TBinded>();
			this._idLookup = new Dictionary<int, TBinded>();

			int count = Mathf.Min(this.SelectedValue.Length, this.BindedValues.Length);

			for (int i = 0; i < count; i++) {
				int id = this.SelectedValue[i];
				EnumInstance instance = this.Source.GetInstance(id);

				if (instance != null) {
					TBinded value = this.BindedValues[i];
					this._bindLookup[instance] = value;
					this._idLookup[id] = value;
				}
			}
		}

		public TBinded Get(int enumId) {
			EnsureInitialized();
			return this._idLookup.TryGetValue(enumId, out TBinded value) ? value : default;
		}

		public TBinded Get(EnumInstance instance) {
			if (instance == null) return default;
			EnsureInitialized();
			return this._bindLookup.TryGetValue(instance, out TBinded value) ? value : default;
		}

		public Dictionary<EnumInstance, TBinded> BindLookup {
			get {
				EnsureInitialized();
				return this._bindLookup;
			}
		}
	}
}
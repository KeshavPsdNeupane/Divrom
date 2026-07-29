using System;
using System.Collections.Generic;
using Kope.Core.Attribute;
using Project.Scripts.Features.PathFinding.GraphManager;
using UnityEngine;



namespace Kope.Feature.PathFinding.Data {

	[Serializable]
	public struct MacroConnectionListWrapper {

		[SerializeField, ReadOnly]
		private List<MacroConnectionData> _connections;

		public readonly List<MacroConnectionData> Connections => this._connections;

		public MacroConnectionListWrapper(List<MacroConnectionData> connections) {
			this._connections = connections ?? new();
		}
	}

}
using Kope.Core.Attribute;
using Kope.EntityIdentity;
using Kope.Feature.PathFindingNew.Tile;
using UnityEngine;
[System.Serializable]
public struct GridStorageData {
	[SerializeField, ReadOnly] private long[] _pPos;
	[SerializeField, ReadOnly] private byte[] _iT;
	[SerializeField, ReadOnly] private TileType[] _tt;
	[SerializeField, ReadOnly] private MovementCapability[] _ac;
	[SerializeField, ReadOnly] private int[] _qC;
	public GridStorageData(long[] pPos, byte[] isTraversable,
	TileType[] biomeType, MovementCapability[] allowedCapabilities, int[] qCostMul) {
		this._pPos = pPos;
		this._iT = isTraversable;
		this._tt = biomeType;
		this._ac = allowedCapabilities;
		this._qC = qCostMul;
	}

	public readonly long[] PackedPosition => this._pPos;
	public readonly byte[] IsTraversable => this._iT;
	public readonly TileType[] TileType => this._tt;
	public readonly MovementCapability[] AllowedCapabilities => this._ac;
	public readonly int[] QCostMultiplier => this._qC;
}
[System.Serializable]
public struct RegionStorageData {
	[SerializeField, ReadOnly] ushort[] _rId;
	[SerializeField, ReadOnly] GridStorageData[] _rdata;

	public readonly ushort[] RegionId => this._rId;
	public readonly GridStorageData[] RegionData => this._rdata;
	public RegionStorageData(ushort[] regionId, GridStorageData[] regionData) {
		this._rId = regionId;
		this._rdata = regionData;
	}
}
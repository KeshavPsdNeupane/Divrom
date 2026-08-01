using Kope.EntityIdentity;
using Kope.Feature.PathFindingNew.Tile;
using UnityEngine;
public struct TileStorageData {
	[SerializeField] private long[] _pPos;
	[SerializeField] private byte[] _isTraversable;
	[SerializeField] private BiomeType[] _biomeType;
	[SerializeField] private MovementCapability[] _allowedCapabilities;
	[SerializeField] private int[] _qCostMul;
	public TileStorageData(long[] pPos, byte[] isTraversable,
	BiomeType[] biomeType, MovementCapability[] allowedCapabilities, int[] qCostMul) {
		this._pPos = pPos;
		this._isTraversable = isTraversable;
		this._biomeType = biomeType;
		this._allowedCapabilities = allowedCapabilities;
		this._qCostMul = qCostMul;
	}

	public readonly long[] PackedPosition => this._pPos;
	public readonly byte[] IsTraversable => this._isTraversable;
	public readonly BiomeType[] BiomeType => this._biomeType;
	public readonly MovementCapability[] AllowedCapabilities => this._allowedCapabilities;
	public readonly int[] QCostMultiplier => this._qCostMul;
}
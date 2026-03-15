using System;
using System.Collections.Generic;

namespace ThirdParty.PriorityQueeu {

	// this is modified version of .net priority queue implementation
	// original source: "https://github.com/dotnet/runtime/blob/main/src/libraries/System.Collections/src/System/Collections/Generic/PriorityQueue.cs"
	// just simplified to be Unity compatible and quaternary heap for better performance in pathfinding scenarios
	// also added indexed mode for O(log n) priority updates
	// license: MIT License
	// original license: MIT License


	/// <summary>
	/// Interface for objects that expose a cost/priority for use in a priority queue.
	/// </summary>
	/// <typeparam name="TPriority">The type of the cost/priority.</typeparam>
	public interface IHasCost<TPriority> {
		/// <summary>
		/// Returns the cost or priority of this element.
		/// </summary>
		TPriority GetCost();
	}

	/// <summary>
	/// Unity-safe quaternary (4-ary) priority queue for A* or similar algorithms.
	/// Supports custom comparers for min-heap or max-heap.
	///
	/// Remarks:
	/// - By default this is a min-heap: lower priority values are dequeued first.
	/// - Provide a custom <see cref="IComparer{TPriority}"/> or <see cref="Comparer{TPriority}"/>
	///   to change ordering (e.g. to implement a max-heap).
	/// </summary>
	/// <typeparam name="TElement">The type of element stored in the queue, must implement <see cref="IHasCost{TPriority}"/>.</typeparam>
	/// <typeparam name="TPriority">The type used for priority/cost.</typeparam>
	public class PriorityQueueSimple<TElement, TPriority>
		where TElement : IHasCost<TPriority> {
		private (TElement Element, TPriority Priority)[] _nodes;
		private int _size;
		private const int Arity = 4;
		private const int Log2Arity = 2;
		private readonly Comparer<TPriority> _comparer;
		private readonly bool _useIndex;
		// here the int stores the index array of the element in the heap
		// so we can do O(log n) priority updates
		private readonly Dictionary<TElement, int> _indexMap;

		#region Constructors
		/// <summary>
		/// Creates a priority queue.
		/// </summary>
		/// <param name="capacity">Initial capacity (at least 4).</param>
		/// <param name="comparer">Optional comparer to control ordering.</param>
		/// <param name="useIndex">Enable indexed mode for O(log n) priority updates.</param>
		public PriorityQueueSimple(int capacity = 16, Comparer<TPriority> comparer = null, bool useIndex = true) {
			_nodes = new (TElement, TPriority)[Math.Max(4, capacity)];
			_size = 0;
			_comparer = comparer ?? Comparer<TPriority>.Default;
			_useIndex = useIndex;
			if (_useIndex) _indexMap = new Dictionary<TElement, int>();
		}

		/// <summary>
		/// Creates a priority queue using an <see cref="IComparer{TPriority}"/>.
		/// </summary>
		public PriorityQueueSimple(int capacity, IComparer<TPriority> comparer, bool useIndex = true) {
			_nodes = new (TElement, TPriority)[Math.Max(4, capacity)];
			_size = 0;
			_comparer = comparer != null ? new ComparerWrapper(comparer) : Comparer<TPriority>.Default;
			_useIndex = useIndex;
			if (_useIndex) _indexMap = new Dictionary<TElement, int>();
		}

		private sealed class ComparerWrapper : Comparer<TPriority> {
			private readonly IComparer<TPriority> _inner;
			public ComparerWrapper(IComparer<TPriority> inner) { _inner = inner; }
			public override int Compare(TPriority x, TPriority y) => _inner.Compare(x, y);
		}
		#endregion

		/// <summary>Returns the number of elements in the queue.</summary>
		public int Count => _size;

		/// <summary>
		/// Enqueue an element using its <see cref="IHasCost{TPriority}.GetCost"/> value.
		/// In indexed mode, duplicates are prevented and priority is updated instead.
		/// </summary>
		public void Enqueue(TElement element) {
			TPriority cost = element.GetCost();
			if (_useIndex && _indexMap.ContainsKey(element)) {
				// Update priority if element exists
				TryUpdatePriority(element, cost);
			} else {
				Enqueue(element, cost);
			}
		}

		/// <summary>
		/// Enqueue an element with an explicit priority. Private, used internally.
		/// </summary>
		private void Enqueue(TElement element, TPriority priority) {
			if (_size == _nodes.Length) Array.Resize(ref _nodes, Math.Max(4, _size * 2));
			_nodes[_size] = (element, priority);
			if (_useIndex)
				_indexMap[element] = _size;
			MoveUp(_size++);
		}

		/// <summary>
		/// Dequeues and returns the element with minimal priority.
		/// </summary>
		public TElement Dequeue() {
			if (_size == 0) throw new InvalidOperationException("Queue is empty.");
			var root = _nodes[0];
			var result = root.Element;
			if (_useIndex) _indexMap.Remove(root.Element);
			_nodes[0] = _nodes[--_size];
			if (_size > 0) {
				if (_useIndex) _indexMap[_nodes[0].Element] = 0;
				MoveDown(0);
			}
			return result;
		}

		/// <summary>
		/// Dequeues and returns both element and its priority.
		/// </summary>
		public TElement Dequeue(out TPriority priority) {
			if (_size == 0) throw new InvalidOperationException("Queue is empty.");
			var root = _nodes[0];
			priority = root.Priority;
			if (_useIndex) _indexMap.Remove(root.Element);
			_nodes[0] = _nodes[--_size];
			if (_size > 0) {
				if (_useIndex) _indexMap[_nodes[0].Element] = 0;
				MoveDown(0);
			}
			return root.Element;
		}

		/// <summary>
		/// Returns the element with minimal priority without removing it.
		/// </summary>
		public TElement Peek() {
			if (_size == 0) throw new InvalidOperationException("Queue is empty.");
			return _nodes[0].Element;
		}

		/// <summary>
		/// Peeks the minimal element and its priority without removing it.
		/// </summary>
		public bool TryPeek(out TElement element, out TPriority priority) {
			if (_size == 0) {
				element = default;
				priority = default;
				return false;
			}
			var root = _nodes[0];
			element = root.Element;
			priority = root.Priority;
			return true;
		}

		/// <summary>
		/// Returns true if the queue contains the specified element.
		/// </summary>
		public bool Contains(TElement element) {
			if (_useIndex) return _indexMap.ContainsKey(element);
			var eq = EqualityComparer<TElement>.Default;
			for (int i = 0; i < _size; i++) if (eq.Equals(_nodes[i].Element, element)) return true;
			return false;
		}

		#region Internal Heap Operations
		private void MoveUp(int index) {
			var node = _nodes[index];
			while (index > 0) {
				int parent = (index - 1) >> Log2Arity;
				if (_comparer.Compare(node.Priority, _nodes[parent].Priority) >= 0) break;
				_nodes[index] = _nodes[parent];
				if (_useIndex) _indexMap[_nodes[index].Element] = index;
				index = parent;
			}
			_nodes[index] = node;
			if (_useIndex) _indexMap[node.Element] = index;
		}

		private void MoveDown(int index) {
			var node = _nodes[index];
			int child;
			int size = _size;
			while ((child = (index << Log2Arity) + 1) < size) {
				int minChild = child;
				int childUpper = Math.Min(child + Arity, size);
				for (int i = child + 1; i < childUpper; i++)
					if (_comparer.Compare(_nodes[i].Priority, _nodes[minChild].Priority) < 0) minChild = i;

				if (_comparer.Compare(node.Priority, _nodes[minChild].Priority) <= 0) break;
				_nodes[index] = _nodes[minChild];
				if (_useIndex) _indexMap[_nodes[index].Element] = index;
				index = minChild;
			}
			_nodes[index] = node;
			if (_useIndex) _indexMap[node.Element] = index;
		}
		#endregion

		/// <summary>Clears all items from the queue.</summary>
		public void Clear() {
			Array.Clear(_nodes, 0, _size);
			_size = 0;
			if (_useIndex) _indexMap.Clear();
		}

		/// <summary>Attempts to dequeue the minimal element, returns false if empty.</summary>
		public bool TryDequeue(out TElement element) {
			if (_size == 0) {
				element = default;
				return false;
			}
			element = Dequeue();
			return true;
		}

		public bool TryUpdatePriority(TElement element) {
			return TryUpdatePriority(element, element.GetCost());
		}

		/// <summary>
		/// Attempts to find an element and update its priority.
		/// </summary>
		public bool TryUpdatePriority(TElement element, TPriority newPriority) {
			if (_useIndex) {
				if (!_indexMap.TryGetValue(element, out int idx)) return false;
				var old = _nodes[idx].Priority;
				_nodes[idx] = (element, newPriority);
				if (_comparer.Compare(newPriority, old) < 0) MoveUp(idx);
				else if (_comparer.Compare(newPriority, old) > 0) MoveDown(idx);
				return true;
			}

			var eq = EqualityComparer<TElement>.Default;
			for (int i = 0; i < _size; i++) {
				if (eq.Equals(_nodes[i].Element, element)) {
					var old = _nodes[i].Priority;
					_nodes[i] = (element, newPriority);
					if (_comparer.Compare(newPriority, old) < 0) MoveUp(i);
					else if (_comparer.Compare(newPriority, old) > 0) MoveDown(i);
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Adds an element and immediately removes and returns the minimal element.
		/// </summary>
		public TElement EnqueueDequeue(TElement element, TPriority priority) {
			if (_size != 0) {
				var root = _nodes[0];
				if (_comparer.Compare(priority, root.Priority) > 0) {
					if (_useIndex) _indexMap.Remove(root.Element);
					MoveDown((element, priority), 0);
					return root.Element;
				}
			}
			return element;
		}

		private void MoveDown((TElement Element, TPriority Priority) node, int index) {
			int child;
			(TElement Element, TPriority Priority)[] nodes = _nodes;
			int size = _size;
			while ((child = (index << Log2Arity) + 1) < size) {
				int minChild = child;
				int childUpper = Math.Min(child + Arity, size);
				for (int i = child + 1; i < childUpper; i++)
					if (_comparer.Compare(nodes[i].Priority, nodes[minChild].Priority) < 0) minChild = i;

				if (_comparer.Compare(node.Priority, nodes[minChild].Priority) <= 0) break;
				nodes[index] = nodes[minChild];
				if (_useIndex) _indexMap[nodes[index].Element] = index;
				index = minChild;
			}
			nodes[index] = node;
			if (_useIndex) _indexMap[node.Element] = index;
		}

		/// <summary>
		/// Attempts to remove the specified element from the queue.
		/// Returns true if removed, false if not found.
		/// Only works in indexed mode.
		/// </summary>
		public bool TryRemove(TElement element) {
			if (!_useIndex || !_indexMap.TryGetValue(element, out int idx))
				return false;

			var lastNode = _nodes[--_size];
			_nodes[idx] = lastNode;

			_indexMap.Remove(element);

			if (idx < _size) {
				_indexMap[lastNode.Element] = idx;
				if (_comparer.Compare(lastNode.Priority, element.GetCost()) < 0) MoveUp(idx);
				else MoveDown(idx);
			}

			return true;
		}
	}

}
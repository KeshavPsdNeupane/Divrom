using System;
using System.Collections.Generic;

namespace ThirdParty.PriorityQueeu {

	// Copyright (c) .NET Foundation and Contributors. All rights reserved.
	// Licensed under the MIT License.
	// Original source: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Collections/src/System/Collections/Generic/PriorityQueue.cs
	//
	// Modifications by Keshav Prasad Neupane (Kope), 2024
	// - Simplified for Unity compatibility
	// - Converted to quaternary heap (4-ary) for pathfinding performance
	// - Added IHasCost<T> interface for self-reporting priority
	// - Added indexed mode (Dictionary-backed) for O(log n) updates
	// - Added TryRemove support
	// Licensed under the MIT License.


	/// <summary>
	/// Defines an object that exposes a cost or priority value, typically used for priority queue evaluations.
	/// </summary>
	/// <remarks>
	/// This interface is intentionally unconstrained to support both reference types (classes) and value types (structs). 
	/// If implementing this interface on a <c>struct</c>, it is strongly recommended to also implement 
	/// <see cref="System.IEquatable{T}"/> to prevent performance overhead from boxing when used in generic collections or priority queues.
	/// </remarks>
	/// <typeparam name="TPriority">The type of the cost or priority metric.</typeparam>
	public interface IHasCost<TPriority> {
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
		private readonly Dictionary<TElement, int> _indexMap;

		/// <summary>
		/// Creates a priority queue with the default comparer for TPriority.
		/// Always uses indexed mode for O(log n) updates. Duplicates are prevented and priority is updated instead.
		/// example to make max-heap comparator: <br/>
		/// <code>
		/// new PriorityQueueSimple&lt;MyElement, int&gt;(comparer: Comparer&lt;int&gt;.Create((a, b) => b.CompareTo(a)))
		/// </code>
		/// </summary>
		public PriorityQueueSimple(int capacity = 16, Comparer<TPriority> comparer = null) {
			this._nodes = new (TElement, TPriority)[Math.Max(4, capacity)];
			this._size = 0;
			this._comparer = comparer ?? Comparer<TPriority>.Default;
			this._indexMap = new Dictionary<TElement, int>();
		}

		/// <summary>Returns the number of elements in the queue.</summary>
		public int Count => _size;


		/// <summary>
		/// Returns an array of the elements in the queue in no particular order. 
		/// Modifying this array does not affect the queue.
		/// </summary>
		/// <returns></returns>
		public TElement[] GetElements() {
			TElement[] elements = new TElement[_size];
			for (int i = 0; i < _size; i++) {
				elements[i] = this._nodes[i].Element;
			}
			return elements;
		}

		/// <summary>
		/// Enqueues an element or updates its priority if it already exists in the queue.
		/// Uses the element's self-reported cost via <see cref="IHasCost{TPriority}.GetCost"/>.
		/// </summary>
		public void EnqueueOrUpdate(TElement element) => EnqueueOrUpdate(element, element.GetCost());

		/// <summary>
		/// Enqueues an element or updates its priority if it already exists in the queue,
		/// using an explicit priority value.
		/// </summary>
		public void EnqueueOrUpdate(TElement element, TPriority priority) {
			if (this._indexMap.ContainsKey(element)) {
				TryUpdatePriority(element, priority);
			} else {
				Enqueue((element, priority));
			}
		}

		/// <summary>
		/// Enqueues an element if it does not already exist in the queue. Returns true if enqueued,
		/// false if it was a duplicate.
		/// See <see cref="EnqueueOrUpdate(TElement)"/> for a method that also updates priority if the element already exists.
		/// </summary>
		public bool TryEnqueue(TElement element) {
			if (this._indexMap.ContainsKey(element)) return false;
			Enqueue((element, element.GetCost()));
			return true;
		}

		private void Enqueue((TElement Element, TPriority Priority) node) {
			if (this._size == this._nodes.Length) Array.Resize(ref this._nodes, Math.Max(4, this._size * 2));
			this._nodes[this._size] = node;
			this._indexMap[node.Element] = this._size;
			this.MoveUp(this._size++);
		}

		/// <summary>
		/// Dequeues and returns the element with minimal priority.
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown when the queue is empty.</exception>
		public TElement Dequeue() {
			if (this._size == 0) throw new InvalidOperationException("Queue is empty.");
			var (Element, _) = this._nodes[0];
			this._indexMap.Remove(Element);
			this._nodes[0] = this._nodes[--this._size];
			if (this._size > 0) {
				this._indexMap[this._nodes[0].Element] = 0;
				MoveDown(0);
			}
			return Element;
		}

		/// <summary>Attempts to dequeue the minimal element, returns false if empty.</summary>
		public bool TryDequeue(out TElement element) {
			if (this._size == 0) {
				element = default;
				return false;
			}
			element = Dequeue();
			return true;
		}

		/// <summary>
		/// Returns the element with minimal priority without removing it.
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown when the queue is empty.</exception>
		public TElement Peek() {
			if (this._size == 0) throw new InvalidOperationException("Queue is empty.");
			return this._nodes[0].Element;
		}

		/// <summary>
		/// Peeks the minimal element and its priority without removing it.
		/// </summary>
		public bool TryPeek(out TElement element, out TPriority priority) {
			if (this._size == 0) {
				element = default;
				priority = default;
				return false;
			}
			var (Element, Priority) = this._nodes[0];
			element = Element;
			priority = Priority;
			return true;
		}

		/// <summary>
		/// Returns true if the queue contains the specified element.
		/// </summary>
		public bool Contains(TElement element) => _indexMap.ContainsKey(element);

		/// <summary>Clears all items from the queue.</summary>
		public void Clear() {
			Array.Clear(this._nodes, 0, this._size);
			this._size = 0;
			this._indexMap.Clear();
		}


		/// <summary>
		/// Attempts to update the priority of an existing element using its self-reported cost via <see cref="IHasCost{TPriority}.GetCost"/>.
		/// Returns true if updated, false if not found.
		/// </summary>
		/// <param name="element"></param>
		/// <returns></returns>
		public bool TryUpdatePriority(TElement element) => TryUpdatePriority(element, element.GetCost());

		/// <summary>
		/// Attempts to update the priority of an existing element. Returns true if updated, false if not found.
		/// </summary>
		public bool TryUpdatePriority(TElement element, TPriority newPriority) {
			if (!this._indexMap.TryGetValue(element, out int idx)) return false;
			var old = this._nodes[idx].Priority;
			this._nodes[idx] = (element, newPriority);
			if (this._comparer.Compare(newPriority, old) < 0) MoveUp(idx);
			else if (this._comparer.Compare(newPriority, old) > 0) MoveDown(idx);
			return true;
		}

		/// <summary>
		/// Adds an element and immediately removes and returns the minimal element.
		/// </summary>
		public TElement EnqueueDequeue(TElement element, TPriority priority) {
			if (this._size != 0) {
				var (Element, Priority) = this._nodes[0];
				if (this._comparer.Compare(priority, Priority) > 0) {
					this._indexMap.Remove(Element);
					this._indexMap[element] = 0;
					MoveDown((element, priority), 0);
					return Element;
				}
			}
			return element;
		}

		/// <summary>
		/// Attempts to remove the specified element from the queue.
		/// Returns true if removed, false if not found.
		/// </summary>
		public bool TryRemove(TElement element) {
			if (!this._indexMap.TryGetValue(element, out int idx))
				return false;

			var removedPriority = this._nodes[idx].Priority;
			var lastNode = this._nodes[--this._size];
			this._nodes[idx] = lastNode;
			this._indexMap.Remove(element);

			if (idx < this._size) {
				this._indexMap[lastNode.Element] = idx;
				if (this._comparer.Compare(lastNode.Priority, removedPriority) < 0) MoveUp(idx);
				else MoveDown(idx);
			}

			return true;
		}

		#region Internal Heap Operations
		private void MoveUp(int index) {
			var node = _nodes[index];
			while (index > 0) {
				int parent = (index - 1) >> Log2Arity;
				if (this._comparer.Compare(node.Priority, this._nodes[parent].Priority) >= 0) break;
				this._nodes[index] = this._nodes[parent];
				this._indexMap[this._nodes[index].Element] = index;
				index = parent;
			}
			this._nodes[index] = node;
			this._indexMap[node.Element] = index;
		}

		private void MoveDown(int index) {
			var node = _nodes[index];
			int child;
			int size = _size;
			while ((child = (index << Log2Arity) + 1) < size) {
				int minChild = child;
				int childUpper = Math.Min(child + Arity, size);
				for (int i = child + 1; i < childUpper; i++)
					if (this._comparer.Compare(this._nodes[i].Priority, this._nodes[minChild].Priority) < 0) minChild = i;

				if (this._comparer.Compare(node.Priority, this._nodes[minChild].Priority) <= 0) break;
				this._nodes[index] = this._nodes[minChild];
				this._indexMap[this._nodes[index].Element] = index;
				index = minChild;
			}
			this._nodes[index] = node;
			this._indexMap[node.Element] = index;
		}

		private void MoveDown((TElement Element, TPriority Priority) node, int index) {
			int child;
			(TElement Element, TPriority Priority)[] nodes = _nodes;
			int size = this._size;
			while ((child = (index << Log2Arity) + 1) < size) {
				int minChild = child;
				int childUpper = Math.Min(child + Arity, size);
				for (int i = child + 1; i < childUpper; i++)
					if (this._comparer.Compare(this._nodes[i].Priority, this._nodes[minChild].Priority) < 0) minChild = i;

				if (this._comparer.Compare(node.Priority, this._nodes[minChild].Priority) <= 0) break;
				nodes[index] = nodes[minChild];
				this._indexMap[nodes[index].Element] = index;
				index = minChild;
			}
			nodes[index] = node;
			this._indexMap[node.Element] = index;
		}
		#endregion
	}
}
using System;
using System.Collections.Generic;

namespace ThirdParty.PriorityQueeu {

	// Copyright (c) .NET Foundation and Contributors. All rights reserved.
	// Licensed under the MIT License.
	// Original source: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Collections/src/System/Collections/Generic/PriorityQueue.cs
	//
	// Modifications by Keshav Prasad Neupane (Kope)
	// - Stripped unnecessary .NET runtime features and allocations to optimize for Unity environments.
	// - Leveraged the native 4-ary (quaternary) heap layout for maximum cache-
	//   	locality during pathfinding.
	// - Introduced the IHasCost<T> interface for self-reporting node priorities.
	// - Implemented a dictionary-backed index map enabling O(1) lookups 
	// 		and O(log n) priority updates/removals.
	// - Added specialized pathfinding utilities: EnqueueOrUpdate, TryUpdatePriority, and TryRemove.
	// Licensed under the MIT License.

	/// <summary>
	/// Defines an object that exposes a cost or priority value, typically used for priority queue evaluations.
	/// </summary>
	/// <remarks>
	/// This interface is intentionally unconstrained to support both reference types (<c>class</c>) and value types (<c>struct</c>). 
	/// If implementing this interface on a <c>struct</c>, it is strongly recommended to also implement 
	/// <see cref="IEquatable{T}"/> to prevent performance overhead from reflection and boxing when used in generic collections or indexed priority queues.
	/// </remarks>
	/// <typeparam name="TPriority">The comparable metric type used for priority evaluation (e.g., <see cref="float"/>, <see cref="int"/>, or custom comparable structs).</typeparam>
	public interface IHasCost<TPriority> where TPriority : IComparable<TPriority> {

		/// <summary>
		/// Retrieves the element's current inherent cost or priority value.
		/// </summary>
		/// <returns>The cost metric used to order this element within a priority queue.</returns>
		TPriority GetCost();
	}

	/// <summary>
	/// High-performance, Unity-safe quaternary (4-ary) priority queue optimized for pathfinding (e.g., A* search) and graph algorithms.
	/// Features a dictionary-backed index lookup for <c>O(log n)</c> priority updates, removals, and duplicate prevention.
	/// </summary>
	/// <remarks>
	/// Key Characteristics:
	/// <list type="bullet">
	///   <item><description><b>4-Ary Heap:</b> Reduced tree depth compared to binary heaps, improving cache locality and reducing CPU memory fetch operations.</description></item>
	///   <item><description><b>Min-Heap Default:</b> Elements with lower priority values are dequeued first. A custom <see cref="IComparer{TPriority}"/> can reverse this to create a max-heap.</description></item>
	///   <item><description><b>Index Map:</b> Maintains a <see cref="Dictionary{TKey, TValue}"/> mapping each element to its array index, allowing direct <c>O(log n)</c> updates without linear scanning.</description></item>
	/// </list>
	/// </remarks>
	/// <typeparam name="TElement">The type of element stored in the queue. Must implement <see cref="IHasCost{TPriority}"/>.</typeparam>
	/// <typeparam name="TPriority">The comparable priority or cost type, constrained by <see cref="IComparable{TPriority}"/>.</typeparam>
	public class QuadPriorityQueue<TElement, TPriority>
		where TElement : IHasCost<TPriority>
		where TPriority : IComparable<TPriority> {

		#region Private Fields & Constants
		/// <summary>
		/// The contiguous array buffer storing element-priority pairs representing the quaternary heap layout.
		/// </summary>
		private (TElement Element, TPriority Priority)[] _nodes;

		/// <summary>
		/// The current number of active elements stored in the queue.
		/// </summary>
		private int _size;

		/// <summary>
		/// The branching factor of the quaternary heap (4 children per parent node).
		/// </summary>
		private const int Arity = 4;

		/// <summary>
		/// Log2 of the branching factor (<c>log2(4) = 2</c>), used for fast 
		/// bitwise shift index calculations.
		/// </summary>
		private const int Log2Arity = 2;

		/// <summary>
		/// The comparer instance used to evaluate relative priorities between elements.
		/// </summary>
		private readonly Comparer<TPriority> _comparer;

		/// <summary>
		/// Dictionary tracking the 0-based array index of each element within <see cref="_nodes"/>.
		/// Enables <c>O(1)</c> element presence lookup and <c>O(log n)</c> priority updates/removals.
		/// </summary>
		private readonly Dictionary<TElement, int> _indexMap;
		#endregion

		#region Properties
		/// <summary>
		/// Gets the total number of elements currently contained in the priority queue.
		/// </summary>
		public int Count => _size;
		#endregion

		#region Constructors
		/// <summary>
		/// Initializes a new instance of the <see cref="QuadPriorityQueue{TElement, TPriority}"/> class.
		/// </summary>
		/// <param name="capacity">The initial allocation capacity for the underlying array buffer (minimum is 4). Defaults to 16.</param>
		/// <param name="comparer">
		/// An optional custom priority comparer. If <c>null</c>, <see cref="Comparer{TPriority}.Default"/> is used (Min-Heap).
		/// </param>
		/// <example>
		/// Creating a Max-Heap using a custom comparer:
		/// <code>
		/// var maxHeap = new PriorityQueueSimple&lt;MyElement, float&gt;(
		///     capacity: 32, 
		///     comparer: Comparer&lt;float&gt;.Create((a, b) => b.CompareTo(a))
		/// );
		/// </code>
		/// </example>
		public QuadPriorityQueue(int capacity = 16, Comparer<TPriority> comparer = null) {
			this._size = 0;
			int initialCap = Math.Max(4, capacity);
			this._nodes = new (TElement, TPriority)[initialCap];
			this._indexMap = new Dictionary<TElement, int>(initialCap);
			this._comparer = comparer ?? Comparer<TPriority>.Default;
		}
		#endregion

		#region Public Methods

		/// <summary>
		/// Copies and returns all elements currently stored in the queue as an unordered array.
		/// Modifying the returned array does not alter the queue state.
		/// </summary>
		/// <returns>A new array containing all current elements in no guaranteed order.</returns>
		public TElement[] GetElements() {
			TElement[] elements = new TElement[_size];
			for (int i = 0; i < _size; i++) {
				elements[i] = this._nodes[i].Element;
			}
			return elements;
		}

		/// <summary>
		/// Inserts an element into the queue if missing, or updates its priority in <c>O(log n)</c> time if it already exists.
		/// Priority is obtained directly via the element's <see cref="IHasCost{TPriority}.GetCost"/> implementation.
		/// </summary>
		/// <param name="element">The element to enqueue or update.</param>
		public void EnqueueOrUpdate(TElement element) => EnqueueOrUpdate(element, element.GetCost());

		/// <summary>
		/// Inserts an element into the queue if missing, or updates its priority in <c>O(log n)</c> time if it already exists,
		/// using an explicitly provided priority value.
		/// Performs a single dictionary lookup to resolve the node index.
		/// </summary>
		/// <param name="element">The target element to insert or update.</param>
		/// <param name="priority">The explicit priority value to assign to the element.</param>
		public void EnqueueOrUpdate(TElement element, TPriority priority) {
			if (this._indexMap.TryGetValue(element, out int idx)) {
				UpdatePriorityInternal(element, priority, idx);
			} else {
				Enqueue((element, priority));
			}
		}

		/// <summary>
		/// Attempts to enqueue an element into the queue if it does not already exist.
		/// </summary>
		/// <param name="element">The element to enqueue.</param>
		/// <returns><c>true</c> if the element was successfully enqueued; <c>false</c> if it was already present in the queue.</returns>
		public bool TryEnqueue(TElement element) {
			if (this._indexMap.ContainsKey(element)) return false;
			Enqueue((element, element.GetCost()));
			return true;
		}

		/// <summary>
		/// Removes and returns the element at the top of the queue (the minimal element in a min-heap).
		/// </summary>
		/// <returns>The element with the lowest priority value.</returns>
		/// <exception cref="InvalidOperationException">Thrown when the queue contains no elements.</exception>
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

		/// <summary>
		/// Attempts to remove and return the element at the top of the queue without throwing an exception if empty.
		/// </summary>
		/// <param name="element">When this method returns, contains the top element if found; otherwise, the default value.</param>
		/// <returns><c>true</c> if an element was successfully dequeued; <c>false</c> if the queue was empty.</returns>
		public bool TryDequeue(out TElement element) {
			if (this._size == 0) {
				element = default;
				return false;
			}
			element = Dequeue();
			return true;
		}

		/// <summary>
		/// Returns the element at the top of the queue without removing it.
		/// </summary>
		/// <returns>The element with the lowest priority value.</returns>
		/// <exception cref="InvalidOperationException">Thrown when the queue contains no elements.</exception>
		public TElement Peek() {
			if (this._size == 0) throw new InvalidOperationException("Queue is empty.");
			return this._nodes[0].Element;
		}

		/// <summary>
		/// Attempts to return the top element and its priority without removing them from the queue.
		/// </summary>
		/// <param name="element">When this method returns, contains the top element if found; otherwise, the default value.</param>
		/// <param name="priority">When this method returns, contains the priority of the top element if found; otherwise, the default value.</param>
		/// <returns><c>true</c> if an element was successfully retrieved; <c>false</c> if the queue was empty.</returns>
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
		/// Determines whether an element is currently stored in the queue in <c>O(1)</c> average time.
		/// </summary>
		/// <param name="element">The element to locate in the queue.</param>
		/// <returns><c>true</c> if the element is present; otherwise, <c>false</c>.</returns>
		public bool Contains(TElement element) => this._indexMap.ContainsKey(element);

		/// <summary>
		/// Clears all elements from the priority queue and resets memory allocations without re-instantiating internal structures.
		/// </summary>
		public void Clear() {
			Array.Clear(this._nodes, 0, this._size);
			this._size = 0;
			this._indexMap.Clear();
		}

		/// <summary>
		/// Attempts to update the priority of an existing element using its self-reported cost via <see cref="IHasCost{TPriority}.GetCost"/>.
		/// Automatically adjusts the heap position in <c>O(log n)</c> time.
		/// </summary>
		/// <param name="element">The target element whose priority should be updated.</param>
		/// <returns><c>true</c> if the element was found and updated; <c>false</c> if the element does not exist in the queue.</returns>
		public bool TryUpdatePriority(TElement element) => TryUpdatePriority(element, element.GetCost());

		/// <summary>
		/// Attempts to update the priority of an existing element using an explicitly provided priority value.
		/// Reorders the heap position in <c>O(log n)</c> time depending on whether the new priority is higher or lower.
		/// </summary>
		/// <param name="element">The target element.</param>
		/// <param name="newPriority">The new priority metric value.</param>
		/// <returns><c>true</c> if the element was found and updated; <c>false</c> if the element does not exist in the queue.</returns>
		public bool TryUpdatePriority(TElement element, TPriority newPriority) {
			if (!this._indexMap.TryGetValue(element, out int idx)) return false;
			UpdatePriorityInternal(element, newPriority, idx);
			return true;
		}

		/// <summary>
		/// Re-evaluates a node's priority at a pre-resolved array index and adjusts its position in the quaternary heap.
		/// </summary>
		/// <param name="element">The element occupying <paramref name="idx"/>.</param>
		/// <param name="newPriority">The new priority metric value.</param>
		/// <param name="idx">The verified 0-based array index of the element within <see cref="_nodes"/>.</param>
		private void UpdatePriorityInternal(TElement element, TPriority newPriority, int idx) {
			var old = this._nodes[idx].Priority;
			this._nodes[idx] = (element, newPriority);
			if (this._comparer.Compare(newPriority, old) < 0) MoveUp(idx);
			else if (this._comparer.Compare(newPriority, old) > 0) MoveDown(idx);
		}
		/// <summary>
		/// Adds a new element and immediately dequeues the top element in a single optimized pass.
		/// Useful for bounded-size candidate evaluation.
		/// </summary>
		/// <param name="element">The element to insert.</param>
		/// <param name="priority">The priority of the inserted element.</param>
		/// <returns>
		/// The previous top element if it had a higher priority than the newly inserted element; 
		/// otherwise, returns <paramref name="element"/> itself without modifying the queue.
		/// </returns>
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
		/// Removes an arbitrary element from anywhere in the priority queue in <c>O(log n)</c> time.
		/// </summary>
		/// <param name="element">The target element to locate and remove.</param>
		/// <returns><c>true</c> if the element was found and removed; <c>false</c> if the element was not found.</returns>
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
		#endregion

		#region Internal Heap Operations
		/// <summary>
		/// Enqueues an internal node tuple and expands the array capacity if required.
		/// </summary>
		/// <param name="node">The element and priority tuple to append.</param>
		private void Enqueue((TElement Element, TPriority Priority) node) {
			if (this._size == this._nodes.Length) Array.Resize(ref this._nodes, Math.Max(4, this._size * 2));
			this._nodes[this._size] = node;
			this._indexMap[node.Element] = this._size;
			this.MoveUp(this._size++);
		}

		/// <summary>
		/// Percolates a node upward in the quaternary heap until heap order is restored.
		/// </summary>
		/// <param name="index">The current 0-based array index of the node to move up.</param>
		/// <remarks>
		/// Parent index calculation in a 4-ary tree uses bit-shifting: <c>parent = (index - 1) >> 2</c>.
		/// </remarks>
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

		/// <summary>
		/// Percolates a node downward in the quaternary heap until heap order is restored.
		/// </summary>
		/// <param name="index">The current 0-based array index of the node to move down.</param>
		/// <remarks>
		/// First child index calculation in a 4-ary tree uses bit-shifting: <c>firstChild = (index &lt;&lt; 2) + 1</c>.
		/// Evaluates up to 4 child nodes per step to find the minimum child priority.
		/// </remarks>
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

		/// <summary>
		/// Overload of <see cref="MoveDown(int)"/> that places a specific replacement node directly into the target index,
		/// avoiding redundant intermediate array writes during <see cref="EnqueueDequeue"/>.
		/// </summary>
		/// <param name="node">The replacement element-priority tuple.</param>
		/// <param name="index">The target index to start percolating down from.</param>
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
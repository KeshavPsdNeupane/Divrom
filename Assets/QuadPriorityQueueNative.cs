using System;
using Unity.Collections;
using Unity.Jobs;

namespace ThirdParty.PriorityQueeu {

	// Copyright (c) .NET Foundation and Contributors. All rights reserved.
	// Licensed under the MIT License.
	// Original source: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Collections/src/System/Collections/Generic/PriorityQueue.cs
	//
	// Modifications by Keshav Prasad Neupane (Kope)
	// - Stripped unnecessary .NET runtime features and allocations to optimize for Unity environments.
	// - Leveraged the native 4-ary (quaternary) heap layout for maximum cache-locality during pathfinding.
	// - Introduced the IHasCost<T> interface for self-reporting node priorities.
	// - Implemented a dictionary-backed index map enabling O(1) lookups and O(log n) priority updates/removals.
	// - Added specialized pathfinding utilities: EnqueueOrUpdate, TryUpdatePriority, and TryRemove.
	//
	// Native conversion by Kope:
	// - Rewritten as a struct backed by NativeArray<Node> + NativeHashMap<TElement,int> so it can be
	//   allocated with an explicit Allocator, passed into Burst-compiled jobs by value, and disposed
	//   deterministically instead of relying on the GC / managed Dictionary.
	// - TElement and TPriority are now forced to `unmanaged` (in addition to their prior constraints),
	//   which is required for both NativeArray<Node> and NativeHashMap<TElement,int> to be blittable.
	// - TElement additionally requires IEquatable<TElement>, which NativeHashMap needs for key hashing.
	// - The managed Comparer<TPriority> customization point was removed (a Comparer<T> is a managed
	//   reference and isn't Burst-safe to carry around in a NativeContainer). In its place there's a
	//   blittable `minHeap` bool that flips ascending/descending ordering via TPriority.CompareTo.
	//   If you need a fully arbitrary comparer inside Burst, that's a FunctionPointer<T> extension point,
	//   not a drop-in replacement — happy to add that if you actually need it.
	// - No AtomicSafetyHandle/DisposeSentinel wiring is included (that's the part Unity's own native
	//   containers use to catch race conditions / use-after-free in the Editor). This type is deliberately
	//   left as a "bare" native struct for now; say the word if you want full Job System safety-handle
	//   integration bolted on.
	// - NOTE: NativeHashMap's `Capacity` / `Count` surface differs a bit across Unity.Collections package
	//   versions. This targets the modern (Collections 2.x) API. If you're on an older package version,
	//   the Capacity getter/setter and Count property calls below are the spots to adjust.
	//
	// If you keep the original managed QuadPriorityQueue.cs in the same project/namespace, delete the
	// IHasCost<TPriority> definition from one of the two files — it only needs to exist once.
	// Licensed under the MIT License.

	/// <summary>
	/// Defines an object that exposes a cost or priority value, typically used for priority queue evaluations.
	/// </summary>
	/// <typeparam name="TPriority">The comparable metric type used for priority evaluation (e.g., <see cref="float"/>, <see cref="int"/>).</typeparam>
	public interface IHasCostNative<TPriority> where TPriority : IComparable<TPriority> {

		/// <summary>
		/// Retrieves the element's current inherent cost or priority value.
		/// </summary>
		TPriority GetCost();
	}

	/// <summary>
	/// Native, Burst-safe quaternary (4-ary) priority queue optimized for pathfinding (e.g., A* search) and graph algorithms.
	/// Backed by <see cref="NativeArray{T}"/> and <see cref="NativeHashMap{TKey, TValue}"/> instead of managed arrays/Dictionary,
	/// so it can be allocated with an explicit <see cref="Allocator"/>, passed by value into jobs, and must be
	/// explicitly <see cref="Dispose()"/>d.
	/// </summary>
	/// <remarks>
	/// Key Characteristics:
	/// <list type="bullet">
	///   <item><description><b>4-Ary Heap:</b> Reduced tree depth compared to binary heaps, improving cache locality.</description></item>
	///   <item><description><b>Min-Heap Default:</b> Pass <c>minHeap: false</c> at construction for a max-heap.</description></item>
	///   <item><description><b>Index Map:</b> A <see cref="NativeHashMap{TKey, TValue}"/> mapping each element to its array index for O(log n) updates without linear scanning.</description></item>
	///   <item><description><b>Unmanaged only:</b> Both <typeparamref name="TElement"/> and <typeparamref name="TPriority"/> must be blittable value types.</description></item>
	/// </list>
	/// </remarks>
	/// <typeparam name="TElement">The element type stored in the queue. Must be unmanaged, self-equatable, and implement <see cref="IHasCostNative{TPriority}"/>.</typeparam>
	/// <typeparam name="TPriority">The unmanaged, comparable priority/cost type.</typeparam>
	public struct QuadPriorityQueueNative<TElement, TPriority> : IDisposable
		where TElement : unmanaged, IEquatable<TElement>, IHasCostNative<TPriority>
		where TPriority : unmanaged, IComparable<TPriority> {

		#region Private Fields & Constants
		/// <summary>
		/// A single element/priority pair as stored in the heap array. Must stay blittable.
		/// </summary>
		private struct Node {
			public TElement Element;
			public TPriority Priority;
		}

		/// <summary>
		/// The contiguous native buffer storing element-priority pairs representing the quaternary heap layout.
		/// </summary>
		private NativeArray<Node> _nodes;

		/// <summary>
		/// Native dictionary tracking the 0-based array index of each element within <see cref="_nodes"/>.
		/// </summary>
		private NativeHashMap<TElement, int> _indexMap;

		/// <summary>
		/// The current number of active elements stored in the queue.
		/// </summary>
		private int _size;

		/// <summary>
		/// The branching factor of the quaternary heap (4 children per parent node).
		/// </summary>
		private const int Arity = 4;

		/// <summary>
		/// Log2 of the branching factor (<c>log2(4) = 2</c>), used for fast bitwise shift index calculations.
		/// </summary>
		private const int Log2Arity = 2;

		/// <summary>
		/// The allocator used for both native buffers, reused whenever the heap array needs to grow.
		/// </summary>
		private readonly Allocator _allocator;

		/// <summary>
		/// Ascending (min-heap) when true, descending (max-heap) when false. Replaces the managed Comparer&lt;T&gt;
		/// customization point from the original implementation, since a Comparer&lt;T&gt; instance isn't Burst-safe.
		/// </summary>
		private readonly bool _minHeap;
		#endregion

		#region Properties
		/// <summary>
		/// Gets the total number of elements currently contained in the priority queue.
		/// </summary>
		public readonly int Count => _size;

		/// <summary>
		/// Whether this instance has been allocated (and not yet disposed).
		/// </summary>
		public bool IsCreated => _nodes.IsCreated;
		#endregion

		#region Constructors
		/// <summary>
		/// Initializes a new instance of the <see cref="QuadPriorityQueueNative{TElement, TPriority}"/> struct.
		/// </summary>
		/// <param name="capacity">The initial allocation capacity for the underlying native buffer (minimum is 4).</param>
		/// <param name="allocator">The allocator to use for both internal native buffers (e.g. <see cref="Allocator.Persistent"/> or <see cref="Allocator.TempJob"/>).</param>
		/// <param name="minHeap">If <c>true</c> (default), lower priority values dequeue first. Pass <c>false</c> for a max-heap.</param>
		public QuadPriorityQueueNative(int capacity, Allocator allocator, bool minHeap = true) {
			int initialCap = Math.Max(4, capacity);
			this._allocator = allocator;
			this._minHeap = minHeap;
			this._size = 0;
			this._nodes = new NativeArray<Node>(initialCap, allocator, NativeArrayOptions.UninitializedMemory);
			this._indexMap = new NativeHashMap<TElement, int>(initialCap, allocator);
		}
		#endregion

		#region Public Methods

		/// <summary>
		/// Copies and returns all elements currently stored in the queue as an unordered managed array.
		/// Modifying the returned array does not alter the queue state. Not intended for use inside jobs.
		/// </summary>
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
		public void EnqueueOrUpdate(TElement element) => EnqueueOrUpdate(element, element.GetCost());

		/// <summary>
		/// Inserts an element into the queue if missing, or updates its priority in <c>O(log n)</c> time if it already exists,
		/// using an explicitly provided priority value.
		/// </summary>
		public void EnqueueOrUpdate(TElement element, TPriority priority) {
			if (this._indexMap.TryGetValue(element, out int idx)) {
				UpdatePriorityInternal(element, priority, idx);
			} else {
				Enqueue(new Node { Element = element, Priority = priority });
			}
		}

		/// <summary>
		/// Attempts to enqueue an element into the queue if it does not already exist.
		/// </summary>
		/// <returns><c>true</c> if the element was successfully enqueued; <c>false</c> if it was already present.</returns>
		public bool TryEnqueue(TElement element) {
			if (this._indexMap.ContainsKey(element)) return false;
			Enqueue(new Node { Element = element, Priority = element.GetCost() });
			return true;
		}

		/// <summary>
		/// Removes and returns the element at the top of the queue (the minimal element in a min-heap).
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown when the queue contains no elements.</exception>
		public TElement Dequeue() {
			if (this._size == 0) throw new InvalidOperationException("Queue is empty.");
			TElement element = this._nodes[0].Element;
			this._indexMap.Remove(element);
			this._nodes[0] = this._nodes[--this._size];
			if (this._size > 0) {
				this._indexMap[this._nodes[0].Element] = 0;
				MoveDown(0);
			}
			return element;
		}

		/// <summary>
		/// Attempts to remove and return the element at the top of the queue without throwing an exception if empty.
		/// </summary>
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
		/// <exception cref="InvalidOperationException">Thrown when the queue contains no elements.</exception>
		public TElement Peek() {
			if (this._size == 0) throw new InvalidOperationException("Queue is empty.");
			return this._nodes[0].Element;
		}

		/// <summary>
		/// Attempts to return the top element and its priority without removing them from the queue.
		/// </summary>
		public bool TryPeek(out TElement element, out TPriority priority) {
			if (this._size == 0) {
				element = default;
				priority = default;
				return false;
			}
			Node top = this._nodes[0];
			element = top.Element;
			priority = top.Priority;
			return true;
		}

		/// <summary>
		/// Determines whether an element is currently stored in the queue in <c>O(1)</c> average time.
		/// </summary>
		public bool Contains(TElement element) => this._indexMap.ContainsKey(element);

		/// <summary>
		/// Clears all elements from the priority queue. Retains the underlying native allocations.
		/// </summary>
		public void Clear() {
			this._size = 0;
			this._indexMap.Clear();
		}

		/// <summary>
		/// Attempts to update the priority of an existing element using its self-reported cost via <see cref="IHasCost{TPriority}.GetCost"/>.
		/// </summary>
		public bool TryUpdatePriority(TElement element) => TryUpdatePriority(element, element.GetCost());

		/// <summary>
		/// Attempts to update the priority of an existing element using an explicitly provided priority value.
		/// </summary>
		public bool TryUpdatePriority(TElement element, TPriority newPriority) {
			if (!this._indexMap.TryGetValue(element, out int idx)) return false;
			UpdatePriorityInternal(element, newPriority, idx);
			return true;
		}

		/// <summary>
		/// Re-evaluates a node's priority at a pre-resolved array index and adjusts its position in the quaternary heap.
		/// </summary>
		private void UpdatePriorityInternal(TElement element, TPriority newPriority, int idx) {
			TPriority old = this._nodes[idx].Priority;
			this._nodes[idx] = new Node { Element = element, Priority = newPriority };
			int cmp = Compare(newPriority, old);
			if (cmp < 0) MoveUp(idx);
			else if (cmp > 0) MoveDown(idx);
		}

		/// <summary>
		/// Adds a new element and immediately dequeues the top element in a single optimized pass.
		/// Useful for bounded-size candidate evaluation.
		/// </summary>
		public TElement EnqueueDequeue(TElement element, TPriority priority) {
			if (this._size != 0) {
				Node top = this._nodes[0];
				if (Compare(priority, top.Priority) > 0) {
					this._indexMap.Remove(top.Element);
					this._indexMap[element] = 0;
					MoveDown(new Node { Element = element, Priority = priority }, 0);
					return top.Element;
				}
			}
			return element;
		}

		/// <summary>
		/// Removes an arbitrary element from anywhere in the priority queue in <c>O(log n)</c> time.
		/// </summary>
		public bool TryRemove(TElement element) {
			if (!this._indexMap.TryGetValue(element, out int idx))
				return false;

			TPriority removedPriority = this._nodes[idx].Priority;
			Node lastNode = this._nodes[--this._size];
			this._nodes[idx] = lastNode;
			this._indexMap.Remove(element);

			if (idx < this._size) {
				this._indexMap[lastNode.Element] = idx;
				if (Compare(lastNode.Priority, removedPriority) < 0) MoveUp(idx);
				else MoveDown(idx);
			}

			return true;
		}

		/// <summary>
		/// Releases the underlying native buffers immediately. Must be called exactly once when the queue is no longer needed.
		/// </summary>
		public void Dispose() {
			if (this._nodes.IsCreated) this._nodes.Dispose();
			if (this._indexMap.IsCreated) this._indexMap.Dispose();
		}

		/// <summary>
		/// Schedules disposal of the underlying native buffers after the given job dependency completes,
		/// returning a new <see cref="JobHandle"/> that represents that disposal work.
		/// </summary>
		public JobHandle Dispose(JobHandle inputDeps) {
			if (!IsCreated) return inputDeps;
			JobHandle handle = this._nodes.Dispose(inputDeps);
			handle = this._indexMap.Dispose(handle);
			return handle;
		}
		#endregion

		#region Internal Heap Operations

		/// <summary>
		/// Compares two priorities respecting the min-/max-heap setting chosen at construction.
		/// </summary>
		private int Compare(TPriority a, TPriority b) {
			int cmp = a.CompareTo(b);
			return this._minHeap ? cmp : -cmp;
		}

		/// <summary>
		/// Enqueues an internal node and grows the native array (allocating a new one and copying) if required.
		/// </summary>
		private void Enqueue(Node node) {
			EnsureNodeCapacity();
			this._nodes[this._size] = node;
			EnsureIndexMapCapacity(this._size + 1);
			this._indexMap[node.Element] = this._size;
			this.MoveUp(this._size++);
		}

		/// <summary>
		/// Grows <see cref="_nodes"/> by allocating a new, larger <see cref="NativeArray{T}"/>, copying existing
		/// contents across, and disposing the old buffer. NativeArray has no in-place resize.
		/// </summary>
		private void EnsureNodeCapacity() {
			if (this._size == this._nodes.Length) {
				int newCap = Math.Max(4, this._nodes.Length * 2);
				NativeArray<Node> newNodes = new(newCap, this._allocator, NativeArrayOptions.UninitializedMemory);
				NativeArray<Node>.Copy(this._nodes, newNodes, this._size);
				this._nodes.Dispose();
				this._nodes = newNodes;
			}
		}

		/// <summary>
		/// Grows the index map's capacity ahead of an insert if needed. Explicit/manual because
		/// NativeHashMap's auto-growth behavior on Add differs across Unity.Collections package versions.
		/// </summary>
		private void EnsureIndexMapCapacity(int required) {
			if (this._indexMap.Capacity < required) {
				this._indexMap.Capacity = Math.Max(required, this._indexMap.Capacity * 2);
			}
		}

		/// <summary>
		/// Percolates a node upward in the quaternary heap until heap order is restored.
		/// </summary>
		private void MoveUp(int index) {
			Node node = this._nodes[index];
			while (index > 0) {
				int parent = (index - 1) >> Log2Arity;
				Node parentNode = this._nodes[parent];
				if (Compare(node.Priority, parentNode.Priority) >= 0) break;
				this._nodes[index] = parentNode;
				this._indexMap[parentNode.Element] = index;
				index = parent;
			}
			this._nodes[index] = node;
			this._indexMap[node.Element] = index;
		}

		/// <summary>
		/// Percolates a node downward in the quaternary heap until heap order is restored.
		/// </summary>
		private void MoveDown(int index) {
			Node node = this._nodes[index];
			int size = this._size;
			int child;
			while ((child = (index << Log2Arity) + 1) < size) {
				int minChild = child;
				int childUpper = Math.Min(child + Arity, size);
				for (int i = child + 1; i < childUpper; i++)
					if (Compare(this._nodes[i].Priority, this._nodes[minChild].Priority) < 0) minChild = i;

				if (Compare(node.Priority, this._nodes[minChild].Priority) <= 0) break;
				this._nodes[index] = this._nodes[minChild];
				this._indexMap[this._nodes[index].Element] = index;
				index = minChild;
			}
			this._nodes[index] = node;
			this._indexMap[node.Element] = index;
		}

		/// <summary>
		/// Overload of <see cref="MoveDown(int)"/> that places a specific replacement node directly into the
		/// target index, avoiding a redundant intermediate write during <see cref="EnqueueDequeue"/>.
		/// </summary>
		private void MoveDown(Node node, int index) {
			int size = this._size;
			int child;
			while ((child = (index << Log2Arity) + 1) < size) {
				int minChild = child;
				int childUpper = Math.Min(child + Arity, size);
				for (int i = child + 1; i < childUpper; i++)
					if (Compare(this._nodes[i].Priority, this._nodes[minChild].Priority) < 0) minChild = i;

				if (Compare(node.Priority, this._nodes[minChild].Priority) <= 0) break;
				this._nodes[index] = this._nodes[minChild];
				this._indexMap[this._nodes[index].Element] = index;
				index = minChild;
			}
			this._nodes[index] = node;
			this._indexMap[node.Element] = index;
		}
		#endregion
	}
}

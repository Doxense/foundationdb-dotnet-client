#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace FoundationDB.Client.Testing
{
	using System;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;

	[DebuggerDisplay("Count={Count}")]
	[DebuggerTypeProxy(typeof(ContextStack<>.DebugView))]
	internal sealed class ContextStack<T>
	{

		private T[] Items = [];

		public int Count { get; private set; }

		private sealed class DebugView
		{

			public DebugView(ContextStack<T> stack)
			{
				var items = stack.ToArray();
				Array.Reverse(items);
				this.Items = items;
			}

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public T[] Items { get; }

		}

		private ref T GetAt(int index) => ref this.Items[this.Count - index - 1];

		public T this[int index] => this.Count > 0 ? GetAt(index) : throw new IndexOutOfRangeException();

		public void Clear()
		{
			this.Items.AsSpan(0, this.Count).Clear();
			this.Count = 0;
		}

		/// <summary>Push the provided item onto the stack</summary>
		public void Push(T item)
		{
			var items = this.Items;
			var count = this.Count;
			if (count >= items.Length)
			{
				Array.Resize(ref items, items.Length + 4);
				this.Items = items;
			}

			items[count] = item;
			this.Count = count + 1;
		}

		/// <summary>Pops and discard the top item on the stack</summary>
		public T Pop()
		{
			var count = this.Count;
			if (count == 0) throw new InvalidOperationException("The stack is empty");

			ref var slot = ref GetAt(0);
			var item = slot;
			slot = default!;
			this.Count = count - 1;
			return item;
		}

		/// <summary>Try to pop and discard the top item on the stack, unless it is empty.</summary>
		public bool TryPop([MaybeNullWhen(false)] out T item)
		{
			var count = this.Count;
			if (count == 0)
			{
				item = default;
				return false;
			}

			ref var slot = ref GetAt(0);
			item = slot;
			slot = default!;
			this.Count = count - 1;
			return true;
		}

		/// <summary>Return the top item on the stack, without discarding it.</summary>
		public T Peek()
		{
			return this.Count != 0 ? GetAt(0) : throw new InvalidOperationException("The stack is empty");
		}

		/// <summary>Return the top item on the stack, without discarding it, unless it is empty.</summary>
		public bool TryPeek([MaybeNullWhen(false)] out T item)
		{
			var count = this.Count;
			if (count == 0)
			{
				item = default;
				return false;
			}
			item = GetAt(0);
			return true;
		}

		/// <summary>Duplicate the top item on the stack</summary>
		public void Dup()
		{
			Push(Peek());
		}

		/// <summary>Swap the items at the specified locations</summary>
		public void Swap(int a, int b)
		{
			var count = this.Count;
			if ((uint) a > count) throw new ArgumentOutOfRangeException(nameof(a), a, "Outside the bounds of the stack");
			if ((uint) b > count) throw new ArgumentOutOfRangeException(nameof(b), b, "Outside the bounds of the stack");

			if (a != b)
			{
				ref var sa = ref GetAt(a);
				ref var sb = ref GetAt(b);
				(sa, sb) = (sb, sa);
			}
		}

		/// <summary>Return the content of the stack, from oldest to newest</summary>
		public T[] ToArray()
		{
			return this.Items.AsSpan(0, this.Count).ToArray();
		}

	}

}

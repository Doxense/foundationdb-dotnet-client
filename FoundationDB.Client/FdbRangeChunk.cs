#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Client
{
	using JetBrains.Annotations;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using Doxense.Serialization.Encoders;

	[DebuggerDisplay("Count={Chunk!=null?Chunk.Length:0}, HasMore={HasMore}, Reversed={Reversed}, Iteration={Iteration}")]
	[PublicAPI]
	public sealed class FdbRangeChunk : IReadOnlyList<KeyValuePair<Slice, Slice>>
	{
		/// <summary>Contains the items that where </summary>
		public KeyValuePair<Slice, Slice>[] Items { get; }

		/// <summary>Set to true if the original range read was reversed (meaning the items are in reverse lexicographic order</summary>
		public bool Reversed { get; }

		/// <summary>Set to true if there are more results in the database than could fit in a single chunk</summary>
		public bool HasMore { get; }

		/// <summary>Iteration number of this chunk (used when paging through a long range)</summary>
		public int Iteration { get; }

		/// <summary>Specify if the chunk contains only keys, only values, or both (default)</summary>
		public FdbReadMode ReadMode { get; }

		/// <summary>Returns the first item in the chunk</summary>
		/// <remarks>Note that if the range is reversed, then the first item will be GREATER than the last !</remarks>
		public Slice Last { get; }

		/// <summary>Returns the last item in the chunk</summary>
		/// <remarks>Note that if the range is reversed, then the last item will be LESS than the first!</remarks>
		public Slice First { get; }

		public FdbRangeChunk(KeyValuePair<Slice, Slice>[] items, bool hasMore, int iteration, bool reversed, FdbReadMode readMode, Slice first, Slice last)
		{
			Contract.NotNull(items);
			this.Items = items;
			this.HasMore = hasMore;
			this.Iteration = iteration;
			this.Reversed = reversed;
			this.ReadMode = readMode;
			this.First = first;
			this.Last = last;
		}

		[Obsolete("This property will be removed in the next release.")]
		public KeyValuePair<Slice, Slice>[] Chunk => this.Items;

		/// <summary>Returns the number of results in this chunk</summary>
		public int Count => this.Items.Length;

		/// <summary>Returns true if the chunk does not contain any item.</summary>
		public bool IsEmpty => this.Items.Length == 0;

		/// <summary>Returns the total size of all keys and values in the chunk</summary>
		public int GetSize()
		{
			long sum = 0;
			var results = this.Items;
			for (int i = 0; i < results.Length; i++)
			{
				sum += results[i].Key.Count + results[i].Value.Count;
			}
			return checked((int) sum);
		}

		#region Items...

		/// <summary>Return a reference to the result at the specified index</summary>
		public KeyValuePair<Slice, Slice> this[int index]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Items[index];
		}

		/// <summary>Return a reference to the result at the specified index</summary>
		public KeyValuePair<Slice, Slice> this[Index index]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Items[index];
		}

		/// <summary>Return a slice of the results in the specified range</summary>
		public ReadOnlySpan<KeyValuePair<Slice, Slice>> this[Range range]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Items.AsSpan(range);
		}

		/// <summary>Return a reference to the result at the specified index</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref readonly KeyValuePair<Slice, Slice> ItemRef(int index) => ref this.Items[index];

		public KeyValuePair<Slice, Slice>[] ToArray()
		{
			var tmp = new KeyValuePair<Slice, Slice>[this.Count];
			this.Items.CopyTo(tmp, 0);
			return tmp;
		}

		public void CopyTo(KeyValuePair<Slice, Slice>[] array, int offset)
		{
			this.Items.CopyTo(array, offset);
		}

		public ChunkEnumerator GetEnumerator()
		{
			return new ChunkEnumerator(this.Items);
		}

		IEnumerator<KeyValuePair<Slice, Slice>> IEnumerable<KeyValuePair<Slice, Slice>>.GetEnumerator() => GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public struct ChunkEnumerator : IEnumerator<KeyValuePair<Slice, Slice>>
		{

			private readonly KeyValuePair<Slice, Slice>[] Items;
			private int Index;

			public ChunkEnumerator(KeyValuePair<Slice, Slice>[] items)
			{
				this.Items = items;
				this.Index = -1;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext()
			{
				int p = this.Index + 1;
				if (p >= this.Items.Length) return MoveNextRare();
				this.Index = p;
				return true;
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private bool MoveNextRare()
			{
				this.Index = this.Items.Length;
				return false;
			}

			public KeyValuePair<Slice, Slice> Current
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.Items[this.Index];
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Dispose()
			{ }

			object IEnumerator.Current => Current;

			void IEnumerator.Reset()
			{
				this.Index = -1;
			}

		}

		#endregion

		#region Keys...

		public KeysCollection Keys => new KeysCollection(this.Items);

		public readonly struct KeysCollection : IReadOnlyList<Slice>
		{
			private readonly KeyValuePair<Slice, Slice>[] Items;

			internal KeysCollection(KeyValuePair<Slice, Slice>[] items)
			{
				this.Items = items;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public ChunkKeysEnumerator GetEnumerator()
			{
				return new ChunkKeysEnumerator(this.Items);
			}

			IEnumerator<Slice> IEnumerable<Slice>.GetEnumerator() => GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

			public int Count => this.Items.Length;

			public Slice this[int index] => this.Items[index].Key;
		}

		public struct ChunkKeysEnumerator : IEnumerator<Slice>
		{

			private readonly KeyValuePair<Slice, Slice>[] Items;
			private int Index;

			public ChunkKeysEnumerator(KeyValuePair<Slice, Slice>[] items)
			{
				this.Items = items;
				this.Index = -1;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext()
			{
				int p = this.Index + 1;
				if (p >= this.Items.Length) return MoveNextRare();
				this.Index = p;
				return true;
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private bool MoveNextRare()
			{
				this.Index = this.Items.Length;
				return false;
			}

			public Slice Current
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.Items[this.Index].Key;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Dispose()
			{ }

			object IEnumerator.Current => Current;

			void IEnumerator.Reset()
			{
				this.Index = -1;
			}

		}

		#endregion

		#region Values...

		public ValuesCollection Values => new ValuesCollection(this.Items);

		/// <summary>Append all the values into a buffer, in sequential order</summary>
		/// <returns>Slice with all values copied in sequential order, or <see cref="Slice.Nil"/> if the chunk is empty</returns>
		/// <remarks>
		/// This is useful when using a range queries to read all the chunks of a single entity.
		/// Since this method will allocate and copy all the results, if the expected size is expected to be large, prefer iterating over the <see cref="Values"/> instead!
		/// </remarks>
		public Slice ConcatValues()
		{
			var items = this.Items;
			switch (items.Length)
			{
				case 0: return Slice.Nil;
				case 1: return items[0].Value;
				case 2: return items[0].Value + items[1].Value;
				default:
				{
					var sw = new SliceWriter();
					AppendValues(ref sw, items);
					return sw.ToSlice();
				}
			}
		}

		/// <summary>Append all the values into a buffer, in sequential order</summary>
		/// <param name="writer">Buffer where to write all the values</param>
		/// <returns>Total number of bytes written to the buffer</returns>
		public int AppendValues(ref SliceWriter writer)
		{
			return AppendValues(ref writer, this.Items);
		}

		private static int AppendValues(ref SliceWriter writer, KeyValuePair<Slice, Slice>[] items)
		{
			switch (items.Length)
			{
				case 0:
				{
					return 0;
				}
				case 1:
				{
					var value = items[0].Value;
					writer.WriteBytes(value);
					return value.Count;
				}
				default:
				{
					long total = 0;
					for (int i = 0; i < items.Length; i++)
					{
						total += items[i].Value.Count;
					}
					if (total >= int.MaxValue) throw new OutOfMemoryException("Total size of merged values exceeds maximum allowed value.");

					writer.EnsureBytes(checked((int) total));
					for (int i = 0; i < items.Length; i++)
					{
						writer.WriteBytes(items[i].Value);
					}
					return (int) total;
				}
			}
		}

		public readonly struct ValuesCollection : IReadOnlyList<Slice>
		{
			private readonly KeyValuePair<Slice, Slice>[] Items;

			internal ValuesCollection(KeyValuePair<Slice, Slice>[] items)
			{
				this.Items = items;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public ChunkKeysEnumerator GetEnumerator()
			{
				return new ChunkKeysEnumerator(this.Items);
			}

			IEnumerator<Slice> IEnumerable<Slice>.GetEnumerator() => GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

			public int Count => this.Items.Length;

			public Slice this[int index] => this.Items[index].Value;
		}

		public struct ChunkValuesEnumerator : IEnumerator<Slice>
		{

			private readonly KeyValuePair<Slice, Slice>[] Items;
			private int Index;

			public ChunkValuesEnumerator(KeyValuePair<Slice, Slice>[] items)
			{
				this.Items = items;
				this.Index = -1;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext()
			{
				int p = this.Index + 1;
				if (p >= this.Items.Length) return MoveNextRare();
				this.Index = p;
				return true;
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private bool MoveNextRare()
			{
				this.Index = this.Items.Length;
				return false;
			}

			public Slice Current
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.Items[this.Index].Value;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Dispose()
			{ }

			object IEnumerator.Current => Current;

			void IEnumerator.Reset()
			{
				this.Index = -1;
			}

		}

		#endregion

		#region Decoding Helpers...

		//REVIEW: is this really used? It could be moved to a set of extensions methods instead?

		/// <summary>Decode the content of this chunk into an array of typed key/value pairs</summary>
		/// <typeparam name="TKey">Type of the keys</typeparam>
		/// <typeparam name="TValue">Type of the values</typeparam>
		/// <param name="keyHandler">Lambda that can decode the keys of this chunk</param>
		/// <param name="valueHandler">Lambda that can decode the values of this chunk</param>
		/// <returns>Array of decoded key/value pairs, or an empty array if the chunk doesn't have any results</returns>
		public KeyValuePair<TKey, TValue>[] Decode<TKey, TValue>(Func<Slice, TKey> keyHandler, Func<Slice, TValue> valueHandler)
		{
			Contract.NotNull(keyHandler);
			Contract.NotNull(valueHandler);

			var results = this.Items;
			var items = new KeyValuePair<TKey, TValue>[results.Length];

			for (int i = 0; i < results.Length; i++)
			{
				items[i] = new KeyValuePair<TKey, TValue>(
					keyHandler(results[i].Key),
					valueHandler(results[i].Value)
				);
			}

			return items;
		}

		/// <summary>Decode the content of this chunk into an array of typed key/value pairs</summary>
		/// <typeparam name="TKey">Type of the keys</typeparam>
		/// <typeparam name="TValue">Type of the values</typeparam>
		/// <param name="subspace">Subspace that is expected to contain all the keys.</param>
		/// <param name="keyEncoder">Instance used to decode the keys of this chunk. The prefix of <paramref name="subspace"/> is removed from the key before calling the encoder.</param>
		/// <param name="valueEncoder">Instance used to decode the values of this chunk</param>
		/// <returns>Array of decoded key/value pairs, or an empty array if the chunk doesn't have any results</returns>
		/// <exception cref="System.ArgumentException">If at least on key in the result is outside <paramref name="subspace"/>.</exception>
		/// <exception cref="System.ArgumentNullException">If either <paramref name="subspace"/>, <paramref name="keyEncoder"/> or <paramref name="valueEncoder"/> is null.</exception>
		public KeyValuePair<TKey, TValue>[] Decode<TKey, TValue>(KeySubspace subspace, IKeyEncoder<TKey> keyEncoder, IValueEncoder<TValue> valueEncoder)
		{
			Contract.NotNull(subspace);
			Contract.NotNull(keyEncoder);
			Contract.NotNull(valueEncoder);

			var results = this.Items;
			var items = new KeyValuePair<TKey, TValue>[results.Length];

			for (int i = 0; i < results.Length; i++)
			{
				items[i] = new KeyValuePair<TKey, TValue>(
					keyEncoder.DecodeKey(subspace.ExtractKey(results[i].Key, boundCheck: true))!,
					valueEncoder.DecodeValue(results[i].Value)!
				);
			}

			return items;
		}

		/// <summary>Decode the content of this chunk into an array of typed key/value pairs</summary>
		/// <typeparam name="TKey">Type of the keys</typeparam>
		/// <typeparam name="TValue">Type of the values</typeparam>
		/// <param name="keyEncoder">Instance used to decode the keys of this chunk</param>
		/// <param name="valueEncoder">Instance used to decode the values of this chunk</param>
		/// <returns>Array of decoded key/value pairs, or an empty array if the chunk doesn't have any results</returns>
		/// <exception cref="System.ArgumentNullException">If either <paramref name="keyEncoder"/> or <paramref name="valueEncoder"/> is null.</exception>
		public KeyValuePair<TKey, TValue>[] Decode<TKey, TValue>(IKeyEncoder<TKey> keyEncoder, IValueEncoder<TValue> valueEncoder)
		{
			Contract.NotNull(keyEncoder);
			Contract.NotNull(valueEncoder);

			var results = this.Items;
			var items = new KeyValuePair<TKey, TValue>[results.Length];

			for (int i = 0; i < results.Length; i++)
			{
				items[i] = new KeyValuePair<TKey, TValue>(
					keyEncoder.DecodeKey(results[i].Key)!,
					valueEncoder.DecodeValue(results[i].Value)!
				);
			}

			return items;
		}

		/// <summary>Decode the content of this chunk into an array of typed keys</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="handler">Instance used to decode the keys of this chunk</param>
		/// <returns>Array of decoded keys, or an empty array if the chunk doesn't have any results</returns>
		public T[] DecodeKeys<T>(Func<Slice, T> handler)
		{
			Contract.NotNull(handler);

			var results = this.Items;
			var keys = new T[results.Length];
			for (int i = 0; i < keys.Length; i++)
			{
				keys[i] = handler(results[i].Key);
			}
			return keys;
		}

		/// <summary>Decode the content of this chunk into an array of typed keys</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="subspace"></param>
		/// <param name="keyEncoder">Instance used to decode the keys of this chunk</param>
		/// <returns>Array of decoded keys, or an empty array if the chunk doesn't have any results</returns>
		public T[] DecodeKeys<T>(KeySubspace subspace, IKeyEncoder<T> keyEncoder)
		{
			Contract.NotNull(subspace);
			Contract.NotNull(keyEncoder);

			var results = this.Items;
			var keys = new T[results.Length];
			for(int i = 0; i< keys.Length;i++)
			{
				keys[i] = keyEncoder.DecodeKey(subspace.ExtractKey(results[i].Key, boundCheck: true))!;
			}
			return keys;
		}

		/// <summary>Decode the content of this chunk into an array of typed keys</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="keyEncoder">Instance used to decode the keys of this chunk</param>
		/// <returns>Array of decoded keys, or an empty array if the chunk doesn't have any results</returns>
		public T[] DecodeKeys<T>(IKeyEncoder<T> keyEncoder)
		{
			Contract.NotNull(keyEncoder);

			var results = this.Items;
			var values = new T[results.Length];
			for (int i = 0; i < values.Length; i++)
			{
				values[i] = keyEncoder.DecodeKey(results[i].Key)!;
			}
			return values;
		}

		/// <summary>Decode the content of this chunk into an array of typed values</summary>
		/// <typeparam name="T">Type of the values</typeparam>
		/// <param name="handler">Lambda that can decode the values of this chunk</param>
		/// <returns>Array of decoded values, or an empty array if the chunk doesn't have any results</returns>
		public T[] DecodeValues<T>(Func<Slice, T> handler)
		{
			Contract.NotNull(handler);

			var results = this.Items;
			var values = new T[results.Length];
			for (int i = 0; i < values.Length; i++)
			{
				values[i] = handler(results[i].Value);
			}
			return values;
		}

		/// <summary>Decode the content of this chunk into an array of typed values</summary>
		/// <typeparam name="T">Type of the values</typeparam>
		/// <param name="valueEncoder">Instance used to decode the values of this chunk</param>
		/// <returns>Array of decoded values, or an empty array if the chunk doesn't have any results</returns>
		public T[] DecodeValues<T>(IValueEncoder<T> valueEncoder)
		{
			Contract.NotNull(valueEncoder);

			var results = this.Items;
			var values = new T[results.Length];
			for (int i = 0; i < values.Length; i++)
			{
				values[i] = valueEncoder.DecodeValue(results[i].Value)!;
			}
			return values;
		}

		#endregion
	}

}

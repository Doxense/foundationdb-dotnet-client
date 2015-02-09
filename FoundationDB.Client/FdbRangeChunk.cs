#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FoundationDB.Client
{
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;

	[DebuggerDisplay("Count={Chunk!=null?Chunk.Length:0}, HasMore={HasMore}, Reversed={Reversed}, Iteration={Iteration}")]
	public struct FdbRangeChunk
	{
		/// <summary>Set to true if there are more results in the database than could fit in a single chunk</summary>
		public readonly bool HasMore;
		//TODO: consider renaming Chunk to Results or Items ?
		// => I saw a lot of "var chunk = tr.GetRangeAsync(...); if (chunk.Chunk.Length > 0 { ... }" which is a bit ugly..
		/// <summary>Contains the items that where </summary>
		public readonly KeyValuePair<Slice, Slice>[] Chunk;

		/// <summary>Iteration number of this chunk (used when paging through a long range)</summary>
		public readonly int Iteration;

		/// <summary>Set to true if the original range read was reversed (meaning the items are in reverse lexicographic order</summary>
		public readonly bool Reversed;

		public FdbRangeChunk(bool hasMore, KeyValuePair<Slice, Slice>[] chunk, int iteration, bool reversed)
		{
			this.HasMore = hasMore;
			this.Chunk = chunk;
			this.Iteration = iteration;
			this.Reversed = reversed;
		}

		/// <summary>Returns the number of results in this chunk</summary>
		public int Count { get { return this.Chunk != null ? this.Chunk.Length : 0; } }

		/// <summary>Returns true if the chunk does not contain any item.</summary>
		public bool IsEmpty { get { return this.Chunk == null || this.Chunk.Length == 0; } }

		/// <summary>Returns the first item in the chunk</summary>
		/// <remarks>Note that if the range is reversed, then the first item will be GREATER than the last !</remarks>
		public KeyValuePair<Slice, Slice> First
		{
			get
			{
				var chunk = this.Chunk;
				if (chunk != null && chunk.Length > 0) return chunk[0];
				return default(KeyValuePair<Slice, Slice>);
			}
		}

		/// <summary>Returns the last item in the chunk</summary>
		/// <remarks>Note that if the range is reversed, then the last item will be LESS than the first!</remarks>
		public KeyValuePair<Slice, Slice> Last
		{
			get
			{
				var chunk = this.Chunk;
				if (chunk != null && chunk.Length > 0) return chunk[chunk.Length - 1];
				return default(KeyValuePair<Slice, Slice>);
			}
		}

		/// <summary>Decode the content of this chunk into an array of typed key/value pairs</summary>
		/// <typeparam name="TKey">Type of the keys</typeparam>
		/// <typeparam name="TValue">Type of the values</typeparam>
		/// <param name="keyHandler">Lambda that can decode the keys of this chunk</param>
		/// <param name="valueHandler">Lambda that can decode the values of this chunk</param>
		/// <returns>Array of decoded key/value pairs, or an empty array if the chunk doesn't have any results</returns>
		[NotNull]
		public KeyValuePair<TKey, TValue>[] Decode<TKey, TValue>([NotNull] Func<Slice, TKey> keyHandler, [NotNull] Func<Slice, TValue> valueHandler)
		{
			if (keyHandler == null) throw new ArgumentNullException("keyHandler");
			if (valueHandler == null) throw new ArgumentNullException("valueHandler");

			var chunk = this.Chunk;
			var results = new KeyValuePair<TKey, TValue>[chunk.Length];

			for (int i = 0; i < chunk.Length; i++)
			{
				results[i] = new KeyValuePair<TKey, TValue>(
					keyHandler(chunk[i].Key),
					valueHandler(chunk[i].Value)
				);
			}

			return results;
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
		[NotNull]
		public KeyValuePair<TKey, TValue>[] Decode<TKey, TValue>([NotNull] FdbSubspace subspace, [NotNull] IKeyEncoder<TKey> keyEncoder, [NotNull] IValueEncoder<TValue> valueEncoder)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (keyEncoder == null) throw new ArgumentNullException("keyEncoder");
			if (valueEncoder == null) throw new ArgumentNullException("valueEncoder");

			var chunk = this.Chunk;
			var results = new KeyValuePair<TKey, TValue>[chunk.Length];

			for (int i = 0; i < chunk.Length; i++)
			{
				results[i] = new KeyValuePair<TKey, TValue>(
					keyEncoder.DecodeKey(subspace.ExtractKey(chunk[i].Key, boundCheck: true)),
					valueEncoder.DecodeValue(chunk[i].Value)
				);
			}

			return results;
		}

		/// <summary>Decode the content of this chunk into an array of typed key/value pairs</summary>
		/// <typeparam name="TKey">Type of the keys</typeparam>
		/// <typeparam name="TValue">Type of the values</typeparam>
		/// <param name="keyEncoder">Instance used to decode the keys of this chunk</param>
		/// <param name="valueEncoder">Instance used to decode the values of this chunk</param>
		/// <returns>Array of decoded key/value pairs, or an empty array if the chunk doesn't have any results</returns>
		/// <exception cref="System.ArgumentNullException">If either <paramref name="keyEncoder"/> or <paramref name="valueEncoder"/> is null.</exception>
		[NotNull]
		public KeyValuePair<TKey, TValue>[] Decode<TKey, TValue>([NotNull] IKeyEncoder<TKey> keyEncoder, [NotNull] IValueEncoder<TValue> valueEncoder)
		{
			if (keyEncoder == null) throw new ArgumentNullException("keyEncoder");
			if (valueEncoder == null) throw new ArgumentNullException("valueEncoder");

			var chunk = this.Chunk;
			var results = new KeyValuePair<TKey, TValue>[chunk.Length];

			for (int i = 0; i < chunk.Length; i++)
			{
				results[i] = new KeyValuePair<TKey, TValue>(
					keyEncoder.DecodeKey(chunk[i].Key),
					valueEncoder.DecodeValue(chunk[i].Value)
				);
			}

			return results;
		}

		/// <summary>Decode the content of this chunk into an array of typed keys</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="handler">Instance used to decode the keys of this chunk</param>
		/// <returns>Array of decoded keys, or an empty array if the chunk doesn't have any results</returns>
		[NotNull]
		public T[] DecodeKeys<T>([NotNull] Func<Slice, T> handler)
		{
			if (handler == null) throw new ArgumentNullException("handler");

			var results = new T[this.Count];
			for (int i = 0; i < results.Length; i++)
			{
				results[i] = handler(this.Chunk[i].Key);
			}
			return results;
		}

		/// <summary>Decode the content of this chunk into an array of typed keys</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="keyEncoder">Instance used to decode the keys of this chunk</param>
		/// <returns>Array of decoded keys, or an empty array if the chunk doesn't have any results</returns>
		[NotNull]
		public T[] DecodeKeys<T>([NotNull] FdbSubspace subspace, [NotNull] IKeyEncoder<T> keyEncoder)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (keyEncoder == null) throw new ArgumentNullException("keyEncoder");

			var results = new T[this.Count];
			for(int i = 0; i< results.Length;i++)
			{
				results[i] = keyEncoder.DecodeKey(subspace.ExtractKey(this.Chunk[i].Key, boundCheck: true));
			}
			return results;
		}

		/// <summary>Decode the content of this chunk into an array of typed keys</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="keyEncoder">Instance used to decode the keys of this chunk</param>
		/// <returns>Array of decoded keys, or an empty array if the chunk doesn't have any results</returns>
		[NotNull]
		public T[] DecodeKeys<T>([NotNull] IKeyEncoder<T> keyEncoder)
		{
			if (keyEncoder == null) throw new ArgumentNullException("keyEncoder");

			var results = new T[this.Count];
			for (int i = 0; i < results.Length; i++)
			{
				results[i] = keyEncoder.DecodeKey(this.Chunk[i].Key);
			}
			return results;
		}

		/// <summary>Decode the content of this chunk into an array of typed values</summary>
		/// <typeparam name="T">Type of the values</typeparam>
		/// <param name="handler">Lambda that can decode the values of this chunk</param>
		/// <returns>Array of decoded values, or an empty array if the chunk doesn't have any results</returns>
		[NotNull]
		public T[] DecodeValues<T>([NotNull] Func<Slice, T> handler)
		{
			if (handler == null) throw new ArgumentNullException("handler");

			var results = new T[this.Count];
			for (int i = 0; i < results.Length; i++)
			{
				results[i] = handler(this.Chunk[i].Value);
			}
			return results;
		}

		/// <summary>Decode the content of this chunk into an array of typed values</summary>
		/// <typeparam name="T">Type of the values</typeparam>
		/// <param name="valueEncoder">Instance used to decode the values of this chunk</param>
		/// <returns>Array of decoded values, or an empty array if the chunk doesn't have any results</returns>
		[NotNull]
		public T[] DecodeValues<T>([NotNull] IValueEncoder<T> valueEncoder)
		{
			if (valueEncoder == null) throw new ArgumentNullException("valueEncoder");

			var results = new T[this.Count];
			for (int i = 0; i < results.Length; i++)
			{
				results[i] = valueEncoder.DecodeValue(this.Chunk[i].Value);
			}
			return results;
		}
	
	}

}

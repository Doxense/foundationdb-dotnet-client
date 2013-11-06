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
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	/// <summary>Factory class for keys</summary>
	public static class FdbKey
	{
		/// <summary>Smallest possible key ('\0')</summary>
		public static readonly Slice MinValue = Slice.FromByte(0);

		/// <summary>Bigest possible key ('\xFF'), excluding the system keys</summary>
		public static readonly Slice MaxValue = Slice.FromByte(255);

		/// <summary>Default Directory Layer prefix ('\xFE')</summary>
		public static readonly Slice Directory = Slice.FromByte(254);

		/// <summary>Default System prefix ('\xFF')</summary>
		public static readonly Slice System = Slice.FromByte(255);

		/// <summary>Returns the first key lexicographically that does not have the passed in <paramref name="slice"/> as a prefix</summary>
		/// <param name="slice">Slice to increment</param>
		/// <returns>New slice that is guaranteed to be the first key lexicographically higher than <paramref name="slice"/> which does not have <paramref name="slice"/> as a prefix</returns>
		/// <remarks>If the last byte is already equal to 0xFF, it will rollover to 0x00 and the next byte will be incremented.</remarks>
		/// <exception cref="System.ArgumentException">If the Slice is equal to Slice.Nil</exception>
		/// <exception cref="System.OverflowException">If the Slice is the empty string or consists only of 0xFF bytes</exception>
		/// <example>
		/// FdbKey.Increment(Slice.FromString("ABC")) => "ABD"
		/// FdbKey.Increment(Slice.FromHexa("01 FF")) => { 02 }
		/// </example>
		public static Slice Increment(Slice slice)
		{
			if (slice.IsNull) throw new ArgumentException("Cannot increment null buffer", "slice");

			int lastNonFFByte;
			var tmp = slice.GetBytes();
			for (lastNonFFByte = tmp.Length - 1; lastNonFFByte >= 0; --lastNonFFByte)
			{
				if (tmp[lastNonFFByte] != 0xFF)
				{
					++tmp[lastNonFFByte];
					break;
				}
			}

			if (lastNonFFByte < 0)
			{
				throw Fdb.Errors.CannotIncrementKey();
			}

			return new Slice(tmp, 0, lastNonFFByte + 1);
		}

		/// <summary>Merge an array of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Array of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] Merge(Slice prefix, Slice[] keys)
		{
			if (prefix == null) throw new ArgumentNullException("prefix");
			if (keys == null) throw new ArgumentNullException("keys");

			// we can pre-allocate exactly the buffer by computing the total size of all keys
			int size = keys.Sum(key => key.Count) + keys.Length * prefix.Count;
			var writer = new SliceWriter(size);
			var next = new List<int>(keys.Length);

			//TODO: use multiple buffers if item count is huge ?

			foreach (var key in keys)
			{
				if (prefix.IsPresent) writer.WriteBytes(prefix);
				writer.WriteBytes(key);
				next.Add(writer.Position);
			}

			return FdbKey.SplitIntoSegments(writer.Buffer, 0, next);
		}

		/// <summary>Merge a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] Merge(Slice prefix, IEnumerable<Slice> keys)
		{
			if (prefix == null) throw new ArgumentNullException("prefix");
			if (keys == null) throw new ArgumentNullException("keys");

			// use optimized version for arrays
			var array = keys as Slice[];
			if (array != null) return Merge(prefix, array);

			// pre-allocate with a count if we can get one...
			var coll = keys as ICollection<Slice>;
			var next = coll == null ? new List<int>() : new List<int>(coll.Count);
			var writer = SliceWriter.Empty;

			//TODO: use multiple buffers if item count is huge ?

			foreach (var key in keys)
			{
				if (prefix.IsPresent) writer.WriteBytes(prefix);
				writer.WriteBytes(key);
				next.Add(writer.Position);
			}

			return FdbKey.SplitIntoSegments(writer.Buffer, 0, next);
		}

		/// <summary>Split a buffer containing multiple contiguous segments into an array of segments</summary>
		/// <param name="buffer">Buffer containing all the segments</param>
		/// <param name="start">Offset of the start of the first segment</param>
		/// <param name="endOffsets">Array containing, for each segment, the offset of the following segment</param>
		/// <returns>Array of segments</returns>
		/// <example>SplitIntoSegments("HelloWorld", 0, [5, 10]) => [{"Hello"}, {"World"}]</example>
		internal static Slice[] SplitIntoSegments(byte[] buffer, int start, List<int> endOffsets)
		{
			var result = new Slice[endOffsets.Count];
			int i = 0;
			int p = start;
			foreach (var end in endOffsets)
			{
				result[i++] = new Slice(buffer, p, end - p);
				p = end;
			}

			return result;
		}

		/// <summary>Split a range of indexes into several batches</summary>
		/// <param name="offset">Offset from which to start counting</param>
		/// <param name="count">Total number of values that will be returned</param>
		/// <param name="batchSize">Maximum size of each batch</param>
		/// <returns>Collection of B batches each containing at most <paramref name="batchSize"/> contiguous indices, counting from <paramref name="offset"/> to (<paramref name="offset"/> + <paramref name="count"/> - 1)</returns>
		/// <example>Batched(0, 100, 20) => [ {0..19}, {20..39}, {40..59}, {60..79}, {80..99} ]</example>
		public static IEnumerable<IEnumerable<int>> BatchedRange(int offset, int count, int batchSize)
		{
			while (count > 0)
			{
				int chunk = Math.Min(count, batchSize);
				yield return Enumerable.Range(offset, chunk);
				offset += chunk;
				count -= chunk;
			}
		}

		private sealed class BatchIterator : IEnumerable<IEnumerable<KeyValuePair<int, int>>>
		{
			private readonly object m_lock = new object();
			private int m_cursor;
			private int m_remaining;

			private readonly int m_offset;
			private readonly int m_count;
			private readonly int m_workers;
			private readonly int m_batchSize;

			public BatchIterator(int offset, int count, int workers, int batchSize)
			{
				m_offset = offset;
				m_count = count;
				m_workers = workers;
				m_batchSize = batchSize;

				m_cursor = offset;
				m_remaining = count;
			}

			private KeyValuePair<int, int> GetChunk()
			{
				if (m_remaining == 0) return default(KeyValuePair<int, int>);

				lock (m_lock)
				{
					int cursor = m_cursor;
					int size = Math.Min(m_remaining, m_batchSize);

					m_cursor += size;
					m_remaining -= size;

					return new KeyValuePair<int, int>(cursor, size);
				}
			}


			public IEnumerator<IEnumerable<KeyValuePair<int, int>>> GetEnumerator()
			{
				for (int k = 0; k < m_workers; k++)
				{
					if (m_remaining == 0) yield break;
					yield return WorkerIterator(k);
				}
			}

			private IEnumerable<KeyValuePair<int, int>> WorkerIterator(int k)
			{
				while (true)
				{
					var chunk = GetChunk();
					if (chunk.Value == 0) break;
					yield return chunk;
				}
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return this.GetEnumerator();
			}
		}

		/// <summary>
		/// Split range of indexes into a fixed number of 'worker' sequence, that will consume batches in parallel
		/// </summary>
		/// <param name="offset">Offset from which to start counting</param>
		/// <param name="count">Total number of values that will be returned</param>
		/// <param name="workers">Number of concurrent workers that will take batches from the pool</param>
		/// <param name="batchSize">Maximum size of each batch</param>
		/// <returns>List of '<paramref name="workers"/>' enumerables that all fetch batches of values from the same common pool. All enumerables will stop when the last batch as been consumed by the last worker.</returns>
		public static IEnumerable<IEnumerable<KeyValuePair<int, int>>> Batched(int offset, int count, int workers, int batchSize)
		{
			return new BatchIterator(offset, count, workers, batchSize);
		}

		/// <summary>Produce a user-friendly version of the slice</summary>
		/// <param name="key">Random binary key</param>
		/// <returns>User friendly version of the key. Attempts to decode the key as a tuple first. Then as an ASCII string. Then as an hex dump of the key.</returns>
		/// <remarks>This can be slow, and should only be used for logging or troubleshooting.</remarks>
		public static string Dump(Slice key)
		{
			if (key.IsPresent)
			{
				if (key[0] <= 28 || key[0] >= 254)
				{ // it could be a tuple...
					try
					{
						IFdbTuple tuple = null;
						bool incr = false;
						try
						{
							tuple = FoundationDB.Layers.Tuples.FdbTuple.Unpack(key);
						}
						catch(Exception e)
						{
							if (e is FormatException || e is ArgumentOutOfRangeException)
							{
								// Exclusive end keys based on tuples may end up with "01" instead of "00" (due to the call to FdbKey.Increment)
								if (key.Count >= 2 && key[-1] > 0)
								{
									var tmp = key[0, -1] + (byte)(key[-1] - 1);
									tuple = FoundationDB.Layers.Tuples.FdbTuple.Unpack(tmp);
									incr = true;
								}
							}
						}
						if (tuple != null) return !incr ? tuple.ToString() : (tuple.ToString() + " + 1");
					}
					catch { }
				}
			}

			return Slice.Dump(key);

		}

	}

}

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
	* Neither the name of the <organization> nor the
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

using FoundationDb.Client.Tuples;
using FoundationDb.Client.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FoundationDb.Client
{

	/// <summary>Factory class for keys</summary>
	public static class FdbKey
	{
		private static readonly byte[] Bytes = new byte[] { 0, 255 };

		/// <summary>Smallest possible key ('\0')</summary>
		public static readonly Slice MinValue = new Slice(Bytes, 0, 1);

		/// <summary>Bigest possible key ('\xFF'), excluding the system keys</summary>
		public static readonly Slice MaxValue = new Slice(Bytes, 1, 1);

		public static Slice Ascii(string key)
		{
			if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key cannot be null or emtpy", "key");

			var bytes = new byte[key.Length];
			for (int i = 0; i < key.Length; i++)
			{
				bytes[i] = (byte)key[i];
			}
			return Binary(bytes);
		}

		public static string Ascii(Slice key)
		{
			return key.ToAscii();
		}

		public static Slice Unicode(string text)
		{
			return Binary(Encoding.UTF8.GetBytes(text));
		}

		public static string Unicode(Slice key)
		{
			return key.ToUnicode();
		}

		public static Slice Binary(byte[] data)
		{
			if (data == null) throw new ArgumentNullException("data");
			return new Slice(data, 0, data.Length);
		}

		public static Slice Binary(byte[] data, int offset, int count)
		{
			return new Slice(data, offset, count);
		}

		public static Slice Increment(IFdbTuple key)
		{
			return Increment(key.ToSlice());
		}

		public static Slice Increment(Slice buffer)
		{
			if (!buffer.HasValue) throw new ArgumentException("Cannot increment null buffer");
			var tmp = buffer.GetBytes();
			int n = tmp.Length - 1;
			while (n >= 0)
			{
				byte c = (byte)((tmp[n] + 1) & 0xFF);
				if (c > 0)
				{
					tmp[n] = c;
					break;
				}
				tmp[n] = 0;
				--n;
			}
			if (n < 0) throw new OverflowException("Cannot increment FdbKey past the maximum value");
			return new Slice(tmp, 0, tmp.Length);
		}

		public static bool AreEqual(IFdbTuple left, IFdbTuple right)
		{
			if (object.ReferenceEquals(left, right)) return true;
			return left.ToSlice() == right.ToSlice();
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
		public static IEnumerable<IEnumerable<int>> Batched(int offset, int count, int batchSize)
		{
			while (count > 0)
			{
				int chunk = Math.Min(count, batchSize);
				yield return Enumerable.Range(offset, chunk);
				offset += chunk;
				count -= chunk;
			}
		}

		private class BatchIterator : IEnumerable<IEnumerable<KeyValuePair<int, int>>>
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

		public static IEnumerable<IEnumerable<KeyValuePair<int, int>>> Batched(int offset, int count, int workers, int batchSize)
		{
			return new BatchIterator(offset, count, workers, batchSize);
		}

#if DEPRECATED

		public static IFdbKey Pack<T1, T2>(T1 item1, T2 item2)
		{
			return FdbTuple.Create<T1, T2>(item1, item2);
		}

		public static IFdbKey Pack<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			return FdbTuple.Create<T1, T2, T3>(item1, item2, item3);
		}

		public static IFdbKey Pack<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return FdbTuple.Create<T1, T2, T3, T4>(item1, item2, item3, item4);
		}

		public static IFdbKey Pack<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return FdbTuple.Create<T1, T2, T3, T4, T5>(item1, item2, item3, item4, item5);
		}

		public static IFdbKey Pack(params object[] items)
		{
			return FdbTuple.Create(items);
		}

		public static IFdbKey Pack(IFdbTuple prefix, params object[] items)
		{
			return prefix.AppendRange(items);
		}

#endif

	}

}

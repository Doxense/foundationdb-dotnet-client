#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
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

namespace FoundationDB
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using FoundationDB.Client;
	using JetBrains.Annotations;

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
		/// <exception cref="ArgumentException">If the Slice is equal to Slice.Nil</exception>
		/// <exception cref="OverflowException">If the Slice is the empty string or consists only of 0xFF bytes</exception>
		/// <example>
		/// FdbKey.Increment(Slice.FromString("ABC")) => "ABD"
		/// FdbKey.Increment(Slice.FromHexa("01 FF")) => { 02 }
		/// </example>
		public static Slice Increment(Slice slice)
		{
			if (slice.IsNull) throw new ArgumentException("Cannot increment null buffer", nameof(slice));

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
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Array of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[NotNull]
		public static Slice[] Merge(Slice prefix, [NotNull] Slice[] keys)
		{
			if (prefix.IsNull) throw new ArgumentNullException(nameof(prefix));
			if (keys == null) throw new ArgumentNullException(nameof(keys));

			//REVIEW: merge this code with Slice.ConcatRange!

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
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[NotNull]
		public static Slice[] Merge(Slice prefix, [NotNull] IEnumerable<Slice> keys)
		{
			if (prefix.IsNull) throw new ArgumentNullException(nameof(prefix));
			if (keys == null) throw new ArgumentNullException(nameof(keys));

			//REVIEW: merge this code with Slice.ConcatRange!

			// use optimized version for arrays
			var array = keys as Slice[];
			if (array != null) return Merge(prefix, array);

			// pre-allocate with a count if we can get one...
			var coll = keys as ICollection<Slice>;
			var next = coll == null ? new List<int>() : new List<int>(coll.Count);
			var writer = default(SliceWriter);

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
		[NotNull]
		internal static Slice[] SplitIntoSegments([NotNull] byte[] buffer, int start, [NotNull] List<int> endOffsets)
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

			private readonly int m_workers;
			private readonly int m_batchSize;

			public BatchIterator(int offset, int count, int workers, int batchSize)
			{
				Contract.Requires(offset >= 0 && count >= 0 && workers >= 0 && batchSize >= 0);
				m_cursor = offset;
				m_remaining = count;
				m_workers = workers;
				m_batchSize = batchSize;
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
		[NotNull]
		public static string Dump(Slice key)
		{
			return PrettyPrint(key, PrettyPrintMode.Single);
		}

		/// <summary>Produce a user-friendly version of the slice</summary>
		/// <param name="key">Random binary key</param>
		/// <param name="mode">Defines if the key is standalone, or is the begin or end part or a key range. This will enable or disable some heuristics that try to properly format key ranges.</param>
		/// <returns>User friendly version of the key. Attempts to decode the key as a tuple first. Then as an ASCII string. Then as an hex dump of the key.</returns>
		/// <remarks>This can be slow, and should only be used for logging or troubleshooting.</remarks>
		[DebuggerNonUserCode]
		[NotNull]
		public static string PrettyPrint(Slice key, PrettyPrintMode mode)
		{
			if (key.Count > 1)
			{
				byte c = key[0];
				//OPTIMIZE: maybe we need a lookup table
				if (c <= 28 || c == 32 || c == 33 || c == 48 || c == 49 || c >= 254)
				{ // it could be a tuple...
					try
					{
						ITuple tuple = null;
						string suffix = null;
						bool skip = false;

						try
						{
							switch (mode)
							{
								case PrettyPrintMode.End:
								{ // the last byte will either be FF, or incremented
									// for tuples, the really bad cases are for byte[]/strings (which normally end with 00)
									// => pack(("string",))+\xFF => <02>string<00><FF>
									// => string(("string",)) => <02>string<01>
									switch (key[-1])
									{
										case 0xFF:
											{
												//***README*** if you break under here, see README in the last catch() block
												tuple = TuPack.Unpack(key[0, -1]);
												suffix = ".<FF>";
												break;
											}
										case 0x01:
											{
												var tmp = key[0, -1] + (byte)0;
												//***README*** if you break under here, see README in the last catch() block
												tuple = TuPack.Unpack(tmp);
												suffix = " + 1";
												break;
											}
									}
									break;
								}
								case PrettyPrintMode.Begin:
								{ // the last byte will usually be 00

									// We can't really know if the tuple ended with NULL (serialized to <00>) or if a <00> was added,
									// but since the ToRange() on tuples add a <00> we can bet on the fact that it is not part of the tuple itself.
									// except maybe if we have "00 FF 00" which would be the expected form of a string that ends with a <00>

									if (key.Count > 2 && key[-1] == 0 && key[-2] != 0xFF)
									{
										//***README*** if you break under here, see README in the last catch() block
										tuple = TuPack.Unpack(key[0, -1]);
										suffix = ".<00>";
									}
									break;
								}
							}
						}
						catch (Exception e)
						{
							suffix = null;
							skip = !(e is FormatException || e is ArgumentOutOfRangeException);
						}

						if (tuple == null && !skip)
						{ // attempt a regular decoding
							//***README*** if you break under here, see README in the last catch() block
							tuple = TuPack.Unpack(key);
						}

						if (tuple != null) return tuple.ToString() + suffix;
					}
					catch (Exception)
					{
						//README: If Visual Studio is breaking inside some Tuple parsing method somewhere inside this try/catch,
						// this is because your debugger is configured to automatically break on thrown exceptions of type FormatException, ArgumentException, or InvalidOperation.
						// Unfortunately, there isn't much you can do except unchecking "break when this exception type is thrown". If you know a way to disable locally this behaviour, please fix this!
						// => only other option would be to redesign the parsing of tuples as a TryParseXXX() that does not throw, OR to have a VerifyTuple() methods that only checks for validity....
					}
				}
			}

			return Slice.Dump(key);
		}

		public enum PrettyPrintMode
		{
			Single = 0,
			Begin = 1,
			End = 2,
		}

	}

}

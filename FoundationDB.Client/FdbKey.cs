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
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using Doxense.Collections.Tuples;
	using Doxense.Collections.Tuples.Encoding;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	/// <summary>Factory class for keys</summary>
	[PublicAPI]
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
		/// <exception cref="ArgumentException">If <paramref name="slice"/> is <see cref="Slice.Nil"/></exception>
		/// <exception cref="OverflowException">If <paramref name="slice"/> is <see cref="Slice.Empty"/> or consists only of 0xFF bytes</exception>
		/// <example>
		/// FdbKey.Increment(Slice.FromString("ABC")) => "ABD"
		/// FdbKey.Increment(Slice.FromHexa("01 FF")) => { 02 }
		/// </example>
		public static Slice Increment(Slice slice)
		{
			if (slice.IsNull) throw new ArgumentException("Cannot increment null buffer", nameof(slice));

			int lastNonFFByte;
			var tmp = slice.GetBytesOrEmpty();
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

			return tmp.AsSlice(0, lastNonFFByte + 1);
		}

		/// <summary>Merge an array of keys with a same prefix, all sharing the same buffer</summary>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Array of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] Merge(Slice prefix, Slice[] keys)
		{
			Contract.NotNull(keys);
			return Merge(prefix, keys.AsSpan());
		}

		/// <summary>Merge an array of keys with a same prefix, all sharing the same buffer</summary>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Array of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] Merge(Slice prefix, ReadOnlySpan<Slice> keys)
		{
			if (prefix.IsNull) throw new ArgumentNullException(nameof(prefix));

			//REVIEW: merge this code with Slice.ConcatRange!

			// we can pre-allocate exactly the buffer by computing the total size of all keys
			long size = keys.Length * prefix.Count;
			for (int i = 0; i < keys.Length; i++)
			{
				size += keys[i].Count;
			}

			var writer = new SliceWriter(checked((int) size));
			var next = new List<int>(keys.Length);

			//TODO: use multiple buffers if item count is huge ?

			var prefixSpan = prefix.Span;
			foreach (var key in keys)
			{
				if (prefixSpan.Length != 0) writer.WriteBytes(prefixSpan);
				writer.WriteBytes(key.Span);
				next.Add(writer.Position);
			}

			return SplitIntoSegments(writer.Buffer, 0, next);
		}

		/// <summary>Merge a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] Merge(Slice prefix, IEnumerable<Slice> keys)
		{
			if (prefix.IsNull) throw new ArgumentNullException(nameof(prefix));
			Contract.NotNull(keys);

			//REVIEW: merge this code with Slice.ConcatRange!

			// use optimized version for arrays
			if (keys is Slice[] array) return Merge(prefix, array);

			// pre-allocate with a count if we can get one...
			var next = !(keys is ICollection<Slice> coll) ? new List<int>() : new List<int>(coll.Count);
			var writer = default(SliceWriter);

			//TODO: use multiple buffers if item count is huge ?

			var prefixSpan = prefix.Span;
			foreach (var key in keys)
			{
				if (prefixSpan.Length != 0) writer.WriteBytes(prefixSpan);
				writer.WriteBytes(key);
				next.Add(writer.Position);
			}

			return SplitIntoSegments(writer.Buffer, 0, next);
		}

		/// <summary>Split a buffer containing multiple contiguous segments into an array of segments</summary>
		/// <param name="buffer">Buffer containing all the segments</param>
		/// <param name="start">Offset of the start of the first segment</param>
		/// <param name="endOffsets">Array containing, for each segment, the offset of the following segment</param>
		/// <returns>Array of segments</returns>
		/// <example>SplitIntoSegments("HelloWorld", 0, [5, 10]) => [{"Hello"}, {"World"}]</example>
		internal static Slice[] SplitIntoSegments(byte[]? buffer, int start, List<int> endOffsets)
		{
			var result = new Slice[endOffsets.Count];
			int i = 0;
			int p = start;
			foreach (var end in endOffsets)
			{
				result[i++] = buffer.AsSlice(p, end - p);
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
			private readonly System.Threading.Lock m_lock = new ();
			private int m_cursor;
			private int m_remaining;

			private readonly int m_workers;
			private readonly int m_batchSize;

			public BatchIterator(int offset, int count, int workers, int batchSize)
			{
				Contract.Debug.Requires(offset >= 0 && count >= 0 && workers >= 0 && batchSize >= 0);
				m_cursor = offset;
				m_remaining = count;
				m_workers = workers;
				m_batchSize = batchSize;
			}

			private KeyValuePair<int, int> GetChunk()
			{
				if (m_remaining == 0) return default;

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
					yield return WorkerIterator();
				}
			}

			private IEnumerable<KeyValuePair<int, int>> WorkerIterator()
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
			return PrettyPrint(key, PrettyPrintMode.Single);
		}

		/// <summary>Produce a user-friendly version of the slice</summary>
		/// <param name="key">Random binary key</param>
		/// <param name="mode">Defines if the key is standalone, or is the begin or end part or a key range. This will enable or disable some heuristics that try to properly format key ranges.</param>
		/// <returns>User friendly version of the key. Attempts to decode the key as a tuple first. Then as an ASCII string. Then as an hex dump of the key.</returns>
		/// <remarks>This can be slow, and should only be used for logging or troubleshooting.</remarks>
		[DebuggerNonUserCode]
		public static string PrettyPrint(Slice key, PrettyPrintMode mode)
		{
			var span = key.Span;
			if (span.Length> 1)
			{
				byte c = span[0];
				//OPTIMIZE: maybe we need a lookup table
				if (c <= 28 || c == 32 || c == 33 || c == 48 || c == 49 || c >= 254)
				{ // it could be a tuple...
					try
					{
						SpanTuple tuple = default;
						string? suffix = null;
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
									switch (span[^1])
									{
										case 0xFF:
										{
											//***README*** if you break under here, see README in the last catch() block
											if (TuPack.TryUnpack(span[0..^1], out tuple))
											{
												suffix = ".<FF>";
											}
											break;
										}
										case 0x01:
										{
											var tmp = span.ToArray();
											tmp[^1] = 0;
											//***README*** if you break under here, see README in the last catch() block
											if (TuPack.TryUnpack(tmp, out tuple))
											{
												suffix = " + 1";
											}
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

									if (span.Length > 2 && span[^1] == 0 && span[^2] != 0xFF)
									{
										//***README*** if you break under here, see README in the last catch() block
										if (TuPack.TryUnpack(span[0..^1], out tuple))
										{
											suffix = ".<00>";
										}
									}
									break;
								}
							}
						}
						catch (Exception e)
						{
							suffix = null;
							skip = e is not (FormatException or ArgumentOutOfRangeException);
						}

						if (tuple.Count != 0)
						{
							return tuple.ToString() + suffix;
						}

						if (!skip)
						{ // attempt a regular decoding
							if (TuPack.TryUnpack(span, out tuple))
							{
								return tuple.ToString() + suffix;
							}
						}
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

		/// <summary>Produce a user-friendly version of the slice</summary>
		/// <param name="key">Random binary key</param>
		/// <returns>User friendly version of the key. Attempts to decode the key as a tuple first. Then as an ASCII string. Then as an hex dump of the key.</returns>
		/// <remarks>This can be slow, and should only be used for logging or troubleshooting.</remarks>
		public static string Dump(ReadOnlySpan<byte> key)
		{
			return PrettyPrint(key, PrettyPrintMode.Single);
		}

		/// <summary>Produce a user-friendly version of the slice</summary>
		/// <param name="key">Random binary key</param>
		/// <param name="mode">Defines if the key is standalone, or is the begin or end part or a key range. This will enable or disable some heuristics that try to properly format key ranges.</param>
		/// <returns>User friendly version of the key. Attempts to decode the key as a tuple first. Then as an ASCII string. Then as an hex dump of the key.</returns>
		/// <remarks>This can be slow, and should only be used for logging or troubleshooting.</remarks>
		[DebuggerNonUserCode]
		public static string PrettyPrint(ReadOnlySpan<byte> key, PrettyPrintMode mode)
		{
			if (key.Length > 1)
			{
				byte c = key[0];
				//OPTIMIZE: maybe we need a lookup table
				if (c <= 28 || c == 32 || c == 33 || c == 48 || c == 49 || c >= 254)
				{ // it could be a tuple...
					try
					{
						SpanTuple tuple = default;
						string? suffix = null;
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
									switch (key[^1])
									{
										case 0xFF:
										{
											//***README*** if you break under here, see README in the last catch() block
											tuple = TuPack.Unpack(key[..^1]);
											suffix = ".<FF>";
											break;
										}
										case 0x01:
										{
											//TODO: HACKHACK: until we find another solution, we have to make a copy :(
											var tmp = key.ToArray();
											tmp[^1] = 0;
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

									if (key.Length > 2 && key[-1] == 0 && key[-2] != 0xFF)
									{
										//***README*** if you break under here, see README in the last catch() block
										tuple = TuPack.Unpack(key[..^1]);
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

						if (tuple.Count == 0 && !skip)
						{ // attempt a regular decoding
							//***README*** if you break under here, see README in the last catch() block
							tuple = TuPack.Unpack(key);
						}

						if (tuple.Count != 0) return tuple.ToString() + suffix;
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

		#region Key Validation...

		/// <summary>Checks that a key is valid, and is inside the global key space of this database</summary>
		/// <param name="key">Key to verify</param>
		/// <param name="endExclusive">If true, the key is allowed to be one past the maximum key allowed by the global namespace</param>
		/// <param name="ignoreError">If true, don't return an exception in <paramref name="error"/>, even if the key is invalid.</param>
		/// <param name="error">Receive an exception object if the key is not valid and <paramref name="ignoreError"/> is false</param>
		/// <returns>Return <c>false</c> if the key is outside of the allowed key space of this database</returns>
		internal static bool ValidateKey(in Slice key, bool endExclusive, bool ignoreError, out Exception? error)
		{
			// null keys are not allowed
			if (key.IsNull)
			{
				error = ignoreError ? null : Fdb.Errors.KeyCannotBeNull();
				return false;
			}
			return ValidateKey(key.Span, endExclusive, ignoreError, out error);
		}

		/// <summary>Checks that a key is valid, and is inside the global key space of this database</summary>
		/// <param name="key">Key to verify</param>
		/// <param name="endExclusive">If true, the key is allowed to be one past the maximum key allowed by the global namespace</param>
		/// <param name="ignoreError"></param>
		/// <param name="error"></param>
		/// <returns>An exception if the key is outside of the allowed key space of this database</returns>
		internal static bool ValidateKey(ReadOnlySpan<byte> key, bool endExclusive, bool ignoreError, out Exception? error)
		{
			error = null;

			// key cannot be larger than maximum allowed key size
			if (key.Length > Fdb.MaxKeySize)
			{
				if (!ignoreError) error = Fdb.Errors.KeyIsTooBig(key);
				return false;
			}

			// special case for system keys
			if (IsSystemKey(key))
			{
				// note: it will fail later if the transaction does not have access to the system keys!
				return true;
			}

			return true;
		}

		/// <summary>Returns true if the key is inside the system key space (starts with '\xFF')</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsSystemKey(ReadOnlySpan<byte> key)
		{
			return key.Length != 0 && key[0] == 0xFF;
		}

		/// <summary>Checks that a key is inside the global namespace of this database, and contained in the optional legal key space specified by the user</summary>
		/// <param name="key">Key to verify</param>
		/// <param name="endExclusive">If true, the key is allowed to be one past the maximum key allowed by the global namespace</param>
		/// <exception cref="FdbException">If the key is outside of the allowed keyspace, throws an FdbException with code FdbError.KeyOutsideLegalRange</exception>
		public static void EnsureKeyIsValid(in Slice key, bool endExclusive = false)
		{
			if (!ValidateKey(key, endExclusive, false, out var ex))
			{
				throw ex!;
			}
		}

		/// <summary>Checks that a key is inside the global namespace of this database, and contained in the optional legal key space specified by the user</summary>
		/// <param name="key">Key to verify</param>
		/// <param name="endExclusive">If true, the key is allowed to be one past the maximum key allowed by the global namespace</param>
		/// <exception cref="FdbException">If the key is outside of the allowed keyspace, throws an FdbException with code FdbError.KeyOutsideLegalRange</exception>
		public static void EnsureKeyIsValid(ReadOnlySpan<byte> key, bool endExclusive = false)
		{
			if (!ValidateKey(key, endExclusive, false, out var ex)) throw ex!;
		}

		/// <summary>Checks that one or more keys are inside the global namespace of this database, and contained in the optional legal key space specified by the user</summary>
		/// <param name="keys">Array of keys to verify</param>
		/// <param name="endExclusive">If true, the keys are allowed to be one past the maximum key allowed by the global namespace</param>
		/// <exception cref="FdbException">If at least on key is outside of the allowed keyspace, throws an FdbException with code FdbError.KeyOutsideLegalRange</exception>
		public static void EnsureKeysAreValid(ReadOnlySpan<Slice> keys, bool endExclusive = false)
		{
			for (int i = 0; i < keys.Length; i++)
			{
				if (!ValidateKey(in keys[i], endExclusive, false, out var ex))
				{
					throw ex!;
				}
			}
		}

		/// <summary>Test if a key is allowed to be used with this database instance</summary>
		/// <param name="key">Key to test</param>
		/// <returns>Returns true if the key is not null or empty, does not exceed the maximum key size, and is contained in the global key space of this database instance. Otherwise, returns false.</returns>
		[Pure]
		public static bool IsKeyValid(Slice key)
		{
			return ValidateKey(key, false, true, out _);
		}

		/// <summary>Test if a key is allowed to be used with this database instance</summary>
		/// <param name="key">Key to test</param>
		/// <returns>Returns true if the key is not null or empty, does not exceed the maximum key size, and is contained in the global key space of this database instance. Otherwise, returns false.</returns>
		[Pure]
		public static bool IsKeyValid(ReadOnlySpan<byte> key)
		{
			return ValidateKey(key, false, true, out _);
		}

		#endregion

		#region Value Validation

		/// <summary>Ensures that a serialized value is valid</summary>
		/// <remarks>Throws an exception if the value is null, or exceeds the maximum allowed size (Fdb.MaxValueSize)</remarks>
		public static void EnsureValueIsValid(Slice value)
		{
			if (value.IsNull) throw Fdb.Errors.ValueCannotBeNull();
			EnsureValueIsValid(value.Span);
		}

		/// <summary>Ensures that a serialized value is valid</summary>
		/// <remarks>Throws an exception if the value is null, or exceeds the maximum allowed size (Fdb.MaxValueSize)</remarks>
		public static void EnsureValueIsValid(ReadOnlySpan<byte> value)
		{
			var ex = ValidateValue(value);
			if (ex != null) throw ex;
		}

		internal static Exception? ValidateValue(ReadOnlySpan<byte> value)
		{
			if (value.Length > Fdb.MaxValueSize)
			{
				return Fdb.Errors.ValueIsTooBig(value);
			}
			return null;
		}

		#endregion
	}

}

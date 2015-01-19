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

namespace FoundationDB.Layers.Blobs
{
	using FoundationDB.Client;
	using FoundationDB.Client.Utils;
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using System.Threading.Tasks;

	/// <summary>Represents a potentially large binary value in FoundationDB.</summary>
	[DebuggerDisplay("Subspace={Subspace}")]
	public class FdbBlob
	{
		private const long CHUNK_LARGE = 10000; // all chunks will be not greater than this size
		private const long CHUNK_SMALL = 200; // all adjacent chunks will sum to more than this size

		private static readonly Slice SizeSuffix = Slice.FromChar('S');
		private static readonly Slice AttributesSuffix = Slice.FromChar('A');
		private static readonly Slice DataSuffix = Slice.FromChar('D');

		/// <summary>
		/// Create a new object representing a binary large object (blob).
		/// Only keys within the subspace will be used by the object. 
		/// Other clients of the database should refrain from modifying the subspace.</summary>
		/// <param name="subspace">Subspace to be used for storing the blob data and metadata</param>
		public FdbBlob(FdbSubspace subspace)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Subspace = subspace;
		}

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		public FdbSubspace Subspace { get; private set; }

		/// <summary>Returns the key for data chunk at the specified offset</summary>
		/// <param name="offset"></param>
		/// <returns>123 => (subspace, 'D', "             123")</returns>
		protected virtual Slice DataKey(long offset)
		{
			//note: python code uses "%16d" % offset, which pads the value with spaces.. Not sure why ?
			return this.Subspace.Pack(DataSuffix, offset.ToString("D16", CultureInfo.InvariantCulture));
		}

		protected virtual long DataKeyOffset(Slice key)
		{
			long offset = Int64.Parse(this.Subspace.UnpackLast<string>(key), CultureInfo.InvariantCulture);
			if (offset < 0) throw new InvalidOperationException("Chunk offset value cannot be less than zero");
			return offset;
		}

		protected virtual Slice SizeKey()
		{
			return this.Subspace.Pack(SizeSuffix);
		}

		protected virtual Slice AttributeKey(string name)
		{
			return this.Subspace.Pack(AttributesSuffix, name);
		}

		#region Internal Helpers...

		private struct Chunk
		{
			public readonly Slice Key;
			public readonly Slice Data;
			public readonly long Offset;

			public Chunk(Slice key, Slice data, long offset)
			{
				this.Key = key;
				this.Data = data;
				this.Offset = offset;
			}
		}

		private async Task<Chunk> GetChunkAtAsync(IFdbTransaction trans, long offset)
		{
			Contract.Requires(trans != null && offset >= 0);

			var chunkKey = await trans.GetKeyAsync(FdbKeySelector.LastLessOrEqual(DataKey(offset))).ConfigureAwait(false);
			if (chunkKey.IsNull)
			{ // nothing before (sparse)
				return default(Chunk);
			}

			if (chunkKey < DataKey(0))
			{ // off beginning
				return default(Chunk);
			}

			long chunkOffset = DataKeyOffset(chunkKey);

			Slice chunkData = await trans.GetAsync(chunkKey).ConfigureAwait(false);

			if (chunkOffset + chunkData.Count <= offset)
			{ // in sparse region after chunk
				return default(Chunk);
			}

			return new Chunk(chunkKey, chunkData, chunkOffset);
		}

		private async Task MakeSplitPointAsync(IFdbTransaction trans, long offset)
		{
			Contract.Requires(trans != null && offset >= 0);

			var chunk = await GetChunkAtAsync(trans, offset).ConfigureAwait(false);
			if (chunk.Key == Slice.Nil) return; // already sparse
			if (chunk.Offset == offset) return; // already a split point

			int splitPoint;
			checked { splitPoint = (int)(offset - chunk.Offset); }
			trans.Set(DataKey(chunk.Offset), chunk.Data.Substring(0, splitPoint));
			trans.Set(DataKey(offset), chunk.Data.Substring(splitPoint));
		}

		private async Task MakeSparseAsync(IFdbTransaction trans, long start, long end)
		{
			await MakeSplitPointAsync(trans, start).ConfigureAwait(false);
			await MakeSplitPointAsync(trans, end).ConfigureAwait(false);
			trans.ClearRange(DataKey(start), DataKey(end));
		}

		private async Task<bool> TryRemoteSplitPointAsync(IFdbTransaction trans, long offset)
		{
			Contract.Requires(trans != null && offset >= 0);

			var b = await GetChunkAtAsync(trans, offset).ConfigureAwait(false);
			if (b.Offset == 0 || b.Key == Slice.Nil) return false; // in sparse region, or at beginning

			var a = await GetChunkAtAsync(trans, b.Offset - 1).ConfigureAwait(false);
			if (a.Key == Slice.Nil) return false; // no previous chunk

			if (a.Offset + a.Data.Count != b.Offset) return false; // chunks can't be joined
			if (a.Data.Count + b.Data.Count > CHUNK_SMALL) return false;  // chunks shouldn't be joined
			// yay--merge chunks
			trans.Clear(b.Key);
			trans.Set(a.Key, a.Data + b.Data);
			return true;
		}

		private void WriteToSparse(IFdbTransaction trans, long offset, Slice data)
		{
			Contract.Requires(trans != null && offset >= 0);

			if (data.IsNullOrEmpty) return;

			int chunks = (int)((data.Count + CHUNK_LARGE - 1) / CHUNK_LARGE);
			int chunkSize = (data.Count + chunks) / chunks;

			for (int n = 0; n < data.Count; n += chunkSize)
			{
				trans.Set(DataKey(offset + n), data[n, n + chunkSize]);
			}
		}

		private void SetSize(IFdbTransaction trans, long size)
		{
			Contract.Requires(trans != null && size >= 0);

			//note: python code converts the size into a string
			trans.Set(SizeKey(), Slice.FromString(size.ToString()));
		}

		#endregion

		/// <summary>
		/// Delete all key-value pairs associated with the blob.
		/// </summary>
		public void Delete(IFdbTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.ClearRange(this.Subspace);
		}

		/// <summary>
		/// Get the size (in bytes) of the blob.
		/// </summary>
		/// <returns>Return null if the blob does not exists, 0 if is empty, or the size in bytes</returns>
		public async Task<long?> GetSizeAsync(IFdbReadOnlyTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			Slice value = await trans.GetAsync(SizeKey()).ConfigureAwait(false);

			if (value.IsNullOrEmpty) return default(long?);

			//note: python code stores the size as a string
			long size = Int64.Parse(value.ToAscii());
			if (size < 0) throw new InvalidOperationException("The internal blob size cannot be negative");
			return size;
		}

		/// <summary>
		/// Read from the blob, starting at <paramref name="offset"/>, retrieving up to <paramref name="n"/> bytes (fewer then n bytes are returned when the end of the blob is reached).
		/// </summary>
		public async Task<Slice> ReadAsync(IFdbReadOnlyTransaction trans, long offset, int n)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (offset < 0) throw new ArgumentNullException("offset", "Offset cannot be less than zero");

			long? size = await GetSizeAsync(trans).ConfigureAwait(false);
			if (size == null) return Slice.Nil; // not found

			if (offset >= size.Value)
				return Slice.Empty;

			// read all chunks matching the segment we need, and copy them in our buffer
			var buffer = new byte[Math.Min(n, size.Value - offset)];

			await trans
				.GetRange(
					FdbKeySelector.LastLessOrEqual(DataKey(offset)),
					FdbKeySelector.FirstGreaterOrEqual(DataKey(offset + n))
				)
				.ForEachAsync((chunk) =>
				{
					// get offset of this chunk
					long chunkOffset = DataKeyOffset(chunk.Key);
					Slice chunkData = chunk.Value;

					checked
					{
						// intersect chunk bounds with output
						int delta = (int)(chunkOffset - offset);
						int start = delta;
						int end = delta + chunkData.Count;
						if (start < 0) start = 0;
						if (end > n) end = n;

						// compute the relative offsets in the chunk
						int rStart = start - delta;
						int rEnd = end - delta;

						var intersect = chunkData[rStart, rEnd];
						if (intersect.IsPresent)
						{ // copy the data that fits
							intersect.CopyTo(buffer, start);
						}
					}
				})
				.ConfigureAwait(false);

			return new Slice(buffer, 0, buffer.Length);
		}

		/// <summary>
		/// Write <paramref name="data"/> to the blob, starting at <param name="offset"/> and overwriting any existing data at that location. The length of the blob is increased if necessary.
		/// </summary>
		public async Task WriteAsync(IFdbTransaction trans, long offset, Slice data)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset cannot be less than zero");

			if (data.IsNullOrEmpty) return;

			long end = offset + data.Count;
			await MakeSparseAsync(trans, offset, end).ConfigureAwait(false);
			WriteToSparse(trans, offset, data);
			await TryRemoteSplitPointAsync(trans, offset).ConfigureAwait(false);

			long oldLength = (await GetSizeAsync(trans).ConfigureAwait(false)) ?? 0;
			if (end > oldLength)
			{ // lengthen file if necessary
				SetSize(trans, end);
			}
			else
			{ // write end needs to be merged
				await TryRemoteSplitPointAsync(trans, end).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Append the contents of <paramref name="data"/> onto the end of the blob.
		/// </summary>
		public async Task AppendAsync(IFdbTransaction trans, Slice data)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			if (data.IsNullOrEmpty) return;

			long oldLength = (await GetSizeAsync(trans).ConfigureAwait(false)) ?? 0;
			WriteToSparse(trans, oldLength, data);
			await TryRemoteSplitPointAsync(trans, oldLength).ConfigureAwait(false);
			SetSize(trans, oldLength + data.Count);
		}

		/// <summary>
		/// Change the blob length to <paramref name="newLength"/>, erasing any data when shrinking, and filling new bytes with 0 when growing.
		/// </summary>
		public async Task TruncateAsync(IFdbTransaction trans, long newLength)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (newLength < 0) throw new ArgumentOutOfRangeException("newLength", "Length cannot be less than zero");

			long? length = await GetSizeAsync(trans).ConfigureAwait(false);
			if (length != null)
			{
				await MakeSparseAsync(trans, newLength, length.Value).ConfigureAwait(false);
			}
			SetSize(trans, newLength);
		}

	}

}

﻿#region BSD Licence
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
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Threading;
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

		public FdbBlob(IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			this.Subspace = new FdbSubspace(tuple);
		}

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		public FdbSubspace Subspace { get; private set; }

		/// <summary>Returns the key for data chunk at the specified offset</summary>
		/// <param name="offset"></param>
		/// <returns>123 => (subspace, 'D', "             123")</returns>
		private Slice DataKey(long offset)
		{
			//note: python code uses "%16d" % offset, which pads the value with spaces.. Not sure why ?
			return this.Subspace.Pack(DataSuffix, offset.ToString("D16", CultureInfo.InvariantCulture));
		}

		private long DataKeyOffset(Slice key)
		{
			//TODO: check that the tuple is prefixed by (subspace, 'D',) ?
			long offset = Int64.Parse(this.Subspace.UnpackLast<string>(key), CultureInfo.InvariantCulture);
			if (offset < 0) throw new InvalidOperationException("Chunk offset value cannot be less than zero");
			return offset;
		}

		private Slice SizeKey()
		{
			return this.Subspace.Pack(SizeSuffix);
		}

		private Slice AttributeKey(string name)
		{
			return this.Subspace.Pack(AttributesSuffix, name);
		}

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
			if (!chunkKey.HasValue)
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

		private async Task<bool> TryRemoteSplitPoint(IFdbTransaction trans, long offset)
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


		public void Delete(IFdbTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.ClearRange(this.Subspace);
		}

		public async Task<long?> GetSizeAsync(IFdbTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			Slice value = await trans.GetAsync(SizeKey()).ConfigureAwait(false);

			if (value.IsNullOrEmpty) return default(long?);

			//note: python code stores the size as a string
			return Int64.Parse(value.ToUnicode());
		}

		/// <summary>
		/// Read from the blob, starting at <paramref name="offset"/>, retrieving up to <paramref name="n"/> bytes (fewer then n bytes are returned when the end of the blob is reached).
		/// </summary>
		/// <param name="trans"></param>
		/// <param name="offset"></param>
		/// <param name="n"></param>
		/// <returns></returns>
		public async Task<Slice> ReadAsync(IFdbTransaction trans, long offset, int n)
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
						if (intersect.Count > 0)
						{ // copy the data that fits
							intersect.CopyTo(buffer, start);
						}
					}
				})
				.ConfigureAwait(false);

			return new Slice(buffer, 0, buffer.Length);
		}

		/// <summary>
		/// Write data to the blob, starting at offset and overwriting any existing data at that location. The length of the blob is increased if necessary.
		/// </summary>
		public async Task WriteAsync(IFdbTransaction trans, long offset, Slice data)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset cannot be less than zero");

			if (data.IsNullOrEmpty) return;

			long end = offset + data.Count;
			await MakeSparseAsync(trans, offset, end).ConfigureAwait(false);
			WriteToSparse(trans, offset, data);
			await TryRemoteSplitPoint(trans, offset).ConfigureAwait(false);

			long oldLength = (await GetSizeAsync(trans).ConfigureAwait(false)) ?? 0;
			if (end > oldLength)
				SetSize(trans, end); // lengthen file if necessary
			else
				await TryRemoteSplitPoint(trans, end).ConfigureAwait(false); // write end needs to be merged
		}

		/// <summary>
		/// Append the contents of data onto the end of the blob.
		/// </summary>
		public async Task AppendAsync(IFdbTransaction trans, Slice data)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			if (data.IsNullOrEmpty) return;

			long oldLength = (await GetSizeAsync(trans).ConfigureAwait(false)) ?? 0;
			WriteToSparse(trans, oldLength, data);
			await TryRemoteSplitPoint(trans, oldLength).ConfigureAwait(false);
			SetSize(trans, oldLength + data.Count);
		}

		/// <summary>
		/// Change the blob length to new_length, erasing any data when shrinking, and filling new bytes with 0 when growing.
		/// </summary>
		public async Task Truncate(IFdbTransaction trans, long newLength)
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

		/// <summary>Clear the blob's content (without loosing the attributes)</summary>
		/// <param name="trans"></param>
		/// <returns></returns>
		public void Clear(IFdbTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.ClearRange(FdbKeyRange.StartsWith(this.Subspace.Pack(DataSuffix)));
			SetSize(trans, 0);
		}

		/// <summary>
		/// Sets the value of an attribute of this blob
		/// </summary>
		public void SetAttribute(IFdbTransaction trans, string name, Slice value)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

			if (value.HasValue)
			{
				trans.Set(AttributeKey(name), value);
			}
			else
			{
				trans.Clear(AttributeKey(name));
			}
		}

		/// <summary>
		/// Returns the value of an attribute of this blob
		/// </summary>
		public Task<Slice> GetAttributeAsync(IFdbTransaction trans, string name)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

			return trans.GetAsync(AttributeKey(name));
		}

		/// <summary>
		/// Returns the list of all known attributes for this blob
		/// </summary>
		public Task<List<KeyValuePair<string, Slice>>> GetAllAttributesAsync(IFdbTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			return trans
				.GetRangeStartsWith(this.Subspace.Pack(AttributesSuffix))
				.Select(
					(key) => FdbTuple.UnpackLast<string>(key),
					(value) => value
				)
				.ToListAsync();
		}

		/// <summary>
		/// Read the entire content of the blob into a slice
		/// </summary>
		/// <returns></returns>
		/// <remarks>Warning, do not call this if you know that the blob can be very large, because it will need to feed in memory, and don't exceed 2GB</remarks>
		public async Task<Slice> ReadToEndAsync(IFdbTransaction trans)
		{
			long? length = await GetSizeAsync(trans).ConfigureAwait(false);

			if (length == null) return Slice.Nil;
			if (length.Value == 0) return Slice.Empty;
			if (length.Value > int.MaxValue) throw new InvalidOperationException(String.Format("Cannot read blob of size {0} because it is over 2 GB", length.Value));

			return await ReadAsync(trans, 0, (int)length.Value);
		}

		public async Task<Stream> DownloadAsync(IFdbTransaction trans, CancellationToken ct = default(CancellationToken))
		{
			if (trans == null) throw new ArgumentNullException("trans");

			ct.ThrowIfCancellationRequested();

			long? length = await GetSizeAsync(trans).ConfigureAwait(false);

			if (length == null) return Stream.Null;

			if (length.Value > int.MaxValue) throw new InvalidOperationException("Cannot download blobs of more than GB");

			var ms = new MemoryStream((int)length.Value);

			await trans
				.GetRangeStartsWith(this.Subspace.Pack(DataSuffix))
				.ForEachAsync((chunk) =>
				{
					long offset = DataKeyOffset(chunk.Key);
					ms.Seek(offset, SeekOrigin.Begin);
					ms.Write(chunk.Value.Array, chunk.Value.Offset, chunk.Value.Count);
				}, ct)
				.ConfigureAwait(false);

			ms.Seek(0, SeekOrigin.Begin);
			return ms;
		}

		public async Task Upload(IFdbTransaction trans, Slice data)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (!data.HasValue) throw new ArgumentNullException("data");

			await Truncate(trans, 0).ConfigureAwait(false);
			await AppendAsync(trans, data).ConfigureAwait(false);
		}

		public async Task UploadAsync(IFdbTransaction trans, Stream stream, CancellationToken ct = default(CancellationToken))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (stream == null) throw new ArgumentNullException("stream");
			if (!stream.CanRead) throw new InvalidOperationException("Cannot read from the stream");

			ct.ThrowIfCancellationRequested();

			Clear(trans);

			if (stream != Stream.Null)
			{
				await AppendStreamAsync(trans, stream, ct).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Append the content of a stream at the end of the blob
		/// </summary>
		public async Task AppendStreamAsync(IFdbTransaction trans, Stream stream, CancellationToken ct = default(CancellationToken))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (stream == null) throw new ArgumentNullException("stream");
			if (!stream.CanRead) throw new InvalidOperationException("Cannot read from the stream");

			ct.ThrowIfCancellationRequested();

			long length = stream.Length;
			if (length >= 0)
			{

				byte[] buffer = new byte[Math.Min(length, CHUNK_LARGE)];

				//TODO: find a way to detect large spans of zeros, and create a sparse blob ?

				long read = 0;
				while (true)
				{
					int n = await stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
					if (n == 0) break;

					ct.ThrowIfCancellationRequested();
					await AppendAsync(trans, new Slice(buffer, 0, n)).ConfigureAwait(false);

					read += n;
				}
			}

		}

	}

}

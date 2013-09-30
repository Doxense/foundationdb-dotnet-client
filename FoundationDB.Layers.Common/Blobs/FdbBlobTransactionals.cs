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
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Transactional methods for the FdbBlob class
	/// </summary>
	public static class FdbBlobTransactionals
	{

		/// <summary>
		/// Delete all key-value pairs associated with the blob.
		/// </summary>
		public static Task DeleteAsync(this FdbBlob blob, IFdbTransactional dbOrTrans, CancellationToken ct = default(CancellationToken))
		{
			if (blob == null) throw new ArgumentNullException("blob");
			if (dbOrTrans == null) throw new ArgumentNullException("dbOrTrans");

			return dbOrTrans.WriteAsync((tr) => blob.Delete(tr), ct);
		}

		/// <summary>
		/// Get the size (in bytes) of the blob.
		/// </summary>
		public static Task<long?> GetSizeAsync(this FdbBlob blob, IFdbReadOnlyTransactional dbOrTrans, CancellationToken ct = default(CancellationToken))
		{
			if (blob == null) throw new ArgumentNullException("blob");
			if (dbOrTrans == null) throw new ArgumentNullException("dbOrTrans");

			return dbOrTrans.ReadAsync((tr) => blob.GetSizeAsync(tr), ct);
		}

		/// <summary>
		/// Read from the blob, starting at <paramref name="offset"/>, retrieving up to <paramref name="n"/> bytes (fewer then n bytes are returned when the end of the blob is reached).
		/// </summary>
		public static Task<Slice> ReadAsync(this FdbBlob blob, IFdbReadOnlyTransactional dbOrTrans, long offset, int n, CancellationToken ct = default(CancellationToken))
		{
			if (blob == null) throw new ArgumentNullException("blob");
			if (dbOrTrans == null) throw new ArgumentNullException("dbOrTrans");

			return dbOrTrans.ReadAsync((tr) => blob.ReadAsync(tr, offset, n), ct);
		}

		/// <summary>
		/// Write <paramref name="data"/> to the blob, starting at <param name="offset"/> and overwriting any existing data at that location. The length of the blob is increased if necessary.
		/// </summary>
		public static Task WriteAsync(this FdbBlob blob, IFdbTransactional dbOrTrans, long offset, Slice data, CancellationToken ct = default(CancellationToken))
		{
			if (blob == null) throw new ArgumentNullException("blob");
			if (dbOrTrans == null) throw new ArgumentNullException("dbOrTrans");

			return dbOrTrans.ReadWriteAsync((tr) => blob.WriteAsync(tr, offset, data), ct);
		}

		/// <summary>
		/// Append the contents of <paramref name="data"/> onto the end of the blob.
		/// </summary>
		public static Task AppendAsync(this FdbBlob blob, IFdbTransactional dbOrTrans, Slice data, CancellationToken ct = default(CancellationToken))
		{
			if (blob == null) throw new ArgumentNullException("blob");
			if (dbOrTrans == null) throw new ArgumentNullException("dbOrTrans");

			return dbOrTrans.ReadWriteAsync((tr) => blob.AppendAsync(tr, data), ct);
		}

		/// <summary>
		/// Change the blob length to <paramref name="newLength"/>, erasing any data when shrinking, and filling new bytes with 0 when growing.
		/// </summary>
		public static Task TruncateAsync(this FdbBlob blob, IFdbTransactional dbOrTrans, long newLength, CancellationToken ct = default(CancellationToken))
		{
			if (blob == null) throw new ArgumentNullException("blob");
			if (dbOrTrans == null) throw new ArgumentNullException("dbOrTrans");

			return dbOrTrans.ReadWriteAsync((tr) => blob.TruncateAsync(tr, newLength), ct);
		}
	}

}

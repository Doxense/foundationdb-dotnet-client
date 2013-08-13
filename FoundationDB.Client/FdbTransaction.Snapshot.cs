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
	using System;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Wraps an FDB_TRANSACTION handle</summary>
	public partial class FdbTransaction
	{

		/// <summary>Snapshot version of this transaction (lazily allocated)</summary>
		private Snapshotted m_snapshotted;

		/// <summary>Returns a version of this transaction that perform snapshotted operations</summary>
		public IFdbReadTransaction Snapshot
		{
			get
			{
				EnsureNotFailedOrDisposed();
				return m_snapshotted ?? (m_snapshotted = new Snapshotted(this));
			}
		}

		/// <summary>Wrapper on a transaction, that will use Snmapshot mode on all read operations</summary>
		private sealed class Snapshotted : IFdbReadTransaction, IDisposable
		{
			private readonly FdbTransaction m_parent;

			public Snapshotted(FdbTransaction parent)
			{
				if (parent == null) throw new ArgumentNullException("parent");
				m_parent = parent;
			}

			public int Id
			{
				get { return m_parent.Id; }
			}

			public int Size
			{
				get { return m_parent.Size; }
			}

			public CancellationToken Token
			{
				get { return m_parent.Token; }
			}

			public bool IsSnapshot
			{
				get { return true; }
			}

			public void EnsureCanRead(CancellationToken ct)
			{
				m_parent.EnsureCanRead(ct);
			}

			public void Reset()
			{
				m_parent.Reset();
			}

			public Task CommitAsync(CancellationToken ct)
			{
				return m_parent.CommitAsync(ct);
			}

			public Task<long> GetReadVersionAsync(CancellationToken ct)
			{
				return m_parent.GetReadVersionAsync(ct);
			}

			public void SetReadVersion(long version)
			{
				m_parent.SetReadVersion(version);
			}

			public long GetCommittedVersion()
			{
				return m_parent.GetCommittedVersion();
			}

			public Task<Slice> GetAsync(Slice keyBytes, CancellationToken ct)
			{
				EnsureCanRead(ct);

				return m_parent.GetCoreAsync(keyBytes, snapshot: true, ct: ct);
			}

			public Task<Slice[]> GetBatchValuesAsync(Slice[] keys, CancellationToken ct)
			{
				if (keys == null) throw new ArgumentNullException("keys");

				EnsureCanRead(ct);

				return m_parent.GetBatchValuesCoreAsync(keys, snapshot: true, ct: ct);
			}

			public Task<Slice> GetKeyAsync(FdbKeySelector selector, CancellationToken ct)
			{
				EnsureCanRead(ct);

				return m_parent.GetKeyCoreAsync(selector, snapshot: true, ct: ct);
			}

			public Task<FdbRangeChunk> GetRangeAsync(FdbKeySelectorPair range, FdbRangeOptions options, int iteration, CancellationToken ct)
			{
				EnsureCanRead(ct);

				return m_parent.GetRangeCoreAsync(range, options, iteration, snapshot: true, ct: ct);
			}

			public FdbRangeQuery GetRange(FdbKeySelector beginInclusive, FdbKeySelector endExclusive, FdbRangeOptions options)
			{
				EnsureCanRead(CancellationToken.None);

				return m_parent.GetRangeCore(FdbKeySelectorPair.Create(beginInclusive, endExclusive), options, snapshot: true);
			}

			public FdbRangeQuery GetRange(FdbKeySelectorPair range, FdbRangeOptions options)
			{
				EnsureCanRead(CancellationToken.None);

				return m_parent.GetRangeCore(range, options, snapshot: true);
			}

			public FdbRangeQuery GetRangeStartsWith(Slice prefix, FdbRangeOptions options)
			{
				if (!prefix.HasValue) throw new ArgumentOutOfRangeException("prefix");

				EnsureCanRead(CancellationToken.None);

				return m_parent.GetRangeCore(FdbKeySelectorPair.StartsWith(prefix), options, snapshot: true);
			}

			public void AddConflictRange(FdbKeyRange range, FdbConflictRangeType type)
			{
				EnsureCanRead(CancellationToken.None);

				m_parent.AddConflictRangeCore(range.Begin, range.End, type);
			}

			public Task OnErrorAsync(FdbError code, CancellationToken ct)
			{
				return m_parent.OnErrorAsync(code, ct);
			}

			void IDisposable.Dispose()
			{
				// NO-OP
			}
		}

	}

}

#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core
{
	using FoundationDB.Client;
	using FoundationDB.Storage.Memory.Utils;
	using System;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;

	[DebuggerDisplay("Sarted={m_startedUtc}, Min={m_minVersion}, Max={m_maxVersion}, Closed={m_closed}, Disposed={m_disposed}")]
	internal sealed class TransactionWindow : IDisposable
	{
		/// <summary>Creation date of this transaction window</summary>
		private readonly DateTime m_startedUtc;
		/// <summary>First commit version for this transaction window</summary>
		private readonly ulong m_minVersion;
		/// <summary>Sequence of the last commited transaction from this window</summary>
		private ulong m_maxVersion;
		/// <summary>Counter for committed write transactions</summary>
		private int m_writeCount;
		/// <summary>If true, the transaction is closed (no more transaction can write to it)</summary>
		private bool m_closed;
		/// <summary>If true, the transaction has been disposed</summary>
		private volatile bool m_disposed;

		/// <summary>Heap used to store the write conflict keys</summary>
		private UnmanagedMemoryHeap m_keys = new UnmanagedMemoryHeap(65536);

		/// <summary>List of all the writes made by transactions committed in this window</summary>
		private ColaRangeDictionary<USlice, ulong> m_writeConflicts = new ColaRangeDictionary<USlice, ulong>(USliceComparer.Default, SequenceComparer.Default);

		public TransactionWindow(DateTime startedUtc, ulong version)
		{
			m_startedUtc = startedUtc;
			m_minVersion = version;
		}

		public bool Closed { get { return m_closed; } }

		public ulong FirstVersion { get { return m_minVersion; } }

		public ulong LastVersion { get { return m_maxVersion; } }

		public DateTime StartedUtc { get { return m_startedUtc; } }

		/// <summary>Number of write transaction that committed during this window</summary>
		public int CommitCount { get { return m_writeCount; } }

		public ColaRangeDictionary<USlice, ulong> Writes { get { return m_writeConflicts; } }

		public void Close()
		{
			Contract.Requires(!m_closed && !m_disposed);

			if (m_disposed) ThrowDisposed();

			m_closed = true;
		}

		private unsafe USlice Store(Slice data)
		{
			uint size = checked((uint)data.Count);
			var buffer = m_keys.AllocateAligned(size);
			UnmanagedHelpers.CopyUnsafe(buffer, data);
			return new USlice(buffer, size);
		}

		public void MergeWrites(ColaRangeSet<Slice> writes, ulong version)
		{
			Contract.Requires(!m_closed && writes != null && version >= m_minVersion && (!m_closed || version <= m_maxVersion));

			if (m_disposed) ThrowDisposed();
			if (m_closed) throw new InvalidOperationException("This transaction has already been closed");

			//Debug.WriteLine("* Merging writes conflicts for version " + version + ": " + String.Join(", ", writes));

			foreach (var range in writes)
			{
				var begin = range.Begin;
				var end = range.End;

				USlice beginKey, endKey;
				if (begin.Offset == end.Offset && object.ReferenceEquals(begin.Array, end.Array) && end.Count >= begin.Count)
				{ // overlapping keys
					endKey = Store(end);
					beginKey = endKey.Substring(0, (uint)begin.Count);
				}
				else
				{
					beginKey = Store(begin);
					endKey = Store(end);
				}

				m_writeConflicts.Mark(beginKey, endKey, version);
			}

			++m_writeCount;
			if (version > m_maxVersion)
			{
				m_maxVersion = version;
			}
		}

		/// <summary>Checks if a list of reads conflicts with at least one write performed in this transaction window</summary>
		/// <param name="reads">List of reads to check for conflicts</param>
		/// <param name="version">Sequence number of the transaction that performed the reads</param>
		/// <returns>True if at least one read is conflicting with a write with a higher sequence number; otherwise, false.<returns>
		public bool Conflicts(ColaRangeSet<Slice> reads, ulong version)
		{
			Contract.Requires(reads != null);

			//Debug.WriteLine("* Testing for conflicts for: " + String.Join(", ", reads));

			if (version > m_maxVersion)
			{ // all the writes are before the reads, so no possible conflict!
				//Debug.WriteLine(" > cannot conflict");
				return false;
			}

			using (var scratch = new UnmanagedSliceBuilder())
			{
				//TODO: do a single-pass version of intersection checking !
				foreach (var read in reads)
				{
					scratch.Clear();
					scratch.Append(read.Begin);
					var p = scratch.Count;
					scratch.Append(read.End);
					var begin = scratch.ToUSlice(p);
					var end = scratch.ToUSlice(p, scratch.Count - p);

					if (m_writeConflicts.Intersect(begin, end, version, (v, min) => v > min))
					{
						Debug.WriteLine(" > Conflicting read: " + read);
						return true;
					}
				}
			}

			//Debug.WriteLine("  > No conflicts found");
			return false;
		}
		
		private void ThrowDisposed()
		{
			throw new ObjectDisposedException(this.GetType().Name);
		}

		public void Dispose()
		{
			if (!m_disposed)
			{
				m_disposed = true;
				m_keys.Dispose();

			}
			GC.SuppressFinalize(this);
		}

		public override string ToString()
		{
			return String.Format(System.Globalization.CultureInfo.InvariantCulture, "#{0} [{1}~{2}]", m_startedUtc.Ticks / TimeSpan.TicksPerMillisecond, m_minVersion, m_maxVersion);
		}
	}

}

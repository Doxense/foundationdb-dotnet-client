#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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

// enable this to capture the stacktrace of the ctor, when troubleshooting leaked database handles
#undef CAPTURE_STACKTRACES

namespace FoundationDB.Client.Native
{
	using FoundationDB.Client.Core;
	using System;
	using System.Diagnostics;

	/// <summary>Wraps a native FDBDatabase* handle</summary>
	[DebuggerDisplay("Handle={m_handle}, Closed={m_handle.IsClosed}")]
	internal sealed class FdbNativeDatabase : IFdbDatabaseHandler, IDisposable
	{
		/// <summary>Handle that wraps the native FDB_DATABASE*</summary>
		private readonly DatabaseHandle m_handle;

#if CAPTURE_STACKTRACES
		private readonly StackTrace m_stackTrace;
#endif

		public FdbNativeDatabase(DatabaseHandle handle)
		{
			if (handle == null) throw new ArgumentNullException("handle");

			m_handle = handle;
#if CAPTURE_STACKTRACES
			m_stackTrace = new StackTrace();
#endif
		}

		//REVIEW: do we really need a destructor ? The handle is a SafeHandle, and will take care of itself...
		~FdbNativeDatabase()
		{
#if CAPTURE_STACKTRACES
			Trace.WriteLine("A database handle (" + m_handle + ") was leaked by " + m_stackTrace);
#endif
#if DEBUG
			// If you break here, that means that a native database handler was leaked by a FdbDatabase instance (or that the database instance was leaked)
			if (Debugger.IsAttached) Debugger.Break();
#endif
			Dispose(false);
		}


		public bool IsInvalid { get { return m_handle.IsInvalid; } }

		public bool IsClosed { get { return m_handle.IsClosed; } }

		public void SetOption(FdbDatabaseOption option, Slice data)
		{
			Fdb.EnsureNotOnNetworkThread();

			unsafe
			{
				if (data.IsNull)
				{
					Fdb.DieOnError(FdbNative.DatabaseSetOption(m_handle, option, null, 0));
				}
				else
				{
					fixed (byte* ptr = data.Array)
					{
						Fdb.DieOnError(FdbNative.DatabaseSetOption(m_handle, option, ptr + data.Offset, data.Count));
					}
				}
			}
		}

		public IFdbTransactionHandler CreateTransaction(FdbOperationContext context)
		{
			TransactionHandle handle = null;
			try
			{
				var err = FdbNative.DatabaseCreateTransaction(m_handle, out handle);
				if (Fdb.Failed(err))
				{
					throw Fdb.MapToException(err);
				}
				return new FdbNativeTransaction(this, handle);
			}
			catch(Exception)
			{
				if (handle != null) handle.Dispose();
				throw;
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (m_handle != null) m_handle.Dispose();
			}
		}
	}

}

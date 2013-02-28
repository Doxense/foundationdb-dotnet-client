using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Data.FoundationDb.Client
{

	public delegate void FdbFutureCallback(IntPtr future, IntPtr parameter);

	public class FdbFuture<T> : IDisposable
	{

		/// <summary>Value of the 'FDBFuture*'</summary>
		private FutureHandle m_handle;
		private readonly TaskCompletionSource<T> m_tcs = new TaskCompletionSource<T>();
		private Func<FutureHandle, T> m_resultSelector;

		internal FdbFuture(FutureHandle handle, Func<FutureHandle, T> selector)
		{
			m_handle = handle;

			if (!handle.IsInvalid)
			{
				// already completed or failed ?
				if (FdbNativeStub.FutureIsReady(handle.Handle))
				{ // either got a value or an error
					Console.WriteLine("Future 0x" + handle.Handle.ToString("x") + " was already complete");
					SetTaskResult(handle, selector);
				}
				else
				{ // we don't know yet, schedule a callback...
					Console.WriteLine("Future 0x" + handle.Handle.ToString("x") + " will complete later");
					m_resultSelector = selector;
					FdbNativeStub.FutureSetCallback(handle.Handle, new FdbFutureCallback(this.CallbackHandler), IntPtr.Zero);
				}
			}
		}

		internal static FdbFuture<T> FromHandle(FutureHandle handle, Func<FutureHandle, T> selector)
		{
			if (selector == null) throw new ArgumentNullException("selector");

			return new FdbFuture<T>(handle, handle.IsInvalid ? null : selector);
		}

		private unsafe void SetTaskResult(FutureHandle handle, Func<FutureHandle, T> selector)
		{
			try
			{
				if (FdbNativeStub.FutureIsError(handle.Handle))
				{
					var err = FdbNativeStub.FutureGetError(handle.Handle);

					if (FdbCore.Failed(err))
					{
						m_tcs.TrySetException(FdbCore.MapToException(err));
					}
					else
					{  // doh?
						m_tcs.TrySetCanceled();
					}
				}
				else
				{
					var result = selector(handle);
					m_tcs.TrySetResult(result);
				}
			}
			catch (Exception e)
			{
				m_tcs.TrySetException(e);
			}
			finally
			{
				handle.Dispose();
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (!m_handle.IsClosed) m_handle.Dispose();
			}
		}

		private void CallbackHandler(IntPtr futureHandle, IntPtr parameter)
		{
			//TODO verify if this is our handle ?

			Console.WriteLine("Future 0x" + futureHandle.ToString("x") + " has fired");

			var selector = m_resultSelector;
			if (selector != null)
			{
				try
				{
					SetTaskResult(m_handle, selector);
				}
				finally
				{
					m_resultSelector = null;
				}
			}
		}

		public Task<T> Task
		{
			get { return m_tcs.Task; }
		}

		public TaskAwaiter<T> GetAwaiter()
		{
			return m_tcs.Task.GetAwaiter();
		}

		public bool IsReady
		{
			get
			{
				return !m_handle.IsInvalid && FdbNativeStub.FutureIsReady(m_handle.Handle);
			}
		}

		public bool IsError
		{
			get
			{
				return m_handle.IsInvalid || FdbNativeStub.FutureIsError(m_handle.Handle);
			}
		}

	}

}

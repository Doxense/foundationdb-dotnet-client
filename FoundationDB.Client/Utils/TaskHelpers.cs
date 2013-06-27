using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDB.Client.Utils
{
	/// <summary>Helper methods to work on tasks</summary>
	public static class TaskHelpers
	{

		/// <summary>Safely cancel and dispose a CancellationTokenSource</summary>
		/// <param name="source">CancellationTokenSource that needs to be cancelled and disposed</param>
		public static void SafeCancelAndDispose(this CancellationTokenSource source)
		{
			if (source != null)
			{
				try
				{
					source.Cancel();
				}
				catch (ObjectDisposedException) { }
				finally
				{
					source.Dispose();
				}
			}
		}

		/// <summary>Safely cancel and dispose a CancellationTokenSource, executing the registered callbacks on the thread pool</summary>
		/// <param name="source">CancellationTokenSource that needs to be cancelled and disposed</param>
		public static void SafeCancelAndDisposeDefered(this CancellationTokenSource source)
		{
			if (source != null)
			{
				ThreadPool.QueueUserWorkItem((state) => { SafeCancelAndDispose((CancellationTokenSource)state); }, source);
			}
		}


	}
}

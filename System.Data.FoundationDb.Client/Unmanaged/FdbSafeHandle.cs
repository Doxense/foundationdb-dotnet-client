using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Data.FoundationDb.Client
{
	internal abstract class FdbSafeHandle : CriticalHandle
	{
		protected FdbSafeHandle()
			: base(IntPtr.Zero)
		{ }

		public override bool IsInvalid
		{
			[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
			get { return this.handle == IntPtr.Zero; }
		}

		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		protected override bool ReleaseHandle()
		{
			if (handle != IntPtr.Zero)
			{
				try
				{
#if DEBUG
					Console.WriteLine("[Destroying handle 0x" + handle.ToString("x") + "]");
#endif
					Destroy(handle);
				}
				catch
				{ // TODO??
					return false;
				}
			}
			return true;
		}

		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		internal bool TrySetHandle(IntPtr handle)
		{
			SetHandle(handle);
			return handle != IntPtr.Zero;
		}

		internal IntPtr Handle
		{
			[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
			get { return this.handle; }
		}

		protected abstract void Destroy(IntPtr handle);
	}

}

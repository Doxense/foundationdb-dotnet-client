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
	internal class SafeAnsiStringHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		public static SafeAnsiStringHandle Empty { get { return new SafeAnsiStringHandle(IntPtr.Zero, true); } }

		public SafeAnsiStringHandle(IntPtr handle, bool ownsHandle)
			: base(ownsHandle)
		{
			if (handle == IntPtr.Zero)
				SetHandleAsInvalid();
			else
				SetHandle(handle);
		}

		public SafeAnsiStringHandle(string value)
			: base(true)
		{
			var ptr = IntPtr.Zero;
			try
			{
				ptr = value != null ? Marshal.StringToHGlobalAuto(value) : IntPtr.Zero;
				SetHandle(ptr);
			}
			catch (Exception)
			{
				if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
				SetHandleAsInvalid();
			}
		}

		public static SafeAnsiStringHandle FromString(string value)
		{
			if (value == null) return SafeAnsiStringHandle.Empty;
			return new SafeAnsiStringHandle(value);
		}

		protected override bool ReleaseHandle()
		{
			if (this.handle != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(this.handle);
			}
			return true;
		}
	}

}

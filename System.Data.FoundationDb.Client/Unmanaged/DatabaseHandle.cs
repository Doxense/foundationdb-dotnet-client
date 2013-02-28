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

	internal class DatabaseHandle : FdbSafeHandle
	{
		public DatabaseHandle()
			: base()
		{ }

		protected override void Destroy(IntPtr handle)
		{
			FdbNativeStub.DatabaseDestroy(handle);
		}
	}

}

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

	internal class ClusterHandle : FdbSafeHandle
	{
		public ClusterHandle()
			: base()
		{ }

		protected override void Destroy(IntPtr handle)
		{
			FdbNativeStub.ClusterDestroy(handle);
		}
	}

}

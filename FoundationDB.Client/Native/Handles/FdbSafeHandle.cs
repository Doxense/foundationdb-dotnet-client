#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

//#define DEBUG_HANDLES

namespace FoundationDB.Client.Native
{
	using System;
	using System.Runtime.ConstrainedExecution;
	using System.Runtime.InteropServices;

	/// <summary>Base class for all wrappers on FDBxxxx* opaque pointers</summary>
	public abstract class FdbSafeHandle : CriticalHandle
	{
		protected FdbSafeHandle()
			: base(IntPtr.Zero)
		{ }

		public override bool IsInvalid
		{
			[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
			get => this.handle == IntPtr.Zero;
		}

		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		protected override bool ReleaseHandle()
		{
			if (handle != IntPtr.Zero)
			{
				try
				{
					Destroy(handle);
#if DEBUG_HANDLES
					Debug.WriteLine("> [Destroyed " + this.GetType().Name + " 0x" + handle.ToString("x") + "]");
#endif
				}
				catch
				{ // TODO??
					return false;
				}
			}
			return true;
		}

		/// <summary>Return the value of the FDBFuture handle, for logging purpose only</summary>
		internal IntPtr Handle
		{
			[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
			get => this.handle;
		}

		/// <summary>Call the appropriate fdb_*_destroy(..)</summary>
		/// <param name="handle">Handle on the FDBFuture</param>
		protected abstract void Destroy(IntPtr handle);
	}

}

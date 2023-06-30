#region BSD License
/* Copyright (c) 2005-2023 Doxense SAS
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

namespace FoundationDB.Client.Native
{
	using System;
	using System.Runtime.ConstrainedExecution;
	using System.Runtime.InteropServices;
	using System.Security;
	using Doxense.Diagnostics.Contracts;
	using Microsoft.Win32.SafeHandles;

	/// <summary>Native Library Loader</summary>
	internal sealed class UnmanagedLibrary : IDisposable
	{
		// See http://msdn.microsoft.com/msdnmag/issues/05/10/Reliability/ for more about safe handles.

#if __MonoCS__
		[SuppressUnmanagedCodeSecurity]
		public sealed class SafeLibraryHandle : FdbSafeHandle
		{

			protected override void Destroy(IntPtr handle)
			{
				//cf Issue #49: it is too dangerous to unload the library because callbacks could still fire from the native side
				//DISABLED: NativeMethods.FreeLibrary(handle);
			}
		}
#else
		[SuppressUnmanagedCodeSecurity]
		public sealed class SafeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
		{
			private SafeLibraryHandle() : base(true) { }

			protected override bool ReleaseHandle()
			{
				//cf Issue #49: it is too dangerous to unload the library because callbacks could still fire from the native side
				//DISABLED: return NativeMethods.FreeLibrary(handle);
				return true;
			}
		}
#endif


		[SuppressUnmanagedCodeSecurity]
		private static class NativeMethods
		{
			const string LIBDL = "dl";


			[DllImport(LIBDL)]
			public static extern SafeLibraryHandle dlopen(string fileName, int flags);


			[DllImport(LIBDL, SetLastError = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern int dlclose(IntPtr hModule);

#if __MonoCS__

			public static SafeLibraryHandle LoadPlatformLibrary(string fileName)
			{
				return dlopen(fileName, 1);
			}
			public static bool FreePlatformLibrary(IntPtr hModule) { return dlclose(hModule) == 0; }

#else
			const string KERNEL = "kernel32";

			[DllImport(KERNEL, CharSet = CharSet.Auto, BestFitMapping = false, SetLastError = true)]
			public static extern SafeLibraryHandle LoadLibrary(string fileName);

			[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
			[DllImport(KERNEL, SetLastError = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool FreeLibrary(IntPtr hModule);

			public static SafeLibraryHandle LoadPlatformLibrary(string fileName) 
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					return LoadLibrary(fileName);
				}
				return dlopen(fileName, 1);
			}

			public static bool FreePlatformLibrary(IntPtr hModule)
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					return FreeLibrary(hModule);
				}
				return dlclose(hModule) == 0;
			}
#endif
		}

		/// <summary>Load a native library into the current process</summary>
		/// <param name="path">Path to the native dll.</param>
		/// <remarks>Throws exceptions on failure. Most common failure would be file-not-found, or that the file is not a  loadable image.</remarks>
		/// <exception cref="System.IO.FileNotFoundException">if fileName can't be found</exception>
		public static UnmanagedLibrary Load(string path)
		{
			Contract.NotNull(path);

			var handle = NativeMethods.LoadPlatformLibrary(path);
			if (handle == null || handle.IsInvalid)
			{
				var ex = Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
				if (ex is System.IO.FileNotFoundException)
				{
					throw new System.IO.FileNotFoundException($"Failed to load native {(IntPtr.Size == 8 ? "x64" : "x86")} library: {path}", path, ex);
				}
				throw ex;
			}
			return new UnmanagedLibrary(handle, path);
		}

		/// <summary>Constructor to load a dll and be responsible for freeing it.</summary>
		/// <param name="handle">Handle to the loaded library</param>
		/// <param name="path">Full path of library to load</param>
		private UnmanagedLibrary(SafeLibraryHandle handle, string path)
		{
			this.Handle = handle;
			this.Path = path;
		}

		/// <summary>Path of the native library, as passed to LoadLibrary</summary>
		public string Path { get; }

		/// <summary>Unmanaged resource. CLR will ensure SafeHandles get freed, without requiring a finalizer on this class.</summary>
		public SafeLibraryHandle Handle { get; }

		/// <summary>Call FreeLibrary on the unmanaged dll. All function pointers handed out from this class become invalid after this.</summary>
		/// <remarks>This is very dangerous because it suddenly invalidate everything retrieved from this dll. This includes any functions handed out via GetProcAddress, and potentially any objects returned from those functions (which may have an implementation in the dll)./// </remarks>
		public void Dispose()
		{
			if (!this.Handle.IsClosed)
			{
				this.Handle.Close();
			}
		}

	}

}

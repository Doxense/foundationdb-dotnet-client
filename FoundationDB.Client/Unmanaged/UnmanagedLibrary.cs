#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
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

using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;

namespace FoundationDB.Client.Native
{

	/// <summary>Native Library Loader</summary>
	internal sealed class UnmanagedLibrary : IDisposable
	{
		// See http://msdn.microsoft.com/msdnmag/issues/05/10/Reliability/ for more about safe handles.
		[SuppressUnmanagedCodeSecurity]
		public sealed class SafeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
		{
			private SafeLibraryHandle() : base(true) { }

			protected override bool ReleaseHandle()
			{
				return NativeMethods.FreeLibrary(handle);
			}
		}

		[SuppressUnmanagedCodeSecurity]
		private static class NativeMethods
		{
			const string KERNEL = "kernel32";

			[DllImport(KERNEL, CharSet = CharSet.Auto, BestFitMapping = false, SetLastError = true)]
			public static extern SafeLibraryHandle LoadLibrary(string fileName);

			[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
			[DllImport(KERNEL, SetLastError = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool FreeLibrary(IntPtr hModule);

			[DllImport(KERNEL, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
			public static extern IntPtr GetProcAddress(SafeLibraryHandle hModule, String procname);
		}

		/// <summary>Load a native library into the current process</summary>
		/// <param name="path">Path to the native dll.</param>
		/// <remarks>Throws exceptions on failure. Most common failure would be file-not-found, or that the file is not a  loadable image.</remarks>
		/// <exception cref="System.IO.FileNotFoundException">if fileName can't be found</exception>
		public static UnmanagedLibrary LoadLibrary(string path)
		{
			var handle = NativeMethods.LoadLibrary(path);
			if (handle == null || handle.IsInvalid)
			{
				int hr = Marshal.GetHRForLastWin32Error();
				var ex = Marshal.GetExceptionForHR(hr);
				if (ex is System.IO.FileNotFoundException)
					throw new System.IO.FileNotFoundException(String.Format("Failed to load native {0} library: {1}", IntPtr.Size == 8 ? "x64" : "x86", path), path, ex);
				else
					throw ex;
			}
			return new UnmanagedLibrary(handle, path);
		}

		/// <summary>Constructor to load a dll and be responible for freeing it.</summary>
		/// <param name="handle">Handle to the loaded library</param>
		/// <param name="path">Full path of library to load</param>
		private UnmanagedLibrary(SafeLibraryHandle handle, string path)
		{
			this.Handle = handle;
			this.Path = path;
		}

		/// <summary>Path of the native library, as passed to LoadLibrary</summary>
		public string Path { get; private set; }

		/// <summary>Unmanaged resource. CLR will ensure SafeHandles get freed, without requiring a finalizer on this class.</summary>
		public SafeLibraryHandle Handle { get; private set; }

		/// <summary>
		/// Dynamically lookup a function in the dll via kernel32!GetProcAddress.
		/// </summary>
		/// <param name="functionName">raw name of the function in the export table.</param>
		/// <returns>null if function is not found. Else a delegate to the unmanaged function.
		/// </returns>
		/// <remarks>GetProcAddress results are valid as long as the dll is not yet unloaded. This
		/// is very very dangerous to use since you need to ensure that the dll is not unloaded
		/// until after you're done with any objects implemented by the dll. For example, if you
		/// get a delegate that then gets an IUnknown implemented by this dll,
		/// you can not dispose this library until that IUnknown is collected. Else, you may free
		/// the library and then the CLR may call release on that IUnknown and it will crash.</remarks>
		public TDelegate GetUnmanagedFunction<TDelegate>(string functionName) where TDelegate : class
		{
			IntPtr p = NativeMethods.GetProcAddress(this.Handle, functionName);

			// Failure is a common case, especially for adaptive code.
			if (p == IntPtr.Zero)
			{
				return null;
			}
			Delegate function = Marshal.GetDelegateForFunctionPointer(p, typeof(TDelegate));

			// Ideally, we'd just make the constraint on TDelegate be
			// System.Delegate, but compiler error CS0702 (constrained can't be System.Delegate)
			// prevents that. So we make the constraint system.object and do the cast from object-->TDelegate.
			object o = function;

			return (TDelegate)o;
		}

		public TDelegate Bind<TDelegate>(string functionName, bool optional = false) where TDelegate : class
		{
			if (this.Handle == null || this.Handle.IsInvalid)
				throw new InvalidOperationException(String.Format("Cannot bind function '{0}' because the library '{1}' failed to load properly", functionName, this.Path));

			var func = GetUnmanagedFunction<TDelegate>(functionName);
			if (func == null && !optional) throw new InvalidOperationException(String.Format("Could not find the entry point for function '{0}' in library '{1}'", functionName, this.Path));

			return func;
		}

		public void Bind<TDelegate>(ref TDelegate stub, string functionName, bool optional = false) where TDelegate : class
		{
			stub = Bind<TDelegate>(functionName);
		}

		/// <summary>Call FreeLibrary on the unmanaged dll. All function pointers handed out from this class become invalid after this.</summary>
		/// <remarks>This is very dangerous because it suddenly invalidate everything retrieved from this dll. This includes any functions handed out via GetProcAddress, and potentially any objects returned from those functions (which may have an implemention in the dll)./// </remarks>
		public void Dispose()
		{
			if (!this.Handle.IsClosed)
			{
				this.Handle.Close();
			}
		}

	}

}
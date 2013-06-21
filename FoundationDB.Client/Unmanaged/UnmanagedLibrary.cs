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
	/// <remarks>Encapsule un LoadLibrary dans un SafeHandle</remarks>
	internal sealed class UnmanagedLibrary : IDisposable
	{
		#region Safe Handles and Native imports

		// See http://msdn.microsoft.com/msdnmag/issues/05/10/Reliability/ for more about safe handles.
		[SuppressUnmanagedCodeSecurity]
		sealed class SafeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
		{
			private SafeLibraryHandle() : base(true) { }

			protected override bool ReleaseHandle()
			{
				return NativeMethods.FreeLibrary(handle);
			}
		}

		[SuppressUnmanagedCodeSecurity]
		static class NativeMethods
		{
			const string KERNEL = "kernel32";

			[DllImport(KERNEL, CharSet = CharSet.Auto, BestFitMapping = false, SetLastError = true)]
			public static extern SafeLibraryHandle LoadLibrary(string fileName);

			[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
			[DllImport(KERNEL, SetLastError = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool FreeLibrary(IntPtr hModule);

			[DllImport(KERNEL)]
			public static extern IntPtr GetProcAddress(SafeLibraryHandle hModule, String procname);
		}

		#endregion // Safe Handles and Native imports

		// Unmanaged resource. CLR will ensure SafeHandles get freed, without requiring a finalizer on this class.
		private readonly SafeLibraryHandle m_hLibrary;

		private readonly string m_fileName;

		/// <summary>
		/// Constructor to load a dll and be responible for freeing it.
		/// </summary>
		/// <param name="fileName">full path name of dll to load</param>
		/// <exception cref="System.IO.FileNotFound">if fileName can't be found</exception>
		/// <remarks>Throws exceptions on failure. Most common failure would be file-not-found, or
		/// that the file is not a  loadable image.</remarks>
		private UnmanagedLibrary(SafeLibraryHandle handle, string fileName)
		{
			m_hLibrary = handle;
			m_fileName = fileName;
		}

		public static UnmanagedLibrary LoadLibrary(string fileName)
		{
			string path = System.IO.Path.GetFullPath(fileName);
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
			return new UnmanagedLibrary(handle, fileName);
		}

		/// <summary>Charge la bonne libraire en fonction de la plateforme (32 bits / x86, ou 64 bits / x64)</summary>
		/// <param name="fileName32bits">Chemin vers la librairie à charger si on est en 32 bits</param>
		/// <param name="fileName64Bits">Chemin vers la librairie à charger si on est en 64 bits</param>
		/// <returns></returns>
		public static UnmanagedLibrary LoadLibrary(string fileName32bits, string fileName64Bits)
		{
			if (IntPtr.Size == 4)
			{ // x86 / 32 bits
				return LoadLibrary(fileName32bits);
			}
			else if (IntPtr.Size == 8)
			{ // x64 / 64 bits
				return LoadLibrary(fileName64Bits);
			}
			else
			{
				throw new InvalidOperationException(String.Format("Could find matching library in '{0}' or '{1}' for unknown platform ({2} bits)", fileName32bits, fileName64Bits, IntPtr.Size * 8));
			}
		}

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
			IntPtr p = NativeMethods.GetProcAddress(m_hLibrary, functionName);

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
			if (m_hLibrary == null || m_hLibrary.IsInvalid)
				throw new InvalidOperationException(String.Format("Cannot bind function '{0}' because the library '{1}' failed to load properly", functionName, m_fileName));

			var func = GetUnmanagedFunction<TDelegate>(functionName);
			if (func == null && !optional) throw new InvalidOperationException(String.Format("Could not find the entry point for function '{0}' in library '{1}'", functionName, m_fileName));

			return func;
		}

		public void Bind<TDelegate>(ref TDelegate stub, string functionName, bool optional = false) where TDelegate : class
		{
			stub = Bind<TDelegate>(functionName);
		}

		#region IDisposable Members

		/// <summary>
		/// Call FreeLibrary on the unmanaged dll. All function pointers
		/// handed out from this class become invalid after this.
		/// </summary>
		/// <remarks>This is very dangerous because it suddenly invalidate
		/// everything retrieved from this dll. This includes any functions
		/// handed out via GetProcAddress, and potentially any objects returned
		/// from those functions (which may have an implemention in the
		/// dll).
		/// </remarks>
		public void Dispose()
		{
			if (!m_hLibrary.IsClosed)
			{
				m_hLibrary.Close();
			}
		}

		#endregion
	}

}
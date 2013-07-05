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

namespace FoundationDB.Client
{
	using System;

	public static partial class Fdb
	{

		/// <summary>Global settings for the FoundationDB binding</summary>
		public static class Options
		{

			#region Native Library Preloading...

			/// <summary>Custom path from where to load the native C API library. If null, let the CLR find the dll. If String.Empty let Win32's LoadLibrary find the correct dll, else use the specified path to load the library</summary>
			public static string NativeLibPath = String.Empty;

			/// <summary>Disable preloading of the native C API library. The CLR will handle the binding of the library.</summary>
			/// <remarks>This MUST be called before calling any other methods !</remarks>
			public static void DisableNativeLibraryPreloading()
			{
				Fdb.Options.NativeLibPath = null;
			}

			/// <summary>Enable automatic preloading of the native C API library. The operating system will handle the binding of the library</summary>
			/// <remarks>This MUST be called before calling any other methods !</remarks>
			public static void EnableNativeLibraryPreloading()
			{
				Fdb.Options.NativeLibPath = String.Empty;
			}

			/// <summary>Preload the native C API library from a specifc path (relative of absolute) where the fdb_c.dll library is located</summary>
			/// <example>SetNativeLibPath(@".\libs\x64") will attempt to load ".\libs\x64\fdb_c.dll"</example>
			/// <remarks>This MUST be called before calling any other methods !</remarks>
			public static void SetNativeLibPath(string path)
			{
				if (path == null) throw new ArgumentNullException("path");

				Fdb.Options.NativeLibPath = path;
			}

			#endregion

			#region Trace Path...

			/// <summary>Default path to the network thread tracing file</summary>
			public static string TracePath = null;

			/// <summary>Sets the custom path where the logs will be stored</summary>
			public static void SetTracePath(string outputDirectory)
			{
				Fdb.Options.TracePath = outputDirectory;
			}

			#endregion

		}

	}

}

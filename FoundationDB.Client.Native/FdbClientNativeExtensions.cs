#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace FoundationDB.Client
{
	using System;
	using System.IO;
	using System.Runtime.InteropServices;
	using FoundationDB.DependencyInjection;

	public static class FdbClientNativeExtensions
	{

		/// <summary>Configures the <see cref="FdbDatabaseProviderOptions"/> to use the native FoundationDB client library that is redistributed with the <c>FoundationDB.Client.Native</c> package.</summary>
		/// <param name="options">The <see cref="FdbDatabaseProviderOptions"/> to configure.</param>
		/// <returns>The configured <see cref="FdbDatabaseProviderOptions"/>.</returns>
		/// <exception cref="FileNotFoundException">
		/// Thrown when the native FoundationDB client library for the running platform could not be found.
		/// </exception>
		/// <remarks>
		/// <para>This method determines the runtime identifier (<c>win-x64</c>, <c>linux-x64</c>, <c>linux-arm64</c>, ...) and the appropriate file name (<c>fdb_c.dll</c>, <c>libfdbc.so</c>, ...) based on the current platform.</para>
		/// <para>
		/// The probed directories are, in order: <list type="bullet">
		/// <item>The <c>runtimes/{rid}/native/</c> subfolder under the application's base directory.</item>
		/// <item>The application's base directory.</item>
		/// </list></para>
		/// <para>The loader will <b>NOT</b> probe any other locations, like the default system folders (<c>System32</c>, ...) or any location defined in the <c>PATH</c> environment variable.</para>
		/// </remarks>
		public static FdbDatabaseProviderOptions UseNativeClient(this FdbDatabaseProviderOptions options, bool allowSystemFallback = false)
		{
			// find out the platform we are running on ("win-x64", "linux-x64", ...)
			var (rid, fileName) = GetRuntimeIdentifierForCurrentPlatform();

			// Depending on how the application is built/published, there are two main locations where the native libs will be copied:
			// - Under ./runtimes/{rid}/native/ when running in Visual Studio, or in "Portable mode" (no rid specified during publish)
			// - In the application base directory, when published with a specific rid (ex: win-x64)
			//
			// We DO NOT allow loading from another location, system System32, or $PATH
			// => The intent of "UseNativeClient()" is to use ONLY libs that came from the package,
			//    and not inherit form a (probably incompatible) fdb_c.dll that would be there from
			//    previous experiments, or a locally installed fdbserver.
			
			// first probe in the runtimes/... subfolder for the native library
			var runtimesNativePath = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native");
			var nativeLibraryPath = Path.Combine(runtimesNativePath, fileName);
			if (!File.Exists(nativeLibraryPath))
			{
				// is it in the current directory?
				nativeLibraryPath = Path.Combine(AppContext.BaseDirectory, fileName);
				if (!File.Exists(nativeLibraryPath))
				{
					// we could not find the correct library!
					if (!allowSystemFallback)
					{
						throw new FileNotFoundException($"Could not find native FoundationDB Client Library '{fileName}' for platform '{rid}' under either '{runtimesNativePath}' nor the base directory '{AppContext.BaseDirectory}'");
					}

					options.NativeLibraryPath = "";
					return options;
				}
			}

			options.NativeLibraryPath = nativeLibraryPath;
			return options;
		}
		
		private static (string Rid, string FileName) GetRuntimeIdentifierForCurrentPlatform()
		{
			// Determine the path to the native libraries based on the platform
			string? platform =
				  RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win"
				: RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
				: RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx"
				: null;
			
			string? arch =
				  RuntimeInformation.OSArchitecture == Architecture.X64 ? "x64"
				: RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64"
				: null;

			string fileName = platform == "win" ? "fdb_c.dll" : "libfbb_c.so";

			if (platform is null || arch is null)
			{
				throw new PlatformNotSupportedException("Unsupported platform");
			}

			return (platform + "-" + arch, fileName);
		}
		
	}
	
	
}

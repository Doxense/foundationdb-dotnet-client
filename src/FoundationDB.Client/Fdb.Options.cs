﻿#region BSD Licence
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

		//REVIEW: consider changing this to an instance class so that we could do a Fluent API ? ex: Fdb.Options.WithFoo(...).WithBar(...).WithBaz(...)

		/// <summary>Global settings for the FoundationDB binding</summary>
		public static class Options
		{

			#region Native Library Preloading...

			/// <summary>Custom path from where to load the native C API library. If null, let the CLR find the dll. If String.Empty let Win32's LoadLibrary find the correct dll, else use the specified path to load the library</summary>
			public static string NativeLibPath = String.Empty;

			/// <summary>Disable preloading of the native C API library. The CLR will handle the binding of the library.</summary>
			/// <remarks>This *must* be called before the start of the network thread, otherwise it won't have any effects.</remarks>
			public static void DisableNativeLibraryPreloading()
			{
				Fdb.Options.NativeLibPath = null;
			}

			/// <summary>Enable automatic preloading of the native C API library. The operating system will handle the binding of the library</summary>
			/// <remarks>This *must* be called before the start of the network thread, otherwise it won't have any effects.</remarks>
			public static void EnableNativeLibraryPreloading()
			{
				Fdb.Options.NativeLibPath = String.Empty;
			}

			/// <summary>Preload the native C API library from a specifc path (relative of absolute) where the fdb_c.dll library is located</summary>
			/// <example>SetNativeLibPath(@".\libs\x64") will attempt to load ".\libs\x64\fdb_c.dll"</example>
			/// <remarks>This *must* be called before the start of the network thread, otherwise it won't have any effects.</remarks>
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
			/// <remarks>This *must* be called before the start of the network thread, otherwise it won't have any effects.</remarks>
			public static void SetTracePath(string outputDirectory)
			{
				Fdb.Options.TracePath = outputDirectory;
			}

			#endregion

			#region TLS...

			/// <summary>File path or linker-resolved name of the custom TLS plugin to load. </summary>
			public static string TLSPlugin { get; private set; }

			/// <summary>Content of the TLS root and client certificates used for TLS connections (none by default)</summary>
			public static Slice TLSCertificateBytes { get; private set; }

			/// <summary>Path to the TLS root and client certificates used for TLS connections (none by default)</summary>
			public static string TLSCertificatePath { get; private set; }

			/// <summary>Path to the Private Key used for TLS connections (none by default)</summary>
			public static Slice TLSPrivateKeyBytes { get; private set; }

			/// <summary>Path to the Private Key used for TLS connections (none by default)</summary>
			public static string TLSPrivateKeyPath { get; private set; }

			/// <summary>Pattern used to verifiy certificates of TLS peers (none by default)</summary>
			public static Slice TLSVerificationPattern { get; private set; }

			/// <summary>Set the file path or linker-resolved name of the custom TLS plugin to load. </summary>
			public static void SetTLSPlugin(string name)
			{
				Fdb.Options.TLSPlugin = name;
			}

			/// <summary>Sets the path to the root certificate and public key for TLS connections</summary>
			/// <remarks>This *must* be called before the start of the network thread, otherwise it won't have any effects.</remarks>
			public static void SetTLSCertificate(Slice bytes)
			{
				Fdb.Options.TLSCertificateBytes = bytes;
				Fdb.Options.TLSCertificatePath = null;
			}

			/// <summary>Sets the path to the root certificate and public key for TLS connections</summary>
			/// <remarks>This *must* be called before the start of the network thread, otherwise it won't have any effects.</remarks>
			public static void SetTLSCertificate(string path)
			{
				Fdb.Options.TLSCertificatePath = path;
				Fdb.Options.TLSCertificateBytes = Slice.Nil;
			}

			/// <summary>Sets the path to the private key for TLS connections</summary>
			/// <remarks>This must be called before the start of the network thread, otherwise it won't have any effects.</remarks>
			public static void SetTLSPrivateKey(Slice bytes)
			{
				Fdb.Options.TLSPrivateKeyBytes = bytes;
				Fdb.Options.TLSPrivateKeyPath = null;
			}

			/// <summary>Sets the path to the private key for TLS connections</summary>
			/// <remarks>This must be called before the start of the network thread, otherwise it won't have any effects.</remarks>
			public static void SetTLSPrivateKey(string path)
			{
				Fdb.Options.TLSPrivateKeyPath = path;
				Fdb.Options.TLSPrivateKeyBytes = Slice.Nil;
			}

			/// <summary>Sets the pattern with wich to verify certificates of TLS peers</summary>
			/// <remarks>This must be called before the start of the network thread, otherwise it won't have any effects.</remarks>
			public static void SetTlsVerificationPattern(Slice pattern)
			{
				Fdb.Options.TLSVerificationPattern = pattern;
			}

			/// <summary>Use TLS to secure the connections to the cluster</summary>
			/// <param name="certificateBytes">Content of the root certificate and public key</param>
			/// <param name="privateKeyBytes">Content of the private key</param>
			/// <param name="verificationPattern">Verification with which to verify certificates of TLS peers</param>
			public static void UseTLS(Slice certificateBytes, Slice privateKeyBytes, Slice verificationPattern = default(Slice), string plugin = null)
			{
				Fdb.Options.TLSPlugin = plugin;
				Fdb.Options.TLSCertificateBytes = certificateBytes;
				Fdb.Options.TLSCertificatePath = null;
				Fdb.Options.TLSPrivateKeyBytes = privateKeyBytes;
				Fdb.Options.TLSPrivateKeyPath = null;
				Fdb.Options.TLSVerificationPattern = verificationPattern;
			}

			/// <summary>Use TLS to secure the connections to the cluster</summary>
			/// <param name="certificatePath">Path to the root certificate and public key</param>
			/// <param name="privateKeyPath">Path to the private key</param>
			/// <param name="verificationPattern">Verification with which to verify certificates of TLS peers</param>
			public static void UseTLS(string certificatePath, string privateKeyPath, Slice verificationPattern = default(Slice), string plugin = null)
			{
				Fdb.Options.TLSPlugin = plugin;
				Fdb.Options.TLSCertificatePath = certificatePath;
				Fdb.Options.TLSCertificateBytes = Slice.Nil;
				Fdb.Options.TLSPrivateKeyPath = privateKeyPath;
				Fdb.Options.TLSPrivateKeyBytes = Slice.Nil;
				Fdb.Options.TLSVerificationPattern = verificationPattern;
			}

			#endregion

		}

	}

}

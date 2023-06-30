﻿#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace Doxense.Testing
{
	using System;
	using System.IO;
	using System.IO.Compression;

	public static class CompressionExtensions
	{
		public static Slice ZstdCompress(this Slice data, int level)
		{
			using (var cmp = new ZstdSharp.Compressor(level))
			{
				var buf = new byte[ZstdSharp.Compressor.GetCompressBound(data.Count)];
				int sz = cmp.Wrap(data.Span, buf.AsSpan());
				return buf.AsSlice(0, sz);
			}
		}

		public static Slice DeflateCompress(this Slice data, CompressionLevel level)
		{
			using (var ms = new MemoryStream())
			{
				using (var zs = new DeflateStream(ms, level, leaveOpen: true))
				{
					zs.Write(data.Span);
				}
				return ms.ToSlice();
			}
		}

		public static Slice GzipCompress(this Slice data, CompressionLevel level)
		{
			using (var ms = new MemoryStream())
			{
				using (var zs = new GZipStream(ms, level, leaveOpen: true))
				{
					zs.Write(data.Span);
				}
				return ms.ToSlice();
			}
		}


	}
}

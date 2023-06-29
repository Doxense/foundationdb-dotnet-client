#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
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

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

#nullable enable

namespace FoundationDB.Types.Json
{
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Text;
	using Doxense.Memory;
	using Doxense.Serialization.Encoders;
	using Newtonsoft.Json;

	/// <summary>Sample codec that uses JSON.Net to serialize values into Slices</summary>
	/// <typeparam name="TDocument"></typeparam>
	public class JsonNetCodec<TDocument> : IValueEncoder<TDocument>, IUnorderedTypeCodec<TDocument>
	{

		private static readonly Encoding s_utf8NoBom = new UTF8Encoding(false);

		private readonly JsonSerializer m_serializer;

		public JsonNetCodec(JsonSerializerSettings? settings = null)
			: this(JsonSerializer.CreateDefault(settings))
		{ }

		public JsonNetCodec(JsonSerializer? serializer)
		{
			m_serializer = serializer ?? JsonSerializer.CreateDefault();
		}

		protected virtual Slice EncodeInternal(TDocument? document)
		{
			var ms = new MemoryStream(256);
			using (var tw = new StreamWriter(ms, s_utf8NoBom))
			{
				m_serializer.Serialize(tw, document, typeof(TDocument));

				tw.Flush();

				// Overflow protection (should never happen since a MemoryStream won't let us write more than 2G, but just to be sure ...)
				if (ms.Length > int.MaxValue) throw new OutOfMemoryException("The serialized JSON document exceeds the maximum allowed size");

				// Reuse the stream's internal buffer to reduce the need for allocations!
				var tmp = ms.GetBuffer();
				int size = checked((int)ms.Length);
				Debug.Assert(tmp != null && size >= 0 && size <= tmp.Length);

				return tmp.AsSlice(0, size);
			}
		}

		public Slice EncodeValue(TDocument? document)
		{
			return EncodeInternal(document);
		}

		public TDocument? DecodeValue(Slice encoded)
		{
			if (encoded.IsNullOrEmpty) return default(TDocument);

			// note: the StreamReader should remove the UTF8 BOM for us
			using (var sr = new StreamReader(encoded.ToStream(), Encoding.UTF8))
			{
				using (var reader = new JsonTextReader(sr))
				{
					return m_serializer.Deserialize<TDocument>(reader);
				}
			}
		}

		void IUnorderedTypeCodec<TDocument>.EncodeUnorderedSelfTerm(ref SliceWriter output, TDocument? value)
		{
			var packed = EncodeInternal(value);
			Debug.Assert(packed.Count >= 0);
			output.WriteVarInt32((uint)packed.Count);
			output.WriteBytes(packed);
		}

		TDocument? IUnorderedTypeCodec<TDocument>.DecodeUnorderedSelfTerm(ref SliceReader input)
		{
			uint size = input.ReadVarInt32();
			if (size > int.MaxValue) throw new FormatException("Malformed data size");

			var packed = input.ReadBytes((int)size);
			return DecodeValue(packed);
		}

		Slice IUnorderedTypeCodec<TDocument>.EncodeUnordered(TDocument? value)
		{
			return EncodeValue(value);
		}

		TDocument? IUnorderedTypeCodec<TDocument>.DecodeUnordered(Slice input)
		{
			return DecodeValue(input);
		}

	}

}

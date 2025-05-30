#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace SnowBank.Data.Tuples.Tests
{
	using SnowBank.Data.Tuples.Binary;
	using SnowBank.Buffers;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class TupleTypeCodecFacts : SimpleTest
	{

		[Test]
		public void Test_Simple_Integer_Codec()
		{
			var codec = TupleCodec<int>.Default;
			Assert.That(codec, Is.Not.Null);

			Assert.That(codec.EncodeOrdered(0), Is.EqualTo(TuPack.EncodeKey(0)));
			Assert.That(codec.EncodeOrdered(123), Is.EqualTo(TuPack.EncodeKey(123)));
			Assert.That(codec.EncodeOrdered(123456), Is.EqualTo(TuPack.EncodeKey(123456)));

			Assert.That(codec.DecodeOrdered(TuPack.EncodeKey(0)), Is.EqualTo(0));
			Assert.That(codec.DecodeOrdered(TuPack.EncodeKey(123)), Is.EqualTo(123));
			Assert.That(codec.DecodeOrdered(TuPack.EncodeKey(123456)), Is.EqualTo(123456));
		}

		[Test]
		public void Test_Simple_String_Codec()
		{
			var codec = TupleCodec<string>.Default;
			Assert.That(codec, Is.Not.Null);

			Assert.That(codec.EncodeOrdered("héllø Wörld"), Is.EqualTo(TuPack.EncodeKey("héllø Wörld")));
			Assert.That(codec.EncodeOrdered(string.Empty), Is.EqualTo(TuPack.EncodeKey("")));
			Assert.That(codec.EncodeOrdered(null), Is.EqualTo(TuPack.EncodeKey(default(string))));

			Assert.That(codec.DecodeOrdered(TuPack.EncodeKey("héllø Wörld")), Is.EqualTo("héllø Wörld"));
			Assert.That(codec.DecodeOrdered(TuPack.EncodeKey(string.Empty)), Is.EqualTo(""));
			Assert.That(codec.DecodeOrdered(TuPack.EncodeKey(default(string))), Is.Null);
		}

		[Test]
		public void Test_Simple_SelfTerms_Codecs()
		{
			// encodes a key using 3 parts: (x, y, z) => ordered_key

			string x = "abc";
			long y = 123;
			Guid z = Guid.NewGuid();

			var first = TupleCodec<string>.Default;
			var second = TupleCodec<long>.Default;
			var third = TupleCodec<Guid>.Default;

			var writer = default(SliceWriter);
			first.EncodeOrderedTo(ref writer, x);
			second.EncodeOrderedTo(ref writer, y);
			third.EncodeOrderedTo(ref writer, z);
			var data = writer.ToSlice();
			Assert.That(data, Is.EqualTo(TuPack.EncodeKey(x, y, z)));

			var reader = new SliceReader(data);
			Assert.That(first.DecodeOrderedFrom(ref reader), Is.EqualTo(x));
			Assert.That(second.DecodeOrderedFrom(ref reader), Is.EqualTo(y));
			Assert.That(third.DecodeOrderedFrom(ref reader), Is.EqualTo(z));
			Assert.That(reader.HasMore, Is.False);
		}

	}

}

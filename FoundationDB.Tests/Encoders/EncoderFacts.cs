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

namespace FoundationDB.Client.Converters.Tests
{
	using FoundationDB.Client;
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Tuples;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text;

	[TestFixture]
	public class EncoderFacts
	{

		[Test]
		public void Test_Simple_Encoder()
		{
			var encoder = KeyValueEncoders.Unordered.Bind(FdbTupleCodec<int>.Default);
			Assert.That(encoder, Is.Not.Null);

			Assert.That(encoder.Encode(0), Is.EqualTo(FdbTuple.Pack(0)));
			Assert.That(encoder.Encode(123), Is.EqualTo(FdbTuple.Pack(123)));
			Assert.That(encoder.Encode(123456), Is.EqualTo(FdbTuple.Pack(123456)));

			Assert.That(encoder.Decode(FdbTuple.Pack(0)), Is.EqualTo(0));
			Assert.That(encoder.Decode(FdbTuple.Pack(123)), Is.EqualTo(123));
			Assert.That(encoder.Decode(FdbTuple.Pack(123456)), Is.EqualTo(123456));
		}

		[Test]
		public void Test_Simple_String_Codec()
		{
			var encoder = KeyValueEncoders.Ordered.Bind(FdbTupleCodec<string>.Default);
			Assert.That(encoder, Is.Not.Null);

			Assert.That(encoder.Encode("héllø Wörld"), Is.EqualTo(FdbTuple.Pack("héllø Wörld")));
			Assert.That(encoder.Encode(String.Empty), Is.EqualTo(FdbTuple.Pack("")));
			Assert.That(encoder.Encode(null), Is.EqualTo(FdbTuple.Pack(default(string))));

			Assert.That(encoder.Decode(FdbTuple.Pack("héllø Wörld")), Is.EqualTo("héllø Wörld"));
			Assert.That(encoder.Decode(FdbTuple.Pack(String.Empty)), Is.EqualTo(""));
			Assert.That(encoder.Decode(FdbTuple.Pack(default(string))), Is.Null);
		}

		[Test]
		public void Test_Simple_SelfTerms_Codecs()
		{
			// encodes a key using 3 parts: (x, y, z) => ordered_key

			string x = "abc";
			long y = 123;
			Guid z = Guid.NewGuid();

			var encoder = KeyValueEncoders.Tuples.Default<string, long, Guid>();
			Assert.That(encoder, Is.Not.Null);

			var data = encoder.Encode(x, y, z);
			Assert.That(data, Is.EqualTo(FdbTuple.Pack(x, y, z)));

			var items = encoder.Decode(data);
			Assert.That(items.Item1, Is.EqualTo(x));
			Assert.That(items.Item2, Is.EqualTo(y));
			Assert.That(items.Item3, Is.EqualTo(z));
		}

	}
}

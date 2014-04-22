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
		public void Test_Order_Int32Encoder()
		{
			// The current ordered int32 encoder uses the Tuple encoding

			var encoder = KeyValueEncoders.Ordered.Int32Encoder;
			Assert.That(encoder, Is.Not.Null);

			Assert.That(encoder.EncodeKey(0), Is.EqualTo(Slice.Unescape("<14>")));
			Assert.That(encoder.EncodeKey(123), Is.EqualTo(Slice.Unescape("<15><7B>")));
			Assert.That(encoder.EncodeKey(123456), Is.EqualTo(Slice.Unescape("<17><01><E2><40>")));
			Assert.That(encoder.EncodeKey(-1), Is.EqualTo(Slice.Unescape("<13><FE>")));

			Assert.That(encoder.DecodeKey(Slice.Unescape("<14>")), Is.EqualTo(0));
			Assert.That(encoder.DecodeKey(Slice.Unescape("<15><7B>")), Is.EqualTo(123));
			Assert.That(encoder.DecodeKey(Slice.Unescape("<17><01><E2><40>")), Is.EqualTo(123456));
			Assert.That(encoder.DecodeKey(Slice.Unescape("<13><FE>")), Is.EqualTo(-1));
		}

		[Test]
		public void Test_Ordered_StringEncoder()
		{
			// The current ordered string encoder uses UTF-8 and the Tuple encoding

			var encoder = KeyValueEncoders.Ordered.StringEncoder;
			Assert.That(encoder, Is.Not.Null);

			Assert.That(encoder.EncodeKey("héllø Wörld"), Is.EqualTo(Slice.Unescape("<02>h<C3><A9>ll<C3><B8> W<C3><B6>rld<00>")));
			Assert.That(encoder.EncodeKey("\0"), Is.EqualTo(Slice.Unescape("<02><00><FF><00>")));
			Assert.That(encoder.EncodeKey(String.Empty), Is.EqualTo(Slice.Unescape("<02><00>")));
			Assert.That(encoder.EncodeKey(null), Is.EqualTo(Slice.Unescape("<00>")));

			Assert.That(encoder.DecodeKey(Slice.Unescape("<02>h<C3><A9>ll<C3><B8> W<C3><B6>rld<00>")), Is.EqualTo("héllø Wörld"));
			Assert.That(encoder.DecodeKey(Slice.Unescape("<02><00><FF><00>")), Is.EqualTo("\0"));
			Assert.That(encoder.DecodeKey(Slice.Unescape("<02><00>")), Is.EqualTo(""));
			Assert.That(encoder.DecodeKey(Slice.Unescape("<00>")), Is.Null);
		}

		[Test]
		public void Test_Ordered_BinaryEncoder()
		{
			// The current ordered binary encoder uses the Tuple encoding

			var encoder = KeyValueEncoders.Ordered.BinaryEncoder;
			Assert.That(encoder, Is.Not.Null);

			Assert.That(encoder.EncodeKey(Slice.FromString("hello world")), Is.EqualTo(Slice.Unescape("<01>hello world<00>")));
			Assert.That(encoder.EncodeKey(Slice.Create(new byte[] {  0, 0xFF, 0 })), Is.EqualTo(Slice.Unescape("<01><00><FF><FF><00><FF><00>")));
			Assert.That(encoder.EncodeKey(Slice.Empty), Is.EqualTo(Slice.Unescape("<01><00>")));
			Assert.That(encoder.EncodeKey(Slice.Nil), Is.EqualTo(Slice.Unescape("<00>")));

			Assert.That(encoder.DecodeKey(Slice.Unescape("<01>hello world<00>")).ToUnicode(), Is.EqualTo("hello world"));
			Assert.That(encoder.DecodeKey(Slice.Unescape("<01><00><FF><FF><00><FF><00>")).ToString(), Is.EqualTo("<00><FF><00>"));
			Assert.That(encoder.DecodeKey(Slice.Unescape("<01><00>")), Is.EqualTo(Slice.Empty));
			Assert.That(encoder.DecodeKey(Slice.Unescape("<00>")), Is.EqualTo(Slice.Nil));
		}

		[Test]
		public void Test_Tuple_Composite_Encoder()
		{
			// encodes a key using 3 parts: (x, y, z) => ordered_key

			string x = "abc";
			long y = 123;
			Guid z = Guid.NewGuid();

			var encoder = KeyValueEncoders.Tuples.CompositeKey<string, long, Guid>();
			Assert.That(encoder, Is.Not.Null);

			// full key encoding

			// note: EncodeKey(...) is just a shortcurt for packing all items in a tuple, and EncodeComposite(..., count = 3)
			var data = encoder.EncodeKey(x, y, z);
			Assert.That(data, Is.EqualTo(FdbTuple.Pack(x, y, z)));

			var items = encoder.DecodeKey(data);
			Assert.That(items.Item1, Is.EqualTo(x));
			Assert.That(items.Item2, Is.EqualTo(y));
			Assert.That(items.Item3, Is.EqualTo(z));

			// partial key encoding

			data = encoder.EncodeComposite(items, 2);
			Assert.That(data, Is.EqualTo(FdbTuple.Pack(x, y)));
			items = encoder.DecodeComposite(FdbTuple.Pack(x, y), 2);
			Assert.That(items.Item1, Is.EqualTo(x));
			Assert.That(items.Item2, Is.EqualTo(y));
			Assert.That(items.Item3, Is.EqualTo(default(Guid)));

			data = encoder.EncodeComposite(items, 1);
			Assert.That(data, Is.EqualTo(FdbTuple.Pack(x)));
			items = encoder.DecodeComposite(FdbTuple.Pack(x), 1);
			Assert.That(items.Item1, Is.EqualTo(x));
			Assert.That(items.Item2, Is.EqualTo(default(long)));
			Assert.That(items.Item3, Is.EqualTo(default(Guid)));

			// should fail if number of items to encode is out of range
			Assert.That(() => { encoder.EncodeComposite(items, 4); }, Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => { encoder.EncodeComposite(items, 0); }, Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

	}
}

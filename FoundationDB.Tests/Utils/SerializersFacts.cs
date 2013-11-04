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

namespace FoundationDB.Client.Serializers.Tests
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
	public class SerializerFacts
	{

		[Test]
		public void Test_Default_Serializer_For_Slice()
		{
			// the default serializer for Slice should just roundtrip the value

			var identity = FdbSliceSerializer<Slice>.Default;
			Assert.That(identity, Is.Not.Null);
			Assert.That(identity, Is.InstanceOf<FdbSliceSerializer<Slice>>()); // in the current implementation !
			Assert.That(FdbSliceSerializer<Slice>.Default, Is.SameAs(identity), "Default serializers should be singletons");

			// ToSlice(Slice)
			Assert.That(identity.ToSlice(Slice.Nil), Is.EqualTo(Slice.Nil));
			Assert.That(identity.ToSlice(Slice.Empty), Is.EqualTo(Slice.Empty));
			Assert.That(identity.ToSlice(Slice.FromString("bonjour")), Is.EqualTo(Slice.FromString("bonjour")));

			// FromSlice(Slice)
			Assert.That(identity.FromSlice(Slice.Nil), Is.EqualTo(Slice.Nil));
			Assert.That(identity.FromSlice(Slice.Empty), Is.EqualTo(Slice.Empty));
			Assert.That(identity.FromSlice(Slice.FromString("bonjour")), Is.EqualTo(Slice.FromString("bonjour")));
		}

		[Test]
		public void Test_Default_Serializer_For_Value_Types()
		{
			// Int32
			Assert.That(FdbSliceSerializer<bool>.Default, Is.Not.Null);
			Assert.That(FdbSliceSerializer<bool>.Default.ToSlice(false), Is.EqualTo(Slice.FromByte(0)));
			Assert.That(FdbSliceSerializer<bool>.Default.ToSlice(true), Is.EqualTo(Slice.FromByte(1)));
			Assert.That(FdbSliceSerializer<bool>.Default.FromSlice(Slice.Nil), Is.False);
			Assert.That(FdbSliceSerializer<bool>.Default.FromSlice(Slice.Empty), Is.False);
			Assert.That(FdbSliceSerializer<bool>.Default.FromSlice(Slice.FromByte(0)), Is.False);
			Assert.That(FdbSliceSerializer<bool>.Default.FromSlice(Slice.FromByte(1)), Is.True);

			// Int32
			Assert.That(FdbSliceSerializer<int>.Default, Is.Not.Null);
			Assert.That(FdbSliceSerializer<int>.Default.ToSlice(123), Is.EqualTo(Slice.FromByte(123)));
			Assert.That(FdbSliceSerializer<int>.Default.FromSlice(Slice.FromInt32(int.MaxValue)), Is.EqualTo(int.MaxValue));

			// UInt32
			Assert.That(FdbSliceSerializer<uint>.Default, Is.Not.Null);
			Assert.That(FdbSliceSerializer<uint>.Default.ToSlice(123), Is.EqualTo(Slice.FromByte(123)));
			Assert.That(FdbSliceSerializer<uint>.Default.FromSlice(Slice.FromUInt32(uint.MaxValue)), Is.EqualTo(uint.MaxValue));

			// Int64
			Assert.That(FdbSliceSerializer<long>.Default, Is.Not.Null);
			Assert.That(FdbSliceSerializer<long>.Default.ToSlice(123), Is.EqualTo(Slice.FromByte(123)));
			Assert.That(FdbSliceSerializer<long>.Default.FromSlice(Slice.FromInt64(long.MaxValue)), Is.EqualTo(long.MaxValue));

			// UInt64
			Assert.That(FdbSliceSerializer<ulong>.Default, Is.Not.Null);
			Assert.That(FdbSliceSerializer<ulong>.Default.ToSlice(123), Is.EqualTo(Slice.FromByte(123)));
			Assert.That(FdbSliceSerializer<ulong>.Default.FromSlice(Slice.FromUInt64(ulong.MaxValue)), Is.EqualTo(ulong.MaxValue));

			// Guid
			var guid = Guid.NewGuid();
			Assert.That(FdbSliceSerializer<Guid>.Default, Is.Not.Null);
			Assert.That(FdbSliceSerializer<Guid>.Default.ToSlice(Guid.Empty), Is.EqualTo(Slice.Create(16)));
			Assert.That(FdbSliceSerializer<Guid>.Default.ToSlice(guid), Is.EqualTo(Slice.FromGuid(guid)));
			Assert.That(FdbSliceSerializer<Guid>.Default.FromSlice(Slice.Create(16)), Is.EqualTo(Guid.Empty));
			Assert.That(FdbSliceSerializer<Guid>.Default.FromSlice(Slice.FromGuid(guid)), Is.EqualTo(guid));
		}

		[Test]
		public void Test_Default_Serializer_For_Tuples()
		{
			// the default serializer for Tuples calls Pack() / Unpack()

			var tuplifier = FdbSliceSerializer<IFdbTuple>.Default;
			Assert.That(tuplifier, Is.Not.Null);
			Assert.That(tuplifier, Is.InstanceOf<FdbSliceSerializer<IFdbTuple>>()); // in the current implementation !
			Assert.That(FdbSliceSerializer<IFdbTuple>.Default, Is.SameAs(tuplifier), "Default serializers should be singletons");
			Assert.That(FdbSliceSerializer.Tuple, Is.SameAs(tuplifier));

			// ToSlice(Slice)
			Assert.That(tuplifier.ToSlice(null), Is.EqualTo(Slice.Nil));
			Assert.That(tuplifier.ToSlice(FdbTuple.Empty), Is.EqualTo(Slice.Empty));
			Assert.That(tuplifier.ToSlice(FdbTuple.Create("ABC")), Is.EqualTo(Slice.Unescape("<02>ABC<00>")));

			// FromSlice(Slice)
			Assert.That(tuplifier.FromSlice(Slice.Nil), Is.Null);
			Assert.That(tuplifier.FromSlice(Slice.Empty), Is.EqualTo(FdbTuple.Empty));
			Assert.That(tuplifier.FromSlice(Slice.Unescape("<02>ABC<00>")), Is.EqualTo(FdbTuple.Create("ABC")));

		}

		[Test]
		public void Test_Default_Serializer_For_ISliceSerializables()
		{
			var _ = FdbSliceSerializer<ISliceSerializable>.Default;

			var serializer = FdbSliceSerializer<Schmilblick>.Default;
			Assert.That(serializer, Is.Not.Null);
			Assert.That(serializer.ToSlice(null), Is.EqualTo(Slice.Nil));
			
			var instance = new Schmilblick(1234);
			Assert.That(serializer.ToSlice(instance), Is.EqualTo(Slice.FromInt32(0x55555187)));
			instance = serializer.FromSlice(Slice.FromInt32(0x55555187));
			Assert.That(instance, Is.Not.Null);
			Assert.That(instance.Value, Is.EqualTo(1234));
		}

		public class Schmilblick : ISliceSerializable
		{
			public Schmilblick()
			{ }

			public Schmilblick(int value)
			{
				this.Value = value;
			}

			public int Value { get; private set; }

			Slice ISliceSerializable.ToSlice()
			{
				return Slice.FromFixed32(this.Value ^ 0x55555555);
			}

			void ISliceSerializable.FromSlice(Slice slice)
			{
				this.Value = slice.ToInt32() ^ 0x55555555;
			}
		}

	}
}

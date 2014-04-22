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

namespace FoundationDB.Client.Tests
{
	using FoundationDB.Client;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Linq;

	[TestFixture]
	public class UuidFacts
	{
		[Test]
		public void Test_Uuid_Empty()
		{
			Assert.That(Uuid.Empty.ToString(), Is.EqualTo("00000000-0000-0000-0000-000000000000"));
			Assert.That(Uuid.Empty, Is.EqualTo(default(Uuid)));
			Assert.That(Uuid.Empty, Is.EqualTo(new Uuid(new byte[16])));
		}

		[Test]
		public void Test_Uuid_Parse()
		{
			Uuid uuid;

			uuid = Uuid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f");
			Assert.That(uuid.ToString(), Is.EqualTo("00010203-0405-0607-0809-0a0b0c0d0e0f"));
			Assert.That(uuid.ToByteArray(), Is.EqualTo(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 }));

			uuid = Uuid.Parse("{00010203-0405-0607-0809-0a0b0c0d0e0f}");
			Assert.That(uuid.ToString(), Is.EqualTo("00010203-0405-0607-0809-0a0b0c0d0e0f"));
			Assert.That(uuid.ToByteArray(), Is.EqualTo(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 }));
		}

		[Test]
		public void Test_Uuid_From_Bytes()
		{
			Uuid uuid;

			uuid = new Uuid(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 });
			Assert.That(uuid.ToString(), Is.EqualTo("00010203-0405-0607-0809-0a0b0c0d0e0f"));
			Assert.That(uuid.ToByteArray(), Is.EqualTo(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 }));

			uuid = new Uuid(new byte[16]);
			Assert.That(uuid.ToString(), Is.EqualTo("00000000-0000-0000-0000-000000000000"));
		}

		[Test]
		public void Test_Uuid_Vs_Guid()
		{
			Guid guid = Guid.NewGuid();

			var uuid = new Uuid(guid);
			Assert.That(uuid.ToString(), Is.EqualTo(guid.ToString()));
			Assert.That(uuid.ToGuid(), Is.EqualTo(guid));
			Assert.That((Guid)uuid, Is.EqualTo(guid));
			Assert.That((Uuid)guid, Is.EqualTo(uuid));
			Assert.That(Uuid.Parse(guid.ToString()), Is.EqualTo(uuid));
			Assert.That(uuid.Equals(guid), Is.True);
			Assert.That(uuid.Equals((object)guid), Is.True);
			Assert.That(uuid == guid, Is.True);
			Assert.That(guid == uuid, Is.True);

			Assert.That(uuid.Equals(Guid.NewGuid()), Is.False);
			Assert.That(uuid == Guid.NewGuid(), Is.False);
			Assert.That(Guid.NewGuid() == uuid, Is.False);
		}

		[Test]
		public void Test_Uuid_Equality()
		{
			Assert.That(Uuid.Empty.Equals(new Uuid(new byte[16])), Is.True);
			Assert.That(Uuid.Empty.Equals(Uuid.NewUuid()), Is.False);

			var uuid1 = Uuid.NewUuid();
			var uuid2 = Uuid.NewUuid();

			Assert.That(uuid1.Equals(uuid1), Is.True);
			Assert.That(uuid2.Equals(uuid2), Is.True);
			Assert.That(uuid1.Equals(uuid2), Is.False);
			Assert.That(uuid2.Equals(uuid1), Is.False);

			Assert.That(uuid1.Equals((object)uuid1), Is.True);
			Assert.That(uuid2.Equals((object)uuid2), Is.True);
			Assert.That(uuid1.Equals((object)uuid2), Is.False);
			Assert.That(uuid2.Equals((object)uuid1), Is.False);

			var uuid1b = Uuid.Parse(uuid1.ToString());
			Assert.That(uuid1b.Equals(uuid1), Is.True);
			Assert.That(uuid1b.Equals((object)uuid1), Is.True);

		}

		[Test]
		public void Test_Uuid_NewUuid()
		{
			var uuid = Uuid.NewUuid();
			Assert.That(uuid, Is.Not.EqualTo(Uuid.Empty));
			Assert.That(uuid.ToGuid().ToString(), Is.EqualTo(uuid.ToString()));
		}

		[Test]
		public void Test_Uuid_ToSlice()
		{
			var uuid = Uuid.NewUuid();
			Assert.That(uuid.ToSlice().Count, Is.EqualTo(16));
			Assert.That(uuid.ToSlice().Offset, Is.GreaterThanOrEqualTo(0));
			Assert.That(uuid.ToSlice().Array, Is.Not.Null);
			Assert.That(uuid.ToSlice().Array.Length, Is.GreaterThanOrEqualTo(16));
			Assert.That(uuid.ToSlice(), Is.EqualTo(Slice.Create(uuid.ToByteArray())));
			Assert.That(uuid.ToSlice().GetBytes(), Is.EqualTo(uuid.ToByteArray()));
		}

		[Test]
		public void Test_Uuid_Version()
		{
			//note: these UUIDs are from http://docs.python.org/2/library/uuid.html

			Uuid uuid;

			uuid = Uuid.Parse("a8098c1a-f86e-11da-bd1a-00112444be1e");
			Assert.That(uuid.Version, Is.EqualTo(1));

			uuid = Uuid.Parse("6fa459ea-ee8a-3ca4-894e-db77e160355e");
			Assert.That(uuid.Version, Is.EqualTo(3));

			uuid = Uuid.Parse("16fd2706-8baf-433b-82eb-8c7fada847da");
			Assert.That(uuid.Version, Is.EqualTo(4));

			uuid = Uuid.Parse("886313e1-3b8a-5372-9b90-0c9aee199e5d");
			Assert.That(uuid.Version, Is.EqualTo(5));
		}

		[Test]
		public void Test_Uuid_Timestamp_And_ClockSequence()
		{
			DateTime now = DateTime.UtcNow;

			// UUID V1 : 60-bit timestamp, in 100-ns ticks since 1582-10-15T00:00:00.000

			// note: this uuid was generated in Python as 'uuid.uuid1(None, 12345)' on the 2013-09-09 at 14:33:50 GMT+2
			var uuid = Uuid.Parse("14895400-194c-11e3-b039-1803deadb33f");
			Assert.That(uuid.Timestamp, Is.EqualTo(135980228304000000L));
			Assert.That(uuid.ClockSequence, Is.EqualTo(12345));
			Assert.That(uuid.Node, Is.EqualTo(0x1803deadb33f)); // no, this is not my real mac address !

			// the Timestamp should be roughly equal to the current UTC time (note: epoch is 1582-10-15T00:00:00.000)
			var epoch = new DateTime(1582, 10, 15, 0, 0, 0, DateTimeKind.Utc);
			Assert.That(epoch.AddTicks(uuid.Timestamp).ToString("O"), Is.EqualTo("2013-09-09T12:33:50.4000000Z"));

			// UUID V3 : MD5 hash of the name

			//note: this uuid was generated in Python as 'uuid.uuid3(uuid.NAMESPACE_DNS, 'foundationdb.com')'
			uuid = Uuid.Parse("4b1ddea9-d4d0-39a0-82d8-9d53e2c42a3d");
			Assert.That(uuid.Timestamp, Is.EqualTo(0x9A0D4D04B1DDEA9L));
			Assert.That(uuid.ClockSequence, Is.EqualTo(728));
			Assert.That(uuid.Node, Is.EqualTo(0x9D53E2C42A3D));

			// UUID V5 : SHA1 hash of the name
						
			//note: this uuid was generated in Python as 'uuid.uuid5(uuid.NAMESPACE_DNS, 'foundationdb.com')'
			uuid = Uuid.Parse("e449df19-a87d-5410-aaab-d5870625c6b7");
			Assert.That(uuid.Timestamp, Is.EqualTo(0x410a87de449df19L));
			Assert.That(uuid.ClockSequence, Is.EqualTo(10923));
			Assert.That(uuid.Node, Is.EqualTo(0xD5870625C6B7));
			
		}

		[Test]
		public void Test_Uuid_Ordered()
		{
			const int N = 1000;

			// create a a list of random ids
			var source = new List<Uuid>(N);
			for (int i = 0; i < N; i++) source.Add(Uuid.NewUuid());

			// sort them by their string literals
			var literals = source.Select(id => id.ToString()).ToList();
			literals.Sort();

			// sort them by their byte representation
			var bytes = source.Select(id => id.ToSlice()).ToList();
			bytes.Sort();

			// now sort the Uuid themselves
			source.Sort();

			// they all should be in the same order
			for(int i=0;i<N;i++)
			{
				Assert.That(literals[i], Is.EqualTo(source[i].ToString()));
				Assert.That(bytes[i], Is.EqualTo(source[i].ToSlice()));
			}

		}
	
	}

}

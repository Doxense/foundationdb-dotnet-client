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

namespace Doxense.Core.Tests
{
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.Self)]
	public class UuidFacts : SimpleTest
	{

		[Test]
		public void Test_Uuid_Empty()
		{
			Log(Uuid128.Empty);
			Assert.That(Uuid128.Empty.ToString(), Is.EqualTo("00000000-0000-0000-0000-000000000000"));
			Assert.That(Uuid128.Empty, Is.EqualTo(default(Uuid128)));
			Assert.That(Uuid128.Empty, Is.EqualTo(new Uuid128(new byte[16])));
			Assert.That(Uuid128.Empty, Is.EqualTo(Guid.Empty));

			Uuid128.Empty.Deconstruct(out Uuid64 hi, out Uuid64 lo);
			Assert.That(hi, Is.EqualTo(Uuid64.Empty));
			Assert.That(lo, Is.EqualTo(Uuid64.Empty));

			var tmp = new byte[16];
			tmp.AsSpan().Fill(0xAA);
			Assert.That(Uuid128.Empty.TryWriteTo(tmp), Is.True);
			Assert.That(tmp, Is.EqualTo(new byte[16]));

		}

		[Test]
		public void Test_Uuid_MaxValue()
		{
			Log(Uuid128.MaxValue);
			Assert.That(Uuid128.MaxValue.ToString(), Is.EqualTo("ffffffff-ffff-ffff-ffff-ffffffffffff"));
			Assert.That(Uuid128.MaxValue.ToByteArray(), Is.EqualTo(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }));
			Assert.That(Uuid128.MaxValue, Is.EqualTo((Uuid128) Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")));
			Assert.That(Uuid128.MaxValue, Is.EqualTo(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")));

			Uuid128.MaxValue.Deconstruct(out Uuid64 hi, out Uuid64 lo);
			Assert.That(hi, Is.EqualTo(Uuid64.MaxValue));
			Assert.That(lo, Is.EqualTo(Uuid64.MaxValue));

			var tmp = new byte[16];
			tmp.AsSpan().Fill(0xAA);
			Assert.That(Uuid128.MaxValue.TryWriteTo(tmp), Is.True);
			Assert.That(tmp, Is.EqualTo(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }));
		}

		[Test]
		public void Test_Uuid_Parse()
		{

			static void CheckSuccess(string literal, Uuid128 expected)
			{
				// string
				Assert.That(Uuid128.Parse(literal), Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid128.Parse(literal).ToByteArray(), Is.EqualTo(expected.ToByteArray()));
				Assert.That(Uuid128.Parse(literal, CultureInfo.InvariantCulture), Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid128.Parse(literal.ToUpperInvariant()), Is.EqualTo(expected), $"Should be case-insensitive: {literal}");
				Assert.That(Uuid128.Parse(literal.ToUpperInvariant()), Is.EqualTo(expected), $"Should be case-insensitive: {literal}");
				Assert.That(Uuid128.TryParse(literal, out var res), Is.True, $"Should parse: {literal}");
				Assert.That(res, Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid128.TryParse(literal, CultureInfo.InvariantCulture, out res), Is.True, $"Should parse: {literal}");
				Assert.That(res, Is.EqualTo(expected), $"Should parse: {literal}");

				// ReadOnlySpan<char>
				Assert.That(Uuid128.Parse(literal.AsSpan()), Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid128.Parse(literal.AsSpan(), CultureInfo.InvariantCulture), Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid128.Parse(literal.ToUpperInvariant().AsSpan()), Is.EqualTo(expected), $"Should be case-insensitive: {literal}");
				Assert.That(Uuid128.Parse(literal.ToUpperInvariant().AsSpan(), CultureInfo.InvariantCulture), Is.EqualTo(expected), $"Should be case-insensitive: {literal}");
				Assert.That(Uuid128.TryParse(literal.AsSpan(), out res), Is.True, $"Should parse: {literal}");
				Assert.That(res, Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid128.TryParse(literal.AsSpan(), CultureInfo.InvariantCulture, out res), Is.True, $"Should parse: {literal}");
				Assert.That(res, Is.EqualTo(expected), $"Should parse: {literal}");

#if NET8_0_OR_GREATER

				// ReadOnlySpan<byte>
				var bytes = Encoding.UTF8.GetBytes(literal).AsSpan();
				Assert.That(Uuid128.Parse(bytes).ToByteArray(), Is.EqualTo(expected.ToByteArray()));
				Assert.That(Uuid128.Parse(bytes, CultureInfo.InvariantCulture), Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid128.TryParse(bytes, out res), Is.True, $"Should parse: {literal}");
				Assert.That(res, Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid128.TryParse(bytes, CultureInfo.InvariantCulture, out res), Is.True, $"Should parse: {literal}");
				Assert.That(res, Is.EqualTo(expected), $"Should parse: {literal}");

#endif
			}

			CheckSuccess("00000000-0000-0000-0000-000000000000", Uuid128.Empty);
			CheckSuccess("ffffffff-ffff-ffff-ffff-ffffffffffff", Uuid128.MaxValue);
			CheckSuccess("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF", Uuid128.MaxValue);
			CheckSuccess("00010203-0405-0607-0809-0a0b0c0d0e0f", (Uuid128) Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f"));
			CheckSuccess("00010203-0405-0607-0809-0A0B0C0D0E0F", (Uuid128) Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f"));
			CheckSuccess("{00010203-0405-0607-0809-0a0b0c0d0e0f}", (Uuid128) Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f"));
			CheckSuccess(" \t80203aeb-14a9-4ee2-8ef9-117b3e130e17", (Uuid128) Guid.Parse("80203aeb-14a9-4ee2-8ef9-117b3e130e17")); // leading spaces are allowed
			CheckSuccess("80203aeb-14a9-4ee2-8ef9-117b3e130e17\r\n", (Uuid128) Guid.Parse("80203aeb-14a9-4ee2-8ef9-117b3e130e17")); // trailing spaces are allowed
			var guid = Guid.NewGuid();
			CheckSuccess(guid.ToString(), new Uuid128(guid));

			static void CheckFailure(string? literal, string message)
			{
				if (literal == null)
				{
					Assert.That(() => Uuid128.Parse(literal!), Throws.ArgumentNullException, message);
					Assert.That(() => Uuid128.Parse(literal!, CultureInfo.InvariantCulture), Throws.ArgumentNullException, message);
					Assert.That(Uuid128.TryParse(literal!, CultureInfo.InvariantCulture, out _), Is.False, message);
				}
				else
				{
					Assert.That(() => Uuid128.Parse(literal), Throws.InstanceOf<FormatException>(), message);
					Assert.That(() => Uuid128.Parse(literal, CultureInfo.InvariantCulture), Throws.InstanceOf<FormatException>(), message);
					Assert.That(() => Uuid128.Parse(literal.AsSpan()), Throws.InstanceOf<FormatException>(), message);
					Assert.That(() => Uuid128.Parse(literal.AsSpan(), CultureInfo.InvariantCulture), Throws.InstanceOf<FormatException>(), message);

					Assert.That(Uuid128.TryParse(literal, out _), Is.False, message);
					Assert.That(Uuid128.TryParse(literal, CultureInfo.InvariantCulture, out _), Is.False, message);
					Assert.That(Uuid128.TryParse(literal.AsSpan(), out _), Is.False, message);
					Assert.That(Uuid128.TryParse(literal.AsSpan(), CultureInfo.InvariantCulture, out _), Is.False, message);
				}
			}

			CheckFailure(null, "Null is not allowed");
			CheckFailure("", "Empty string");
			CheckFailure(" \r\n", "Only white spaces");
			CheckFailure("hello there!", "Not a guid");
			CheckFailure("123456", "Not a guid");
			CheckFailure("123456", "Not a guid");
			CheckFailure("80203aeb-14a9-4ee2-8ef9-117b3e130e17 with extra", "Extra");
			CheckFailure("80203ae-b14a9-4ee2-8ef9-117b3e130e17", "Misplaced '-'");
			CheckFailure("80203aeb-14a9-4ee2-8ef9-117b3e130e1_", "Invalid character");
		}

		[Test]
		public void Test_Uuid_From_Bytes()
		{
			{
				var uuid = new Uuid128(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 });
				Assert.That(uuid.ToString(), Is.EqualTo("00010203-0405-0607-0809-0a0b0c0d0e0f"));
				Assert.That(uuid.ToByteArray(), Is.EqualTo(new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15}));
			}
			{
				var uuid = new Uuid128(new byte[16]);
				Assert.That(uuid, Is.EqualTo(Uuid128.Empty));
				Assert.That(uuid.ToString(), Is.EqualTo("00000000-0000-0000-0000-000000000000"));
			}
			{
				var uuid = new Uuid128(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff });
				Assert.That(uuid, Is.EqualTo(Uuid128.MaxValue));
				Assert.That(uuid.ToString(), Is.EqualTo("ffffffff-ffff-ffff-ffff-ffffffffffff"));
			}
		}

		[Test]
		public void Test_Uuid_Vs_Guid()
		{
			var guid = Guid.NewGuid();

			var uuid = new Uuid128(guid);
			Assert.That(uuid.ToString(), Is.EqualTo(guid.ToString()));
			Assert.That(uuid.ToGuid(), Is.EqualTo(guid));
			Assert.That((Guid)uuid, Is.EqualTo(guid));
			Assert.That((Uuid128)guid, Is.EqualTo(uuid));
			Assert.That(Uuid128.Parse(guid.ToString()), Is.EqualTo(uuid));
			Assert.That(uuid.Equals(guid), Is.True);
			Assert.That(uuid.Equals((object) guid), Is.True);
			Assert.That(uuid == guid, Is.True);
			Assert.That(guid == uuid, Is.True);

			Assert.That(uuid.Equals(Guid.NewGuid()), Is.False);
			Assert.That(uuid == Guid.NewGuid(), Is.False);
			Assert.That(Guid.NewGuid() == uuid, Is.False);
		}

		[Test]
		public void Test_Uuid_Equality()
		{
			Assert.That(Uuid128.Empty.Equals(new Uuid128(new byte[16])), Is.True);
			Assert.That(Uuid128.Empty.Equals(Uuid128.NewUuid()), Is.False);

			var uuid1 = Uuid128.NewUuid();
			var uuid2 = Uuid128.NewUuid();

			Assert.That(uuid1.Equals(uuid1), Is.True);
			Assert.That(uuid2.Equals(uuid2), Is.True);
			Assert.That(uuid1.Equals(uuid2), Is.False);
			Assert.That(uuid2.Equals(uuid1), Is.False);

			Assert.That(uuid1.Equals((object)uuid1), Is.True);
			Assert.That(uuid2.Equals((object)uuid2), Is.True);
			Assert.That(uuid1.Equals((object)uuid2), Is.False);
			Assert.That(uuid2.Equals((object)uuid1), Is.False);

			var uuid1b = Uuid128.Parse(uuid1.ToString());
			Assert.That(uuid1b.Equals(uuid1), Is.True);
			Assert.That(uuid1b.Equals((object)uuid1), Is.True);

		}

		[Test]
		public void Test_Uuid_NewUuid()
		{
			var uuid = Uuid128.NewUuid();
			Assert.That(uuid, Is.Not.EqualTo(Uuid128.Empty));
			Assert.That(uuid.ToGuid().ToString(), Is.EqualTo(uuid.ToString()));
		}

		[Test]
		public void Test_Uuid_Increment()
		{
			var @base = Uuid128.Parse("6be5d394-03a6-42ab-aac2-89b7d9312402");
			Log(@base);
			DumpHexa(@base.ToByteArray());

			{ // +1
				var uuid = @base.Increment(1);
				Log(uuid);
				DumpHexa(uuid.ToByteArray());
				Assert.That(uuid.ToString(), Is.EqualTo("6be5d394-03a6-42ab-aac2-89b7d9312403"));
			}
			{ // +256
				var uuid = @base.Increment(256);
				Log(uuid);
				DumpHexa(uuid.ToByteArray());
				Assert.That(uuid.ToString(), Is.EqualTo("6be5d394-03a6-42ab-aac2-89b7d9312502"));
			}
			{ // almost overflow (low)
				var uuid = @base.Increment(0x553D764826CEDBFDUL); // delta nécessaire pour avoir 0xFFFFFFFFFFFFFFFF a la fin
				Log(uuid);
				DumpHexa(uuid.ToByteArray());
				Assert.That(uuid.ToString(), Is.EqualTo("6be5d394-03a6-42ab-ffff-ffffffffffff"));
			}
			{ // overflow (low)
				var uuid = @base.Increment(0x553D764826CEDBFEUL); // encore 1 de plus pour trigger l'overflow
				Log(uuid);
				DumpHexa(uuid.ToByteArray());
				Assert.That(uuid.ToString(), Is.EqualTo("6be5d394-03a6-42ac-0000-000000000000"));
			}
			{ // overflow (cascade)
				var uuid = Uuid128.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff").Increment(1);
				Log(uuid);
				DumpHexa(uuid.ToByteArray());
				Assert.That(uuid.ToString(), Is.EqualTo("00000000-0000-0000-0000-000000000000"));
			}

		}

		[Test]
		public void Test_Uuid_ToSlice()
		{
			var uuid = Uuid128.NewUuid();
			Assert.That(uuid.ToSlice().Count, Is.EqualTo(16));
			Assert.That(uuid.ToSlice().Offset, Is.GreaterThanOrEqualTo(0));
			Assert.That(uuid.ToSlice().Array, Is.Not.Null);
			Assert.That(uuid.ToSlice().Array.Length, Is.GreaterThanOrEqualTo(16));
			Assert.That(uuid.ToSlice(), Is.EqualTo(uuid.ToByteArray().AsSlice()));
			Assert.That(uuid.ToSlice().GetBytes(), Is.EqualTo(uuid.ToByteArray()));
		}

		[Test]
		public void Test_Uuid_Version()
		{
			//note: these UUIDs are from http://docs.python.org/2/library/uuid.html

			Assert.That(Uuid128.Parse("a8098c1a-f86e-11da-bd1a-00112444be1e").Version, Is.EqualTo(1));
			Assert.That(Uuid128.Parse("6fa459ea-ee8a-3ca4-894e-db77e160355e").Version, Is.EqualTo(3));
			Assert.That(Uuid128.Parse("16fd2706-8baf-433b-82eb-8c7fada847da").Version, Is.EqualTo(4));
			Assert.That(Uuid128.Parse("886313e1-3b8a-5372-9b90-0c9aee199e5d").Version, Is.EqualTo(5));
		}

		[Test]
		public void Test_Uuid_Timestamp_And_ClockSequence()
		{
			DateTime now = DateTime.UtcNow;

			// UUID V1 : 60-bit timestamp, in 100-ns ticks since 1582-10-15T00:00:00.000

			// note: this uuid was generated in Python as 'uuid.uuid1(None, 12345)' on the 2013-09-09 at 14:33:50 GMT+2
			var uuid = Uuid128.Parse("14895400-194c-11e3-b039-1803deadb33f");
			Assert.That(uuid.Timestamp, Is.EqualTo(135980228304000000L));
			Assert.That(uuid.ClockSequence, Is.EqualTo(12345));
			Assert.That(uuid.Node, Is.EqualTo(0x1803deadb33f)); // no, this is not my real mac address !

			// the Timestamp should be roughly equal to the current UTC time (note: epoch is 1582-10-15T00:00:00.000)
			var epoch = new DateTime(1582, 10, 15, 0, 0, 0, DateTimeKind.Utc);
			Assert.That(epoch.AddTicks(uuid.Timestamp).ToString("O"), Is.EqualTo("2013-09-09T12:33:50.4000000Z"));

			// UUID V3 : MD5 hash of the name

			//note: this uuid was generated in Python as 'uuid.uuid3(uuid.NAMESPACE_DNS, 'foundationdb.com')'
			uuid = Uuid128.Parse("4b1ddea9-d4d0-39a0-82d8-9d53e2c42a3d");
			Assert.That(uuid.Timestamp, Is.EqualTo(0x9A0D4D04B1DDEA9L));
			Assert.That(uuid.ClockSequence, Is.EqualTo(728));
			Assert.That(uuid.Node, Is.EqualTo(0x9D53E2C42A3D));

			// UUID V5 : SHA1 hash of the name

			//note: this uuid was generated in Python as 'uuid.uuid5(uuid.NAMESPACE_DNS, 'foundationdb.com')'
			uuid = Uuid128.Parse("e449df19-a87d-5410-aaab-d5870625c6b7");
			Assert.That(uuid.Timestamp, Is.EqualTo(0x410a87de449df19L));
			Assert.That(uuid.ClockSequence, Is.EqualTo(10923));
			Assert.That(uuid.Node, Is.EqualTo(0xD5870625C6B7));

		}

		[Test]
		public void Test_Uuid_Ordered()
		{
			const int N = 1000;

			// create a a list of random ids
			var source = new List<Uuid128>(N);
			for (int i = 0; i < N; i++) source.Add(Uuid128.NewUuid());

			// sort them by their string literals
			var literals = source.Select(id => id.ToString()).ToList();
			literals.Sort();

			// sort them by their byte representation
			var bytes = source.Select(id => id.ToSlice()).ToList();
			bytes.Sort();

			// now sort the Uuid themselves
			source.Sort();

			// they all should be in the same order
			for (int i = 0; i < N; i++)
			{
				Assert.That(literals[i], Is.EqualTo(source[i].ToString()));
				Assert.That(bytes[i], Is.EqualTo(source[i].ToSlice()));
			}

		}

	}

}

#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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

// ReSharper disable AssignNullToNotNullAttribute
namespace Doxense.Memory.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;

	[TestFixture]
	public class Uuid64Facts : FdbTest
	{
		[Test]
		public void Test_Uuid64_Empty()
		{
			Assert.That(Uuid64.Empty.ToString(), Is.EqualTo("00000000-00000000"));
			Assert.That(Uuid64.Empty, Is.EqualTo(default(Uuid64)));
			Assert.That(Uuid64.Empty, Is.EqualTo(new Uuid64(0L)));
			Assert.That(Uuid64.Empty, Is.EqualTo(new Uuid64(0UL)));
			Assert.That(Uuid64.Empty, Is.EqualTo(Uuid64.Read(new byte[8])));
		}

		[Test]
		public void Test_Uuid64_Casting()
		{
			// implicit
			Uuid64 a = (long)0;
			Uuid64 b = (long)42;
			Uuid64 c = (long)0xDEADBEEF;
			Uuid64 d = 0xBADC0FFEE0DDF00DUL;
			Uuid64 e = ulong.MaxValue;

			// ToUInt64
			Assert.That(a.ToUInt64(), Is.EqualTo(0UL));
			Assert.That(b.ToUInt64(), Is.EqualTo(42UL));
			Assert.That(c.ToUInt64(), Is.EqualTo(3735928559UL));
			Assert.That(d.ToUInt64(), Is.EqualTo(13464654573299691533UL));
			Assert.That(e.ToUInt64(), Is.EqualTo(ulong.MaxValue));

			// ToInt64
			Assert.That(a.ToInt64(), Is.EqualTo(0L));
			Assert.That(b.ToInt64(), Is.EqualTo(42L));
			Assert.That(c.ToInt64(), Is.EqualTo(3735928559L));
			Assert.That(d.ToInt64(), Is.EqualTo(-4982089500409860083L));
			Assert.That(e.ToInt64(), Is.EqualTo(-1L));

			// explicit
			Assert.That((long)a, Is.EqualTo(0));
			Assert.That((long)b, Is.EqualTo(42));
			Assert.That((long)c, Is.EqualTo(0xDEADBEEF));
			Assert.That((ulong)d, Is.EqualTo(13464654573299691533UL));
			Assert.That((ulong)e, Is.EqualTo(ulong.MaxValue));
			Assert.That((long)e, Is.EqualTo(-1L));
		}

		[Test]
		public void Test_Uuid64_ToString()
		{
			var guid = new Uuid64(0xBADC0FFEE0DDF00DUL);
			Assert.That(guid.ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));
			Assert.That(guid.ToString(), Is.EqualTo("BADC0FFE-E0DDF00D"));
			Assert.That(guid.ToString("X"), Is.EqualTo("BADC0FFEE0DDF00D"));
			Assert.That(guid.ToString("B"), Is.EqualTo("{BADC0FFE-E0DDF00D}"));
			Assert.That(guid.ToString("C"), Is.EqualTo("G2eGAUq82Hd"));

			guid = new Uuid64(0xDEADBEEFUL);
			Assert.That(guid.ToUInt64(), Is.EqualTo(0xDEADBEEFUL));
			Assert.That(guid.ToString(), Is.EqualTo("00000000-DEADBEEF"));
			Assert.That(guid.ToString("X"), Is.EqualTo("00000000DEADBEEF"));
			Assert.That(guid.ToString("B"), Is.EqualTo("{00000000-DEADBEEF}"));
			Assert.That(guid.ToString("C"), Is.EqualTo("44pZgF"));
		}

		[Test]
		public void Test_Uuid64_Parse_Hexa16()
		{
			// string

			Assert.That(Uuid64.Parse("badc0ffe-e0ddf00d").ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));
			Assert.That(Uuid64.Parse("BADC0FFE-E0DDF00D").ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL), "Should be case-insensitive");

			Assert.That(Uuid64.Parse("badc0ffee0ddf00d").ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));
			Assert.That(Uuid64.Parse("BADC0FFEE0DDF00D").ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL), "Should be case-insensitive");

			Assert.That(Uuid64.Parse("{badc0ffe-e0ddf00d}").ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));
			Assert.That(Uuid64.Parse("{BADC0FFE-E0DDF00D}").ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL), "Should be case-insensitive");

			Assert.That(Uuid64.Parse("{badc0ffee0ddf00d}").ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));
			Assert.That(Uuid64.Parse("{BADC0FFEE0DDF00D}").ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL), "should be case-insensitive");

			Assert.That(Uuid64.Parse("00000000-deadbeef").ToUInt64(), Is.EqualTo(0xDEADBEEFUL));
			Assert.That(Uuid64.Parse("{00000000-deadbeef}").ToUInt64(), Is.EqualTo(0xDEADBEEFUL));

			// errors
			Assert.That(() => Uuid64.Parse(default(string)), Throws.ArgumentNullException);
			Assert.That(() => Uuid64.Parse("hello"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.Parse("12345678-9ABCDEFG"), Throws.InstanceOf<FormatException>(), "Invalid hexa character 'G'");
			Assert.That(() => Uuid64.Parse("00000000-0000000 "), Throws.InstanceOf<FormatException>(), "Two short + extra space");
			Assert.That(() => Uuid64.Parse("zzzzzzzz-zzzzzzzz"), Throws.InstanceOf<FormatException>(), "Invalid char");
			Assert.That(() => Uuid64.Parse("badc0ffe-e0ddf00"), Throws.InstanceOf<FormatException>(), "Missing last char");
			Assert.That(() => Uuid64.Parse("baadc0ffe-e0ddf00"), Throws.InstanceOf<FormatException>(), "'-' at invalid position");
			Assert.That(() => Uuid64.Parse("badc0fe-ee0ddf00d"), Throws.InstanceOf<FormatException>(), "'-' at invalid position");
			Assert.That(() => Uuid64.Parse("badc0ffe-e0ddf00d "), Throws.InstanceOf<FormatException>(), "Extra space at the end");
			Assert.That(() => Uuid64.Parse(" badc0ffe-e0ddf00d"), Throws.InstanceOf<FormatException>(), "Extra space at the start");

			// span from string

			Assert.That(Uuid64.Parse("badc0ffe-e0ddf00d".AsSpan()).ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));
			Assert.That(Uuid64.Parse("badc0ffee0ddf00d".AsSpan()).ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));
			Assert.That(Uuid64.Parse("hello badc0ffe-e0ddf00d world!".AsSpan().Slice(6, 17)).ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));
			Assert.That(Uuid64.Parse("hello badc0ffee0ddf00d world!".AsSpan().Slice(6, 16)).ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));

			// span from char[]

			Assert.That(Uuid64.Parse("badc0ffe-e0ddf00d".ToCharArray().AsSpan()).ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));
			Assert.That(Uuid64.Parse("badc0ffee0ddf00d".ToCharArray().AsSpan()).ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));
			Assert.That(Uuid64.Parse("hello badc0ffe-e0ddf00d world!".ToCharArray().AsSpan().Slice(6, 17)).ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));
			Assert.That(Uuid64.Parse("hello badc0ffee0ddf00d world!".ToCharArray().AsSpan().Slice(6, 16)).ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));

			// span from stackalloc

			unsafe
			{
				char* buf = stackalloc char[64];
				var span = new Span<char>(buf, 64);

				span.Clear();
				"badc0ffe-e0ddf00d".AsSpan().CopyTo(span);
				Assert.That(Uuid64.Parse(span.Slice(0, 17)).ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));

				span.Clear();
				"badc0ffee0ddf00d".AsSpan().CopyTo(span);
				Assert.That(Uuid64.Parse(span.Slice(0, 16)).ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));

				span.Clear();
				"{badc0ffe-e0ddf00d}".AsSpan().CopyTo(span);
				Assert.That(Uuid64.Parse(span.Slice(0, 19)).ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));

				span.Clear();
				"{badc0ffee0ddf00d}".AsSpan().CopyTo(span);
				Assert.That(Uuid64.Parse(span.Slice(0, 18)).ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));
			}
		}

		[Test]
		public void Test_Uuid64_ToString_Base62()
		{
			char[] chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray();
			Assert.That(chars.Length, Is.EqualTo(62));

			// single digit
			for (int i = 0; i < 62;i++)
			{
				Assert.That(new Uuid64(i).ToString("C"), Is.EqualTo(chars[i].ToString()));
				Assert.That(new Uuid64(i).ToString("Z"), Is.EqualTo("0000000000" + chars[i]));
			}

			// two digits
			for (int j = 1; j < 62; j++)
			{
				var prefix = chars[j].ToString();
				for (int i = 0; i < 62; i++)
				{
					Assert.That(new Uuid64(j * 62 + i).ToString("C"), Is.EqualTo(prefix + chars[i]));
					Assert.That(new Uuid64(j * 62 + i).ToString("Z"), Is.EqualTo("000000000" + prefix + chars[i]));
				}
			}

			// 4 digits
			var rnd = new Random();
			for (int i = 0; i < 100_000; i++)
			{
				var a = rnd.Next(2) == 0 ? 0 : rnd.Next(62);
				var b = rnd.Next(2) == 0 ? 0 : rnd.Next(62);
				var c = rnd.Next(2) == 0 ? 0 : rnd.Next(62);
				var d = rnd.Next(62);

				ulong x = (ulong)a;
				x += 62 * (ulong)b;
				x += 62 * 62 * (ulong)c;
				x += 62 * 62 * 62 * (ulong)d;
				var uuid = new Uuid64(x);

				// no padding
				string expected =
					d > 0 ? ("" + chars[d] + chars[c] + chars[b] + chars[a]) :
					c > 0 ? ("" + chars[c] + chars[b] + chars[a]) :
					b > 0 ? ("" + chars[b] + chars[a]) :
					("" + chars[a]);
				Assert.That(uuid.ToString("C"), Is.EqualTo(expected));

				// padding
				Assert.That(uuid.ToString("Z"), Is.EqualTo("0000000" + chars[d] + chars[c] + chars[b] + chars[a]));
			}

			// Numbers of the form 62^n should be encoded as '1' followed by n x '0', for n from 0 to 10
			ulong val = 1;
			for (int i = 0; i <= 10; i++)
			{
				Assert.That(new Uuid64(val).ToString("C"), Is.EqualTo("1" + new string('0', i)), "62^{0}", i);
				val *= 62;
			}

			// Numbers of the form 62^n - 1 should be encoded as n x 'z', for n from 1 to 10
			val = 0;
			for (int i = 1; i <= 10; i++)
			{
				val += 61;
				Assert.That(new Uuid64(val).ToString("C"), Is.EqualTo(new string('z', i)), "62^{0} - 1", i);
				val *= 62;
			}

			// well known values
			Assert.That(new Uuid64(0xB45B07).ToString("C"), Is.EqualTo("narf"));
			Assert.That(new Uuid64(0xE0D0ED).ToString("C"), Is.EqualTo("zort"));
			Assert.That(new Uuid64(0xDEADBEEF).ToString("C"), Is.EqualTo("44pZgF"));
			Assert.That(new Uuid64(0xDEADBEEF).ToString("Z"), Is.EqualTo("0000044pZgF"));
			Assert.That(new Uuid64(0xBADC0FFEE0DDF00DUL).ToString("C"), Is.EqualTo("G2eGAUq82Hd"));
			Assert.That(new Uuid64(0xBADC0FFEE0DDF00DUL).ToString("Z"), Is.EqualTo("G2eGAUq82Hd"));

			Assert.That(new Uuid64(255).ToString("C"), Is.EqualTo("47"));
			Assert.That(new Uuid64(ushort.MaxValue).ToString("C"), Is.EqualTo("H31"));
			Assert.That(new Uuid64(uint.MaxValue).ToString("C"), Is.EqualTo("4gfFC3"));
			Assert.That(new Uuid64(ulong.MaxValue - 1).ToString("C"), Is.EqualTo("LygHa16AHYE"));
			Assert.That(new Uuid64(ulong.MaxValue).ToString("C"), Is.EqualTo("LygHa16AHYF"));
		}

		[Test]
		public void Test_Uuid64_Parse_Base62()
		{

			Assert.That(Uuid64.FromBase62("").ToUInt64(), Is.EqualTo(0));
			Assert.That(Uuid64.FromBase62("0").ToUInt64(), Is.EqualTo(0));
			Assert.That(Uuid64.FromBase62("9").ToUInt64(), Is.EqualTo(9));
			Assert.That(Uuid64.FromBase62("A").ToUInt64(), Is.EqualTo(10));
			Assert.That(Uuid64.FromBase62("Z").ToUInt64(), Is.EqualTo(35));
			Assert.That(Uuid64.FromBase62("a").ToUInt64(), Is.EqualTo(36));
			Assert.That(Uuid64.FromBase62("z").ToUInt64(), Is.EqualTo(61));
			Assert.That(Uuid64.FromBase62("10").ToUInt64(), Is.EqualTo(62));
			Assert.That(Uuid64.FromBase62("zz").ToUInt64(), Is.EqualTo(3843));
			Assert.That(Uuid64.FromBase62("100").ToUInt64(), Is.EqualTo(3844));
			Assert.That(Uuid64.FromBase62("zzzzzzzzzz").ToUInt64(), Is.EqualTo(839299365868340223UL));
			Assert.That(Uuid64.FromBase62("10000000000").ToUInt64(), Is.EqualTo(839299365868340224UL));
			Assert.That(Uuid64.FromBase62("LygHa16AHYF").ToUInt64(), Is.EqualTo(ulong.MaxValue), "ulong.MaxValue in base 62");

			// well known values

			Assert.That(Uuid64.FromBase62("narf").ToUInt64(), Is.EqualTo(0xB45B07));
			Assert.That(Uuid64.FromBase62("zort").ToUInt64(), Is.EqualTo(0xE0D0ED));
			Assert.That(Uuid64.FromBase62("44pZgF").ToUInt64(), Is.EqualTo(0xDEADBEEF));
			Assert.That(Uuid64.FromBase62("0000044pZgF").ToUInt64(), Is.EqualTo(0xDEADBEEF));

			Assert.That(Uuid64.FromBase62("G2eGAUq82Hd").ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));

			Assert.That(Uuid64.FromBase62("4gfFC3").ToUInt64(), Is.EqualTo(uint.MaxValue));
			Assert.That(Uuid64.FromBase62("000004gfFC3").ToUInt64(), Is.EqualTo(uint.MaxValue));


			// invalid chars
			Assert.That(() => Uuid64.FromBase62("/"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.FromBase62("@"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.FromBase62("["), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.FromBase62("`"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.FromBase62("{"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.FromBase62("zaz/"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.FromBase62("z/o&r=g"), Throws.InstanceOf<FormatException>());

			// overflow
			Assert.That(() => Uuid64.FromBase62("zzzzzzzzzzz"), Throws.InstanceOf<OverflowException>(), "62^11 - 1 => OVERFLOW");
			Assert.That(() => Uuid64.FromBase62("LygHa16AHYG"), Throws.InstanceOf<OverflowException>(), "ulong.MaxValue + 1 => OVERFLOW");

			// invalid length
			Assert.That(() => Uuid64.FromBase62(default(string)), Throws.ArgumentNullException);
			Assert.That(() => Uuid64.FromBase62("100000000000"), Throws.InstanceOf<FormatException>(), "62^11 => TOO BIG");

		}

		[Test]
		public void Test_Uuid64_NewUid()
		{
			var a = Uuid64.NewUuid();
			var b = Uuid64.NewUuid();
			Assert.That(a.ToUInt64(), Is.Not.EqualTo(b.ToUInt64()));
			Assert.That(a, Is.Not.EqualTo(b));

			const int N = 1_000;
			var uids = new HashSet<ulong>();
			for (int i = 0; i < N; i++)
			{
				var uid = Uuid64.NewUuid();
				if (uids.Contains(uid.ToUInt64())) Assert.Fail("Duplicate Uuid64 generated: {0}", uid);
				uids.Add(uid.ToUInt64());
			}
			Assert.That(uids.Count, Is.EqualTo(N));
		}

		[Test]
		public void Test_Uuid64RandomGenerator_NewUid()
		{
			var gen = Uuid64.RandomGenerator.Default;
			Assert.That(gen, Is.Not.Null);

			var a = gen.NewUuid();
			var b = gen.NewUuid();
			Assert.That(a.ToUInt64(), Is.Not.EqualTo(b.ToUInt64()));
			Assert.That(a, Is.Not.EqualTo(b));

			const int N = 1_000;
			var uids = new HashSet<ulong>();
			for (int i = 0; i < N; i++)
			{
				var uid = gen.NewUuid();
				if (uids.Contains(uid.ToUInt64())) Assert.Fail("Duplicate Uuid64 generated: {0}", uid);
				uids.Add(uid.ToUInt64());
			}
			Assert.That(uids.Count, Is.EqualTo(N));
		}

		[Test]
		public void Test_Uuid64_Equality_Check()
		{
			var a = new Uuid64(42);
			var b = new Uuid64(42);
			var c = new Uuid64(40) + 2;
			var d = new Uuid64(0xDEADBEEF);

			// Equals(Uuid64)
			Assert.That(a.Equals(a), Is.True, "a == a");
			Assert.That(a.Equals(b), Is.True, "a == b");
			Assert.That(a.Equals(c), Is.True, "a == c");
			Assert.That(a.Equals(d), Is.False, "a != d");

			// == Uuid64
			Assert.That(a == b, Is.True, "a == b");
			Assert.That(a == c, Is.True, "a == c");
			Assert.That(a == d, Is.False, "a != d");

			// != Uuid64
			Assert.That(a != b, Is.False, "a == b");
			Assert.That(a != c, Is.False, "a == c");
			Assert.That(a != d, Is.True, "a != d");

			// == numbers
			Assert.That(a == 42L, Is.True, "a == 42");
			Assert.That(a == 42UL, Is.True, "a == 42");
			Assert.That(d == 42L, Is.False, "d != 42");
			Assert.That(d == 42UL, Is.False, "d != 42");

			// != numbers
			Assert.That(a != 42L, Is.False, "a == 42");
			Assert.That(a != 42UL, Is.False, "a == 42");
			Assert.That(d != 42L, Is.True, "d != 42");
			Assert.That(d != 42UL, Is.True, "d != 42");

			// Equals(objecct)
			Assert.That(a.Equals((object)a), Is.True, "a == a");
			Assert.That(a.Equals((object)b), Is.True, "a == b");
			Assert.That(a.Equals((object)c), Is.True, "a == c");
			Assert.That(a.Equals((object)d), Is.False, "a != d");
			Assert.That(a.Equals((object)42L), Is.True, "a == 42");
			Assert.That(a.Equals((object)42UL), Is.True, "a == 42");
			Assert.That(d.Equals((object)42L), Is.False, "d != 42");
			Assert.That(d.Equals((object)42UL), Is.False, "d != 42");

		}

		[Test]
		public void Test_Uuid64_Ordering()
		{
			var a = new Uuid64(42);
			var a2 = new Uuid64(42);
			var b = new Uuid64(77);

			Assert.That(a.CompareTo(a), Is.EqualTo(0));
			Assert.That(a.CompareTo(b), Is.EqualTo(-1));
			Assert.That(b.CompareTo(a), Is.EqualTo(+1));

			Assert.That(a < b, Is.True, "a < b");
			Assert.That(a <= b, Is.True, "a <= b");
			Assert.That(a < a2, Is.False, "a < a");
			Assert.That(a <= a2, Is.True, "a <= a");

			Assert.That(a > b, Is.False, "a > b");
			Assert.That(a >= b, Is.False, "a >= b");
			Assert.That(a > a2, Is.False, "a > a");
			Assert.That(a >= a2, Is.True, "a >= a");

			// parsed from string
			Assert.That(Uuid64.Parse("137bcf31-0c8873a2") < Uuid64.Parse("604bdf8a-2512b4ad"), Is.True);
			Assert.That(Uuid64.Parse("d8f17a26-82adb1a4") < Uuid64.Parse("22abbf33-1b2c1db0"), Is.False);
			Assert.That(Uuid64.Parse("{137bcf31-0c8873a2}") > Uuid64.Parse("{604bdf8a-2512b4ad}"), Is.False);
			Assert.That(Uuid64.Parse("{d8f17a26-82adb1a4}") > Uuid64.Parse("{22abbf33-1b2c1db0}"), Is.True);
			Assert.That(Uuid64.FromBase62("2w6CTjUiXVp") < Uuid64.FromBase62("DVM0UnynZ1Q"), Is.True);
			Assert.That(Uuid64.FromBase62("0658JY2ORSJ") > Uuid64.FromBase62("FMPaNaMEUWc"), Is.False);

			// verify byte ordering
			var c = new Uuid64(0x0000000100000002);
			var d = new Uuid64(0x0000000200000001);
			Assert.That(c.CompareTo(d), Is.EqualTo(-1));
			Assert.That(d.CompareTo(c), Is.EqualTo(+1));

			// verify that we can sort an array of Uuid64
			var uids = new Uuid64[100];
			for (int i = 0; i < uids.Length; i++)
			{
				uids[i] = Uuid64.NewUuid();
			}
			Assume.That(uids, Is.Not.Ordered, "This can happen with a very small probability. Please try again");
			Array.Sort(uids);
			Assert.That(uids, Is.Ordered);

			// ordering should be preserved in integer or textual form

			Assert.That(uids.Select(x => x.ToUInt64()), Is.Ordered, "order should be preserved when ordering by unsigned value");
			//note: ToInt64() will not work because of negative values
			Assert.That(uids.Select(x => x.ToString()), Is.Ordered.Using<string>(StringComparer.Ordinal), "order should be preserved when ordering by text (hexa)");
			Assert.That(uids.Select(x => x.ToString("Z")), Is.Ordered.Using<string>(StringComparer.Ordinal), "order should be preserved when ordering by text (base62)");
			//note: ToString("C") will not work for ordering because it will produce "z" > "aa", instead of expected "0z" < "aa"
		}

		[Test]
		public void Test_Uuid64_Arithmetic()
		{
			var uid = Uuid64.Empty;

			Assert.That(uid + 42L, Is.EqualTo(new Uuid64(42)));
			Assert.That(uid + 42UL, Is.EqualTo(new Uuid64(42)));
			uid++;
			Assert.That(uid.ToInt64(), Is.EqualTo(1));
			uid++;
			Assert.That(uid.ToInt64(), Is.EqualTo(2));
			uid--;
			Assert.That(uid.ToInt64(), Is.EqualTo(1));
			uid--;
			Assert.That(uid.ToInt64(), Is.EqualTo(0));

			uid = Uuid64.NewUuid();

			Assert.That(uid + 123L, Is.EqualTo(new Uuid64(uid.ToInt64() + 123)));
			Assert.That(uid + 123UL, Is.EqualTo(new Uuid64(uid.ToUInt64() + 123)));

			Assert.That(uid - 123L, Is.EqualTo(new Uuid64(uid.ToInt64() - 123)));
			Assert.That(uid - 123UL, Is.EqualTo(new Uuid64(uid.ToUInt64() - 123)));
		}

		[Test]
		public void Test_Uuid64_Read_From_Bytes()
		{
			// test buffer with included padding
			byte[] buf = { 0x55, 0x55, 0x55, 0x55, /* start */ 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF, /* stop */ 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA };
			var original = Uuid64.Parse("01234567-89ABCDEF");
			Assume.That(original.ToUInt64(), Is.EqualTo(0x0123456789ABCDEF));

			// ReadOnlySpan<byte>
			Assert.That(Uuid64.Read(buf.AsSpan(4, 8)), Is.EqualTo(original));

			// Slice
			Assert.That(Uuid64.Read(buf.AsSlice(4, 8)), Is.EqualTo(original));

			// byte[]
			Assert.That(Uuid64.Read(buf.AsSlice(4, 8).GetBytesOrEmpty()), Is.EqualTo(original));

			unsafe
			{
				fixed (byte* ptr = &buf[4])
				{
					Assert.That(Uuid64.Read(new ReadOnlySpan<byte>(ptr, 8)), Is.EqualTo(original));
				}
			}
		}

		[Test]
		public void Test_UUid64_WriteTo()
		{
			var original = Uuid64.Parse("01234567-89ABCDEF");
			Assume.That(original.ToUInt64(), Is.EqualTo(0x0123456789ABCDEF));

			// span with more space
			var scratch = MutableSlice.Repeat(0xAA, 16);
			original.WriteTo(scratch.Span);
			Assert.That(scratch.ToString("X"), Is.EqualTo("01 23 45 67 89 AB CD EF AA AA AA AA AA AA AA AA"));

			// span with no offset and exact size
			scratch = MutableSlice.Repeat(0xAA, 16);
			original.WriteTo(scratch.Span.Slice(0, 8));
			Assert.That(scratch.ToString("X"), Is.EqualTo("01 23 45 67 89 AB CD EF AA AA AA AA AA AA AA AA"));

			// span with offset
			scratch = MutableSlice.Repeat(0xAA, 16);
			original.WriteTo(scratch.Span.Slice(4));
			Assert.That(scratch.ToString("X"), Is.EqualTo("AA AA AA AA 01 23 45 67 89 AB CD EF AA AA AA AA"));

			// span with offset and exact size
			scratch = MutableSlice.Repeat(0xAA, 16);
			original.WriteTo(scratch.Span.Slice(4, 8));
			Assert.That(scratch.ToString("X"), Is.EqualTo("AA AA AA AA 01 23 45 67 89 AB CD EF AA AA AA AA"));

			unsafe
			{
				Span<byte> buf = stackalloc byte[16];
				buf.Fill(0xAA);

				original.WriteToUnsafe(buf.Slice(2));
				Assert.That(buf.ToArray().AsSlice().ToString("X"), Is.EqualTo("AA AA 01 23 45 67 89 AB CD EF AA AA AA AA AA AA"));
			}

			// errors

			Assert.That(() => original.WriteTo(Span<byte>.Empty), Throws.InstanceOf<ArgumentException>(), "Target buffer is empty");

			scratch = MutableSlice.Repeat(0xAA, 16);
			Assert.That(() => original.WriteTo(scratch.Span.Slice(0, 7)), Throws.InstanceOf<ArgumentException>(), "Target buffer is too small");
			Assert.That(scratch.ToString("X"), Is.EqualTo("AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA"), "Buffer should not have been overwritten!");

		}

		[Test]
		public void Test_Uuid64_TryWriteTo()
		{
			var original = Uuid64.Parse("01234567-89ABCDEF");
			Assume.That(original.ToUInt64(), Is.EqualTo(0x0123456789ABCDEF));

			// span with more space
			var scratch = MutableSlice.Repeat(0xAA, 16);
			Assert.That(original.TryWriteTo(scratch.Span), Is.True);
			Assert.That(scratch.ToString("X"), Is.EqualTo("01 23 45 67 89 AB CD EF AA AA AA AA AA AA AA AA"));

			// span with no offset and exact size
			scratch = MutableSlice.Repeat(0xAA, 16);
			Assert.That(original.TryWriteTo(scratch.Span.Slice(0, 8)), Is.True);
			Assert.That(scratch.ToString("X"), Is.EqualTo("01 23 45 67 89 AB CD EF AA AA AA AA AA AA AA AA"));

			// span with offset
			scratch = MutableSlice.Repeat(0xAA, 16);
			Assert.That(original.TryWriteTo(scratch.Span.Slice(4)), Is.True);
			Assert.That(scratch.ToString("X"), Is.EqualTo("AA AA AA AA 01 23 45 67 89 AB CD EF AA AA AA AA"));

			// span with offset and exact size
			scratch = MutableSlice.Repeat(0xAA, 16);
			Assert.That(original.TryWriteTo(scratch.Span.Slice(4, 8)), Is.True);
			Assert.That(scratch.ToString("X"), Is.EqualTo("AA AA AA AA 01 23 45 67 89 AB CD EF AA AA AA AA"));

			// errors

			Assert.That(original.TryWriteTo(Span<byte>.Empty), Is.False, "Target buffer is empty");

			scratch = MutableSlice.Repeat(0xAA, 16);
			Assert.That(original.TryWriteTo(scratch.Span.Slice(0, 7)), Is.False, "Target buffer is too small");
			Assert.That(scratch.ToString("X"), Is.EqualTo("AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA"), "Buffer should not have been overwritten!");

		}

	}

}

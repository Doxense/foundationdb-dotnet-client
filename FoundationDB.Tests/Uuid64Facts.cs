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
	public class Uuid64Facts
	{
		[Test]
		public void Test_Uuid64_Empty()
		{
			Assert.That(Uuid64.Empty.ToString(), Is.EqualTo("00000000-00000000"));
			Assert.That(Uuid64.Empty, Is.EqualTo(default(Uuid64)));
			Assert.That(Uuid64.Empty, Is.EqualTo(new Uuid64(0L)));
			Assert.That(Uuid64.Empty, Is.EqualTo(new Uuid64(0UL)));
			Assert.That(Uuid64.Empty, Is.EqualTo(new Uuid64(new byte[8])));
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

			// explict
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
			Assert.That(guid.ToString(), Is.EqualTo("badc0ffe-e0ddf00d"));
			Assert.That(guid.ToString("X"), Is.EqualTo("badc0ffee0ddf00d"));
			Assert.That(guid.ToString("B"), Is.EqualTo("{badc0ffe-e0ddf00d}"));
			Assert.That(guid.ToString("C"), Is.EqualTo("G2eGAUq82Hd"));

			guid = new Uuid64(0xDEADBEEFUL);
			Assert.That(guid.ToUInt64(), Is.EqualTo(0xDEADBEEFUL));
			Assert.That(guid.ToString(), Is.EqualTo("00000000-deadbeef"));
			Assert.That(guid.ToString("X"), Is.EqualTo("00000000deadbeef"));
			Assert.That(guid.ToString("B"), Is.EqualTo("{00000000-deadbeef}"));
			Assert.That(guid.ToString("C"), Is.EqualTo("44pZgF"));
		}

		[Test]
		public void Test_Uuid64_Parse_Hexa16()
		{
			var uuid = Uuid64.Parse("badc0ffe-e0ddf00d");
			Assert.That(uuid.ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));

			uuid = Uuid64.Parse("{badc0ffe-e0ddf00d}");
			Assert.That(uuid.ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));

			uuid = Uuid64.Parse("00000000-deadbeef");
			Assert.That(uuid.ToUInt64(), Is.EqualTo(0xDEADBEEFUL));

			uuid = Uuid64.Parse("{00000000-deadbeef}");
			Assert.That(uuid.ToUInt64(), Is.EqualTo(0xDEADBEEFUL));
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
			for (int i = 0; i < 100 * 1000; i++)
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

			Assert.That(Uuid64.Parse("0").ToUInt64(), Is.EqualTo(0));
			Assert.That(Uuid64.Parse("9").ToUInt64(), Is.EqualTo(9));
			Assert.That(Uuid64.Parse("A").ToUInt64(), Is.EqualTo(10));
			Assert.That(Uuid64.Parse("Z").ToUInt64(), Is.EqualTo(35));
			Assert.That(Uuid64.Parse("a").ToUInt64(), Is.EqualTo(36));
			Assert.That(Uuid64.Parse("z").ToUInt64(), Is.EqualTo(61));
			Assert.That(Uuid64.Parse("10").ToUInt64(), Is.EqualTo(62));
			Assert.That(Uuid64.Parse("zz").ToUInt64(), Is.EqualTo(3843));
			Assert.That(Uuid64.Parse("100").ToUInt64(), Is.EqualTo(3844));
			Assert.That(Uuid64.Parse("zzzzzzzzzz").ToUInt64(), Is.EqualTo(839299365868340223UL));
			Assert.That(Uuid64.Parse("10000000000").ToUInt64(), Is.EqualTo(839299365868340224UL));
			Assert.That(Uuid64.Parse("LygHa16AHYF").ToUInt64(), Is.EqualTo(ulong.MaxValue), "ulong.MaxValue in base 62");

			// well known values

			Assert.That(Uuid64.Parse("narf").ToUInt64(), Is.EqualTo(0xB45B07));
			Assert.That(Uuid64.Parse("zort").ToUInt64(), Is.EqualTo(0xE0D0ED));
			Assert.That(Uuid64.Parse("44pZgF").ToUInt64(), Is.EqualTo(0xDEADBEEF));
			Assert.That(Uuid64.Parse("0000044pZgF").ToUInt64(), Is.EqualTo(0xDEADBEEF));

			Assert.That(Uuid64.Parse("G2eGAUq82Hd").ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));

			Assert.That(Uuid64.Parse("4gfFC3").ToUInt64(), Is.EqualTo(uint.MaxValue));
			Assert.That(Uuid64.Parse("000004gfFC3").ToUInt64(), Is.EqualTo(uint.MaxValue));


			// invalid chars
			Assert.That(() => Uuid64.Parse("/"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.Parse("@"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.Parse("["), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.Parse("`"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.Parse("{"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.Parse("zaz/"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.Parse("z/o&r=g"), Throws.InstanceOf<FormatException>());

			// overflow
			Assert.That(() => Uuid64.Parse("zzzzzzzzzzz"), Throws.InstanceOf<OverflowException>(), "62^11 - 1 => OVERFLOW");
			Assert.That(() => Uuid64.Parse("LygHa16AHYG"), Throws.InstanceOf<OverflowException>(), "ulong.MaxValue + 1 => OVERFLOW");

			// invalid length
			Assert.That(() => Uuid64.Parse(null), Throws.InstanceOf<ArgumentNullException>());
			Assert.That(() => Uuid64.Parse(""), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.Parse("100000000000"), Throws.InstanceOf<FormatException>(), "62^11 => TOO BIG");

		}

		[Test]
		public void Test_Uuid64_NewUid()
		{
			var a = Uuid64.NewUuid();
			var b = Uuid64.NewUuid();
			Assert.That(a.ToUInt64(), Is.Not.EqualTo(b.ToUInt64()));
			Assert.That(a, Is.Not.EqualTo(b));

			const int N = 1 * 1000;
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
		public void Test_Uuid64RangomGenerator_NewUid()
		{
			var gen = Uuid64RandomGenerator.Default;
			Assert.That(gen, Is.Not.Null);

			var a = gen.NewUuid();
			var b = gen.NewUuid();
			Assert.That(a.ToUInt64(), Is.Not.EqualTo(b.ToUInt64()));
			Assert.That(a, Is.Not.EqualTo(b));

			const int N = 1 * 1000;
			var uids = new HashSet<ulong>();
			for (int i = 0; i < N; i++)
			{
				var uid = gen.NewUuid();
				if (uids.Contains(uid.ToUInt64())) Assert.Fail("Duplicate Uuid64 generated: {0}", uid);
				uids.Add(uid.ToUInt64());
			}
			Assert.That(uids.Count, Is.EqualTo(N));
		}

	}

}

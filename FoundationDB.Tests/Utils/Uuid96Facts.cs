﻿#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
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
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using NUnit.Framework;

	[TestFixture]
	public class Uuid96Facts : FdbTest
	{
		[Test]
		public void Test_Uuid96_Empty()
		{
			Assert.That(Uuid96.Empty.ToString(), Is.EqualTo("00000000-00000000-00000000"));
			Assert.That(Uuid96.Empty, Is.EqualTo(default(Uuid96)));
			Assert.That(Uuid96.Empty, Is.EqualTo(new Uuid96(0, 0UL)));
			Assert.That(Uuid96.Empty, Is.EqualTo(new Uuid96(0, 0U, 0U)));
			Assert.That(Uuid96.Empty, Is.EqualTo(new Uuid96(0, 0L)));
			Assert.That(Uuid96.Empty, Is.EqualTo(new Uuid96(0, 0, 0)));
			Assert.That(Uuid96.Empty, Is.EqualTo(Uuid96.Read(new byte[12])));
		}

		[Test]
		public void Test_Uuid96_ToString()
		{
			var guid = new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL);
			Assert.That(guid.ToString(), Is.EqualTo("89ABCDEF-BADC0FFE-E0DDF00D"));
			Assert.That(guid.ToString("d"), Is.EqualTo("89abcdef-badc0ffe-e0ddf00d"));
			Assert.That(guid.ToString("X"), Is.EqualTo("89ABCDEFBADC0FFEE0DDF00D"));
			Assert.That(guid.ToString("x"), Is.EqualTo("89abcdefbadc0ffee0ddf00d"));
			Assert.That(guid.ToString("B"), Is.EqualTo("{89ABCDEF-BADC0FFE-E0DDF00D}"));
			Assert.That(guid.ToString("b"), Is.EqualTo("{89abcdef-badc0ffe-e0ddf00d}"));
		}

		[Test]
		public void Test_Uuid96_Parse_Hexa16()
		{
			// string

			Assert.That(Uuid96.Parse("89abcdef-badc0ffe-e0ddf00d"), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid96.Parse("89ABCDEF-BADC0FFE-E0DDF00D"), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)), "Should be case-insensitive");

			Assert.That(Uuid96.Parse("89abcdefbadc0ffee0ddf00d"), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid96.Parse("89ABCDEFBADC0FFEE0DDF00D"), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)), "Should be case-insensitive");

			Assert.That(Uuid96.Parse("{89abcdef-badc0ffe-e0ddf00d}"), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid96.Parse("{89ABCDEF-BADC0FFE-E0DDF00D}"), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)), "Should be case-insensitive");

			Assert.That(Uuid96.Parse("{89abcdefbadc0ffee0ddf00d}"), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid96.Parse("{89ABCDEFBADC0FFEE0DDF00D}"), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)), "should be case-insensitive");

			Assert.That(Uuid96.Parse("ffffffff-00000000-deadbeef"), Is.EqualTo(new Uuid96(0xFFFFFFFF, 0, 0xDEADBEEFU)));
			Assert.That(Uuid96.Parse("{ffffffff-00000000-deadbeef}"), Is.EqualTo(new Uuid96(0xFFFFFFFF, 0, 0xDEADBEEFU)));

			// errors
			Assert.That(() => Uuid96.Parse(default(string)), Throws.ArgumentNullException);
			Assert.That(() => Uuid96.Parse("hello"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid96.Parse("89abcdef-12345678-9ABCDEFG"), Throws.InstanceOf<FormatException>(), "Invalid hexa character 'G'");
			Assert.That(() => Uuid96.Parse("0000-00000000-0000000 "), Throws.InstanceOf<FormatException>(), "Two short + extra space");
			Assert.That(() => Uuid96.Parse("zzzz-zzzzzzzz-zzzzzzzz"), Throws.InstanceOf<FormatException>(), "Invalid char");
			Assert.That(() => Uuid96.Parse("89abcdef-badc0ffe-e0ddf00"), Throws.InstanceOf<FormatException>(), "Missing last char");
			Assert.That(() => Uuid96.Parse("89abcdefbaadc0ffe-e0ddf00"), Throws.InstanceOf<FormatException>(), "'-' at invalid position");
			Assert.That(() => Uuid96.Parse("89abcdef-badc0fe-ee0ddf00d"), Throws.InstanceOf<FormatException>(), "'-' at invalid position");
			Assert.That(() => Uuid96.Parse("89abcdefb-adc0ffe-e0ddf00d"), Throws.InstanceOf<FormatException>(), "'-' at invalid position");
			Assert.That(() => Uuid96.Parse("89abcdef-badc0ffe-e0ddf00d "), Throws.InstanceOf<FormatException>(), "Extra space at the end");
			Assert.That(() => Uuid96.Parse(" 89abcdef-badc0ffe-e0ddf00d"), Throws.InstanceOf<FormatException>(), "Extra space at the start");

#if ENABLE_SPAN
			// span from string

			Assert.That(Uuid96.Parse("89abcdef-badc0ffe-e0ddf00d".AsSpan()), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid96.Parse("89abcdefbadc0ffee0ddf00d".AsSpan()), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid96.Parse("hello 89abcdef-badc0ffe-e0ddf00d world!".AsSpan().Slice(6, 26)), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid96.Parse("hello 89abcdefbadc0ffee0ddf00d world!".AsSpan().Slice(6, 24)), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)));

			// span from char[]

			Assert.That(Uuid96.Parse("89abcdef-badc0ffe-e0ddf00d".ToCharArray().AsSpan()), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid96.Parse("89abcdefbadc0ffee0ddf00d".ToCharArray().AsSpan()), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid96.Parse("hello 89abcdef-badc0ffe-e0ddf00d world!".ToCharArray().AsSpan().Slice(6, 26)), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid96.Parse("hello 89abcdefbadc0ffee0ddf00d world!".ToCharArray().AsSpan().Slice(6, 24)), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)));

			// span from stackalloc

			unsafe
			{
				char* buf = stackalloc char[64];
				var span = new Span<char>(buf, 64);

				span.Clear();
				"89abcdef-badc0ffe-e0ddf00d".AsSpan().CopyTo(span);
				Assert.That(Uuid96.Parse(span.Slice(0, 26)), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)));

				span.Clear();
				"89abcdefbadc0ffee0ddf00d".AsSpan().CopyTo(span);
				Assert.That(Uuid96.Parse(span.Slice(0, 24)), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)));

				span.Clear();
				"{89abcdef-badc0ffe-e0ddf00d}".AsSpan().CopyTo(span);
				Assert.That(Uuid96.Parse(span.Slice(0, 28)), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)));

				span.Clear();
				"{89abcdefbadc0ffee0ddf00d}".AsSpan().CopyTo(span);
				Assert.That(Uuid96.Parse(span.Slice(0, 26)), Is.EqualTo(new Uuid96(0x89ABCDEF, 0xBADC0FFEE0DDF00DUL)));
			}
#endif
		}

		[Test]
		public void Test_Uuid96_NewUid()
		{
			var a = Uuid96.NewUuid();
			var b = Uuid96.NewUuid();
			Assert.That(a, Is.Not.EqualTo(b));

			const int N = 1_000;
			var uids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < N; i++)
			{
				var uid = Uuid96.NewUuid();
				string s = uid.ToString();
				if (uids.Contains(s)) Assert.Fail("Duplicate Uuid96 generated: {0}", uid);
				uids.Add(s);
			}
			Assert.That(uids.Count, Is.EqualTo(N));
		}

		[Test]
		public void Test_Uuid96RandomGenerator_NewUid()
		{
			var gen = Uuid96.RandomGenerator.Default;
			Assert.That(gen, Is.Not.Null);

			var a = gen.NewUuid();
			var b = gen.NewUuid();
			Assert.That(a, Is.Not.EqualTo(b));

			const int N = 1_000;
			var uids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < N; i++)
			{
				var uid = gen.NewUuid();
				string s = uid.ToString();
				if (uids.Contains(s)) Assert.Fail("Duplicate Uuid96 generated: {0}", uid);
				uids.Add(s);
			}
			Assert.That(uids.Count, Is.EqualTo(N));
		}

		[Test]
		public void Test_Uuid96_Equality_Check()
		{
			var a = new Uuid96(123, 42);
			var b = new Uuid96(123, 42);
			var c = new Uuid96(123, 40) + 2;
			var d = new Uuid96(123, 0xDEADBEEF);
			var e = new Uuid96(124, 42);

			// Equals(Uuid96)
			Assert.That(a.Equals(a), Is.True, "a == a");
			Assert.That(a.Equals(b), Is.True, "a == b");
			Assert.That(a.Equals(c), Is.True, "a == c");
			Assert.That(a.Equals(d), Is.False, "a != d");
			Assert.That(a.Equals(e), Is.False, "a != e");

			// == Uuid96
			Assert.That(a == b, Is.True, "a == b");
			Assert.That(a == c, Is.True, "a == c");
			Assert.That(a == d, Is.False, "a != d");
			Assert.That(a == e, Is.False, "a != e");

			// != Uuid96
			Assert.That(a != b, Is.False, "a == b");
			Assert.That(a != c, Is.False, "a == c");
			Assert.That(a != d, Is.True, "a != d");
			Assert.That(a != e, Is.True, "a != e");

			// Equals(objecct)
			Assert.That(a.Equals((object)a), Is.True, "a == a");
			Assert.That(a.Equals((object)b), Is.True, "a == b");
			Assert.That(a.Equals((object)c), Is.True, "a == c");
			Assert.That(a.Equals((object)d), Is.False, "a != d");
			Assert.That(a.Equals((object)e), Is.False, "a != e");
		}

		[Test]
		public void Test_Uuid96_Ordering()
		{
			var a = new Uuid96(123, 42);
			var a2 = new Uuid96(123, 42);
			var b = new Uuid96(123, 77);
			var c = new Uuid96(124, 41);

			Assert.That(a.CompareTo(a), Is.EqualTo(0));
			Assert.That(a.CompareTo(b), Is.EqualTo(-1));
			Assert.That(b.CompareTo(a), Is.EqualTo(+1));
			Assert.That(c.CompareTo(a), Is.EqualTo(+1));

			Assert.That(a < b, Is.True, "a < b");
			Assert.That(a <= b, Is.True, "a <= b");
			Assert.That(a < a2, Is.False, "a < a");
			Assert.That(a <= a2, Is.True, "a <= a");
			Assert.That(a < c, Is.True, "a < c");

			Assert.That(a > b, Is.False, "a > b");
			Assert.That(a >= b, Is.False, "a >= b");
			Assert.That(a > a2, Is.False, "a > a");
			Assert.That(a >= a2, Is.True, "a >= a");
			Assert.That(a > c, Is.False, "a > c");

			// parsed from string
			Assert.That(Uuid96.Parse("12345678-137bcf31-0c8873a2") < Uuid96.Parse("12345678-604bdf8a-2512b4ad"), Is.True);
			Assert.That(Uuid96.Parse("12345678-d8f17a26-82adb1a4") < Uuid96.Parse("12345678-22abbf33-1b2c1db0"), Is.False);
			Assert.That(Uuid96.Parse("{12345678-137bcf31-0c8873a2}") > Uuid96.Parse("{12345678-604bdf8a-2512b4ad}"), Is.False);
			Assert.That(Uuid96.Parse("{12345678-d8f17a26-82adb1a4}") > Uuid96.Parse("{12345678-22abbf33-1b2c1db0}"), Is.True);

			// verify byte ordering
			var d = new Uuid96(0x00010002, 0x0000000300000004);
			var e = new Uuid96(0x00040003, 0x0000000200000001);
			Assert.That(d.CompareTo(e), Is.LessThan(0));
			Assert.That(e.CompareTo(d), Is.GreaterThan(0));

			// verify that we can sort an array of Uuid96
			var uids = new Uuid96[100];
			for (int i = 0; i < uids.Length; i++)
			{
				uids[i] = Uuid96.NewUuid();
			}
			Assume.That(uids, Is.Not.Ordered, "This can happen with a very small probability. Please try again");
			Array.Sort(uids);
			Assert.That(uids, Is.Ordered);

			// ordering should be preserved in integer or textual form

			Assert.That(uids.Select(x => x.ToString()), Is.Ordered.Using<string>(StringComparer.Ordinal), "order should be preserved when ordering by text (hexa)");
		}

		[Test]
		public void Test_Uuid96_Arithmetic()
		{
			var uid = Uuid96.Empty;

			Assert.That(uid + 42L, Is.EqualTo(new Uuid96(0, 42)));
			Assert.That(uid + 42UL, Is.EqualTo(new Uuid96(0, 42)));
			uid++;
			Assert.That(uid.ToString(), Is.EqualTo("00000000-00000000-00000001"));
			uid++;
			Assert.That(uid.ToString(), Is.EqualTo("00000000-00000000-00000002"));
			uid--;
			Assert.That(uid.ToString(), Is.EqualTo("00000000-00000000-00000001"));
			uid--;
			Assert.That(uid.ToString(), Is.EqualTo("00000000-00000000-00000000"));

			uid = new Uuid96(42, ulong.MaxValue - 99);
			Assert.That(uid + 123L, Is.EqualTo(new Uuid96(43, 23)));
			Assert.That(uid + 123UL, Is.EqualTo(new Uuid96(43, 23)));

			uid = new Uuid96(42, 99);
			Assert.That(uid - 123L, Is.EqualTo(new Uuid96(41, ulong.MaxValue - 23)));
			Assert.That(uid - 123UL, Is.EqualTo(new Uuid96(41, ulong.MaxValue - 23)));
		}

		[Test]
		public void Test_Uuid96_Read_From_Bytes()
		{
			// test buffer with included padding
			byte[] buf = { 0x55, 0x55, 0x55, 0x55, /* start */ 0x1E, 0x2D, 0x3C, 0x4B, 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF, /* stop */ 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA };
			var original = Uuid96.Parse("1E2D3C4B-01234567-89ABCDEF");
			(var hi, var lo) = original;
			Assume.That(hi, Is.EqualTo(0x1E2D3C4B));
			Assume.That(lo, Is.EqualTo(0x0123456789ABCDEF));

			// Slice
			Assert.That(Uuid96.Read(buf.AsSlice(4, 12)), Is.EqualTo(original));

			// byte[]
			Assert.That(Uuid96.Read(buf.AsSlice(4, 12).GetBytesOrEmpty()), Is.EqualTo(original));

#if ENABLE_SPAN
			// ReadOnlySpan<byte>
			Assert.That(Uuid96.Read(buf.AsSpan(4, 12)), Is.EqualTo(original));

			unsafe
			{
				fixed (byte* ptr = &buf[4])
				{
					Assert.That(Uuid96.Read(ptr, 12), Is.EqualTo(original));
					Assert.That(Uuid96.Read(USlice.FromUnmanagedPointer(ptr, 12)), Is.EqualTo(original));
					Assert.That(Uuid96.Read(new ReadOnlySpan<byte>(ptr, 12)), Is.EqualTo(original));
				}
			}
#endif
		}

		[Test]
		public void Test_Uuid96_WriteTo()
		{
			var original = Uuid96.Parse("1E2D3C4B-01234567-89ABCDEF");
			(var hi, var lo) = original;
			Assume.That(hi, Is.EqualTo(0x1E2D3C4B));
			Assume.That(lo, Is.EqualTo(0x0123456789ABCDEF));

			// span with more space
			var scratch = Slice.Repeat(0xAA, 20);
			original.WriteTo(scratch);
			Assert.That(scratch.ToString("X"), Is.EqualTo("1E 2D 3C 4B 01 23 45 67 89 AB CD EF AA AA AA AA AA AA AA AA"));

			// span with no offset and exact size
			scratch = Slice.Repeat(0xAA, 20);
			original.WriteTo(scratch.Substring(0, 12));
			Assert.That(scratch.ToString("X"), Is.EqualTo("1E 2D 3C 4B 01 23 45 67 89 AB CD EF AA AA AA AA AA AA AA AA"));

			// span with offset
			scratch = Slice.Repeat(0xAA, 20);
			original.WriteTo(scratch.Substring(4));
			Assert.That(scratch.ToString("X"), Is.EqualTo("AA AA AA AA 1E 2D 3C 4B 01 23 45 67 89 AB CD EF AA AA AA AA"));

			// span with offset and exact size
			scratch = Slice.Repeat(0xAA, 20);
			original.WriteTo(scratch.Substring(4, 12));
			Assert.That(scratch.ToString("X"), Is.EqualTo("AA AA AA AA 1E 2D 3C 4B 01 23 45 67 89 AB CD EF AA AA AA AA"));

			scratch = Slice.Repeat(0xAA, 20);
			original.WriteToUnsafe(scratch.Array, scratch.Offset);
			Assert.That(scratch.ToString("X"), Is.EqualTo("1E 2D 3C 4B 01 23 45 67 89 AB CD EF AA AA AA AA AA AA AA AA"));

			// errors

			Assert.That(() => original.WriteTo(Slice.Empty), Throws.InstanceOf<ArgumentException>(), "Target buffer is empty");
			Assert.That(() => original.WriteTo(null, 12), Throws.InstanceOf<ArgumentException>(), "Target buffer is null");
			Assert.That(() => original.WriteTo(null, 0), Throws.InstanceOf<ArgumentException>(), "Target buffer is null");

			scratch = Slice.Repeat(0xAA, 16);
			Assert.That(() => original.WriteTo(scratch.Substring(0, 11)), Throws.InstanceOf<ArgumentException>(), "Target buffer is too small");
			Assert.That(scratch.ToString("X"), Is.EqualTo("AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA"), "Buffer should not have been overwritten!");

		}

		[Test]
		public void Test_Uuid96_TryWriteTo()
		{
			var original = Uuid96.Parse("1E2D3C4B-01234567-89ABCDEF");
			(var hi, var lo) = original;
			Assume.That(hi, Is.EqualTo(0x1E2D3C4B));
			Assume.That(lo, Is.EqualTo(0x0123456789ABCDEF));

			// span with more space
			var scratch = Slice.Repeat(0xAA, 20);
			Assert.That(original.TryWriteTo(scratch), Is.True);
			Assert.That(scratch.ToString("X"), Is.EqualTo("1E 2D 3C 4B 01 23 45 67 89 AB CD EF AA AA AA AA AA AA AA AA"));

			// span with no offset and exact size
			scratch = Slice.Repeat(0xAA, 20);
			Assert.That(original.TryWriteTo(scratch.Substring(0, 12)), Is.True);
			Assert.That(scratch.ToString("X"), Is.EqualTo("1E 2D 3C 4B 01 23 45 67 89 AB CD EF AA AA AA AA AA AA AA AA"));

			// span with offset
			scratch = Slice.Repeat(0xAA, 20);
			Assert.That(original.TryWriteTo(scratch.Substring(4)), Is.True);
			Assert.That(scratch.ToString("X"), Is.EqualTo("AA AA AA AA 1E 2D 3C 4B 01 23 45 67 89 AB CD EF AA AA AA AA"));

			// span with offset and exact size
			scratch = Slice.Repeat(0xAA, 20);
			Assert.That(original.TryWriteTo(scratch.Substring(4, 12)), Is.True);
			Assert.That(scratch.ToString("X"), Is.EqualTo("AA AA AA AA 1E 2D 3C 4B 01 23 45 67 89 AB CD EF AA AA AA AA"));

			// errors

			Assert.That(original.TryWriteTo(Slice.Empty), Is.False, "Target buffer is empty");

			scratch = Slice.Repeat(0xAA, 20);
			Assert.That(original.TryWriteTo(scratch.Substring(0, 11)), Is.False, "Target buffer is too small");
			Assert.That(scratch.ToString("X"), Is.EqualTo("AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA"), "Buffer should not have been overwritten!");

		}
	}

}

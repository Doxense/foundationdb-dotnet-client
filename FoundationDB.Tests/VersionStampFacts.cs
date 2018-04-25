#region BSD Licence
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
	using Doxense.Memory;
	using NUnit.Framework;

	[TestFixture]
	public class VersionStampFacts : FdbTest
	{

		[Test]
		public void Test_Incomplete_VersionStamp()
		{
			{ // 80-bits (no user version)
				var vs = VersionStamp.Incomplete();
				Log(vs);
				Assert.That(vs.TransactionVersion, Is.EqualTo(ulong.MaxValue));
				Assert.That(vs.TransactionOrder, Is.EqualTo(ushort.MaxValue));
				Assert.That(vs.IsIncomplete, Is.True);
				Assert.That(vs.HasUserVersion, Is.False, "80-bits VersionStamps don't have a user version");
				Assert.That(vs.UserVersion, Is.Zero, "80-bits VersionStamps don't have a user version");

				Assert.That(vs.GetLength(), Is.EqualTo(10));
				Assert.That(vs.ToSlice().ToHexaString(' '), Is.EqualTo("FF FF FF FF FF FF FF FF FF FF"));
				Assert.That(vs.ToString(), Is.EqualTo("@?"));
			}

			{ // 96-bits, default user version
				var vs = VersionStamp.Incomplete(0);
				Log(vs);
				Assert.That(vs.TransactionVersion, Is.EqualTo(ulong.MaxValue));
				Assert.That(vs.TransactionOrder, Is.EqualTo(ushort.MaxValue));
				Assert.That(vs.IsIncomplete, Is.True);
				Assert.That(vs.HasUserVersion, Is.True, "96-bits VersionStamps have a user version");
				Assert.That(vs.UserVersion, Is.EqualTo(0));

				Assert.That(vs.GetLength(), Is.EqualTo(12));
				Assert.That(vs.ToSlice().ToHexaString(' '), Is.EqualTo("FF FF FF FF FF FF FF FF FF FF 00 00"));
				Assert.That(vs.ToString(), Is.EqualTo("@?#0"));
			}

			{ // 96 bits, custom user version
				var vs = VersionStamp.Incomplete(123);
				Log(vs);
				Assert.That(vs.TransactionVersion, Is.EqualTo(ulong.MaxValue));
				Assert.That(vs.TransactionOrder, Is.EqualTo(ushort.MaxValue));
				Assert.That(vs.HasUserVersion, Is.True);
				Assert.That(vs.UserVersion, Is.EqualTo(123));
				Assert.That(vs.IsIncomplete, Is.True);
				Assert.That(vs.ToSlice().ToHexaString(' '), Is.EqualTo("FF FF FF FF FF FF FF FF FF FF 00 7B"));
				Assert.That(vs.ToString(), Is.EqualTo("@?#123"));
			}

			{ // 96 bits, large user version
				var vs = VersionStamp.Incomplete(12345);
				Log(vs);
				Assert.That(vs.TransactionVersion, Is.EqualTo(ulong.MaxValue));
				Assert.That(vs.TransactionOrder, Is.EqualTo(ushort.MaxValue));
				Assert.That(vs.HasUserVersion, Is.True);
				Assert.That(vs.UserVersion, Is.EqualTo(12345));
				Assert.That(vs.IsIncomplete, Is.True);
				Assert.That(vs.ToSlice().ToHexaString(' '), Is.EqualTo("FF FF FF FF FF FF FF FF FF FF 30 39"));
				Assert.That(vs.ToString(), Is.EqualTo("@?#12345"));
			}

			Assert.That(() => VersionStamp.Incomplete(-1), Throws.ArgumentException, "User version cannot be negative");
			Assert.That(() => VersionStamp.Incomplete(65536), Throws.ArgumentException, "User version cannot be larger than 0xFFFF");

			{
				var writer = default(SliceWriter);
				writer.WriteFixed24BE(0xAAAAAA);
				VersionStamp.Incomplete(123).WriteTo(ref writer);
				writer.WriteFixed24BE(0xAAAAAA);
				Assert.That(writer.ToSlice().ToHexaString(' '), Is.EqualTo("AA AA AA FF FF FF FF FF FF FF FF FF FF 00 7B AA AA AA"));

				var reader = new SliceReader(writer.ToSlice());
				reader.Skip(3);
				var vs = VersionStamp.Parse(reader.ReadBytes(12));
				Assert.That(reader.Remaining, Is.EqualTo(3));

				Assert.That(vs.TransactionVersion, Is.EqualTo(ulong.MaxValue));
				Assert.That(vs.TransactionOrder, Is.EqualTo(ushort.MaxValue));
				Assert.That(vs.UserVersion, Is.EqualTo(123));
				Assert.That(vs.IsIncomplete, Is.False, "NOTE: reading stamps is only supposed to happen for stamps already in the database!");
			}

			{
				var buf = Slice.Repeat(0xAA, 18);
				VersionStamp.Incomplete(123).WriteTo(buf.Substring(3, 12));
				Assert.That(buf.ToHexaString(' '), Is.EqualTo("AA AA AA FF FF FF FF FF FF FF FF FF FF 00 7B AA AA AA"));
			}
		}

		[Test]
		public void Test_Complete_VersionStamp()
		{
			{ // 80-bits, no user version
				var vs = VersionStamp.Complete(0x0123456789ABCDEFUL, 123);
				Log(vs);
				Assert.That(vs.TransactionVersion, Is.EqualTo(0x0123456789ABCDEFUL));
				Assert.That(vs.TransactionOrder, Is.EqualTo(123));
				Assert.That(vs.HasUserVersion, Is.False);
				Assert.That(vs.UserVersion, Is.Zero);
				Assert.That(vs.IsIncomplete, Is.False);
				Assert.That(vs.ToSlice().ToHexaString(' '), Is.EqualTo("01 23 45 67 89 AB CD EF 00 7B"));
				Assert.That(vs.ToString(), Is.EqualTo("@81985529216486895-123"));
			}

			{ // 96 bits, default user version
				var vs = VersionStamp.Complete(0x0123456789ABCDEFUL, 123, 0);
				Log(vs);
				Assert.That(vs.TransactionVersion, Is.EqualTo(0x0123456789ABCDEFUL));
				Assert.That(vs.TransactionOrder, Is.EqualTo(123));
				Assert.That(vs.HasUserVersion, Is.True);
				Assert.That(vs.UserVersion, Is.Zero);
				Assert.That(vs.IsIncomplete, Is.False);
				Assert.That(vs.ToSlice().ToHexaString(' '), Is.EqualTo("01 23 45 67 89 AB CD EF 00 7B 00 00"));
				Assert.That(vs.ToString(), Is.EqualTo("@81985529216486895-123#0"));
			}

			{ // custom user version
				var vs = VersionStamp.Complete(0x0123456789ABCDEFUL, 123, 456);
				Log(vs);
				Assert.That(vs.TransactionVersion, Is.EqualTo(0x0123456789ABCDEFUL));
				Assert.That(vs.TransactionOrder, Is.EqualTo(123));
				Assert.That(vs.HasUserVersion, Is.True);
				Assert.That(vs.UserVersion, Is.EqualTo(456));
				Assert.That(vs.IsIncomplete, Is.False);
				Assert.That(vs.ToSlice().ToHexaString(' '), Is.EqualTo("01 23 45 67 89 AB CD EF 00 7B 01 C8"));
				Assert.That(vs.ToString(), Is.EqualTo("@81985529216486895-123#456"));
			}

			{ // two bytes user version
				var vs = VersionStamp.Complete(0x0123456789ABCDEFUL, 12345, 6789);
				Log(vs);
				Assert.That(vs.TransactionVersion, Is.EqualTo(0x0123456789ABCDEFUL));
				Assert.That(vs.TransactionOrder, Is.EqualTo(12345));
				Assert.That(vs.UserVersion, Is.EqualTo(6789));
				Assert.That(vs.IsIncomplete, Is.False);
				Assert.That(vs.ToSlice().ToHexaString(' '), Is.EqualTo("01 23 45 67 89 AB CD EF 30 39 1A 85"));
				Assert.That(vs.ToString(), Is.EqualTo("@81985529216486895-12345#6789"));
			}

			Assert.That(() => VersionStamp.Complete(0x0123456789ABCDEFUL, 0, -1), Throws.ArgumentException, "User version cannot be negative");
			Assert.That(() => VersionStamp.Complete(0x0123456789ABCDEFUL, 0, 65536), Throws.ArgumentException, "User version cannot be larger than 0xFFFF");

			{
				var writer = default(SliceWriter);
				writer.WriteFixed24BE(0xAAAAAA);
				VersionStamp.Complete(0x0123456789ABCDEFUL, 123, 456).WriteTo(ref writer);
				writer.WriteFixed24BE(0xAAAAAA);
				Assert.That(writer.ToSlice().ToHexaString(' '), Is.EqualTo("AA AA AA 01 23 45 67 89 AB CD EF 00 7B 01 C8 AA AA AA"));

				var reader = new SliceReader(writer.ToSlice());
				reader.Skip(3);
				var vs = VersionStamp.Parse(reader.ReadBytes(12));
				Assert.That(reader.Remaining, Is.EqualTo(3));

				Assert.That(vs.TransactionVersion, Is.EqualTo(0x0123456789ABCDEFUL));
				Assert.That(vs.TransactionOrder, Is.EqualTo(123));
				Assert.That(vs.UserVersion, Is.EqualTo(456));
				Assert.That(vs.IsIncomplete, Is.False);
			}

			{
				var buf = Slice.Repeat(0xAA, 18);
				VersionStamp.Complete(0x0123456789ABCDEFUL, 123, 456).WriteTo(buf.Substring(3, 12));
				Assert.That(buf.ToHexaString(' '), Is.EqualTo("AA AA AA 01 23 45 67 89 AB CD EF 00 7B 01 C8 AA AA AA"));
			}
		}

	}
}

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

// ReSharper disable StringLiteralTypo

namespace FoundationDB.Client.Tests
{

	[TestFixture]
	[Category("Fdb-Client-InProc")]
	[Parallelizable(ParallelScope.All)]
	public class VersionStampFacts : SimpleTest
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
				Assert.That(vs.ToSlice().ToHexString(' '), Is.EqualTo("FF FF FF FF FF FF FF FF FF FF"));
				Assert.That(vs.ToString(), Is.EqualTo("@?"));
				Assert.That(VersionStamp.Parse("@?"), Is.EqualTo(vs));
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
				Assert.That(vs.ToSlice().ToHexString(' '), Is.EqualTo("FF FF FF FF FF FF FF FF FF FF 00 00"));
				Assert.That(vs.ToString(), Is.EqualTo("@?#0"));
				Assert.That(VersionStamp.Parse("@?#0"), Is.EqualTo(vs));
			}

			{ // 96 bits, custom user version
				var vs = VersionStamp.Incomplete(42);
				Log(vs);
				Assert.That(vs.TransactionVersion, Is.EqualTo(ulong.MaxValue));
				Assert.That(vs.TransactionOrder, Is.EqualTo(ushort.MaxValue));
				Assert.That(vs.HasUserVersion, Is.True);
				Assert.That(vs.UserVersion, Is.EqualTo(42));
				Assert.That(vs.IsIncomplete, Is.True);
				Assert.That(vs.ToSlice().ToHexString(' '), Is.EqualTo("FF FF FF FF FF FF FF FF FF FF 00 2A"));
				Assert.That(vs.ToString(), Is.EqualTo("@?#2a"));
				Assert.That(VersionStamp.Parse("@?#2a"), Is.EqualTo(vs));
			}

			{ // 96 bits, large user version
				var vs = VersionStamp.Incomplete(0x1234);
				Log(vs);
				Assert.That(vs.TransactionVersion, Is.EqualTo(ulong.MaxValue));
				Assert.That(vs.TransactionOrder, Is.EqualTo(ushort.MaxValue));
				Assert.That(vs.HasUserVersion, Is.True);
				Assert.That(vs.UserVersion, Is.EqualTo(0x1234));
				Assert.That(vs.IsIncomplete, Is.True);
				Assert.That(vs.ToSlice().ToHexString(' '), Is.EqualTo("FF FF FF FF FF FF FF FF FF FF 12 34"));
				Assert.That(vs.ToString(), Is.EqualTo("@?#1234"));
				Assert.That(VersionStamp.Parse("@?#1234"), Is.EqualTo(vs));
			}

			Assert.That(() => VersionStamp.Incomplete(-1), Throws.InstanceOf<ArgumentException>(), "User version cannot be negative");
			Assert.That(() => VersionStamp.Incomplete(65536), Throws.InstanceOf<ArgumentException>(), "User version cannot be larger than 0xFFFF");

			{
				var writer = default(SliceWriter);
				writer.WriteUInt24BE(0xAAAAAA);
				VersionStamp.Incomplete(123).WriteTo(ref writer);
				writer.WriteUInt24BE(0xAAAAAA);
				Assert.That(writer.ToSlice().ToHexString(' '), Is.EqualTo("AA AA AA FF FF FF FF FF FF FF FF FF FF 00 7B AA AA AA"));

				var reader = new SliceReader(writer.ToSlice());
				reader.Skip(3);
				var vs = VersionStamp.ReadFrom(reader.ReadBytes(12));
				Assert.That(reader.Remaining, Is.EqualTo(3));

				Assert.That(vs.TransactionVersion, Is.EqualTo(ulong.MaxValue));
				Assert.That(vs.TransactionOrder, Is.EqualTo(ushort.MaxValue));
				Assert.That(vs.UserVersion, Is.EqualTo(123));
				Assert.That(vs.IsIncomplete, Is.True);
			}

			{
				var buf = new byte[18];

				buf.AsSpan().Fill(0xAA);
				VersionStamp.Incomplete().WriteTo(buf.AsSpan(3, 10));
				Assert.That(buf.AsSlice().ToHexString(' '), Is.EqualTo("AA AA AA FF FF FF FF FF FF FF FF FF FF AA AA AA AA AA"));

				buf.AsSpan().Fill(0xAA);
				VersionStamp.Incomplete(123).WriteTo(buf.AsSpan(3, 12));
				Assert.That(buf.AsSlice().ToHexString(' '), Is.EqualTo("AA AA AA FF FF FF FF FF FF FF FF FF FF 00 7B AA AA AA"));

				buf.AsSpan().Fill(0xAA);
				Assert.That(VersionStamp.Incomplete(123).TryWriteTo(buf.AsSpan(3, 12)), Is.True);
				Assert.That(buf.AsSlice().ToHexString(' '), Is.EqualTo("AA AA AA FF FF FF FF FF FF FF FF FF FF 00 7B AA AA AA"));

				buf.AsSpan().Fill(0xAA);
				Assert.That(VersionStamp.Incomplete().TryWriteTo(buf.AsSpan(3, 10)), Is.True);
				Assert.That(buf.AsSlice().ToHexString(' '), Is.EqualTo("AA AA AA FF FF FF FF FF FF FF FF FF FF AA AA AA AA AA"));

				buf.AsSpan().Fill(0xAA);
				Assert.That(VersionStamp.Incomplete().TryWriteTo(buf.AsSpan(3, 12), out var written), Is.True);
				Assert.That(written, Is.EqualTo(10));
				Assert.That(buf.AsSlice().ToHexString(' '), Is.EqualTo("AA AA AA FF FF FF FF FF FF FF FF FF FF AA AA AA AA AA"));

				buf.AsSpan().Fill(0xAA);
				Assert.That(VersionStamp.Incomplete(123).TryWriteTo(buf.AsSpan(3, 12), out written), Is.True);
				Assert.That(written, Is.EqualTo(12));
				Assert.That(buf.AsSlice().ToHexString(' '), Is.EqualTo("AA AA AA FF FF FF FF FF FF FF FF FF FF 00 7B AA AA AA"));
			}
		}

		[Test]
		public void Test_Complete_VersionStamp()
		{
			{ // 80-bits, no user version
				var vs = VersionStamp.Complete(0x0123456789ABCDEFUL, 0x357A);
				Log(vs);
				Assert.Multiple(() =>
				{
					Assert.That(vs.TransactionVersion, Is.EqualTo(0x0123456789abcdefUL));
					Assert.That(vs.TransactionOrder, Is.EqualTo(0x357A));
					Assert.That(vs.HasUserVersion, Is.False);
					Assert.That(vs.UserVersion, Is.Zero);
					Assert.That(vs.IsIncomplete, Is.False);
					Assert.That(vs.ToSlice().ToHexString(' '), Is.EqualTo("01 23 45 67 89 AB CD EF 35 7A"));
					Assert.That(vs.ToString(), Is.EqualTo("@123456789abcdef-357a"));
					Assert.That(VersionStamp.Parse("@123456789abcdef-357a"), Is.EqualTo(vs));
				});
			}

			{ // 96 bits, default user version
				var vs = VersionStamp.Complete(0x0123456789abcdefUL, 0x357A, 0);
				Log(vs);
				Assert.Multiple(() =>
				{
					Assert.That(vs.TransactionVersion, Is.EqualTo(0x0123456789abcdefUL));
					Assert.That(vs.TransactionOrder, Is.EqualTo(0x357A));
					Assert.That(vs.HasUserVersion, Is.True);
					Assert.That(vs.UserVersion, Is.Zero);
					Assert.That(vs.IsIncomplete, Is.False);
					Assert.That(vs.ToSlice().ToHexString(' '), Is.EqualTo("01 23 45 67 89 AB CD EF 35 7A 00 00"));
					Assert.That(vs.ToString(), Is.EqualTo("@123456789abcdef-357a#0"));
					Assert.That(VersionStamp.Parse("@123456789abcdef-357a#0"), Is.EqualTo(vs));
				});
			}

			{ // custom user version
				var vs = VersionStamp.Complete(0x0123456789abcdefUL, 0x357A, 42);
				Log(vs);
				Assert.Multiple(() =>
				{
					Assert.That(vs.TransactionVersion, Is.EqualTo(0x0123456789abcdefUL));
					Assert.That(vs.TransactionOrder, Is.EqualTo(0x357A));
					Assert.That(vs.HasUserVersion, Is.True);
					Assert.That(vs.UserVersion, Is.EqualTo(42));
					Assert.That(vs.IsIncomplete, Is.False);
					Assert.That(vs.ToSlice().ToHexString(' '), Is.EqualTo("01 23 45 67 89 AB CD EF 35 7A 00 2A"));
					Assert.That(vs.ToString(), Is.EqualTo("@123456789abcdef-357a#2a"));
					Assert.That(VersionStamp.Parse("@123456789abcdef-357a#2a"), Is.EqualTo(vs));
				});
			}

			{ // two bytes user version
				var vs = VersionStamp.Complete(0x0123456789abcdefUL, 0x357A, 0x1234);
				Log(vs);
				Assert.Multiple(() =>
				{
					Assert.That(vs.TransactionVersion, Is.EqualTo(0x0123456789abcdefUL));
					Assert.That(vs.TransactionOrder, Is.EqualTo(0x357A));
					Assert.That(vs.UserVersion, Is.EqualTo(0x1234));
					Assert.That(vs.IsIncomplete, Is.False);
					Assert.That(vs.ToSlice().ToHexString(' '), Is.EqualTo("01 23 45 67 89 AB CD EF 35 7A 12 34"));
					Assert.That(vs.ToString(), Is.EqualTo("@123456789abcdef-357a#1234"));
					Assert.That(VersionStamp.Parse("@123456789abcdef-357a#1234"), Is.EqualTo(vs));
				});
			}

			Assert.That(() => VersionStamp.Complete(0x0123456789abcdefUL, 0, -1), Throws.InstanceOf<ArgumentException>(), "User version cannot be negative");
			Assert.That(() => VersionStamp.Complete(0x0123456789abcdefUL, 0, 65536), Throws.InstanceOf<ArgumentException>(), "User version cannot be larger than 0xFFFF");

			{
				var writer = default(SliceWriter);
				writer.WriteUInt24BE(0xAAAAAA);
				VersionStamp.Complete(0x0123456789abcdefUL, 0x357A, 456).WriteTo(ref writer);
				writer.WriteUInt24BE(0xAAAAAA);
				Assert.That(writer.ToSlice().ToHexString(' '), Is.EqualTo("AA AA AA 01 23 45 67 89 AB CD EF 35 7A 01 C8 AA AA AA"));

				var reader = new SliceReader(writer.ToSlice());
				reader.Skip(3);
				var vs = VersionStamp.ReadFrom(reader.ReadBytes(12));
				Assert.That(reader.Remaining, Is.EqualTo(3));

				Assert.Multiple(() =>
				{
					Assert.That(vs.TransactionVersion, Is.EqualTo(0x0123456789ABCDEFUL));
					Assert.That(vs.TransactionOrder, Is.EqualTo(0x357A));
					Assert.That(vs.UserVersion, Is.EqualTo(456));
					Assert.That(vs.IsIncomplete, Is.False);
				});
			}

			{
				var buf = new byte[18];

				buf.AsSpan().Fill(0xAA);
				VersionStamp.Complete(0x0123456789abcdefUL, 0x357A).WriteTo(buf.AsSpan(3, 10));
				Assert.That(buf.AsSlice().ToHexString(' '), Is.EqualTo("AA AA AA 01 23 45 67 89 AB CD EF 35 7A AA AA AA AA AA"));

				buf.AsSpan().Fill(0xAA);
				VersionStamp.Complete(0x0123456789abcdefUL, 0x357A, 456).WriteTo(buf.AsSpan(3, 12));
				Assert.That(buf.AsSlice().ToHexString(' '), Is.EqualTo("AA AA AA 01 23 45 67 89 AB CD EF 35 7A 01 C8 AA AA AA"));

				buf.AsSpan().Fill(0xAA);
				Assert.That(VersionStamp.Complete(0x0123456789abcdefUL, 0x357A).TryWriteTo(buf.AsSpan(3, 10)), Is.True);
				Assert.That(buf.AsSlice().ToHexString(' '), Is.EqualTo("AA AA AA 01 23 45 67 89 AB CD EF 35 7A AA AA AA AA AA"));

				buf.AsSpan().Fill(0xAA);
				Assert.That(VersionStamp.Complete(0x0123456789abcdefUL, 0x357A, 456).TryWriteTo(buf.AsSpan(3, 12)), Is.True);
				Assert.That(buf.AsSlice().ToHexString(' '), Is.EqualTo("AA AA AA 01 23 45 67 89 AB CD EF 35 7A 01 C8 AA AA AA"));

				buf.AsSpan().Fill(0xAA);
				Assert.That(VersionStamp.Complete(0x0123456789abcdefUL, 0x357A).TryWriteTo(buf.AsSpan(3, 10), out var written), Is.True);
				Assert.That(written, Is.EqualTo(10));
				Assert.That(buf.AsSlice().ToHexString(' '), Is.EqualTo("AA AA AA 01 23 45 67 89 AB CD EF 35 7A AA AA AA AA AA"));

				buf.AsSpan().Fill(0xAA);
				Assert.That(VersionStamp.Complete(0x0123456789abcdefUL, 0x357A, 456).TryWriteTo(buf.AsSpan(3, 12), out written), Is.True);
				Assert.That(written, Is.EqualTo(12));
				Assert.That(buf.AsSlice().ToHexString(' '), Is.EqualTo("AA AA AA 01 23 45 67 89 AB CD EF 35 7A 01 C8 AA AA AA"));
			}
		}

		[Test]
		public void Test_VersionStamp_ToString()
		{
			// "default" ("D")
			Assert.Multiple(() =>
			{
				Assert.That(VersionStamp.Incomplete().ToString(), Is.EqualTo("@?"));
				Assert.That(VersionStamp.Incomplete(0).ToString(), Is.EqualTo("@?#0"));
				Assert.That(VersionStamp.Incomplete(0x1234).ToString(), Is.EqualTo("@?#1234"));

				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0).ToString(), Is.EqualTo("@123456789abcdef-0"));
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x357A).ToString(), Is.EqualTo("@123456789abcdef-357a"));
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x357A, 0).ToString(), Is.EqualTo("@123456789abcdef-357a#0"));
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x357A, 0x1234).ToString(), Is.EqualTo("@123456789abcdef-357a#1234"));
				Assert.That(VersionStamp.Complete(1, 2).ToString(), Is.EqualTo("@1-2"));
				Assert.That(VersionStamp.Complete(1, 2, 3).ToString(), Is.EqualTo("@1-2#3"));
			});

			// "Java-like" ("J")
			Assert.Multiple(() =>
			{
				Assert.That(VersionStamp.Incomplete().ToString("J"), Is.EqualTo("Versionstamp(<incomplete> 0)"));
				Assert.That(VersionStamp.Incomplete(0).ToString("J"), Is.EqualTo("Versionstamp(<incomplete> 0)"));
				Assert.That(VersionStamp.Incomplete(0x29a).ToString("J"), Is.EqualTo("Versionstamp(<incomplete> 666)"));

				Assert.That(VersionStamp.Complete(0x48656c6c6f576f72UL, 0).ToString("J"), Is.EqualTo(@"Versionstamp(HelloWor\x00\x00 0)"));
				Assert.That(VersionStamp.Complete(0x48656c6c6f576f72UL, 0x6c64).ToString("J"), Is.EqualTo("Versionstamp(HelloWorld 0)"));
				Assert.That(VersionStamp.Complete(0x48656c6c6f576f72UL, 0x6c64, 0).ToString("J"), Is.EqualTo("Versionstamp(HelloWorld 0)"));
				Assert.That(VersionStamp.Complete(0x48656c6c6f576f72UL, 0x6c64, 0x29a).ToString("J"), Is.EqualTo("Versionstamp(HelloWorld 666)"));
				Assert.That(VersionStamp.Complete(0x0001273041615c7fUL, 0x80ff, 0x29a).ToString("J"), Is.EqualTo(@"Versionstamp(\x00\x01'0Aa\\\x7f\x80\xff 666)"));
			});
		}

		[Test]
		public void Test_VersionStamp_TryFormat_Chars()
		{
			static void VerifyTryFormat(VersionStamp vs, string expected)
			{
				{ // with large enough buffer
					var arr = new char[expected.Length + 100];
					arr.AsSpan().Fill('%');
					Span<char> buffer = arr;
					Assert.That(vs.TryFormat(buffer, out int charsWritten), Is.True);
					Assert.That(buffer[..charsWritten].ToString(), Is.EqualTo(expected));
					Assert.That(arr.AsSpan(charsWritten).ContainsAnyExcept('%'), Is.False);
				}
				{ // with exactly sized buffer
					var arr = new char[expected.Length + 100];
					arr.AsSpan().Fill('%');
					Span<char> buffer = arr.AsSpan(0, expected.Length);
					Assert.That(vs.TryFormat(buffer, out int charsWritten), Is.True);
					Assert.That(buffer[..charsWritten].ToString(), Is.EqualTo(expected));
					Assert.That(arr.AsSpan(charsWritten).ContainsAnyExcept('%'), Is.False);
				}
				{ // with empty buffer
					Assert.That(vs.TryFormat(Span<char>.Empty, out int charsWritten), Is.False);
					Assert.That(charsWritten, Is.Zero);
				}
				{ // with buffer just one short
					var arr = new char[expected.Length + 100];
					arr.AsSpan().Fill('%');
					Span<char> buffer = arr.AsSpan(0, expected.Length - 1);
					Assert.That(vs.TryFormat(buffer, out int charsWritten), Is.False);
					Assert.That(charsWritten, Is.Zero);
					Assert.That(arr.AsSpan(expected.Length - 1).ContainsAnyExcept('%'), Is.False);
				}
			}

			VerifyTryFormat(VersionStamp.Incomplete(), "@?");
			VerifyTryFormat(VersionStamp.Incomplete(0), "@?#0");
			VerifyTryFormat(VersionStamp.Incomplete(0x12), "@?#12");
			VerifyTryFormat(VersionStamp.Incomplete(0x1234), "@?#1234");
			VerifyTryFormat(VersionStamp.Complete(0x0123456789ABCDEFUL, 0), "@123456789abcdef-0");
			VerifyTryFormat(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x357A), "@123456789abcdef-357a");
			VerifyTryFormat(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x357A, 0), "@123456789abcdef-357a#0");
			VerifyTryFormat(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x357A, 0x1234), "@123456789abcdef-357a#1234");
			VerifyTryFormat(VersionStamp.Complete(1, 2), "@1-2");
			VerifyTryFormat(VersionStamp.Complete(1, 2, 3), "@1-2#3");
		}

		[Test]
		public void Test_VersionStamp_Parse()
		{
			static void VerifyParse(string literal, VersionStamp expected)
			{
				// TryParse (chars)
				Assert.That(VersionStamp.TryParse(literal, null, out var actual), Is.True);
				Assert.That(actual, Is.EqualTo(expected));

				// Parse(chars)
				Assert.That(VersionStamp.Parse(literal), Is.EqualTo(expected));

				var bytes = Encoding.UTF8.GetBytes(literal);

				// TryParse (bytes)
				Assert.That(VersionStamp.TryParse(bytes, null, out actual), Is.True);
				Assert.That(actual, Is.EqualTo(expected));

				// Parse(bytes)
				Assert.That(VersionStamp.Parse(bytes), Is.EqualTo(expected));

			}

			VerifyParse("@?", VersionStamp.Incomplete());
			VerifyParse("@?#0", VersionStamp.Incomplete(0));
			VerifyParse("@?#00", VersionStamp.Incomplete(0));
			VerifyParse("@?#12", VersionStamp.Incomplete(0x12));
			VerifyParse("@?#012", VersionStamp.Incomplete(0x12));
			VerifyParse("@?#1234", VersionStamp.Incomplete(0x1234));
			VerifyParse("@123456789abcdef-0", VersionStamp.Complete(0x0123456789ABCDEFUL, 0));
			VerifyParse("@123456789abcdef-0000", VersionStamp.Complete(0x0123456789ABCDEFUL, 0));
			VerifyParse("@123456789abcdef-3", VersionStamp.Complete(0x0123456789ABCDEFUL, 0x3));
			VerifyParse("@123456789abcdef-35", VersionStamp.Complete(0x0123456789ABCDEFUL, 0x35));
			VerifyParse("@123456789abcdef-357", VersionStamp.Complete(0x0123456789ABCDEFUL, 0x357));
			VerifyParse("@123456789abcdef-357a", VersionStamp.Complete(0x0123456789ABCDEFUL, 0x357A));
			VerifyParse("@123456789abcdef-357a#0", VersionStamp.Complete(0x0123456789ABCDEFUL, 0x357A, 0));
			VerifyParse("@123456789abcdef-357a#12", VersionStamp.Complete(0x0123456789ABCDEFUL, 0x357A, 0x12));
			VerifyParse("@123456789abcdef-357a#012", VersionStamp.Complete(0x0123456789ABCDEFUL, 0x357A, 0x12));
			VerifyParse("@123456789abcdef-357a#1234", VersionStamp.Complete(0x0123456789ABCDEFUL, 0x357A, 0x1234));
			VerifyParse("@1-2", VersionStamp.Complete(1, 2));
			VerifyParse("@1-2#3", VersionStamp.Complete(1, 2, 3));

			static void VerifyFail(string literal)
			{
				Assert.That(VersionStamp.TryParse(literal, null, out _), Is.False);
				Assert.That(() => VersionStamp.Parse(literal), Throws.InstanceOf<FormatException>());

				var bytes = Encoding.UTF8.GetBytes(literal);
				Assert.That(VersionStamp.TryParse(bytes, null, out _), Is.False);
				Assert.That(() => VersionStamp.Parse(bytes), Throws.InstanceOf<FormatException>());
			}

			VerifyFail("");
			VerifyFail("not_a_stamp");
			VerifyFail(" @?");
			VerifyFail("@");
			VerifyFail("@ ?");
			VerifyFail("@??");
			VerifyFail("@?-0");
			VerifyFail("@?#z");
			VerifyFail("@?#12345");
			VerifyFail("@1234-");
			VerifyFail("@1234-5678-");
			VerifyFail("@nothexa-123");
			VerifyFail("@0123456789abcdez-1234");
			VerifyFail("@0123456789abcdef7-1234");
			VerifyFail("@0123456789abcdef-123z");
			VerifyFail("@0123456789abcdef-12345");
			VerifyFail("@1-2#");
			VerifyFail("@1-2#12345");
			VerifyFail("@1-2#123z");
		}

		[Test]
		public void Test_VersionStamp_TryFormat_Utf8_Bytes()
		{
			static void VerifyTryFormat(VersionStamp vs, string expected)
			{
				{ // with large enough buffer
					var arr = new byte[expected.Length + 100];
					arr.AsSpan().Fill(0xAA);
					Span<byte> buffer = arr;
					Assert.That(vs.TryFormat(buffer, out int bytesWritten), Is.True); 
					Assert.That(Encoding.UTF8.GetString(buffer[..bytesWritten]), Is.EqualTo(expected));
					Assert.That(arr.AsSpan(bytesWritten).ContainsAnyExcept((byte) 0xAA), Is.False);
				}
				{ // with exactly sized buffer
					var arr = new byte[expected.Length + 100];
					arr.AsSpan().Fill(0xAA);
					Span<byte> buffer = arr.AsSpan(0, expected.Length);
					Assert.That(vs.TryFormat(buffer, out int bytesWritten), Is.True);
					Assert.That(Encoding.UTF8.GetString(buffer[..bytesWritten]), Is.EqualTo(expected));
					Assert.That(arr.AsSpan(bytesWritten).ContainsAnyExcept((byte) 0xAA), Is.False);
				}
				{ // with empty buffer
					Assert.That(vs.TryFormat(Span<byte>.Empty, out int bytesWritten), Is.False);
					Assert.That(bytesWritten, Is.Zero);
				}
				{ // with buffer just one short
					var arr = new byte[expected.Length + 100];
					arr.AsSpan().Fill(0xAA);
					Span<byte> buffer = arr.AsSpan(0, expected.Length - 1);
					Assert.That(vs.TryFormat(buffer, out int bytesWritten), Is.False);
					Assert.That(bytesWritten, Is.Zero);
					Assert.That(arr.AsSpan(expected.Length - 1).ContainsAnyExcept((byte) 0xAA), Is.False);
				}
			}

			VerifyTryFormat(VersionStamp.Incomplete(), "@?");
			VerifyTryFormat(VersionStamp.Incomplete(0), "@?#0");
			VerifyTryFormat(VersionStamp.Incomplete(0x1234), "@?#1234");
			VerifyTryFormat(VersionStamp.Complete(0x0123456789ABCDEFUL, 0), "@123456789abcdef-0");
			VerifyTryFormat(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x357A), "@123456789abcdef-357a");
			VerifyTryFormat(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x357A, 0), "@123456789abcdef-357a#0");
			VerifyTryFormat(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x357A, 0x1234), "@123456789abcdef-357a#1234");
			VerifyTryFormat(VersionStamp.Complete(1, 2), "@1-2");
			VerifyTryFormat(VersionStamp.Complete(1, 2, 3), "@1-2#3");
		}

		[Test]
		public void Test_VersionStamp_Equality()
		{
			// IEquatable

			Assert.Multiple(() =>
			{
				Assert.That(VersionStamp.Incomplete(), Is.EqualTo(VersionStamp.Incomplete()));
				Assert.That(VersionStamp.Incomplete(), Is.Not.EqualTo(VersionStamp.Incomplete(0x1234)));
				Assert.That(VersionStamp.Incomplete(0x1234), Is.Not.EqualTo(VersionStamp.Incomplete()));
				Assert.That(VersionStamp.Incomplete(0x1234), Is.EqualTo(VersionStamp.Incomplete(0x1234)));
				Assert.That(VersionStamp.Incomplete(0x1234), Is.Not.EqualTo(VersionStamp.Incomplete(0x4321)));

				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA), Is.EqualTo(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA)));
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA), Is.Not.EqualTo(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x33CC)));
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x33CC), Is.Not.EqualTo(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA)));
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x33CC), Is.EqualTo(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x33CC)));
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x33CC), Is.Not.EqualTo(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x3C3C)));
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x33CC), Is.Not.EqualTo(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x5A5A, 0x33CC)));

				// ReSharper disable EqualExpressionComparison

				// ==
				Assert.That(VersionStamp.Incomplete() == VersionStamp.Incomplete(), Is.True);
				Assert.That(VersionStamp.Incomplete() == VersionStamp.Incomplete(0x1234), Is.False);
				Assert.That(VersionStamp.Incomplete(0x1234) == VersionStamp.Incomplete(), Is.False);
				Assert.That(VersionStamp.Incomplete(0x1234) == VersionStamp.Incomplete(0x1234), Is.True);
				Assert.That(VersionStamp.Incomplete(0x1234) == VersionStamp.Incomplete(0x4321), Is.False);

				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) == VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA), Is.True);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) == VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x33CC), Is.False);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x33CC) == VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA), Is.False);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x33CC) == VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x33CC), Is.True);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x33CC) == VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x3C3C), Is.False);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x33CC) == VersionStamp.Complete(0x0123456789ABCDEFUL, 0x5A5A, 0x33CC), Is.False);

				// !=
				Assert.That(VersionStamp.Incomplete() != VersionStamp.Incomplete(), Is.False);
				Assert.That(VersionStamp.Incomplete() != VersionStamp.Incomplete(0x1234), Is.True);
				Assert.That(VersionStamp.Incomplete(0x1234) != VersionStamp.Incomplete(), Is.True);
				Assert.That(VersionStamp.Incomplete(0x1234) != VersionStamp.Incomplete(0x1234), Is.False);
				Assert.That(VersionStamp.Incomplete(0x1234) != VersionStamp.Incomplete(0x4321), Is.True);

				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) != VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA), Is.False);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) != VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x33CC), Is.True);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x33CC) != VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA), Is.True);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x33CC) != VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x33CC), Is.False);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x33CC) != VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x3C3C), Is.True);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA, 0x33CC) != VersionStamp.Complete(0x0123456789ABCDEFUL, 0x5A5A, 0x33CC), Is.True);

				Assert.That(VersionStamp.Incomplete(0x1234), Is.LessThan(VersionStamp.Incomplete(0x1235)));
				Assert.That(VersionStamp.Incomplete(0x1235), Is.GreaterThan(VersionStamp.Incomplete(0x1234)));

				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) < VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA), Is.False);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) < VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AB), Is.True);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) < VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55A9), Is.False);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEEUL, 0x55AA) < VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA), Is.True);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) < VersionStamp.Complete(0x0123456789ABCDEEUL, 0x55AA), Is.False);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEEUL, 0xFFFF) < VersionStamp.Complete(0x0123456789ABCDEFUL, 0x0000), Is.True);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x0000) < VersionStamp.Complete(0x0123456789ABCDEEUL, 0xFFFF), Is.False);

				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) <= VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA), Is.True);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) <= VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AB), Is.True);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) <= VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55A9), Is.False);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEEUL, 0x55AA) <= VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA), Is.True);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) <= VersionStamp.Complete(0x0123456789ABCDEEUL, 0x55AA), Is.False);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEEUL, 0xFFFF) <= VersionStamp.Complete(0x0123456789ABCDEFUL, 0x0000), Is.True);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x0000) <= VersionStamp.Complete(0x0123456789ABCDEEUL, 0xFFFF), Is.False);

				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) > VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA), Is.False);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) > VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AB), Is.False);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) > VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55A9), Is.True);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEEUL, 0x55AA) > VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA), Is.False);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) > VersionStamp.Complete(0x0123456789ABCDEEUL, 0x55AA), Is.True);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEEUL, 0xFFFF) > VersionStamp.Complete(0x0123456789ABCDEFUL, 0x0000), Is.False);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x0000) > VersionStamp.Complete(0x0123456789ABCDEEUL, 0xFFFF), Is.True);

				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) >= VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA), Is.True);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) >= VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AB), Is.False);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) >= VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55A9), Is.True);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEEUL, 0x55AA) >= VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA), Is.False);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA) >= VersionStamp.Complete(0x0123456789ABCDEEUL, 0x55AA), Is.True);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEEUL, 0xFFFF) >= VersionStamp.Complete(0x0123456789ABCDEFUL, 0x0000), Is.False);
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x0000) >= VersionStamp.Complete(0x0123456789ABCDEEUL, 0xFFFF), Is.True);

				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA), Is.LessThan(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AB)));
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA), Is.GreaterThan(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55A9)));
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEEUL, 0x55AA), Is.LessThan(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA)));
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x55AA), Is.GreaterThan(VersionStamp.Complete(0x0123456789ABCDEEUL, 0x55AA)));
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEEUL, 0xFFFF), Is.LessThan(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x0000)));
				Assert.That(VersionStamp.Complete(0x0123456789ABCDEFUL, 0x0000), Is.GreaterThan(VersionStamp.Complete(0x0123456789ABCDEEUL, 0xFFFF)));

			});

			//TODO: tests with user version !

			// ReSharper restore EqualExpressionComparison
		}

		[Test]
		public void Test_VersionStamp_To_Uuid80()
		{
			// To
			Assert.That(VersionStamp.Incomplete().ToUuid80(), Is.EqualTo(Uuid80.MaxValue));
			Assert.That(VersionStamp.Complete(0x0123456789ABCDEF, 0x55AA).ToUuid80(), Is.EqualTo(new Uuid80(0x0123, 0x456789ABCDEF55AA)));

			// From
			Assert.That(VersionStamp.FromUuid80(Uuid80.MaxValue), Is.EqualTo(VersionStamp.Incomplete()));
			Assert.That(VersionStamp.FromUuid80(new Uuid80(0x0123, 0x456789ABCDEF55AA)), Is.EqualTo(VersionStamp.Complete(0x0123456789ABCDEF, 0x55AA)));

			// casting
			Assert.That((Uuid80) VersionStamp.Incomplete(), Is.EqualTo(Uuid80.MaxValue));
			Assert.That((Uuid80) VersionStamp.Complete(0x0123456789ABCDEF, 0x55AA), Is.EqualTo(new Uuid80(0x0123, 0x456789ABCDEF55AA)));
			Assert.That((VersionStamp) Uuid80.MaxValue, Is.EqualTo(VersionStamp.Incomplete()));
			Assert.That((VersionStamp) new Uuid80(0x0123, 0x456789ABCDEF55AA), Is.EqualTo(VersionStamp.Complete(0x0123456789ABCDEF, 0x55AA)));

			// should fail if size does not match
			Assert.That(() => VersionStamp.Incomplete(0x1234).ToUuid80(), Throws.Exception);
			Assert.That(() => VersionStamp.Complete(0x0123456789ABCDEF, 0x55AA, 0x33CC).ToUuid80(), Throws.Exception);
		}

		[Test]
		public void Test_VersionStamp_To_Uuid96()
		{
			// To
			Assert.That(VersionStamp.Incomplete(0xFFFF).ToUuid96(), Is.EqualTo(Uuid96.MaxValue));
			Assert.That(VersionStamp.Incomplete(0x1234).ToUuid96(), Is.EqualTo(new Uuid96(0xFFFFFFFF, 0xFFFFFFFFFFFF1234UL)));
			Assert.That(VersionStamp.Complete(0x0123456789ABCDEF, 0x55AA, 0x33CC).ToUuid96(), Is.EqualTo(new Uuid96(0x01234567U, 0x89ABCDEF55AA33CCUL)));

			// From
			Assert.That(VersionStamp.FromUuid96(Uuid96.MaxValue), Is.EqualTo(VersionStamp.Incomplete(0xFFFF)));
			Assert.That(VersionStamp.FromUuid96(new Uuid96(0xFFFFFFFF, 0xFFFFFFFFFFFF1234UL)), Is.EqualTo(VersionStamp.Incomplete(0x1234)));
			Assert.That(VersionStamp.FromUuid96(new Uuid96(0x01234567U, 0x89ABCDEF55AA33CCUL)), Is.EqualTo(VersionStamp.Complete(0x0123456789ABCDEF, 0x55AA, 0x33CC)));

			// cast
			Assert.That((Uuid96) VersionStamp.Incomplete(0xFFFF), Is.EqualTo(Uuid96.MaxValue));
			Assert.That((Uuid96) VersionStamp.Incomplete(0x1234), Is.EqualTo(new Uuid96(0xFFFFFFFF, 0xFFFFFFFFFFFF1234UL)));
			Assert.That((Uuid96) VersionStamp.Complete(0x0123456789ABCDEF, 0x55AA, 0x33CC), Is.EqualTo(new Uuid96(0x01234567U, 0x89ABCDEF55AA33CCUL)));
			Assert.That((VersionStamp) Uuid96.MaxValue, Is.EqualTo(VersionStamp.Incomplete(0xFFFF)));
			Assert.That((VersionStamp) new Uuid96(0xFFFFFFFF, 0xFFFFFFFFFFFF1234UL), Is.EqualTo(VersionStamp.Incomplete(0x1234)));
			Assert.That((VersionStamp) new Uuid96(0x01234567U, 0x89ABCDEF55AA33CCUL), Is.EqualTo(VersionStamp.Complete(0x0123456789ABCDEF, 0x55AA, 0x33CC)));

			// should fail if size does not match
			Assert.That(() => VersionStamp.Incomplete().ToUuid96(), Throws.Exception);
			Assert.That(() => VersionStamp.Complete(0x0123456789ABCDEF, 0x55AA).ToUuid96(), Throws.Exception);

		}

	}

}

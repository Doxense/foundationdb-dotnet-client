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
				Dump(vs);
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
				Dump(vs);
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
				Dump(vs);
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
				Dump(vs);
				Assert.That(vs.TransactionVersion, Is.EqualTo(ulong.MaxValue));
				Assert.That(vs.TransactionOrder, Is.EqualTo(ushort.MaxValue));
				Assert.That(vs.HasUserVersion, Is.True);
				Assert.That(vs.UserVersion, Is.EqualTo(12345));
				Assert.That(vs.IsIncomplete, Is.True);
				Assert.That(vs.ToSlice().ToHexaString(' '), Is.EqualTo("FF FF FF FF FF FF FF FF FF FF 30 39"));
				Assert.That(vs.ToString(), Is.EqualTo("@?#12345"));
			}

			Assert.That(() => VersionStamp.Incomplete(-1), Throws.InstanceOf<ArgumentException>(), "User version cannot be negative");
			Assert.That(() => VersionStamp.Incomplete(65536), Throws.InstanceOf<ArgumentException>(), "User version cannot be larger than 0xFFFF");

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
				Assert.That(vs.IsIncomplete, Is.True);
			}

			{
				var buf = MutableSlice.Repeat(0xAA, 18);
				VersionStamp.Incomplete(123).WriteTo(buf.Substring(3, 12));
				Assert.That(buf.Slice.ToHexaString(' '), Is.EqualTo("AA AA AA FF FF FF FF FF FF FF FF FF FF 00 7B AA AA AA"));
			}
		}

		[Test]
		public void Test_Complete_VersionStamp()
		{
			{ // 80-bits, no user version
				var vs = VersionStamp.Complete(0x0123456789ABCDEFUL, 123);
				Dump(vs);
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
				Dump(vs);
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
				Dump(vs);
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
				Dump(vs);
				Assert.That(vs.TransactionVersion, Is.EqualTo(0x0123456789ABCDEFUL));
				Assert.That(vs.TransactionOrder, Is.EqualTo(12345));
				Assert.That(vs.UserVersion, Is.EqualTo(6789));
				Assert.That(vs.IsIncomplete, Is.False);
				Assert.That(vs.ToSlice().ToHexaString(' '), Is.EqualTo("01 23 45 67 89 AB CD EF 30 39 1A 85"));
				Assert.That(vs.ToString(), Is.EqualTo("@81985529216486895-12345#6789"));
			}

			Assert.That(() => VersionStamp.Complete(0x0123456789ABCDEFUL, 0, -1), Throws.InstanceOf<ArgumentException>(), "User version cannot be negative");
			Assert.That(() => VersionStamp.Complete(0x0123456789ABCDEFUL, 0, 65536), Throws.InstanceOf<ArgumentException>(), "User version cannot be larger than 0xFFFF");

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
				var buf = MutableSlice.Repeat(0xAA, 18);
				VersionStamp.Complete(0x0123456789ABCDEFUL, 123, 456).WriteTo(buf.Substring(3, 12));
				Assert.That(buf.Slice.ToHexaString(' '), Is.EqualTo("AA AA AA 01 23 45 67 89 AB CD EF 00 7B 01 C8 AA AA AA"));
			}
		}

		[Test]
		public void Test_VersionStamp_Equality()
		{
			// IEquatable

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

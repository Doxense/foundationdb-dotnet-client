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
	using FoundationDB.Layers.Tuples;
	using NUnit.Framework;
	using System;
	using System.Linq;
	using System.Text;

	[TestFixture]
	public class KeyFacts
	{

		[Test]
		public void Test_FdbKey_Constants()
		{
			Assert.That(FdbKey.MinValue.GetBytes(), Is.EqualTo(new byte[] { 0 }));
			Assert.That(FdbKey.MaxValue.GetBytes(), Is.EqualTo(new byte[] { 255 }));
			Assert.That(FdbKey.System.GetBytes(), Is.EqualTo(new byte[] { 255 }));
			Assert.That(FdbKey.Directory.GetBytes(), Is.EqualTo(new byte[] { 254 }));

			Assert.That(Fdb.SystemKeys.ConfigPrefix.ToString(), Is.EqualTo("<FF>/conf/"));
			Assert.That(Fdb.SystemKeys.Coordinators.ToString(), Is.EqualTo("<FF>/coordinators"));
			Assert.That(Fdb.SystemKeys.KeyServers.ToString(), Is.EqualTo("<FF>/keyServers/"));
			Assert.That(Fdb.SystemKeys.MinValue.ToString(), Is.EqualTo("<FF><00>"));
			Assert.That(Fdb.SystemKeys.MaxValue.ToString(), Is.EqualTo("<FF><FF>"));
			Assert.That(Fdb.SystemKeys.ServerKeys.ToString(), Is.EqualTo("<FF>/serverKeys/"));
			Assert.That(Fdb.SystemKeys.ServerList.ToString(), Is.EqualTo("<FF>/serverList/"));
			Assert.That(Fdb.SystemKeys.Workers.ToString(), Is.EqualTo("<FF>/workers/"));
		}

		[Test]
		public void Test_FdbKey_Increment()
		{

			var key = FdbKey.Increment(FdbKey.Ascii("Hello"));
			Assert.That(FdbKey.Ascii(key), Is.EqualTo("Hellp"));

			key = FdbKey.Increment(FdbKey.Ascii("Hello\x00"));
			Assert.That(FdbKey.Ascii(key), Is.EqualTo("Hello\x01"));

			key = FdbKey.Increment(FdbKey.Ascii("Hello\xFE"));
			Assert.That(FdbKey.Ascii(key), Is.EqualTo("Hello\xFF"));

			key = FdbKey.Increment(FdbKey.Ascii("Hello\xFF"));
			Assert.That(FdbKey.Ascii(key), Is.EqualTo("Hellp"));

			key = FdbKey.Increment(FdbKey.Ascii("A\xFF\xFF\xFF"));
			Assert.That(FdbKey.Ascii(key), Is.EqualTo("B"));

		}

		[Test]
		public void Test_FdbKey_AreEqual()
		{
			Assert.That(FdbKey.Ascii("Hello").Equals(FdbKey.Ascii("Hello")), Is.True);
			Assert.That(FdbKey.Ascii("Hello") == FdbKey.Ascii("Hello"), Is.True);

			Assert.That(FdbKey.Ascii("Hello").Equals(FdbKey.Ascii("Helloo")), Is.False);
			Assert.That(FdbKey.Ascii("Hello") == FdbKey.Ascii("Helloo"), Is.False);
		}

		[Test]
		public void Test_FdbKey_Merge()
		{
			// get a bunch of random slices
			var rnd = new Random();
			var slices = Enumerable.Range(0, 16).Select(x => Slice.Random(rnd, 4 + rnd.Next(32))).ToArray();

			var merged = FdbKey.Merge(Slice.FromByte(42), slices);
			Assert.That(merged, Is.Not.Null);
			Assert.That(merged.Length, Is.EqualTo(slices.Length));

			for (int i = 0; i < slices.Length; i++)
			{
				var expected = Slice.FromByte(42) + slices[i];
				Assert.That(merged[i], Is.EqualTo(expected));

				Assert.That(merged[i].Array, Is.SameAs(merged[0].Array), "All slices should be stored in the same buffer");
				if (i > 0) Assert.That(merged[i].Offset, Is.EqualTo(merged[i - 1].Offset + merged[i - 1].Count), "All slices should be contiguous");
			}
		}

		[Test]
		public void Test_FdbKey_Merge_Of_T()
		{
			string[] words = new string[] { "hello", "world", "très bien", "断トツ", "abc\0def", null, String.Empty };

			var merged = FdbKey.Merge(Slice.FromByte(42), words);
			Assert.That(merged, Is.Not.Null);
			Assert.That(merged.Length, Is.EqualTo(words.Length));

			for (int i = 0; i < words.Length; i++)
			{
				var expected = Slice.FromByte(42) + FdbTuple.Pack(words[i]);
				Assert.That(merged[i], Is.EqualTo(expected));

				Assert.That(merged[i].Array, Is.SameAs(merged[0].Array), "All slices should be stored in the same buffer");
				if (i > 0) Assert.That(merged[i].Offset, Is.EqualTo(merged[i - 1].Offset + merged[i - 1].Count), "All slices should be contiguous");
			}
		}
	}
}

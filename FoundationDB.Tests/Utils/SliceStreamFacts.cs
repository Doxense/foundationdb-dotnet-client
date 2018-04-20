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
	using FoundationDB.Client;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;

	[TestFixture]
	public class SliceStreamFacts : FdbTest
	{
		private const string UNICODE_TEXT = "Thïs Ïs à strîng thât contaÎns somé ùnicodè charactêrs and should be encoded in UTF-8: よろしくお願いします";
		private static readonly byte[] UNICODE_BYTES = Encoding.UTF8.GetBytes(UNICODE_TEXT);

		[Test]
		public void Test_SliceStream_Basics()
		{
			using (var stream = Slice.FromString(UNICODE_TEXT).AsStream())
			{
				Assert.That(stream, Is.Not.Null);
				Assert.That(stream.Length, Is.EqualTo(UNICODE_BYTES.Length));
				Assert.That(stream.Position, Is.EqualTo(0));

				Assert.That(stream.CanRead, Is.True);
				Assert.That(stream.CanWrite, Is.False);
				Assert.That(stream.CanSeek, Is.True);

				Assert.That(stream.CanTimeout, Is.False);
				Assert.That(() => stream.ReadTimeout, Throws.InstanceOf<InvalidOperationException>());
				Assert.That(() => stream.ReadTimeout = 123, Throws.InstanceOf<InvalidOperationException>());

				stream.Close();
				Assert.That(stream.Length, Is.EqualTo(0));
				Assert.That(stream.CanRead, Is.False);
				Assert.That(stream.CanSeek, Is.False);
			}
		}

		[Test]
		public void Test_SliceStream_ReadByte()
		{

			// ReadByte
			using (var stream = Slice.FromString(UNICODE_TEXT).AsStream())
			{
				var ms = new MemoryStream();
				int b;
				while ((b = stream.ReadByte()) >= 0)
				{
					ms.WriteByte((byte)b);
					Assert.That(ms.Length, Is.LessThanOrEqualTo(UNICODE_BYTES.Length));
				}
				Assert.That(ms.Length, Is.EqualTo(UNICODE_BYTES.Length));
				Assert.That(ms.ToArray(), Is.EqualTo(UNICODE_BYTES));
			}
		}

		[Test]
		public void Test_SliceStream_Read()
		{
			var rnd = new Random();

			// Read (all at once)
			using (var stream = Slice.FromString(UNICODE_TEXT).AsStream())
			{
				var buf = new byte[UNICODE_BYTES.Length];
				int readBytes = stream.Read(buf, 0, UNICODE_BYTES.Length);
				Assert.That(readBytes, Is.EqualTo(UNICODE_BYTES.Length));
				Assert.That(buf, Is.EqualTo(UNICODE_BYTES));
			}

			// Read (random chunks)
			for (int i = 0; i < 100; i++)
			{
				using (var stream = Slice.FromString(UNICODE_TEXT).AsStream())
				{
					var ms = new MemoryStream();

					int remaining = UNICODE_BYTES.Length;
					while (remaining > 0)
					{
						int chunkSize = 1 + rnd.Next(remaining - 1);
						var buf = new byte[chunkSize];

						int readBytes = stream.Read(buf, 0, chunkSize);
						Assert.That(readBytes, Is.EqualTo(chunkSize));

						ms.Write(buf, 0, buf.Length);
						remaining -= chunkSize;
					}

					Assert.That(ms.ToArray(), Is.EqualTo(UNICODE_BYTES));
				}
			}
		}

		[Test]
		public void Test_SliceStream_CopyTo()
		{
			// CopyTo
			using (var stream = Slice.FromString(UNICODE_TEXT).AsStream())
			{
				var ms = new MemoryStream();
				stream.CopyTo(ms);
				Assert.That(ms.Length, Is.EqualTo(UNICODE_BYTES.Length));
			}

		}
	
		[Test]
		public void Test_SliceListStream_Basics()
		{
			const int N = 65536;
			var rnd = new Random();
			Slice slice;

			// create a random buffer
			var bytes = new byte[N];
			rnd.NextBytes(bytes);

			// splits it in random slices
			var slices = new List<Slice>();
			int r = N;
			int p = 0;
			while(r > 0)
			{
				int sz = Math.Min(1 + rnd.Next(1024), r);
				slice = Slice.Create(bytes, p, sz);
				if (rnd.Next(2) == 1) slice = slice.Memoize();
				slices.Add(slice);

				p += sz;
				r -= sz;
			}
			Assert.That(slices.Sum(sl => sl.Count), Is.EqualTo(N));

			using(var stream = new SliceListStream(slices.ToArray()))
			{
				Assert.That(stream.Position, Is.EqualTo(0));
				Assert.That(stream.Length, Is.EqualTo(N));
				Assert.That(stream.CanRead, Is.True);
				Assert.That(stream.CanSeek, Is.True);
				Assert.That(stream.CanWrite, Is.False);
				Assert.That(stream.CanTimeout, Is.False);

				// CopyTo
				var ms = new MemoryStream();
				stream.CopyTo(ms);
				Assert.That(ms.ToArray(), Is.EqualTo(bytes));

				// Seek
				Assert.That(stream.Position, Is.EqualTo(N));
				Assert.That(stream.Seek(0, SeekOrigin.Begin), Is.EqualTo(0));
				Assert.That(stream.Position, Is.EqualTo(0));

				// Read All
				var buf = new byte[N];
				int readBytes = stream.Read(buf, 0, N);
				Assert.That(readBytes, Is.EqualTo(N));
				Assert.That(buf, Is.EqualTo(bytes));
			}
		}

	}
}

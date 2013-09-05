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

	[TestFixture]
	public class KeyFacts
	{

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
			Assert.That(FdbKey.Ascii(key), Is.EqualTo("Hellp\x00"));

			key = FdbKey.Increment(FdbKey.Ascii("A\xFF\xFF\xFF"));
			Assert.That(FdbKey.Ascii(key), Is.EqualTo("B\x00\x00\x00"));

		}

		[Test]
		public void Test_FdbKey_AreEqual()
		{
			Assert.That(FdbKey.Ascii("Hello").Equals(FdbKey.Ascii("Hello")), Is.True);
			Assert.That(FdbKey.Ascii("Hello") == FdbKey.Ascii("Hello"), Is.True);

			Assert.That(FdbKey.Ascii("Hello").Equals(FdbKey.Ascii("Helloo")), Is.False);
			Assert.That(FdbKey.Ascii("Hello") == FdbKey.Ascii("Helloo"), Is.False);
		}

	}
}

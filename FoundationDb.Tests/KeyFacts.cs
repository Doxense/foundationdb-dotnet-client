using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using FoundationDb.Client;
using System.Threading.Tasks;
using System.Threading;

namespace FoundationDb.Tests
{

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
			Assert.That(FdbKey.AreEqual(FdbKey.Ascii("Hello"), FdbKey.Ascii("Hello")), Is.True);
		}

	}
}

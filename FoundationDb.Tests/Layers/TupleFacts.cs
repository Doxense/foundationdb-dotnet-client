using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using FoundationDb.Client;
using System.Threading.Tasks;
using System.Threading;
using FoundationDb.Client.Tuples;
using FoundationDb.Client.Tables;
using System.Diagnostics;

namespace FoundationDb.Tests
{

	[TestFixture]
	public class TupleFacts
	{

		[Test]
		public void Test_FdbTuple_Create()
		{
			var t1 = FdbTuple.Create("hello world");
			Assert.That(t1.Count, Is.EqualTo(1));
			Assert.That(t1.Item1, Is.EqualTo("hello world"));
			Assert.That(t1[0], Is.EqualTo("hello world"));

			var t2 = FdbTuple.Create("hello world", 123);
			Assert.That(t2.Count, Is.EqualTo(2));
			Assert.That(t2.Item1, Is.EqualTo("hello world"));
			Assert.That(t2.Item2, Is.EqualTo(123));
			Assert.That(t2[0], Is.EqualTo("hello world"));
			Assert.That(t2[1], Is.EqualTo(123));

			var t3 = FdbTuple.Create("hello world", 123, false);
			Assert.That(t3.Count, Is.EqualTo(3));
			Assert.That(t3.Item1, Is.EqualTo("hello world"));
			Assert.That(t3.Item2, Is.EqualTo(123));
			Assert.That(t3.Item3, Is.EqualTo(false));
			Assert.That(t3[0], Is.EqualTo("hello world"));
			Assert.That(t3[1], Is.EqualTo(123));
			Assert.That(t3[2], Is.EqualTo(false));

			var tn = FdbTuple.Create(new object[] { "hello world", 123, false, "four", "five", "six" });
			Assert.That(tn.Count, Is.EqualTo(6));
			Assert.That(tn[0], Is.EqualTo("hello world"));
			Assert.That(tn[1], Is.EqualTo(123));
			Assert.That(tn[2], Is.EqualTo(false));
			Assert.That(tn[3], Is.EqualTo("four"));
			Assert.That(tn[4], Is.EqualTo("five"));
			Assert.That(tn[5], Is.EqualTo("six"));
		}

		[Test]
		public void Test_FdbTuple_SameBytes()
		{
			IFdbTuple t1 = FdbTuple.Create("hello world");
			IFdbTuple t2 = FdbTuple.Create(new object[] { "hello world" });

			Assert.That(t1.ToBytes(), Is.EquivalentTo(t2.ToBytes()));

			t1 = FdbTuple.Create("hello world", 123);
			t2 = FdbTuple.Create("hello world").Append(123);

			Assert.That(t1.ToBytes(), Is.EquivalentTo(t2.ToBytes()));
			
		}

		[Test]
		public void Test_FdbTuple_Pack()
		{
			Assert.That(FdbKey.Dump(FdbTuple.Create("hello world").ToArraySegment()),
				Is.EqualTo("<02>hello world<00>")
			);

			Assert.That(FdbKey.Dump(FdbTuple.Create("hello world", 123).ToArraySegment()),
				Is.EqualTo("<02>hello world<00><15>{")
			);

			Assert.That(FdbKey.Dump(FdbTuple.Create("hello world", 123, false).ToArraySegment()),
				Is.EqualTo("<02>hello world<00><15>{<14>")
			);

			Assert.That(
				FdbKey.Dump(FdbTuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }).ToArraySegment()),
				Is.EqualTo("<02>hello world<00><15>{<14><01>{<01>B<00><FF>*<00>")
			);

		}

	}

}

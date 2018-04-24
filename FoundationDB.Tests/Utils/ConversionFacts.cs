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

namespace FoundationDB.Client.Converters.Tests
{
	using System;
	using Doxense.Runtime.Converters;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;

	[TestFixture]
	public class ConversionFacts : FdbTest
	{

		[Test]
		public void Test_Values_Of_Same_Types_Are_Always_Similar()
		{
			Action<object, object> test = (x, y) =>
			{
				bool expected = x == null ? y == null : y != null && x.Equals(y);
				Assert.That(ComparisonHelper.AreSimilar(x, y), Is.EqualTo(expected), expected ? "{0} == {1}" : "{0} != {1}", x, y);
			};

			test(null, null);

			test("hello", "world");
			test("hello", "hello");
			test("hello", "Hello");
			test("hello", null);
			test(null, "world");

			test(123, 123);
			test(123, 456);
			test(123, null);
			test(null, 456);

			test(123L, 123L);
			test(123L, 456L);
			test(123L, null);
			test(null, 456L);

		}

		[Test]
		public void Test_Values_Of_Similar_Types_Are_Similar()
		{
			Action<object, object> similar = (x, y) =>
			{
				if (!ComparisonHelper.AreSimilar(x, y))
				{
					Assert.Fail("({0}) {1} ~= ({2}) {3}", x == null ? "object" : x.GetType().Name, x, y == null ? "object" : y.GetType().Name, y);
				}
			};

			Action<object, object> different = (x, y) =>
			{
				if (ComparisonHelper.AreSimilar(x, y))
				{
					Assert.Fail("({0}) {1} !~= ({2}) {3}", x == null ? "object" : x.GetType().Name, x, y == null ? "object" : y.GetType().Name, y);
				}
			};

			different("hello", 123);
			different(123, "hello");

			similar("A", 'A');
			similar('A', "A");
			different("AA", 'A');
			different('A', "AA");
			different("A", 'B');
			different('A', "B");

			similar("123", 123);
			similar("123", 123L);
			similar("123.4", 123.4f);
			similar("123.4", 123.4d);

			similar(123, "123");
			similar(123L, "123");
			similar(123.4f, "123.4");
			similar(123.4d, "123.4");

			var g = Guid.NewGuid();

			similar(g, g.ToString());
			similar(g.ToString(), g);

			different(g.ToString(), Guid.Empty);
			different(Guid.Empty, g.ToString());

		}

	}
}

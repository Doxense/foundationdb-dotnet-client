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
	using System.Text;

	[TestFixture]
	public class SliceHelperFacts : FdbTest
	{

		#region SliceHelpers...

		[Test]
		public void Test_SliceHelpers_Align()
		{
			// Even though 0 is a multiple of 16, it is always rounded up to 16 to simplify buffer handling logic
			Assert.That(SliceHelpers.Align(0), Is.EqualTo(16));
			// 1..16 => 16
			for (int i = 1; i <= 16; i++) { Assert.That(SliceHelpers.Align(i), Is.EqualTo(16), "Align({0}) => 16", i); }
			// 17..32 => 32
			for (int i = 17; i <= 32; i++) { Assert.That(SliceHelpers.Align(i), Is.EqualTo(32), "Align({0}) => 32", i); }
			// 33..48 => 48
			for (int i = 33; i <= 48; i++) { Assert.That(SliceHelpers.Align(i), Is.EqualTo(48), "Align({0}) => 48", i); }

			// 2^N-1
			for (int i = 6; i < 30; i++)
			{
				Assert.That(SliceHelpers.Align((1 << i) - 1), Is.EqualTo(1 << i));
			}
			// largest non overflowing
			Assert.That(() => SliceHelpers.Align(int.MaxValue - 15), Is.EqualTo((int.MaxValue - 15)));

			// overflow
			Assert.That(() => SliceHelpers.Align(int.MaxValue), Throws.InstanceOf<OverflowException>());
			Assert.That(() => SliceHelpers.Align(int.MaxValue - 14), Throws.InstanceOf<OverflowException>());

			// negative values
			Assert.That(() => SliceHelpers.Align(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => SliceHelpers.Align(int.MinValue), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

		[Test]
		public void Test_SliceHelpers_NextPowerOfTwo()
		{
			// 0 is a special case, to simplify bugger handling logic
			Assert.That(SliceHelpers.NextPowerOfTwo(0), Is.EqualTo(1), "Special case for 0");
			Assert.That(SliceHelpers.NextPowerOfTwo(1), Is.EqualTo(1));
			Assert.That(SliceHelpers.NextPowerOfTwo(2), Is.EqualTo(2));

			for (int i = 2; i < 31; i++)
			{
				Assert.That(SliceHelpers.NextPowerOfTwo((1 << i) - 1), Is.EqualTo(1 << i));
				Assert.That(SliceHelpers.NextPowerOfTwo(1 << i), Is.EqualTo(1 << i));
			}

			Assert.That(() => SliceHelpers.NextPowerOfTwo(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => SliceHelpers.NextPowerOfTwo(-42), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

		[Test]
		public void Test_SliceHelpers_ComputeHashCode()
		{
			//note: if everything fails, check that the hashcode algorithm hasn't changed also !

			Assert.That(SliceHelpers.ComputeHashCode(new byte[0], 0, 0), Is.EqualTo(-2128831035));
			Assert.That(SliceHelpers.ComputeHashCode(new byte[1], 0, 1), Is.EqualTo(84696351));
			Assert.That(SliceHelpers.ComputeHashCode(new byte[2], 0, 1), Is.EqualTo(84696351));
			Assert.That(SliceHelpers.ComputeHashCode(new byte[2], 1, 1), Is.EqualTo(84696351));
			Assert.That(SliceHelpers.ComputeHashCode(new byte[2], 0, 2), Is.EqualTo(292984781));
			Assert.That(SliceHelpers.ComputeHashCode(Encoding.Default.GetBytes("hello"), 0, 5), Is.EqualTo(1335831723));

			Assert.That(SliceHelpers.ComputeHashCodeUnsafe(new byte[0], 0, 0), Is.EqualTo(-2128831035));
			Assert.That(SliceHelpers.ComputeHashCodeUnsafe(new byte[1], 0, 1), Is.EqualTo(84696351));
			Assert.That(SliceHelpers.ComputeHashCodeUnsafe(new byte[2], 0, 1), Is.EqualTo(84696351));
			Assert.That(SliceHelpers.ComputeHashCodeUnsafe(new byte[2], 1, 1), Is.EqualTo(84696351));
			Assert.That(SliceHelpers.ComputeHashCodeUnsafe(new byte[2], 0, 2), Is.EqualTo(292984781));
			Assert.That(SliceHelpers.ComputeHashCodeUnsafe(Encoding.Default.GetBytes("hello"), 0, 5), Is.EqualTo(1335831723));
		}

		#endregion

	}
}

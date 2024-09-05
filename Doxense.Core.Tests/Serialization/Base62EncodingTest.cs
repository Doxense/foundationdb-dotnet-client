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

namespace Doxense.Serialization.Tests
{
	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class Base62EncodingTest : SimpleTest
	{

		[Test]
		public void Test_Known_Ids_Short()
		{
			Assert.That(Base62Encoding.Encode(0), Is.EqualTo("a"));
			Assert.That(Base62Encoding.Encode(1), Is.EqualTo("b"));
			Assert.That(Base62Encoding.Encode(22), Is.EqualTo("w"));
			Assert.That(Base62Encoding.Encode(333), Is.EqualTo("fx"));
			Assert.That(Base62Encoding.Encode(4444), Is.EqualTo("bjG"));
			Assert.That(Base62Encoding.Encode(55555), Is.EqualTo("o2d"));
			Assert.That(Base62Encoding.Encode(666666), Is.EqualTo("cN0G"));
			Assert.That(Base62Encoding.Encode(7777777), Is.EqualTo("6Dwb"));
			Assert.That(Base62Encoding.Encode(88888888), Is.EqualTo("gaYdK"));
			Assert.That(Base62Encoding.Encode(999999999), Is.EqualTo("bfFTGp"));
			Assert.That(Base62Encoding.Encode(1234567890), Is.EqualTo("bv8h5u"));

			Assert.That(Base62Encoding.Encode(1234), Is.EqualTo("tU"));

			Assert.That(Base62Encoding.Encode(int.MaxValue), Is.EqualTo("cvuCBb"));
			Assert.That(Base62Encoding.Encode(int.MinValue), Is.EqualTo("cvuCBc"));
			Assert.That(Base62Encoding.Encode(-1), Is.EqualTo("eGFpmd"));

			Assert.That(Base62Encoding.Encode(long.MaxValue), Is.EqualTo("kZviNa8fiMh"));
			Assert.That(Base62Encoding.Encode(long.MinValue), Is.EqualTo("kZviNa8fiMi"));
			Assert.That(Base62Encoding.Encode(-1L), Is.EqualTo("vYGrAbgkr8p"));
		}

		[Test]
		public void Test_Known_Ids_Int32_Padded()
		{
			Assert.That(Base62Encoding.Encode(0, Base62FormattingOptions.Padded), Is.EqualTo("aaaaaa"));
			Assert.That(Base62Encoding.Encode(1, Base62FormattingOptions.Padded), Is.EqualTo("aaaaab"));
			Assert.That(Base62Encoding.Encode(22, Base62FormattingOptions.Padded), Is.EqualTo("aaaaaw"));
			Assert.That(Base62Encoding.Encode(333, Base62FormattingOptions.Padded), Is.EqualTo("aaaafx"));
			Assert.That(Base62Encoding.Encode(4444, Base62FormattingOptions.Padded), Is.EqualTo("aaabjG"));
			Assert.That(Base62Encoding.Encode(55555, Base62FormattingOptions.Padded), Is.EqualTo("aaao2d"));
			Assert.That(Base62Encoding.Encode(666666, Base62FormattingOptions.Padded), Is.EqualTo("aacN0G"));
			Assert.That(Base62Encoding.Encode(7777777, Base62FormattingOptions.Padded), Is.EqualTo("aa6Dwb"));
			Assert.That(Base62Encoding.Encode(88888888, Base62FormattingOptions.Padded), Is.EqualTo("agaYdK"));
			Assert.That(Base62Encoding.Encode(999999999, Base62FormattingOptions.Padded), Is.EqualTo("bfFTGp"));
			Assert.That(Base62Encoding.Encode(1234567890, Base62FormattingOptions.Padded), Is.EqualTo("bv8h5u"));

			Assert.That(Base62Encoding.Encode(1234, Base62FormattingOptions.Padded), Is.EqualTo("aaaatU"));
			Assert.That(Base62Encoding.Encode(int.MaxValue, Base62FormattingOptions.Padded), Is.EqualTo("cvuCBb"));
			Assert.That(Base62Encoding.Encode(int.MinValue, Base62FormattingOptions.Padded), Is.EqualTo("cvuCBc"));
			Assert.That(Base62Encoding.Encode(-1, Base62FormattingOptions.Padded), Is.EqualTo("eGFpmd"));
		}

		[Test]
		public void Test_Known_Ids_Int32_Lexicographic_Padded()
		{
			Assert.That(Base62Encoding.EncodeSortable(0), Is.EqualTo("000000"));
			Assert.That(Base62Encoding.EncodeSortable(1), Is.EqualTo("000001"));
			Assert.That(Base62Encoding.EncodeSortable(22), Is.EqualTo("00000M"));
			Assert.That(Base62Encoding.EncodeSortable(333), Is.EqualTo("00005N"));
			Assert.That(Base62Encoding.EncodeSortable(4444), Is.EqualTo("00019g"));
			Assert.That(Base62Encoding.EncodeSortable(55555), Is.EqualTo("000ES3"));
			Assert.That(Base62Encoding.EncodeSortable(666666), Is.EqualTo("002nQg"));
			Assert.That(Base62Encoding.EncodeSortable(7777777), Is.EqualTo("00WdM1"));
			Assert.That(Base62Encoding.EncodeSortable(88888888), Is.EqualTo("060y3k"));
			Assert.That(Base62Encoding.EncodeSortable(999999999), Is.EqualTo("15ftgF"));
			Assert.That(Base62Encoding.EncodeSortable(1234567890), Is.EqualTo("1LY7VK"));

			Assert.That(Base62Encoding.EncodeSortable(1234), Is.EqualTo("0000Ju"));
			Assert.That(Base62Encoding.EncodeSortable(int.MaxValue), Is.EqualTo("2LKcb1"));
			Assert.That(Base62Encoding.EncodeSortable(int.MinValue), Is.EqualTo("2LKcb2"));
			Assert.That(Base62Encoding.EncodeSortable(-1), Is.EqualTo("4gfFC3"));
		}

		[Test]
		public void Test_Known_Ids_Int64_Padded()
		{
			Assert.That(Base62Encoding.Encode(0L, Base62FormattingOptions.Padded), Is.EqualTo("aaaaaaaaaaa"));
			Assert.That(Base62Encoding.Encode(1L, Base62FormattingOptions.Padded), Is.EqualTo("aaaaaaaaaab"));
			Assert.That(Base62Encoding.Encode(22L, Base62FormattingOptions.Padded), Is.EqualTo("aaaaaaaaaaw"));
			Assert.That(Base62Encoding.Encode(333L, Base62FormattingOptions.Padded), Is.EqualTo("aaaaaaaaafx"));
			Assert.That(Base62Encoding.Encode(4444L, Base62FormattingOptions.Padded), Is.EqualTo("aaaaaaaabjG"));
			Assert.That(Base62Encoding.Encode(55555L, Base62FormattingOptions.Padded), Is.EqualTo("aaaaaaaao2d"));
			Assert.That(Base62Encoding.Encode(666666L, Base62FormattingOptions.Padded), Is.EqualTo("aaaaaaacN0G"));
			Assert.That(Base62Encoding.Encode(7777777L, Base62FormattingOptions.Padded), Is.EqualTo("aaaaaaa6Dwb"));
			Assert.That(Base62Encoding.Encode(88888888L, Base62FormattingOptions.Padded), Is.EqualTo("aaaaaagaYdK"));
			Assert.That(Base62Encoding.Encode(999999999L, Base62FormattingOptions.Padded), Is.EqualTo("aaaaabfFTGp"));
			Assert.That(Base62Encoding.Encode(1234567890L, Base62FormattingOptions.Padded), Is.EqualTo("aaaaabv8h5u"));

			Assert.That(Base62Encoding.Encode(1234L, Base62FormattingOptions.Padded), Is.EqualTo("aaaaaaaaatU"));
			Assert.That(Base62Encoding.Encode(long.MaxValue, Base62FormattingOptions.Padded), Is.EqualTo("kZviNa8fiMh"));
			Assert.That(Base62Encoding.Encode(long.MinValue, Base62FormattingOptions.Padded), Is.EqualTo("kZviNa8fiMi"));
			Assert.That(Base62Encoding.Encode(-1L, Base62FormattingOptions.Padded), Is.EqualTo("vYGrAbgkr8p"));
		}

		[Test]
		public void Test_Known_Ids_Int64_Lexicographic_Padded()
		{
			Assert.That(Base62Encoding.EncodeSortable(0L), Is.EqualTo("00000000000"));
			Assert.That(Base62Encoding.EncodeSortable(1L), Is.EqualTo("00000000001"));
			Assert.That(Base62Encoding.EncodeSortable(22L), Is.EqualTo("0000000000M"));
			Assert.That(Base62Encoding.EncodeSortable(333L), Is.EqualTo("0000000005N"));
			Assert.That(Base62Encoding.EncodeSortable(4444L), Is.EqualTo("0000000019g"));
			Assert.That(Base62Encoding.EncodeSortable(55555L), Is.EqualTo("00000000ES3"));
			Assert.That(Base62Encoding.EncodeSortable(666666L), Is.EqualTo("00000002nQg"));
			Assert.That(Base62Encoding.EncodeSortable(7777777L), Is.EqualTo("0000000WdM1"));
			Assert.That(Base62Encoding.EncodeSortable(88888888L), Is.EqualTo("00000060y3k"));
			Assert.That(Base62Encoding.EncodeSortable(999999999L), Is.EqualTo("0000015ftgF"));
			Assert.That(Base62Encoding.EncodeSortable(1234567890L), Is.EqualTo("000001LY7VK"));

			Assert.That(Base62Encoding.EncodeSortable(1234L), Is.EqualTo("000000000Ju"));
			Assert.That(Base62Encoding.EncodeSortable(long.MaxValue), Is.EqualTo("AzL8n0Y58m7"));
			Assert.That(Base62Encoding.EncodeSortable(long.MinValue), Is.EqualTo("AzL8n0Y58m8"));
			Assert.That(Base62Encoding.EncodeSortable(-1L), Is.EqualTo("LygHa16AHYF"));
		}
	}

}

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

#nullable disable

// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

namespace Doxense.Serialization.Json.Tests
{
	using System.Numerics;

	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	public class JsonNumericsFacts : SimpleTest
	{

		[Test]
		public void Test_JsonNumber_INumber_Additions()
		{
			// Validate the INumber<T> arithmetic for addition

			Assert.That(JsonNumber.Zero + JsonNumber.Zero, Is.EqualTo(0));
			Assert.That(JsonNumber.Zero + JsonNumber.One, Is.EqualTo(1));
			Assert.That(JsonNumber.One + JsonNumber.Zero, Is.EqualTo(1));
			Assert.That(JsonNumber.One + JsonNumber.One, Is.EqualTo(2));

			Assert.That(JsonNumber.Return(123) + JsonNumber.Return(456), Is.EqualTo(579));
			Assert.That(JsonNumber.Return(-123) + JsonNumber.Return(123), Is.EqualTo(0));
			Assert.That(JsonNumber.Return(+123) + JsonNumber.Return(-123), Is.EqualTo(0));
			Assert.That(JsonNumber.Return(0) + JsonNumber.Return(long.MaxValue), Is.EqualTo(long.MaxValue));
			Assert.That(JsonNumber.Return(0) + JsonNumber.Return(ulong.MaxValue), Is.EqualTo(ulong.MaxValue));
			Assert.That(JsonNumber.Return(1) + JsonNumber.Return(long.MaxValue), Is.EqualTo(9223372036854775808UL));
			Assert.That(JsonNumber.Return(long.MaxValue) + JsonNumber.Return(0), Is.EqualTo(long.MaxValue));
			Assert.That(JsonNumber.Return(long.MaxValue) + JsonNumber.Return(1), Is.EqualTo(9223372036854775808UL));
			Assert.That(JsonNumber.Return(long.MaxValue) + JsonNumber.Return(long.MaxValue), Is.EqualTo(2UL * long.MaxValue));
			Assert.That(JsonNumber.Return(long.MaxValue) + JsonNumber.Return(-long.MaxValue), Is.EqualTo(0));
			Assert.That(JsonNumber.Return(ulong.MaxValue) + JsonNumber.Return(0), Is.EqualTo(ulong.MaxValue));
			Assert.That(JsonNumber.Return(ulong.MaxValue) + JsonNumber.Return(-1), Is.EqualTo(ulong.MaxValue - 1));
			Assert.That(JsonNumber.Return(ulong.MaxValue) + JsonNumber.Return(-1), Is.EqualTo(ulong.MaxValue - 1));

			Assert.That(JsonNumber.Zero + JsonNumber.NaN, Is.EqualTo(double.NaN));
			Assert.That(JsonNumber.One + JsonNumber.NaN, Is.EqualTo(double.NaN));
			Assert.That(JsonNumber.Return(2) + JsonNumber.NaN, Is.EqualTo(double.NaN));
			Assert.That(JsonNumber.NaN + JsonNumber.Zero, Is.EqualTo(double.NaN));
			Assert.That(JsonNumber.NaN + JsonNumber.One, Is.EqualTo(double.NaN));
			Assert.That(JsonNumber.NaN + JsonNumber.Return(2), Is.EqualTo(double.NaN));
			Assert.That(JsonNumber.NaN + JsonNumber.NaN, Is.EqualTo(double.NaN));
		}

		[Test]
		public void Test_JsonNumber_INumber_Subtractions()
		{
			// Validate the INumber<T> arithmetic for subtraction

			Assert.That(JsonNumber.Zero - JsonNumber.Zero, Is.EqualTo(0));
			Assert.That(JsonNumber.Zero - JsonNumber.One, Is.EqualTo(-1));
			Assert.That(JsonNumber.One - JsonNumber.Zero, Is.EqualTo(1));
			Assert.That(JsonNumber.One - JsonNumber.One, Is.EqualTo(0));

			Assert.That(JsonNumber.Return(456) - JsonNumber.Return(123), Is.EqualTo(333));
			Assert.That(JsonNumber.Return(123) - JsonNumber.Return(456), Is.EqualTo(-333));
			Assert.That(JsonNumber.Return(+123) - JsonNumber.Return(+123), Is.EqualTo(0));
			Assert.That(JsonNumber.Return(+123) - JsonNumber.Return(-123), Is.EqualTo(246));
			Assert.That(JsonNumber.Return(-123) - JsonNumber.Return(+123), Is.EqualTo(-246));
			Assert.That(JsonNumber.Return(-123) - JsonNumber.Return(-123), Is.EqualTo(0));
			Assert.That(JsonNumber.Return(0) - JsonNumber.Return(long.MaxValue), Is.EqualTo(-9223372036854775807));
			Assert.That(JsonNumber.Return(-1) - JsonNumber.Return(long.MaxValue), Is.EqualTo(long.MinValue));
			Assert.That(JsonNumber.Return(long.MaxValue) - JsonNumber.Return(0), Is.EqualTo(9223372036854775807));
			Assert.That(JsonNumber.Return(long.MaxValue) - JsonNumber.Return(1), Is.EqualTo(9223372036854775806));
			Assert.That(JsonNumber.Return(long.MaxValue) - JsonNumber.Return(long.MaxValue), Is.EqualTo(0));
			Assert.That(JsonNumber.Return(ulong.MaxValue) - JsonNumber.Return(0), Is.EqualTo(ulong.MaxValue));
			Assert.That(JsonNumber.Return(ulong.MaxValue) - JsonNumber.Return(1), Is.EqualTo(ulong.MaxValue - 1));
			Assert.That(JsonNumber.Return(ulong.MaxValue) - JsonNumber.Return(ulong.MaxValue), Is.EqualTo(0));
			Assert.That(JsonNumber.Return(ulong.MaxValue) - JsonNumber.Return(ulong.MaxValue - 1), Is.EqualTo(1));
			Assert.That(JsonNumber.Return(ulong.MaxValue - 1) - JsonNumber.Return(ulong.MaxValue), Is.EqualTo(-1));

			Assert.That(JsonNumber.Zero - JsonNumber.NaN, Is.EqualTo(double.NaN));
			Assert.That(JsonNumber.One - JsonNumber.NaN, Is.EqualTo(double.NaN));
			Assert.That(JsonNumber.Return(2) - JsonNumber.NaN, Is.EqualTo(double.NaN));
			Assert.That(JsonNumber.NaN - JsonNumber.Zero, Is.EqualTo(double.NaN));
			Assert.That(JsonNumber.NaN - JsonNumber.One, Is.EqualTo(double.NaN));
			Assert.That(JsonNumber.NaN - JsonNumber.Return(2), Is.EqualTo(double.NaN));
			Assert.That(JsonNumber.NaN - JsonNumber.NaN, Is.EqualTo(double.NaN));
		}

		[Test]
		public void Test_JsonNumber_INumber_Multiplications()
		{
			// Validate the INumber<T> arithmetic for multiplication

			Assert.That(JsonNumber.Zero * JsonNumber.Zero, Is.EqualTo(0));
			Assert.That(JsonNumber.Zero * JsonNumber.One, Is.EqualTo(0));
			Assert.That(JsonNumber.One * JsonNumber.Zero, Is.EqualTo(0));
			Assert.That(JsonNumber.One * JsonNumber.One, Is.EqualTo(1));

			Assert.That(JsonNumber.Return(123) * JsonNumber.Return(456), Is.EqualTo(56088));
			Assert.That(JsonNumber.Return(456) * JsonNumber.Return(123), Is.EqualTo(56088));
			Assert.That(JsonNumber.One * JsonNumber.Return(long.MaxValue), Is.EqualTo(long.MaxValue));
			Assert.That(JsonNumber.Return(2) * JsonNumber.Return(long.MaxValue), Is.EqualTo(2UL * long.MaxValue));

			Assert.That(JsonNumber.Zero * JsonNumber.PI, Is.EqualTo(0.0));
			Assert.That(JsonNumber.One * JsonNumber.PI, Is.EqualTo(Math.PI));
			Assert.That(JsonNumber.Return(2) * JsonNumber.PI, Is.EqualTo(2.0 * Math.PI));
			Assert.That(JsonNumber.PI * JsonNumber.Zero, Is.EqualTo(0.0));
			Assert.That(JsonNumber.PI * JsonNumber.One, Is.EqualTo(Math.PI));
			Assert.That(JsonNumber.PI * JsonNumber.Return(2), Is.EqualTo(Math.PI * 2.0));

			Assert.That(JsonNumber.DecimalZero * JsonNumber.PI, Is.EqualTo(0.0));
			Assert.That(JsonNumber.DecimalOne * JsonNumber.PI, Is.EqualTo(Math.PI));
			Assert.That(JsonNumber.Return(2.0) * JsonNumber.PI, Is.EqualTo(2.0 * Math.PI));
			Assert.That(JsonNumber.PI * JsonNumber.DecimalZero, Is.EqualTo(0.0));
			Assert.That(JsonNumber.PI * JsonNumber.DecimalOne, Is.EqualTo(Math.PI));
			Assert.That(JsonNumber.PI * JsonNumber.Return(2.0), Is.EqualTo(Math.PI * 2.0));
			Assert.That(JsonNumber.PI * JsonNumber.PI, Is.EqualTo(Math.PI * Math.PI));

			Assert.That(JsonNumber.Zero * JsonNumber.NaN, Is.EqualTo(double.NaN));
			Assert.That(JsonNumber.One * JsonNumber.NaN, Is.EqualTo(double.NaN));
			Assert.That(JsonNumber.Return(2) * JsonNumber.NaN, Is.EqualTo(double.NaN));
			Assert.That(JsonNumber.NaN * JsonNumber.Zero, Is.EqualTo(double.NaN));
			Assert.That(JsonNumber.NaN * JsonNumber.One, Is.EqualTo(double.NaN));
			Assert.That(JsonNumber.NaN * JsonNumber.Return(2), Is.EqualTo(double.NaN));
			Assert.That(JsonNumber.NaN * JsonNumber.NaN, Is.EqualTo(double.NaN));
		}

#if NET8_0_OR_GREATER

		[Test]
		public void Test_JsonValue_INumber_Addition()
		{
			static TNumber AddNumbers<TNumber>(TNumber a, TNumber b) where TNumber : INumberBase<TNumber> => a + b;

			static JsonValue Add(JsonValue a, JsonValue b) => AddNumbers(a, b);

			Assert.That(Add(123, 456), IsJson.EqualTo(579));
			Assert.That(Add(-123, 456), IsJson.EqualTo(333));
			Assert.That(Add(-123, -456), IsJson.EqualTo(-579));
			Assert.That(Add(123, -456), IsJson.EqualTo(-333));

			Assert.That(Add(1.23d, 4.56d), IsJson.EqualTo(5.79d));
			Assert.That(Add(1.23f, 4.56f), IsJson.EqualTo(5.79f));
			Assert.That(Add(Math.PI, -Math.PI), IsJson.EqualTo(0));

			Assert.That(Add(int.MaxValue, int.MaxValue), IsJson.EqualTo(2L * int.MaxValue));
			Assert.That(Add(int.MinValue, int.MinValue), IsJson.EqualTo(2L * int.MinValue));

		}

		[Test]
		public void Test_JsonNumber_Can_By_Used_With_Generic_Arithmetic()
		{
			JsonNumber a = JsonNumber.One;
			JsonNumber b = JsonNumber.PI;
			GenericINumber(a, b);
		}

		[Test]
		public void Test_JsonValue_Can_By_Used_With_Generic_Arithmetic()
		{
			JsonValue a = JsonNumber.One;
			JsonValue b = JsonNumber.PI;
			GenericINumber(a, b);
		}

		private static void GenericINumber<T>(T x, T y) where T : INumberBase<T>
		{
			_ = x + T.Zero;
			_ = x + y;
			_ = x - y;
			_ = x * T.One;
			_ = x * y;
			_ = x / y;
			_ = x++;
			_ = ++x;
			_ = y--;
			_ = --y;
		}

		[Test]
		public void Test_JsonValue_IComparisonOperators()
		{

			static bool Equal<T>(T x, T y) where T : IComparisonOperators<T, T, bool> => x == y;

			static bool NotEqual<T>(T x, T y) where T : IComparisonOperators<T, T, bool> => x != y;

			static bool GreaterThan<T>(T x, T y) where T : IComparisonOperators<T, T, bool> => x > y;

			static bool GreaterOrEqual<T>(T x, T y) where T : IComparisonOperators<T, T, bool> => x >= y;

			static bool LessThan<T>(T x, T y) where T: IComparisonOperators<T, T, bool> => x < y;

			static bool LessOrEqual<T>(T x, T y) where T : IComparisonOperators<T, T, bool> => x <= y;

			// use random numbers, so that we don't end up just testing reference equality between cached small numbers
			int[] nums = [NextInt32(), NextInt32(), NextInt32()];
			Assume.That(nums, Is.Unique);
			Array.Sort(nums);

			JsonValue x0 = JsonNumber.Return(nums[0]);
			JsonValue x1 = JsonNumber.Return(nums[1]);
			JsonValue x2 = JsonNumber.Return(nums[2]);

			Assert.Multiple(() =>
			{
				Assert.That(Equal(x0, x0), Is.True);
				Assert.That(Equal(x0, x1), Is.False);
				Assert.That(Equal(x0, x2), Is.False);

				Assert.That(NotEqual(x0, x0), Is.False);
				Assert.That(NotEqual(x0, x1), Is.True);
				Assert.That(NotEqual(x0, x2), Is.True);

				Assert.That(LessThan(x0, x0), Is.False);
				Assert.That(LessThan(x0, x1), Is.True);
				Assert.That(LessThan(x0, x2), Is.True);

 				Assert.That(LessOrEqual(x0, x0), Is.True);
				Assert.That(LessOrEqual(x0, x1), Is.True);
				Assert.That(LessOrEqual(x0, x2), Is.True);

				Assert.That(GreaterThan(x0, x0), Is.False);
				Assert.That(GreaterThan(x0, x1), Is.False);
				Assert.That(GreaterThan(x0, x2), Is.False);

				Assert.That(GreaterOrEqual(x0, x0), Is.True);
				Assert.That(GreaterOrEqual(x0, x1), Is.False);
				Assert.That(GreaterOrEqual(x0, x2), Is.False);
			});
		}

#endif

	}

}

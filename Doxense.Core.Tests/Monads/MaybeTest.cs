#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

// ReSharper disable CompareOfFloatsByEqualityOperator
namespace Doxense.Monads.Tests
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using NUnit.Framework;

	/// <summary>Tests sur l'objet DynamicCache</summary>
	[TestFixture]
	[Category("Core-SDK")]
	public class MaybeTest
	{

		[Test]
		public void Test_Maybe_Holds_To_Value()
		{
			var m = Maybe.Return("Hello World");

			Assert.That(m.HasValue, Is.True);
			Assert.That(m.Value, Is.EqualTo("Hello World"));
			Assert.That(m.Error, Is.Null);
		}

		[Test]
		public void Test_Nothing_Does_Not_Have_Value()
		{
			var m = Maybe<string>.Nothing;

			Assert.That(m.HasValue, Is.False);
			Assert.That(m.Error, Is.Null);
			Assert.That(() => { var x = m.Value; }, Throws.InvalidOperationException);
		}

		[Test]
		public void Test_Maybe_Propagates_Errors()
		{
			var boom = new Exception("EPIC FAIL");

			var m = Maybe<string>.Failure(boom);

			Assert.That(m.HasValue, Is.False);
			Assert.That(m.Error, Is.SameAs(boom));

			var aggEx = Assert.Throws<AggregateException>(() => { var x = m.Value; });
			Assert.That(aggEx.InnerException, Is.SameAs(boom));
		}

		[Test]
		public void Test_Maybe_Equality()
		{
			{ // M<T>.Equals(M<T>), M<T> == M<T>, M<T> != M<T>

				var ms = new[]
				{
					Maybe.Nothing<string>(),
					Maybe.Return("Hello"),
					Maybe.Return("World"),
					Maybe.Return(default(string)),
					Maybe.Error<string>(new InvalidOperationException("KABOOM!")),
				};

				for (int i = 0; i < ms.Length; i++)
				{
					for (int j = 0; j < ms.Length; j++)
					{
						if (i == j)
						{
							Assert.That(ms[i].Equals(ms[j]), Is.True, "{0}.Equals({1})", ms[i], ms[j]);
							Assert.That(ms[i] == ms[j], Is.True, "{0} ==  {1}", ms[i], ms[j]);
							Assert.That(ms[i] != ms[j], Is.False, "{0} !=  {1}", ms[i], ms[j]);
							Assert.That(ms[i].Equals((object) ms[j]), Is.True, "{0}.Equals((object) {1})", ms[i], ms[j]);
						}
						else
						{
							Assert.That(ms[i].Equals(ms[j]), Is.False, "{0}.Equals({1})", ms[i], ms[j]);
							Assert.That(ms[i] == ms[j], Is.False, "{0} ==  {1}", ms[i], ms[j]);
							Assert.That(ms[i] != ms[j], Is.True, "{0} !=  {1}", ms[i], ms[j]);
							Assert.That(ms[i].Equals((object) ms[j]), Is.False, "{0}.Equals((object) {1})", ms[i], ms[j]);
						}
					}
				}
			}

			{ // M<T>.Equals(T)

				Assert.That(Maybe.Nothing<string>().Equals("Hello"), Is.False, "<nothing> eq 'Hello'");
				Assert.That(Maybe.Return(default(string)).Equals("Hello"), Is.False, "<null> eq 'World'");
				Assert.That(Maybe.Return("Hello").Equals("Hello"), Is.True, "'Hello' eq 'Hello'");
				Assert.That(Maybe.Return("World").Equals("Hello"), Is.False, "'World' eq 'World'");
				Assert.That(Maybe.Return("Hello").Equals(default(string)), Is.False, "'Hello' eq <null>");
				Assert.That(Maybe.Error<string>(new InvalidOperationException("KABOOM!")).Equals("Hello"), Is.False, "<error> eq 'World'");
			}

			// corner cases
			Assert.That(Maybe.Return(double.NaN).Equals(Maybe.Return(double.NaN)), Is.True, "M(NaN) == M(NaN)"); // meme si NaN != NaN
		}

		[Test]
		public void Test_Maybe_Comparison()
		{
			{ // M<T>.CompareTo(T)

				// order by sort order
				var ms = new[]
				{
					Maybe.Nothing<string>(),
					Maybe.Return(default(string)),
					Maybe.Return("Hello"),
					Maybe.Return("World"),
					Maybe.Error<string>(new InvalidOperationException("KABOOM!")),
				};

				for (int i = 0; i < ms.Length; i++)
				{
					for (int j = 0; j < ms.Length; j++)
					{
						int cmp = ms[i].CompareTo(ms[j]);
						if (i == j)
						{
							Assert.That(cmp, Is.Zero, "{0} cmp {1}", ms[i], ms[j]);
						}
						else if (i < j)
						{
							Assert.That(cmp, Is.Negative, "{0} cmp {1}", ms[i], ms[j]);
						}
						else
						{
							Assert.That(cmp, Is.GreaterThan(0), "{0} cmp {1}", ms[i], ms[j]);
						}
					}
				}
			}

			{ // M<T>.CompareTo(T)

				Assert.That(Maybe.Nothing<string>().CompareTo("Hello"), Is.Negative, "<nothing> cmp 'Hello'");
				Assert.That(Maybe.Return(default(string)).CompareTo("Hello"), Is.Negative, "<null> cmp 'World'");
				Assert.That(Maybe.Return("Foo").CompareTo("Hello"), Is.Negative, "'Foo' cmp 'Hello'");
				Assert.That(Maybe.Return("Hello").CompareTo("Hello"), Is.Zero, "'Hello' cmp 'Hello'");
				Assert.That(Maybe.Return("World").CompareTo("Hello"), Is.GreaterThan(0), "'World' cmp 'World'");
				Assert.That(Maybe.Error<string>(new InvalidOperationException("KABOOM!")).CompareTo("Hello"), Is.GreaterThan(0), "<error> cmp 'World'");
			}


			{ // Exceptions are sorted using their hashcodes
				var ex1 = Maybe.Error<string>(new InvalidOperationException("KABOOM!"));
				var ex2 = Maybe.Error<string>(new InvalidOperationException("KAPOW!"));
				Assert.That(ex1.CompareTo(ex1), Is.Zero, "Comparison should return 0 for the same exceptions");
				Assert.That(ex1.CompareTo(ex2), Is.Not.Zero, "Comparison should not return 0 for different exceptions");
				Assert.That(ex1.CompareTo(ex2), Is.EqualTo(ex1.CompareTo(ex2)), "Comparison of exceptions should be stable in time");
				Assert.That(Math.Sign(ex2.CompareTo(ex1)), Is.EqualTo(-Math.Sign(ex1.CompareTo(ex2))), "(ERROR1 cmp ERROR2) should have opposite sign to (ERROR2 cmp ERROR1)");
			}
		}

		[Test]
		public void Test_Maybe_Lift_Computation()
		{
			var f = Maybe<int>.Return((x) => x * 2);

			var m = f(2);
			Assert.That(m.Value, Is.EqualTo(4));

			m = f(Maybe<int>.Nothing);
			Assert.That(m.HasValue, Is.False);
			Assert.That(m.Error, Is.Null);

			m = f(new Exception("KABOOM"));
			Assert.That(m.HasValue, Is.False);
			Assert.IsInstanceOf<Exception>(m.Error);
			Assert.That(m.Error.Message, Is.EqualTo("KABOOM"));
		}

		[Test]
		public void Test_Maybe_FromTask()
		{
			{ // Task<T>

				// TASK: OK
				var m = Maybe.FromTask(Task.FromResult("Hello"));
				Assert.That(m.HasValue, Is.True);
				Assert.That(m.Value, Is.EqualTo("Hello"));

				// TASK: ERROR
				m = Maybe.FromTask(Task.FromException<string>(new InvalidOperationException("KABOOM")));
				Assert.That(m.HasValue, Is.False);
				Assert.That(m.Error, Is.Not.Null.And.InstanceOf<InvalidOperationException>().With.Message.EqualTo("KABOOM"));

				// TASK: CANCELLED
				var cts = new CancellationTokenSource();
				cts.Cancel();
				m = Maybe.FromTask(Task.FromCanceled<string>(cts.Token));
				Assert.That(m.HasValue, Is.False);
				Assert.That(m.Error, Is.Not.Null.And.InstanceOf<OperationCanceledException>());

				// TASK: PENDING
				var pending = new TaskCompletionSource<string>();
				Assert.That(() => Maybe.FromTask(pending.Task), Throws.InvalidOperationException);
			}

			{ // Task<Maybe<T>

				// TASK: OK, MAYBE: OK
				var m = Maybe.FromTask(Task.FromResult(Maybe.Return("Hello")));
				Assert.That(m.HasValue, Is.True);
				Assert.That(m.Value, Is.EqualTo("Hello"));

				// TASK: OK, MAYBE: ERROR
				m = Maybe.FromTask(Task.FromResult(Maybe.Error<string>(new InvalidOperationException("KABOOM"))));
				Assert.That(m.HasValue, Is.False);
				Assert.That(m.Error, Is.Not.Null.And.InstanceOf<InvalidOperationException>().With.Message.EqualTo("KABOOM"));

				// TASK: OK, MAYBE: NOTHING
				m = Maybe.FromTask(Task.FromResult(Maybe.Nothing<string>()));
				Assert.That(m.HasValue, Is.False);
				Assert.That(m.Error, Is.Null);

				// TASK: ERROR
				m = Maybe.FromTask(Task.FromException<Maybe<string>>(new InvalidOperationException("KABOOM")));
				Assert.That(m.HasValue, Is.False);
				Assert.That(m.Error, Is.Not.Null.And.InstanceOf<InvalidOperationException>().With.Message.EqualTo("KABOOM"));

				// TASK: CANCELLED
				var cts = new CancellationTokenSource();
				cts.Cancel();
				m = Maybe.FromTask(Task.FromCanceled<Maybe<string>>(cts.Token));
				Assert.That(m.HasValue, Is.False);
				Assert.That(m.Error, Is.Not.Null.And.InstanceOf<OperationCanceledException>());

				// TASK: PENDING
				var pending = new TaskCompletionSource<Maybe<string>>();
				Assert.That(() => Maybe.FromTask(pending.Task), Throws.InvalidOperationException);
			}
		}

		[Test]
		public void Test_Bind_With_One_Value()
		{

			var safeSqrt = Maybe<double>.Bind((x) =>
			{
				Console.WriteLine("safeSqrt(" + x + ")");
				if (double.IsNaN(x) || x < 0) return Maybe<double>.Nothing;
				return Math.Sqrt(x);
			});

			var m = safeSqrt(4);
			Assert.That(m.Value, Is.EqualTo(2));

			m = safeSqrt(-1);
			Assert.That(m.HasValue, Is.False);

			m = safeSqrt(double.NaN);
			Assert.That(m.HasValue, Is.False);

			m = safeSqrt(Maybe<double>.Nothing);
			Assert.That(m.HasValue, Is.False);

			m = safeSqrt(Maybe<double>.Failure(new Exception("PAF")));
			Assert.That(m.HasValue, Is.False);
			Assert.IsInstanceOf<Exception>(m.Error);

		}

		[Test]
		public void Test_Bind_With_Two_Values()
		{
			var safeDivide = Maybe<int>.Bind((x, y) =>
			{
				Console.WriteLine("safeDivide(" + x + ", " + y + ")");
				if (y == 0) return Maybe<int>.Nothing;
				return x / y;
			});

			var m = safeDivide(6, 2);
			Assert.That(m.HasValue, Is.True);
			Assert.That(m.Value, Is.EqualTo(3));

			m = safeDivide(6, 0);
			Assert.That(m.HasValue, Is.False);

			m = safeDivide(Maybe<int>.Nothing, 2);
			Assert.That(m.HasValue, Is.False);

			m = safeDivide(3, Maybe<int>.Nothing);
			Assert.That(m.HasValue, Is.False);

			m = safeDivide(3, new Exception("BOOM"));
			Assert.That(m.HasValue, Is.False);
			Assert.IsInstanceOf<Exception>(m.Error);

			m = safeDivide(new Exception("POW"), 2);
			Assert.That(m.HasValue, Is.False);
			Assert.IsInstanceOf<Exception>(m.Error);
		}

		[Test]
		public void Test_Bind_Combine_Other_Binds()
		{

			var combined = Maybe.Bind<double, double, double>(
				f: x =>
				{
					Console.WriteLine("F(" + x + ")");
					if (double.IsNaN(x) || x < 0) return Maybe<double>.Nothing;
					if (x == 666.0) throw new InvalidOperationException("F");
					if (x == 42.0) return Maybe<double>.Nothing;
					return Maybe.Return(Math.Sqrt(x));
				},
				g: x =>
				{
					Console.WriteLine("G(" + x + ")");
					if (x == 666.0) throw new InvalidOperationException("G");
					if (x == 42.0) return Maybe<double>.Nothing;
					return Maybe.Return(x * 123);
				}
			);

			var m = combined(100);
			Assert.That(m.Value, Is.EqualTo(1230));

			m = combined(Maybe<double>.Nothing);
			Assert.That(m.HasValue, Is.False);

			m = combined(new Exception("KABOOM"));
			Assert.That(m.HasValue, Is.False);
			Assert.IsInstanceOf<Exception>(m.Error);
			Assert.That(m.Error.Message, Is.EqualTo("KABOOM"));

			// premi�re fonction intercepte...

			m = combined(42);
			Assert.That(m.HasValue, Is.False);

			m = combined(666);
			Assert.That(m.HasValue, Is.False);
			Assert.IsInstanceOf<Exception>(m.Error);
			Assert.That(m.Error.Message, Is.EqualTo("F"));

			// deuxi�me fonction intercepte

			m = combined(42 * 42);
			Assert.That(m.HasValue, Is.False);

			m = combined(666 * 666);
			Assert.That(m.HasValue, Is.False);
			Assert.IsInstanceOf<Exception>(m.Error);
			Assert.That(m.Error.Message, Is.EqualTo("G"));

		}

	}

}
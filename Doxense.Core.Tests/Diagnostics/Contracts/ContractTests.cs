#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Diagnostics.Contracts.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Diagnostics.Contracts;
	using System.IO;
	using System.Threading;
	using Doxense.Testing;
	using NUnit.Framework;
	using Contract = Doxense.Diagnostics.Contracts.Contract;

	/// <summary>Tests sur la classe statique Doxense.Diagnostics.Contracts.Contract</summary>
	[TestFixture]
	[Category("Core-SDK")]
	public class ContractTests : DoxenseTest
	{
		private bool m_status;

		protected override void OnBeforeEverything()
		{
			Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
			Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
		}

		[SetUp]
		public void Before()
		{
			m_status = Contract.IsUnitTesting;
		}

		[TearDown]
		public void After()
		{
			Contract.IsUnitTesting = m_status;
		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
		public void Test_Contract_NotNull_Throws_ArgumentNullException()
		{
			ArgumentNullException anex;

			#region NotNull(string)

			Assert.DoesNotThrow(() => { Contract.NotNull("foobar", paramName: "value"); });
			Assert.DoesNotThrow(() => { Contract.NotNull("", paramName: "value"); });

			anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNull(default(string), paramName: "foo"); });
			Assert.That(anex.Message, Is.EqualTo("Precondition failed: foo != null  Value cannot be null. (Parameter 'foo')"));
			Assert.That(anex.ParamName, Is.EqualTo("foo"));

			anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNull(default(string), message: "You shall not pass!", paramName: "foo"); });
			Assert.That(anex.Message, Is.EqualTo("You shall not pass! (Parameter 'foo')"));
			Assert.That(anex.ParamName, Is.EqualTo("foo"));

			anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNull(default(string), message: "You shall not pass!", paramName: "offset"); });
			Assert.That(anex.Message, Is.EqualTo("You shall not pass! (Parameter 'offset')"));
			Assert.That(anex.ParamName, Is.EqualTo("offset"));

			#endregion

			#region NotNull<T>(T)

			Assert.DoesNotThrow(() => { Contract.NotNull(new Dictionary<int, string>(), paramName: "foo"); });
			Assert.DoesNotThrow(() => { Contract.NotNull(new byte[1], paramName: "foo"); });

			anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNull(default(Stream), paramName: "stream"); });
			Assert.That(anex.Message, Is.EqualTo("Precondition failed: stream != null  Value cannot be null. (Parameter 'stream')"));
			Assert.That(anex.ParamName, Is.EqualTo("stream"));

			anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNull(default(Dictionary<int, string>), paramName: "foo"); });
			Assert.That(anex.Message, Is.EqualTo("Precondition failed: foo != null  Value cannot be null. (Parameter 'foo')"));
			Assert.That(anex.ParamName, Is.EqualTo("foo"));

			anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNull(default(byte[]), message: "You shall not pass!", paramName: "foo"); });
			Assert.That(anex.Message, Is.EqualTo("You shall not pass! (Parameter 'foo')"));
			Assert.That(anex.ParamName, Is.EqualTo("foo"));

			anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNull(default(byte[]), message: "You shall not pass!", paramName: "source"); });
			Assert.That(anex.Message, Is.EqualTo("You shall not pass! (Parameter 'source')"));
			Assert.That(anex.ParamName, Is.EqualTo("source"));

			#endregion

		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
		public void Test_Contract_NotNullOrEmpty_Throws_Correct_Exception()
		{
			// STRINGS

			Assert.DoesNotThrow(() => { Contract.NotNullOrEmpty("foobar", paramName: "x"); });

			// si c'est null => ArgumentNullException
			var anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNullOrEmpty(default(string), paramName: "foo"); });
			Assert.That(anex.Message, Is.EqualTo("Precondition failed: foo != null  Value cannot be null. (Parameter 'foo')"));
			Assert.That(anex.ParamName, Is.EqualTo("foo"));

			// si c'est empty => ArgumentException
			var aex = Assert.Throws<ArgumentException>(() => { Contract.NotNullOrEmpty(String.Empty, paramName: "foo"); });
			Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo.Length > 0  String cannot be empty. (Parameter 'foo')"));
			Assert.That(aex.ParamName, Is.EqualTo("foo"));

			// ARRAYS

			Assert.DoesNotThrow(() => { Contract.NotNullOrEmpty(new [] { 1, 2, 3 }, paramName: "x"); });

			// si c'est null => ArgumentNullException
			anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNullOrEmpty(default(int[]), paramName: "foo"); });
			Assert.That(anex.Message, Is.EqualTo("Precondition failed: foo != null  Value cannot be null. (Parameter 'foo')"));
			Assert.That(anex.ParamName, Is.EqualTo("foo"));

			// si c'est empty => ArgumentException
			aex = Assert.Throws<ArgumentException>(() => { Contract.NotNullOrEmpty(new int[0], paramName: "foo"); });
			Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo.Count > 0  Collection cannot be empty. (Parameter 'foo')"));
			Assert.That(aex.ParamName, Is.EqualTo("foo"));

			// Collections

			Assert.DoesNotThrow(() => { Contract.NotNullOrEmpty(new List<int> { 1, 2, 3 }, paramName: "x"); });

			// si c'est null => ArgumentNullException
			anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNullOrEmpty(default(List<int>), paramName: "foo"); });
			Assert.That(anex.Message, Is.EqualTo("Precondition failed: foo != null  Value cannot be null. (Parameter 'foo')"));
			Assert.That(anex.ParamName, Is.EqualTo("foo"));

			// si c'est empty => ArgumentException
			aex = Assert.Throws<ArgumentException>(() => { Contract.NotNullOrEmpty(new List<int>(), paramName: "foo"); });
			Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo.Count > 0  Collection cannot be empty. (Parameter 'foo')"));
			Assert.That(aex.ParamName, Is.EqualTo("foo"));

		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
		public void Test_Contract_NotNullOrWhiteSpace_Throws_Correct_Exception()
		{
			Assert.DoesNotThrow(() => { Contract.NotNullOrEmpty("foobar", paramName: "x"); });
			Assert.DoesNotThrow(() => { Contract.NotNullOrEmpty("  foobar", paramName: "x"); });
			Assert.DoesNotThrow(() => { Contract.NotNullOrEmpty("\0", paramName: "x"); }); //note: '\0' n'est PAS consid�r� comme du whitespace!

			// si c'est null => ArgumentNullException
			var anex = Assert.Throws<ArgumentNullException>(() => { Contract.NotNullOrWhiteSpace(default(string), paramName: "foo"); });
			Assert.That(anex.Message, Is.EqualTo("Precondition failed: foo != null  Value cannot be null. (Parameter 'foo')"));
			Assert.That(anex.ParamName, Is.EqualTo("foo"));

			// si c'est empty => ArgumentException
			var aex = Assert.Throws<ArgumentException>(() => { Contract.NotNullOrWhiteSpace(String.Empty, paramName: "foo"); });
			Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo.Length > 0  String cannot be empty. (Parameter 'foo')"));
			Assert.That(aex.ParamName, Is.EqualTo("foo"));

			// si c'est des espace => ArgumentException
			aex = Assert.Throws<ArgumentException>(() => { Contract.NotNullOrWhiteSpace("  ", paramName: "foo"); });
			Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo.All(c => !char.IsWhiteSpace(c))  String cannot contain only whitespaces. (Parameter 'foo')"));
			Assert.That(aex.ParamName, Is.EqualTo("foo"));

			// si c'est n'importe quel type de "whitespace" => ArgumentException
			aex = Assert.Throws<ArgumentException>(() => { Contract.NotNullOrWhiteSpace("\t \r\n  \n", paramName: "foo"); });
			Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo.All(c => !char.IsWhiteSpace(c))  String cannot contain only whitespaces. (Parameter 'foo')"));
			Assert.That(aex.ParamName, Is.EqualTo("foo"));
		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
		public void Test_Contract_Positive_Throws_ArgumentException()
		{

			Assert.DoesNotThrow(() => { Contract.Positive(0, paramName: "x"); });
			Assert.DoesNotThrow(() => { Contract.Positive(123, paramName: "x"); });
			Assert.DoesNotThrow(() => { Contract.Positive(0L, paramName: "x"); });
			Assert.DoesNotThrow(() => { Contract.Positive(123L, paramName: "x"); });

			var aex = Assert.Throws<ArgumentException>(() => { Contract.Positive(-1, message: "le message", paramName: "x"); });
			Assert.That(aex.Message, Is.EqualTo("le message (Parameter 'x')"));
			Assert.That(aex.ParamName, Is.EqualTo("x"));

			aex = Assert.Throws<ArgumentException>(() => { Contract.Positive(-1L, message: "le message", paramName: "x"); });
			Assert.That(aex.Message, Is.EqualTo("le message (Parameter 'x')"));
			Assert.That(aex.ParamName, Is.EqualTo("x"));
		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
		public void Test_Contract_GreaterThan_Throws_ArgumentException()
		{

			Assert.DoesNotThrow(() => { Contract.GreaterThan(1, 0, "foo"); });
			Assert.DoesNotThrow(() => { Contract.GreaterThan(66, 42, "foo"); });
			Assert.DoesNotThrow(() => { Contract.GreaterThan(1L, 0, "foo"); });
			Assert.DoesNotThrow(() => { Contract.GreaterThan(1L + int.MaxValue, int.MaxValue, "foo"); });
			Assert.DoesNotThrow(() => { Contract.GreaterThan(int.MaxValue, int.MaxValue - 1, "foo"); });
			Assert.DoesNotThrow(() => { Contract.GreaterThan(0, int.MinValue, "foo"); });
			Assert.DoesNotThrow(() => { Contract.GreaterThan(long.MaxValue, long.MaxValue - 1, "foo"); });
			Assert.DoesNotThrow(() => { Contract.GreaterThan(0, long.MinValue, "foo"); });

			{
				Assert.That(() => { int foo = 0; Contract.GreaterThan(foo, 1); }, Throws.InstanceOf<ArgumentException>().Catch<ArgumentException>(out var aex));
				Assert.That(aex.Exception.Message, Is.EqualTo("Precondition failed: foo > 1  The specified value is too small. (Parameter 'foo')"));
				Assert.That(aex.Exception.ParamName, Is.EqualTo("foo"));
			}
			{
				Assert.That(() => { long foo = 0; Contract.GreaterThan(foo, 0L); }, Throws.InstanceOf<ArgumentException>().Catch<ArgumentException>(out var aex));
				Assert.That(aex.Exception.Message, Is.EqualTo("Precondition failed: foo > 0L  Non-Zero Positive number required. (Parameter 'foo')"));
				Assert.That(aex.Exception.ParamName, Is.EqualTo("foo"));
			}
			{
				Assert.That(() => { int foo = 0; Contract.GreaterThan(foo, 1, "le message"); }, Throws.InstanceOf<ArgumentException>().Catch<ArgumentException>(out var aex));
				Assert.That(aex.Exception.Message, Is.EqualTo("le message (Parameter 'foo')"));
				Assert.That(aex.Exception.ParamName, Is.EqualTo("foo"));
			}
			{
				Assert.That(() => { long foo = 0; Contract.GreaterThan(foo, 0L, "le message"); }, Throws.InstanceOf<ArgumentException>().Catch<ArgumentException>(out var aex));
				Assert.That(aex.Exception.Message, Is.EqualTo("le message (Parameter 'foo')"));
				Assert.That(aex.Exception.ParamName, Is.EqualTo("foo"));
			}
		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
		public void Test_Contract_GreaterOrEqual_Throws_ArgumentException()
		{

			Assert.DoesNotThrow(() => { Contract.GreaterOrEqual(1, 0, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.GreaterOrEqual(66, 42, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.GreaterOrEqual(1L, 0, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.GreaterOrEqual(1L + int.MaxValue, int.MaxValue, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.GreaterOrEqual(int.MaxValue, int.MaxValue - 1, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.GreaterOrEqual(0, int.MinValue, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.GreaterOrEqual(long.MaxValue, long.MaxValue - 1, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.GreaterOrEqual(0, long.MinValue, valueExpression: "foo"); });

			{
				var aex = Assert.Throws<ArgumentException>(() => { Contract.GreaterOrEqual(0, 1, valueExpression: "foo"); });
				Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo >= 1  The specified value is too small. (Parameter 'foo')"));
				Assert.That(aex.ParamName, Is.EqualTo("foo"));
			}
			{
				var aex = Assert.Throws<ArgumentException>(() => { Contract.GreaterOrEqual(-1L, 0L, valueExpression: "foo"); });
				Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo >= 0L  Positive number required. (Parameter 'foo')"));
				Assert.That(aex.ParamName, Is.EqualTo("foo"));
			}
			{
				var aex = Assert.Throws<ArgumentException>(() => { Contract.GreaterOrEqual(0, 1, message: "le message", valueExpression: "foo"); });
				Assert.That(aex.Message, Is.EqualTo("le message (Parameter 'foo')"));
				Assert.That(aex.ParamName, Is.EqualTo("foo"));
			}
			{
				var aex = Assert.Throws<ArgumentException>(() => { Contract.GreaterOrEqual(-1L, 0L, message: "le message", valueExpression: "foo"); });
				Assert.That(aex.Message, Is.EqualTo("le message (Parameter 'foo')"));
				Assert.That(aex.ParamName, Is.EqualTo("foo"));
			}
		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
		public void Test_Contract_LessThan_Throws_ArgumentException()
		{

			Assert.DoesNotThrow(() => { Contract.LessThan(0, 1, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.LessThan(42, 66, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.LessThan(-1, 0, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.LessThan(-1L, 0, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.LessThan(int.MaxValue - 1, int.MaxValue, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.LessThan(long.MaxValue - 1, long.MaxValue, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.LessThan(int.MinValue, 0, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.LessThan(long.MinValue, 0, valueExpression: "foo"); });

			{
				var aex = Assert.Throws<ArgumentException>(() => { Contract.LessThan(1, 0, valueExpression: "foo"); });
				Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo < 0  The specified value is too big. (Parameter 'foo')"));
				Assert.That(aex.ParamName, Is.EqualTo("foo"));
			}
			{
				var aex = Assert.Throws<ArgumentException>(() => { Contract.LessThan(1L, 0L, valueExpression: "foo"); });
				Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo < 0L  The specified value is too big. (Parameter 'foo')"));
				Assert.That(aex.ParamName, Is.EqualTo("foo"));
			}
			{
				var aex = Assert.Throws<ArgumentException>(() => { Contract.LessThan(2, 1, message: "le message", valueExpression: "foo"); });
				Assert.That(aex.Message, Is.EqualTo("le message (Parameter 'foo')"));
				Assert.That(aex.ParamName, Is.EqualTo("foo"));
			}
			{
				var aex = Assert.Throws<ArgumentException>(() => { Contract.LessThan(2L, 0L, message: "le message", valueExpression: "foo"); });
				Assert.That(aex.Message, Is.EqualTo("le message (Parameter 'foo')"));
				Assert.That(aex.ParamName, Is.EqualTo("foo"));
			}
		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
		public void Test_Contract_LessOrEqual_Throws_ArgumentException()
		{

			Assert.DoesNotThrow(() => { Contract.LessOrEqual(1, 2, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.LessOrEqual(42, 66, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.LessOrEqual(0L, 1L, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.LessOrEqual(int.MaxValue, 1L + int.MaxValue, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.LessOrEqual(int.MaxValue - 1, int.MaxValue, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.LessOrEqual(int.MinValue, 0, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.LessOrEqual(long.MaxValue - 1, long.MaxValue, valueExpression: "foo"); });
			Assert.DoesNotThrow(() => { Contract.LessOrEqual(long.MinValue, 0, valueExpression: "foo"); });

			{
				var aex = Assert.Throws<ArgumentException>(() => { Contract.LessOrEqual(2, 1, valueExpression: "foo"); });
				Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo <= 1  The specified value is too big. (Parameter 'foo')"));
				Assert.That(aex.ParamName, Is.EqualTo("foo"));
			}
			{
				var aex = Assert.Throws<ArgumentException>(() => { Contract.LessOrEqual(1L, 0L, valueExpression: "foo"); });
				Assert.That(aex.Message, Is.EqualTo("Precondition failed: foo <= 0L  The specified value is too big. (Parameter 'foo')"));
				Assert.That(aex.ParamName, Is.EqualTo("foo"));
			}
			{
				var aex = Assert.Throws<ArgumentException>(() => { Contract.LessOrEqual(2, 1, message: "le message", valueExpression: "foo"); });
				Assert.That(aex.Message, Is.EqualTo("le message (Parameter 'foo')"));
				Assert.That(aex.ParamName, Is.EqualTo("foo"));
			}
			{
				var aex = Assert.Throws<ArgumentException>(() => { Contract.LessOrEqual(1L, 0L, message: "le message", valueExpression: "foo"); });
				Assert.That(aex.Message, Is.EqualTo("le message (Parameter 'foo')"));
				Assert.That(aex.ParamName, Is.EqualTo("foo"));
			}
		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
		public void Test_Contract_Between_Throws_ArgumentException()
		{
			Assert.DoesNotThrow(() => Contract.Between(0, 0, 2, valueExpression: "foo"));
			Assert.DoesNotThrow(() => Contract.Between(1, 0, 2, valueExpression: "foo"));
			Assert.DoesNotThrow(() => Contract.Between(2, 0, 2, valueExpression: "foo"));
			Assert.DoesNotThrow(() => Contract.Between(0, int.MinValue, int.MaxValue, valueExpression: "foo"));
			Assert.DoesNotThrow(() => Contract.Between(-10, -42, +10, valueExpression: "foo"));

			Assert.That(
				() => Contract.Between(11, 0, 10, valueExpression: "foo"),
				Throws.InstanceOf<ArgumentException>()
					.With.Message.EqualTo("Precondition failed: 0 <= foo <= 10  The specified value was outside the specified range. (Parameter 'foo')")
					.And.Property("ParamName").EqualTo("foo")
			);

			Assert.That(
				() => Contract.Between(-1, 0, 10, valueExpression: "foo"),
				Throws.InstanceOf<ArgumentException>()
					.With.Message.EqualTo("Precondition failed: 0 <= foo <= 10  The specified value was outside the specified range. (Parameter 'foo')")
					.And.Property("ParamName").EqualTo("foo")
			);
		}

		[Test]
		public void Test_Contract_Requires_And_Asserts_Map_To_NUnit_Assertion_When_Under_Test()
		{
			// il y a de la magie dans le constructeur de Contract, qui d�tecte si on est dans un test unitaire,
			// et qui se met a envoeyr des AssertionException, au lieu de ContractException, pour simplifier les tests.

			// Cette fonction peut etre activ�e ou d�sactiver via Contract.IsUnderTesting :

			// si on l'active, on obtient une AssertionException
			Contract.IsUnitTesting = true;
			Assert.Throws<AssertionException>(() => { Contract.Requires(false); });

			// si par contre on d�sactive ce mode, on doit obtenir une ContractException !
			Contract.IsUnitTesting = false;
			Assert.Throws<ContractException>(() => { Contract.Requires(false); });
		}

		[Test]
		public void Test_Contract_Requires_Will_Throw_A_ContractException()
		{
			// note: on d�sactive le testing mode, pour �tre en situation "r�elle"
			Contract.IsUnitTesting = false;

			Assert.DoesNotThrow(() => { Contract.Requires(true); });

			int x = 69;
			var cex = Assert.Throws<ContractException>(() => { Contract.Requires(x == 42, "le message"); });
			Assert.That(cex.Message, Is.EqualTo("Precondition failed: x == 42  le message"));
			Assert.That(cex.UserMessage, Is.EqualTo("le message"), ".UserMessage");
			Assert.That(cex.Condition, Is.EqualTo("x == 42"), ".Condition");
			Assert.That(cex.Kind, Is.EqualTo(ContractFailureKind.Precondition));
		}

		[Test]
		public void Test_Contract_Assert_Will_Throw_A_ContractException()
		{
			// note: on d�sactive le testing mode, pour �tre en situation "r�elle"
			Contract.IsUnitTesting = false;

			Assert.DoesNotThrow(() => { Contract.Assert(true); });

			// note: Contract.Assert(...) fait un Debug.Fail(...) pour spammer les logs
			int x = 69;
			Trace.WriteLine("====== vvv IGNORER L'ASSERTION QUI EST JUSTE APRES (C'EST NORMAL! :) vvv ======");
			var cex = Assert.Throws<ContractException>(() => { Contract.Assert(x == 42, "le message"); });
			Trace.WriteLine("====== ^^^ IGNORER L'ASSERTION QUI EST JUSTE AVANT (C'EST NORMAL! :) ^^^ ======");
			Assert.That(cex.Message, Is.EqualTo("Assertion failed: x == 42  le message"));
			Assert.That(cex.UserMessage, Is.EqualTo("le message"), ".UserMessage");
			Assert.That(cex.Condition, Is.EqualTo("x == 42"), ".Condition");
			Assert.That(cex.Kind, Is.EqualTo(ContractFailureKind.Assert));
		}

		[Test]
		public void Test_Contract_Ensures_Will_Throw_A_ContractException()
		{
			// note: on d�sactive le testing mode, pour �tre en situation "r�elle"
			Contract.IsUnitTesting = false;

			Assert.DoesNotThrow(() => { Contract.Ensures(true); });

			int x = 69;
			var cex = Assert.Throws<ContractException>(() => { Contract.Ensures(x == 42, "le message"); });
			Assert.That(cex.Message, Is.EqualTo("Postcondition failed: x == 42  le message"));
			Assert.That(cex.UserMessage, Is.EqualTo("le message"), ".UserMessage");
			Assert.That(cex.Condition, Is.EqualTo("x == 42"), ".Condition");
			Assert.That(cex.Kind, Is.EqualTo(ContractFailureKind.Postcondition));
		}

		[Test]
		public void Test_Contract_Invariant_Will_Throw_A_ContractException()
		{
			// note: on d�sactive le testing mode, pour �tre en situation "r�elle"
			Contract.IsUnitTesting = false;

			Assert.DoesNotThrow(() => { Contract.Invariant(true); });

			int x = 69;
			var cex = Assert.Throws<ContractException>(() => { Contract.Invariant(x == 42, "le message"); });
			Assert.That(cex.Message, Is.EqualTo("Invariant failed: x == 42  le message"));
			Assert.That(cex.UserMessage, Is.EqualTo("le message"), ".UserMessage");
			Assert.That(cex.Condition, Is.EqualTo("x == 42"), ".Condition");
			Assert.That(cex.Kind, Is.EqualTo(ContractFailureKind.Invariant));
		}

		[Test]
#if !DEBUG
		[Ignore("Only works in DEBUG mode")]
#endif
		public void Test_Contract_Debug_Requires_And_Asserts_Map_To_NUnit_Assertion_When_Under_Test()
		{
			// il y a de la magie dans le constructeur de Contract, qui d�tecte si on est dans un test unitaire,
			// et qui se met a envoeyr des AssertionException, au lieu de ContractException, pour simplifier les tests.

			// Cette fonction peut etre activ�e ou d�sactiver via Contract.IsUnderTesting :

			// si on l'active, on obtient une AssertionException
			Contract.IsUnitTesting = true;
			Assert.Throws<AssertionException>(() => { Contract.Debug.Requires(false); });

			// si par contre on d�sactive ce mode, on doit obtenir une ContractException !
			Contract.IsUnitTesting = false;
			Assert.Throws<ContractException>(() => { Contract.Debug.Requires(false); });
		}

		[Test]
#if !DEBUG
		[Ignore("Only works in DEBUG mode")]
#endif
		public void Test_Contract_Debug_Requires_Will_Throw_A_ContractException()
		{
			// note: on d�sactive le testing mode, pour �tre en situation "r�elle"
			Contract.IsUnitTesting = false;

			Assert.DoesNotThrow(() => { Contract.Debug.Requires(true); });

			int x = 69;
			var cex = Assert.Throws<ContractException>(() => { Contract.Debug.Requires(x == 42, "le message"); });
			Assert.That(cex.Message, Is.EqualTo("Precondition failed: x == 42  le message"));
			Assert.That(cex.UserMessage, Is.EqualTo("le message"), ".UserMessage");
			Assert.That(cex.Condition, Is.EqualTo("x == 42"), ".Condition");
			Assert.That(cex.Kind, Is.EqualTo(ContractFailureKind.Precondition));
		}

		[Test]
#if !DEBUG
		[Ignore("Only works in DEBUG mode")]
#endif
		public void Test_Contract_Debug_Assert_Will_Throw_A_ContractException()
		{
			// note: on d�sactive le testing mode, pour �tre en situation "r�elle"
			Contract.IsUnitTesting = false;

			Assert.DoesNotThrow(() => { Contract.Debug.Assert(true); });

			// note: Contract.Assert(...) fait un Debug.Fail(...) pour spammer les logs
			int x = 69;
			Trace.WriteLine("====== vvv IGNORER L'ASSERTION QUI EST JUSTE APRES (C'EST NORMAL! :) vvv ======");
			var cex = Assert.Throws<ContractException>(() => { Contract.Debug.Assert(x == 42, "le message"); });
			Trace.WriteLine("====== ^^^ IGNORER L'ASSERTION QUI EST JUSTE AVANT (C'EST NORMAL! :) ^^^ ======");
			Assert.That(cex.Message, Is.EqualTo("Assertion failed: x == 42  le message"));
			Assert.That(cex.UserMessage, Is.EqualTo("le message"), ".UserMessage");
			Assert.That(cex.Condition, Is.EqualTo("x == 42"), ".Condition");
			Assert.That(cex.Kind, Is.EqualTo(ContractFailureKind.Assert));
		}

		[Test]
#if !DEBUG
		[Ignore("Only works in DEBUG mode")]
#endif
		public void Test_Contract_Debug_Ensures_Will_Throw_A_ContractException()
		{
			// note: on d�sactive le testing mode, pour �tre en situation "r�elle"
			Contract.IsUnitTesting = false;

			Assert.DoesNotThrow(() => { Contract.Debug.Ensures(true); });

			int x = 69;
			var cex = Assert.Throws<ContractException>(() => { Contract.Debug.Ensures(x == 42, "le message"); });
			Assert.That(cex.Message, Is.EqualTo("Postcondition failed: x == 42  le message"));
			Assert.That(cex.UserMessage, Is.EqualTo("le message"), ".UserMessage");
			Assert.That(cex.Condition, Is.EqualTo("x == 42"), ".Condition");
			Assert.That(cex.Kind, Is.EqualTo(ContractFailureKind.Postcondition));
		}

		[Test]
#if !DEBUG
		[Ignore("Only works in DEBUG mode")]
#endif
		public void Test_Contract_Debug_Invariant_Will_Throw_A_ContractException()
		{
			// note: on d�sactive le testing mode, pour �tre en situation "r�elle"
			Contract.IsUnitTesting = false;

			Assert.DoesNotThrow(() => { Contract.Debug.Invariant(true); });

			int x = 69;
			var cex = Assert.Throws<ContractException>(() => { Contract.Debug.Invariant(x == 42, "le message"); });
			Assert.That(cex.Message, Is.EqualTo("Invariant failed: x == 42  le message"));
			Assert.That(cex.UserMessage, Is.EqualTo("le message"), ".UserMessage");
			Assert.That(cex.Condition, Is.EqualTo("x == 42"), ".Condition");
			Assert.That(cex.Kind, Is.EqualTo(ContractFailureKind.Invariant));
		}

	}

}
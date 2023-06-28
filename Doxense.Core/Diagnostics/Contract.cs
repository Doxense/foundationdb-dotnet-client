#region Copyright (c) 2005-2023 Doxense SAS
// See License.MD for license information
#endregion

#nullable enable

namespace Doxense.Diagnostics.Contracts
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Reflection;
	using System.Runtime.CompilerServices;
	using SDC = System.Diagnostics.Contracts;
	using SRC = System.Runtime.CompilerServices;
	using JetBrains.Annotations;

	internal static class ContractMessages
	{

		public const string ValueCannotBeNull = "Value cannot be null.";
		public const string StringCannotBeEmpty = "String cannot be empty.";
		public const string StringCannotBeWhiteSpaces = "String cannot contain only whitespaces.";
		public const string CollectionCannotBeEmpty = "Collection cannot be empty.";
		public const string BufferCannotBeNull = "Buffer cannot be null.";
		public const string BufferCannotBeEmpty = "Buffer cannot be empty.";
		public const string PositiveNumberRequired = "Positive number required.";
		public const string PowerOfTwoRequired = "Power of two number required.";
		public const string AboveZeroNumberRequired = "Non-Zero Positive number required.";
		public const string ValueIsTooSmall = "The specified value is too small.";
		public const string ValueIsTooBig = "The specified value is too big.";
		public const string ValueIsForbidden = "The specified value is not allowed.";
		public const string ValueIsExpected = "The specified value is not the expected value.";
		public const string ValueMustBeBetween = "The specified value was outside the specified range.";
		public const string ValueMustBeMultiple = "The specified value must be a multiple of another value.";
		public const string NonNegativeNumberRequired = "Non-negative number required.";
		public const string OffsetMustBeWithinBuffer = "Offset and length were out of bounds for the buffer or count is greater than the number of elements from index to the end of the buffer.";

		public const string ConditionNotNull = "{0} != null";
		public const string ConditionNotEmptyLength = "{0}.Length > 0";
		public const string ConditionNotWhiteSpace = "{0}.All(c => !char.IsWhiteSpace(c))";
		public const string ConditionNotEmptyCount = "{0}.Count > 0";
		public const string ConditionArgPositive = "{0} >= 0";
		public const string ConditionArgNotEqualTo = "{0} != x";
		public const string ConditionArgEqualTo = "{0} == x";
		public const string ConditionArgMultiple = "{0} % x == 0";
		public const string ConditionArgBetween = "min <= {0} <= max";
		public const string ConditionArgBufferOverflow = "(buffer.Length - offset) < count";
	}

	/// <summary>Classe helper pour la vérification de pré-requis, invariants, assertions, ...</summary>
	[DebuggerNonUserCode]
	[PublicAPI]
	public static partial class Contract
	{

		private static readonly (ConstructorInfo? One, ConstructorInfo? Two) s_constructorNUnitException = GetAssertionExceptionCtor();

		public static bool IsUnitTesting { get; set; }

		private static (ConstructorInfo? One, ConstructorInfo? Two) GetAssertionExceptionCtor()
		{
			// détermine si on est lancé depuis des tests unitaires (pour désactiver les breakpoints et autres opérations intrusives qui vont parasiter les tests)

			var nUnitAssert = Type.GetType("NUnit.Framework.AssertionException,nunit.framework");
			if (nUnitAssert != null)
			{
				// on convertit les échecs "soft" en échec d'assertion NUnit
				IsUnitTesting = true;
				return (nUnitAssert.GetConstructor(new [] { typeof (string) }), nUnitAssert.GetConstructor(new [] { typeof (string), typeof(Exception) }));
			}
			return (null, null);
		}

		private static Exception? MapToNUnitAssertion(string message, Exception? exception)
		{
			// => new NUnit.Framework.AssertionException(...)
			return exception == null
				? (Exception?) s_constructorNUnitException.One?.Invoke(new object[] { message })
				: (Exception?) s_constructorNUnitException.Two?.Invoke(new object[] { message, exception })
				;
		}

		#region DEBUG checks...

		/// <summary>Vérifie qu'une pré-condition est vrai, lors de l'entrée dans une méthode</summary>
		/// <param name="condition">Condition qui ne doit jamais être fausse</param>
		/// <param name="userMessage">Message décrivant l'erreur (optionnel)</param>
		/// <param name="conditionText">Texte de la condition (optionnel, injecté par le compilateur)</param>
		/// <remarks>Ne fait rien si la condition est vrai. Sinon déclenche une ContractException, après avoir essayé de breakpointer le debugger</remarks>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Requires(
			[AssertionCondition(AssertionConditionType.IS_TRUE)]
			[System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]
			bool condition,
			string? userMessage = null,
			[CallerArgumentExpression("condition")] string? conditionText = null
		)
		{
			if (!condition) throw RaiseContractFailure(SDC.ContractFailureKind.Precondition, userMessage, conditionText);
		}

		/// <summary>Vérifie qu'une condition est toujours vrai, dans le body dans une méthode</summary>
		/// <param name="condition">Condition qui ne doit jamais être fausse</param>
		/// <param name="userMessage">Message décrivant l'erreur (optionnel)</param>
		/// <param name="conditionText">Texte de la condition (optionnel, injecté par le compilateur)</param>
		/// <remarks>Ne fait rien si la condition est vrai. Sinon déclenche une ContractException, après avoir essayé de breakpointer le debugger</remarks>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Assert(
			[AssertionCondition(AssertionConditionType.IS_TRUE)]
			[System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]
			bool condition,
			string? userMessage = null,
			[CallerArgumentExpression("condition")] string? conditionText = null)
		{
			if (!condition) throw RaiseContractFailure(SDC.ContractFailureKind.Assert, userMessage, conditionText);
		}

		/// <summary>Vérifie qu'une condition est toujours vrai, lors de la sortie d'une méthode</summary>
		/// <param name="condition">Condition qui ne doit jamais être fausse</param>
		/// <param name="userMessage">Message décrivant l'erreur (optionnel)</param>
		/// <param name="conditionText">Texte de la condition (optionnel, injecté par le compilateur)</param>
		/// <remarks>Ne fait rien si la condition est vrai. Sinon déclenche une ContractException, après avoir essayé de breakpointer le debugger</remarks>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Ensures(
			[AssertionCondition(AssertionConditionType.IS_TRUE)]
			[System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]
			bool condition,
			string? userMessage = null,
			[CallerArgumentExpression("condition")] string? conditionText = null
		)
		{
			if (!condition) throw RaiseContractFailure(SDC.ContractFailureKind.Postcondition, userMessage, conditionText);
		}

		/// <summary>Vérifie qu'une condition est toujours vrai pendant toute la vie d'une instance</summary>
		/// <param name="condition">Condition qui ne doit jamais être fausse</param>
		/// <param name="userMessage">Message décrivant l'erreur (optionnel)</param>
		/// <param name="conditionText">Texte de la condition (optionnel, injecté par le compilateur)</param>
		/// <remarks>Ne fait rien si la condition est vrai. Sinon déclenche une ContractException, après avoir essayé de breakpointer le debugger</remarks>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Invariant(
			[AssertionCondition(AssertionConditionType.IS_TRUE)]
			[System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]
			bool condition,
			string? userMessage = null,
			[CallerArgumentExpression("condition")] string? conditionText = null
		)
		{
			if (!condition) throw RaiseContractFailure(SDC.ContractFailureKind.Invariant, userMessage, conditionText);
		}

		/// <summary>Unconditionally trigger an assertion fault</summary>
		/// <param name="userMessage">Message décrivant l'erreur (optionnel)</param>
		/// <param name="exception">Optional exception linked to the issue</param>
		/// <remarks>Ne fait rien si la condition est vrai. Sinon déclenche une ContractException, après avoir essayé de breakpointer le debugger</remarks>
		[AssertionMethod, MethodImpl(MethodImplOptions.NoInlining)]
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
		public static void Fail(string? userMessage, Exception? exception = null)
		{
			throw RaiseContractFailure(SDC.ContractFailureKind.Invariant, userMessage, null, exception);
		}

		#region Contract.NotNull

		/// <summary>The specified instance must not be null (assert: value != null)</summary>
		/// <exception cref="ArgumentNullException">if <paramref name="value"/> is null</exception>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotNull(
			[System.Diagnostics.CodeAnalysis.NotNull] [AssertionCondition(AssertionConditionType.IS_NOT_NULL)] [NoEnumeration]
			string? value,
			string message = null,
			[InvokerParameterName, CallerArgumentExpression("value")] string paramName = null
		)
		{
			if (value == null) throw FailArgumentNull(paramName, message);
		}

		/// <summary>The specified instance must not be null (assert: value != null)</summary>
		/// <exception cref="ArgumentNullException">if <paramref name="value"/> is null</exception>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotNull<TValue>(
			[System.Diagnostics.CodeAnalysis.NotNull] [AssertionCondition(AssertionConditionType.IS_NOT_NULL)] [NoEnumeration]
			TValue? value,
			string message = null,
			[InvokerParameterName, CallerArgumentExpression("value")] string paramName = null
		)
			where TValue : class
		{
			if (value == null) throw FailArgumentNull(paramName, message);
		}

		/// <summary>The specified instance must not be null (assert: value != null)</summary>
		/// <remarks>This methods allow structs (that can never be null)</remarks>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotNullAllowStructs<TValue>(
			[System.Diagnostics.CodeAnalysis.NotNull]
			[AssertionCondition(AssertionConditionType.IS_NOT_NULL), NoEnumeration] TValue? value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null
		)
		{
			if (value == null) throw FailArgumentNull(paramName, message);
		}

		/// <summary>The specified pointer must not be null (assert: pointer != null)</summary>
		/// <exception cref="ArgumentNullException">if <paramref name="pointer"/> is null</exception>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void PointerNotNull(
			[System.Diagnostics.CodeAnalysis.AllowNull]
			[System.Diagnostics.CodeAnalysis.NotNull]
			[AssertionCondition(AssertionConditionType.IS_NOT_NULL)] void* pointer,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression("pointer")] string? paramName = null)
		{
			if (pointer == null) throw FailArgumentNull(paramName, message);
		}

		/// <summary>The specified value cannot be null (assert: value != null)</summary>
		/// <returns>Passed value, or throws an exception if it was null</returns>
		/// <exception cref="ArgumentNullException">if <paramref name="value"/> is null</exception>
		/// <remarks>This method is intended for use in single-line property setters</remarks>
		/// <example><code>
		/// private string m_fooThatIsNeverNull;
		/// public string Foo
		/// {
		///     get => return m_fooThatIsNeverNull;
		///     set => m_fooThatIsNeverNull = Contract.ValueNotNull(value, "Foo cannot be set to null");
		/// }
		/// </code> </example>
		[Pure, AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T ValueNotNull<T>(
			[System.Diagnostics.CodeAnalysis.NotNull]
			[AssertionCondition(AssertionConditionType.IS_NOT_NULL), NoEnumeration] T? value,
			string? message = null
		)
		{
			return value ?? throw FailArgumentNull(nameof(value), message);
		}

		#endregion

		#region Contract.NotNullOrEmpty

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailStringNullOrEmpty(string? value, string? paramName, string? message = null)
		{
			return value == null
				? ReportFailure(typeof(ArgumentNullException), ContractMessages.ValueCannotBeNull, message, paramName, ContractMessages.ConditionNotNull)
				: ReportFailure(typeof(ArgumentException), ContractMessages.StringCannotBeEmpty, message, paramName, ContractMessages.ConditionNotEmptyLength);
		}

		/// <summary>The specified string must not be null or empty (assert: value != null &amp;&amp; value.Length != 0)</summary>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotNullOrEmpty(
			[System.Diagnostics.CodeAnalysis.NotNull] [AssertionCondition(AssertionConditionType.IS_NOT_NULL)]
			string? value,
			string message = null,
			[InvokerParameterName] [CallerArgumentExpression("value")]
			string? paramName = null)
		{
			if (string.IsNullOrEmpty(value)) throw FailStringNullOrEmpty(value, paramName, message);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailStringNullOrWhiteSpace(string? value, string? paramName, string? message = null)
		{
			return value == null ? ReportFailure(typeof(ArgumentNullException), ContractMessages.ValueCannotBeNull, message, paramName, ContractMessages.ConditionNotNull)
				: value.Length == 0 ? ReportFailure(typeof(ArgumentException), ContractMessages.StringCannotBeEmpty, message, paramName, ContractMessages.ConditionNotEmptyLength)
				: ReportFailure(typeof(ArgumentException), ContractMessages.StringCannotBeWhiteSpaces, message, paramName, ContractMessages.ConditionNotWhiteSpace);
		}

		/// <summary>The specified string must not be null, empty or contain only whitespaces (assert: value != null &amp;&amp; value.Length != 0)</summary>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotNullOrWhiteSpace(
			[System.Diagnostics.CodeAnalysis.NotNull] [AssertionCondition(AssertionConditionType.IS_NOT_NULL)]
			string? value,
			string? message = null,
			[InvokerParameterName] [CallerArgumentExpression("value")]
			string? paramName = null)
		{
			if (string.IsNullOrWhiteSpace(value)) throw FailStringNullOrWhiteSpace(value, paramName, message);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArrayNullOrEmpty(object? collection, string? paramName, string? message = null)
		{
			return collection == null
				? ReportFailure(typeof(ArgumentNullException), ContractMessages.ValueCannotBeNull, message, paramName, ContractMessages.ConditionNotNull)
				: ReportFailure(typeof(ArgumentException), ContractMessages.CollectionCannotBeEmpty, message, paramName, ContractMessages.ConditionNotEmptyCount);
		}

		/// <summary>The specified array must not be null or empty (assert: value != null &amp;&amp; value.Count != 0)</summary>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotNullOrEmpty<T>(
			[System.Diagnostics.CodeAnalysis.NotNull] 
			[AssertionCondition(AssertionConditionType.IS_NOT_NULL)] T[]? value,
			string message = null,
			[InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null
		)
		{
			if (value == null || value.Length == 0) throw FailArrayNullOrEmpty(value, paramName, message);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailCollectionNullOrEmpty(object? collection, string paramName, string? message = null)
		{
			return collection == null
				? ReportFailure(typeof(ArgumentNullException), ContractMessages.ValueCannotBeNull, message, paramName, ContractMessages.ConditionNotNull)
				: ReportFailure(typeof(ArgumentException), ContractMessages.CollectionCannotBeEmpty, message, paramName, ContractMessages.ConditionNotEmptyCount);
		}

		/// <summary>The specified collection must not be null or empty (assert: value != null &amp;&amp; value.Count != 0)</summary>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotNullOrEmpty<T>(
			[System.Diagnostics.CodeAnalysis.NotNull]
			[AssertionCondition(AssertionConditionType.IS_NOT_NULL)] ICollection<T>? value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null
		)
		{
			if (value == null || value.Count == 0) throw FailCollectionNullOrEmpty(value, paramName, null);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailBufferNull(string paramName, string? message = null)
		{
			return ReportFailure(typeof(ArgumentNullException), ContractMessages.BufferCannotBeNull, message, paramName, ContractMessages.ConditionNotNull);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailBufferNullOrEmpty(object? array, string paramName, string? message = null)
		{
			return array == null
				? ReportFailure(typeof(ArgumentNullException), ContractMessages.BufferCannotBeNull, message, paramName, ContractMessages.ConditionNotNull)
				: ReportFailure(typeof(ArgumentException), ContractMessages.BufferCannotBeEmpty, message, paramName, ContractMessages.ConditionNotEmptyCount);
		}

		/// <summary>The specified buffer must not be null or empty (assert: buffer.Array != null &amp;&amp; buffer.Count != 0)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotNullOrEmpty(Slice buffer, string? message = null, [InvokerParameterName, CallerArgumentExpression("buffer")] string? paramName = null)
		{
			if (buffer.Array == null | buffer.Count == 0) throw FailBufferNullOrEmpty(buffer.Array, paramName, message);
		}

		/// <summary>The specified buffer must not be null or empty (assert: buffer.Array != null &amp;&amp; buffer.Count != 0)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotNullOrEmpty<T>(ArraySegment<T> buffer, string message = null, [InvokerParameterName, CallerArgumentExpression("buffer")] string? paramName = null)
		{
			if (buffer.Array == null || buffer.Count == 0) throw FailBufferNullOrEmpty(buffer.Array, paramName, message);
		}

		#endregion

		#region Contract.Positive, LessThan[OrEqual], GreaterThen[OrEqual], EqualTo, Between, ...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNotPositive(string paramName, string? message = null)
		{
			return ReportFailure(typeof(ArgumentException), ContractMessages.PositiveNumberRequired, message, paramName, ContractMessages.ConditionArgPositive);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNotNonNegative(string paramName, string? message = null)
		{
			return ReportFailure(typeof(ArgumentException), ContractMessages.NonNegativeNumberRequired, message, paramName, ContractMessages.ConditionArgPositive);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNotPowerOfTwo(string paramName, string? message = null)
		{
			return ReportFailure(typeof(ArgumentException), ContractMessages.PowerOfTwoRequired, message, paramName, ContractMessages.ConditionArgPositive);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentForbidden<T>(string paramName, T forbidden, string? message = null)
		{
			//TODO: need support for two format arguments for conditionTxt !
			return ReportFailure(typeof(ArgumentException), ContractMessages.ValueIsForbidden, message, paramName, ContractMessages.ConditionArgNotEqualTo);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentExpected<T>(string paramName, T expected, string? message = null)
		{
			//TODO: need support for two format arguments for conditionTxt !
			return ReportFailure(typeof(ArgumentException), ContractMessages.ValueIsExpected, message, paramName, ContractMessages.ConditionArgEqualTo);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNotGreaterThan(string valueExpression, string thresholdExpression, bool zero, string? message = null)
		{
			return ReportFailure(typeof(ArgumentException), zero ? ContractMessages.AboveZeroNumberRequired : ContractMessages.ValueIsTooSmall, message, valueExpression, "{0} > " + thresholdExpression);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNotGreaterOrEqual(string valueExpression, string thresholdExpression, bool zero, string? message = null)
		{
			return ReportFailure(typeof(ArgumentException), zero ? ContractMessages.PositiveNumberRequired : ContractMessages.ValueIsTooSmall, message, valueExpression, "{0} >= " + thresholdExpression);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNotLessThan(string valueExpression, string thresholdExpression, string? message = null)
		{
			return ReportFailure(typeof(ArgumentException), ContractMessages.ValueIsTooBig, message, valueExpression, "{0} < " + thresholdExpression);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNotLessOrEqual(string valueExpression, string thresholdExpression, string? message = null)
		{
			return ReportFailure(typeof(ArgumentException), ContractMessages.ValueIsTooBig, message, valueExpression, "{0} <= " + thresholdExpression);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentOutOfBounds(string paramName, string? message = null)
		{
			return ReportFailure(typeof(ArgumentException), ContractMessages.ValueMustBeBetween, message, paramName, ContractMessages.ConditionArgBetween);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentOutOfBounds(string valueExpression, string minExpression, string maxExpression, string? message = null)
		{
			return ReportFailure(typeof(ArgumentException), ContractMessages.ValueMustBeBetween, message, valueExpression, minExpression + " <= {0} <= " + maxExpression);
		}

		#region Positive...

		/// <summary>The specified value must not be a negative number (assert: value >= 0)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Positive(int value, string? message = null, [InvokerParameterName] [CallerArgumentExpression("value")] string? paramName = null)
		{
			if (value < 0) throw FailArgumentNotPositive(paramName, message);
		}

		/// <summary>The specified value must not be a negative number (assert: value >= 0)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Positive(long value, string? message = null, [InvokerParameterName] [CallerArgumentExpression("value")] string? paramName = null)
		{
			if (value < 0) throw FailArgumentNotPositive(paramName, message);
		}

		/// <summary>The specified value must not be a negative number (assert: value >= 0)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Positive(double value, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
		{
			if (Math.Sign(value) != 1) throw FailArgumentNotPositive(paramName, message);
		}

		/// <summary>The specified value must not be a negative number (assert: value >= 0)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Positive(float value, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
		{
#if NETFRAMEWORK || NETSTANDARD
			if (!(value >= 0)) throw FailArgumentNotPositive(paramName, message);
#else
			if (MathF.Sign(value) != 1) throw FailArgumentNotPositive(paramName, message);
#endif
		}

		#endregion

		#region PowerOfTwo...

		/// <summary>The specified value must be a power of two (assert: NextPowerOfTwo(value) == value)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void PowerOfTwo(int value, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
		{
			if (value < 0 || unchecked((value & (value - 1)) != 0)) throw FailArgumentNotPowerOfTwo(paramName, message);
		}

		/// <summary>The specified value must be a power of two (assert: NextPowerOfTwo(value) == value)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void PowerOfTwo(uint value, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
		{
			if (unchecked((value & (value - 1)) != 0)) throw FailArgumentNotPowerOfTwo(paramName, message);
		}

		/// <summary>The specified value must be a power of two (assert: NextPowerOfTwo(value) == value)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void PowerOfTwo(long value, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
		{
			if (value < 0 || unchecked((value & (value - 1)) != 0)) throw FailArgumentNotPowerOfTwo(paramName, message);
		}

		/// <summary>The specified value must be a power of two (assert: NextPowerOfTwo(value) == value)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void PowerOfTwo(ulong value, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
		{
			if (unchecked((value & (value - 1)) != 0)) throw FailArgumentNotPowerOfTwo(paramName, message);
		}

		#endregion

		#region EqualTo...

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EqualTo(int value, int expected, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
		{
			if (value != expected) throw FailArgumentExpected(paramName, expected, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EqualTo(long value, long expected, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
		{
			if (value != expected) throw FailArgumentExpected(paramName, expected, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EqualTo(uint value, uint expected, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
		{
			if (value != expected) throw FailArgumentExpected(paramName, expected, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EqualTo(ulong value, ulong expected, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
		{
			if (value != expected) throw FailArgumentExpected(paramName, expected, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EqualTo(string? value, string? expected, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
		{
			if (value != expected) throw FailArgumentExpected(paramName, expected, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EqualTo<T>(T value, T expected, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
			where T : struct, IEquatable<T>
		{
			if (!value.Equals(expected)) throw FailArgumentExpected(paramName, expected, message);
		}

		#endregion

		#region NotEqualTo...

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotEqualTo(int value, int forbidden, string? message = null, [InvokerParameterName] [CallerArgumentExpression("value")] string? paramName = null)
		{
			if (value == forbidden) throw FailArgumentForbidden(paramName, forbidden, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotEqualTo(long value, long forbidden, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
		{
			if (value == forbidden) throw FailArgumentForbidden(paramName, forbidden, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotEqualTo(uint value, uint forbidden, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
		{
			if (value == forbidden) throw FailArgumentForbidden(paramName, forbidden, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotEqualTo(ulong value, ulong forbidden, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
		{
			if (value == forbidden) throw FailArgumentForbidden(paramName, forbidden, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotEqualTo(string value, string forbidden, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
		{
			if (value == forbidden) throw FailArgumentForbidden(paramName, forbidden, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotEqualTo<T>(T value, T forbidden, string? message = null, [InvokerParameterName] [CallerArgumentExpression("value")] string? paramName = null)
			where T : struct, IEquatable<T>
		{
			if (value.Equals(forbidden)) throw FailArgumentForbidden(paramName, forbidden, message);
		}

		#endregion

		#region GreaterThan...

		/// <summary>The specified value must not less than or equal to the specified lower bound (assert: value > threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterThan(int value, int threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value <= threshold) throw FailArgumentNotGreaterThan(valueExpression, thresholdExpression, threshold == 0, message);
		}

		/// <summary>The specified value must not less than or equal to the specified lower bound (assert: value > threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterThan(uint value, uint threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value <= threshold) throw FailArgumentNotGreaterThan(valueExpression, thresholdExpression, threshold == 0, message);
		}

		/// <summary>The specified value must not less than or equal to the specified lower bound (assert: value > threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterThan(long value, long threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value <= threshold) throw FailArgumentNotGreaterThan(valueExpression, thresholdExpression, threshold == 0, message); //BUGBUG: TODO: injecter le thresholdExpression!
		}

		/// <summary>The specified value must not less than or equal to the specified lower bound (assert: value > threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterThan(ulong value, ulong threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value <= threshold) throw FailArgumentNotGreaterThan(valueExpression, thresholdExpression, threshold == 0, message);
		}

		/// <summary>The specified value must not less than or equal to the specified lower bound (assert: value > threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterThan(float value, float threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (value <= threshold) throw FailArgumentNotGreaterThan(valueExpression, thresholdExpression, threshold == 0.0f, message);
		}

		/// <summary>The specified value must not less than or equal to the specified lower bound (assert: value > threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterThan(double value, double threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (value <= threshold) throw FailArgumentNotGreaterThan(valueExpression, thresholdExpression, threshold == 0.0d, message);
		}

		/// <summary>The specified value must not less than or equal to the specified lower bound (assert: value > threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterThan<T>(T value, T threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
			where T: struct, IComparable<T>
		{
			if (value.CompareTo(threshold) <= 0) throw FailArgumentNotGreaterThan(valueExpression, thresholdExpression, threshold.CompareTo(default(T)) == 0, message);
		}

		#endregion
		
		#region GreaterOrEqual...

		/// <summary>The specified value must not less than the specified lower bound (assert: value >= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterOrEqual(int value, int threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value < threshold) throw FailArgumentNotGreaterOrEqual(valueExpression, thresholdExpression, threshold == 0, message);
		}

		/// <summary>The specified value must not less than the specified lower bound (assert: value >= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterOrEqual(uint value, uint threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value < threshold) throw FailArgumentNotGreaterOrEqual(valueExpression, thresholdExpression, false, message);
		}

		/// <summary>The specified value must not less than the specified lower bound (assert: value >= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterOrEqual(long value, long threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value < threshold) throw FailArgumentNotGreaterOrEqual(valueExpression, thresholdExpression, threshold == 0, message);
		}

		/// <summary>The specified value must not less than the specified lower bound (assert: value >= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterOrEqual(ulong value, ulong threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value < threshold) throw FailArgumentNotGreaterOrEqual(valueExpression, thresholdExpression, false, message);
		}

		/// <summary>The specified value must not less than the specified lower bound (assert: value >= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterOrEqual(float value, float threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (value < threshold) throw FailArgumentNotGreaterOrEqual(valueExpression, thresholdExpression, threshold == 0.0f, message);
		}

		/// <summary>The specified value must not less than the specified lower bound (assert: value >= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterOrEqual(double value, double threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (value < threshold) throw FailArgumentNotGreaterOrEqual(valueExpression, thresholdExpression, threshold == 0.0d, message);
		}

		/// <summary>The specified value must not less than the specified lower bound (assert: value >= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterOrEqual<T>(T value, T threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
			where T : struct, IComparable<T>
		{
			if (value.CompareTo(threshold) < 0) throw FailArgumentNotGreaterOrEqual(valueExpression, thresholdExpression, threshold.CompareTo(default(T)) == 0, message);
		}

		#endregion

		#region LessThan...

		/// <summary>The specified value must not greater than or equal to the specified upper bound</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessThan(int value, int threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value >= threshold) throw FailArgumentNotLessThan(valueExpression, thresholdExpression, message);
		}

		/// <summary>The specified value must not greater than or equal to the specified upper bound</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessThan(uint value, uint threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value >= threshold) throw FailArgumentNotLessThan(valueExpression, thresholdExpression, message);
		}

		/// <summary>The specified value must not greater than or equal to the specified upper bound (assert: value &lt; threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessThan(long value, long threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value >= threshold) throw FailArgumentNotLessThan(valueExpression, thresholdExpression, message);
		}

		/// <summary>The specified value must not greater than or equal to the specified upper bound (assert: value &lt; threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessThan(ulong value, ulong threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value >= threshold) throw FailArgumentNotLessThan(valueExpression, thresholdExpression, message);
		}

		/// <summary>The specified value must not greater than or equal to the specified upper bound (assert: value &lt; threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessThan(float value, float threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value >= threshold) throw FailArgumentNotLessThan(valueExpression, thresholdExpression, message);
		}

		/// <summary>The specified value must not greater than or equal to the specified upper bound (assert: value &lt; threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessThan(double value, double threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value >= threshold) throw FailArgumentNotLessThan(valueExpression, thresholdExpression, message);
		}

		/// <summary>The specified value must not greater than or equal to the specified upper bound (assert: value &lt; threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessThan<T>(T value, T threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
			where T : struct, IComparable<T>
		{
			if (value.CompareTo(threshold) >= 0) throw FailArgumentNotLessThan(valueExpression, thresholdExpression, message);
		}

		#endregion

		#region LessOrEqual...

		/// <summary>The specified value must not greater than the specified upper bound (assert: value &lt;= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessOrEqual(int value, int threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value > threshold) throw FailArgumentNotLessOrEqual(valueExpression, thresholdExpression, message);
		}

		/// <summary>The specified value must not greater than the specified upper bound (assert: value &lt;= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessOrEqual(uint value, uint threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value > threshold) throw FailArgumentNotLessOrEqual(valueExpression, thresholdExpression, message);
		}

		/// <summary>The specified value must not greater than the specified upper bound (assert: value &lt;= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessOrEqual(long value, long threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value > threshold) throw FailArgumentNotLessOrEqual(valueExpression, thresholdExpression, message);
		}

		/// <summary>The specified value must not greater than the specified upper bound (assert: value &lt;= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessOrEqual(ulong value, ulong threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value > threshold) throw FailArgumentNotLessOrEqual(valueExpression, thresholdExpression, message);
		}

		/// <summary>The specified value must not greater than the specified upper bound (assert: value &lt;= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessOrEqual(float value, float threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value > threshold) throw FailArgumentNotLessOrEqual(valueExpression, thresholdExpression, message);
		}

		/// <summary>The specified value must not greater than the specified upper bound (assert: value &lt;= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessOrEqual(double value, double threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
		{
			if (value > threshold) throw FailArgumentNotLessOrEqual(valueExpression, thresholdExpression, message);
		}

		/// <summary>The specified value must not greater than the specified upper bound (assert: value &lt;= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessOrEqual<T>(T value, T threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("threshold")] string? thresholdExpression = null)
			where T : struct, IComparable<T>
		{
			if (value.CompareTo(threshold) > 0) throw FailArgumentNotLessOrEqual(valueExpression, thresholdExpression, message);
		}

		#endregion

		#region Between...

		/// <summary>The specified value must not be outside of the specified bounds (assert: min &lt;= value &lt;= max)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Between(int value, int minimumInclusive, int maximumInclusive, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("minimumInclusive")] string? minExpression = null, [InvokerParameterName, CallerArgumentExpression("maximumInclusive")] string? maxExpression = null)
		{
			if (value < minimumInclusive || value > maximumInclusive) throw FailArgumentOutOfBounds(valueExpression, minExpression, maxExpression, message);
		}

		/// <summary>The specified value must not be outside of the specified bounds (assert: min &lt;= value &lt;= max)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Between(uint value, uint minimumInclusive, uint maximumInclusive, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("minimumInclusive")] string? minExpression = null, [InvokerParameterName, CallerArgumentExpression("maximumInclusive")] string? maxExpression = null)
		{
			if (value < minimumInclusive || value > maximumInclusive) throw FailArgumentOutOfBounds(valueExpression, minExpression, maxExpression, message);
		}

		/// <summary>The specified value must not be outside of the specified bounds (assert: min &lt;= value &lt;= max)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Between(long value, long minimumInclusive, long maximumInclusive, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("minimumInclusive")] string? minExpression = null, [InvokerParameterName, CallerArgumentExpression("maximumInclusive")] string? maxExpression = null)
		{
			if (value < minimumInclusive || value > maximumInclusive) throw FailArgumentOutOfBounds(valueExpression, minExpression, maxExpression, message);
		}

		/// <summary>The specified value must not be outside of the specified bounds (assert: min &lt;= value &lt;= max)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Between(ulong value, ulong minimumInclusive, ulong maximumInclusive, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("minimumInclusive")] string? minExpression = null, [InvokerParameterName, CallerArgumentExpression("maximumInclusive")] string? maxExpression = null)
		{
			if (value < minimumInclusive || value > maximumInclusive) throw FailArgumentOutOfBounds(valueExpression, minExpression, maxExpression, message);
		}

		/// <summary>The specified value must not be outside of the specified bounds (assert: min &lt;= value &lt;= max)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Between(float value, float minimumInclusive, float maximumInclusive, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("minimumInclusive")] string? minExpression = null, [InvokerParameterName, CallerArgumentExpression("maximumInclusive")] string? maxExpression = null)
		{
			if (value < minimumInclusive || value > maximumInclusive) throw FailArgumentOutOfBounds(valueExpression, minExpression, maxExpression, message);
		}

		/// <summary>The specified value must not be outside of the specified bounds (assert: min &lt;= value &lt;= max)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Between(double value, double minimumInclusive, double maximumInclusive, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression("minimumInclusive")] string? minExpression = null, [InvokerParameterName, CallerArgumentExpression("maximumInclusive")] string? maxExpression = null)
		{
			if (value < minimumInclusive || value > maximumInclusive) throw FailArgumentOutOfBounds(valueExpression, minExpression, maxExpression, message);
		}

		#endregion

		#region Multiple...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNotMultiple(string paramName, string? message = null)
		{
			return ReportFailure(typeof(ArgumentException), ContractMessages.ValueMustBeMultiple, message, paramName, ContractMessages.ConditionArgMultiple);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Multiple(int value, int multiple, string? message = null, [InvokerParameterName] [CallerArgumentExpression("value")] string? paramName = null)
		{
			if (value % multiple != 0) throw FailArgumentNotMultiple(paramName, message);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Multiple(uint value, uint multiple, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
		{
			if (value % multiple != 0) throw FailArgumentNotMultiple(paramName, message);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Multiple(long value, long multiple, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
		{
			if (value % multiple != 0) throw FailArgumentNotMultiple(paramName, message);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Multiple(ulong value, ulong multiple, string? message = null, [InvokerParameterName, CallerArgumentExpression("value")] string? paramName = null)
		{
			if (value % multiple != 0) throw FailArgumentNotMultiple(paramName, message);
		}

		#endregion

		#endregion

		#region Contract.DoesNotOverflow

		/// <summary>Vérifie qu'une couple index/count ne débord pas d'un buffer, et qu'il n'est pas null</summary>
		/// <param name="buffer">Buffer (qui ne doit pas être null)</param>
		/// <param name="index">Index (qui ne doit pas être négatif)</param>
		/// <param name="count">Taille (qui ne doit pas être négative)</param>
		/// <param name="message"></param>
		[AssertionMethod]
		public static void DoesNotOverflow(
			[System.Diagnostics.CodeAnalysis.NotNull]
			[AssertionCondition(AssertionConditionType.IS_NOT_NULL)] string? buffer,
			int index,
			int count,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression("buffer")] string? paramBuffer = null,
			[InvokerParameterName, CallerArgumentExpression("index")] string? paramIndex = null,
			[InvokerParameterName, CallerArgumentExpression("count")] string? paramCount = null
		)
		{
			if (buffer == null) throw FailArgumentNull(paramBuffer, message);
			if (index < 0 || count < 0) throw FailArgumentNotNonNegative(index < 0 ? paramIndex : paramCount, message);
			if ((buffer.Length - index) < count) throw FailBufferTooSmall(paramCount, message);
		}

		/// <summary>Vérifie qu'une couple index/count ne débord pas d'un buffer, et qu'il n'est pas null</summary>
		/// <param name="bufferLength">Taille du buffer</param>
		/// <param name="offset">Index (qui ne doit pas être négatif)</param>
		/// <param name="count">Taille (qui ne doit pas être négative)</param>
		[AssertionMethod]
		public static void DoesNotOverflow(int bufferLength, int offset, int count, string? message = null)
		{
			if (offset < 0 || count < 0) throw FailArgumentNotNonNegative(offset < 0 ? "offset" : "count", message);
			if ((bufferLength - offset) < count) throw FailBufferTooSmall("count", message);
		}

		/// <summary>Vérifie qu'une couple index/count ne débord pas d'un buffer, et qu'il n'est pas null</summary>
		/// <param name="bufferLength">Taille du buffer</param>
		/// <param name="offset">Index (qui ne doit pas être négatif)</param>
		/// <param name="count">Taille (qui ne doit pas être négative)</param>
		[AssertionMethod]
		public static void DoesNotOverflow(long bufferLength, long offset, long count, string? message = null)
		{
			if (offset < 0 || count < 0) throw FailArgumentNotNonNegative(offset < 0 ? "offset" : "count", message);
			if ((bufferLength - offset) < count) throw FailBufferTooSmall("count", message);
		}

		/// <summary>Vérifie qu'une couple index/count ne débord pas d'un buffer, et qu'il n'est pas null</summary>
		/// <param name="buffer">Buffer (qui ne doit pas être null)</param>
		/// <param name="offset">Index (qui ne doit pas être négatif)</param>
		/// <param name="count">Taille (qui ne doit pas être négative)</param>
		/// <param name="message"></param>
		[AssertionMethod]
		public static void DoesNotOverflow<TElement>(
			[System.Diagnostics.CodeAnalysis.NotNull]
			[AssertionCondition(AssertionConditionType.IS_NOT_NULL)] TElement[]? buffer,
			int offset,
			int count,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression("buffer")] string? paramBuffer = null,
			[InvokerParameterName, CallerArgumentExpression("offset")] string? paramOffset = null,
			[InvokerParameterName, CallerArgumentExpression("count")] string? paramCount = null
		)
		{
			if (buffer == null) throw FailArgumentNull(paramBuffer, message);
			if (offset < 0 || count < 0) throw FailArgumentNotNonNegative(offset < 0 ? paramOffset : paramCount, message);
			if ((buffer.Length - offset) < count) throw FailBufferTooSmall(paramCount, message);
		}

		/// <summary>Vérifie qu'une couple index/count ne débord pas d'un buffer, et qu'il n'est pas null</summary>
		/// <param name="buffer">Buffer (qui ne doit pas être null)</param>
		/// <param name="message"></param>
		public static void DoesNotOverflow<TElement>(
			ArraySegment<TElement> buffer,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression("buffer")] string? paramName = null
		)
		{
			if (buffer.Offset < 0 || buffer.Count < 0) throw FailArgumentNotNonNegative(paramName + (buffer.Offset < 0 ? ".Offset" : ".Count"), message);
			if (buffer.Count > 0)
			{
				if (buffer.Array == null) throw FailBufferNull(paramName, message);
				if ((buffer.Array.Length - buffer.Offset) < buffer.Count) throw FailBufferTooSmall(paramName + ".Count", message);
			}
			else
			{
				if (buffer.Array != null && buffer.Array.Length < buffer.Offset) throw FailBufferTooSmall(paramName + ".Count", message);
			}
		}

		/// <summary>Vérifie qu'une couple index/count ne débord pas d'un buffer, et qu'il n'est pas null</summary>
		/// <param name="buffer">Buffer (qui ne doit pas être null)</param>
		/// <param name="offset">Index (qui ne doit pas être négatif)</param>
		/// <param name="count">Taille (qui ne doit pas être négative)</param>
		/// <param name="message"></param>
		[AssertionMethod]
		public static void DoesNotOverflow<TElement>(
			[System.Diagnostics.CodeAnalysis.NotNull]
			[AssertionCondition(AssertionConditionType.IS_NOT_NULL)] ICollection<TElement>? buffer,
			int offset,
			int count,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression("buffer")] string? paramBuffer = null,
			[InvokerParameterName, CallerArgumentExpression("offset")] string? paramOffset = null,
			[InvokerParameterName, CallerArgumentExpression("count")] string? paramCount = null
		)
		{
			if (buffer == null) throw FailArgumentNull(paramBuffer, message);
			if (offset < 0 || count < 0) throw FailArgumentNotNonNegative(offset < 0 ? paramOffset : paramCount, message);
			if ((buffer.Count - offset) < count) throw FailBufferTooSmall(paramCount, message);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailBufferTooSmall(string paramName, string? message = null)
		{
			return ReportFailure(typeof(ArgumentException), ContractMessages.OffsetMustBeWithinBuffer, message, paramName, ContractMessages.ConditionArgBufferOverflow);
		}

		#endregion

		#endregion

		#region Internal Helpers...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNull(string paramName)
		{
			return ReportFailure(typeof(ArgumentNullException), ContractMessages.ValueCannotBeNull, null, paramName, ContractMessages.ConditionNotNull);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNull(string paramName, string? message)
		{
			return ReportFailure(typeof(ArgumentNullException), ContractMessages.ValueCannotBeNull, message, paramName, ContractMessages.ConditionNotNull);
		}

		/// <summary>Déclenche une exception suite à l'échec d'une condition</summary>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static Exception ReportFailure(Type exceptionType, string msg, string? userMessage, string? paramName, string? conditionTxt)
		{
			if (conditionTxt != null && conditionTxt.IndexOf('{') >= 0)
			{ // il y a peut être un "{0}" dans la condition qu'il faut remplacer le nom du paramètre
				conditionTxt = string.Format(conditionTxt, paramName ?? "value");
			}

			string? str = SRC.ContractHelper.RaiseContractFailedEvent(SDC.ContractFailureKind.Precondition, userMessage ?? msg, conditionTxt, null);
			// si l'appelant retourne null, c'est qu'il a lui même traité l'incident ...
			// mais ca n'empêche pas qu'on doit quand même stopper l'exécution !
#if DEBUG
			if (str != null)
			{
				// note: on ne spam les logs si on est en train de unit tester ! (vu qu'on va provoquer intentionnellement plein d'erreurs!)
				if (!IsUnitTesting)
				{
					System.Diagnostics.Debug.Fail(str);
				}
			}
#endif
			string description = userMessage ?? str ?? msg;

			var exception = ThrowHelper.TryMapToKnownException(exceptionType, description, paramName);

			if (exception == null)
			{ // c'est un type compliqué ??
				exception = ThrowHelper.TryMapToComplexException(exceptionType, description, paramName);
			}

			if (exception == null)
			{ // uh? on va quand même envoyer une exception proxy !
				exception = FallbackForUnknownException(description, paramName);
			}

			return exception;
		}

		private static Exception FallbackForUnknownException(string description, string? paramName)
		{
#if DEBUG
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break(); // README: Si vous tombez ici, c'est que l'appelant a spécifié un type d'Exception qu'on n'arrive pas a construire! il faudrait peut être changer le type...
#endif
			if (paramName != null)
				return new ArgumentException(description, paramName);
			else
				return new InvalidOperationException(description);
		}

		/// <summary>Signale l'échec d'une condition en déclenchant une ContractException</summary>
		/// <remarks>Si un debugger est attaché, un breakpoint est déclenché. Sinon, une ContractException est générée</remarks>
		[Pure]
		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static Exception RaiseContractFailure(SDC.ContractFailureKind kind, string? msg, string? conditionText = null, Exception? exception = null)
		{
			//note: actuellement dans .NET Core 3.x, si conditionText == null, le message formaté ne contient pas la partie "kind" !
			// => le contournement est de passer le message a la place de la condition, ce qui change légèrement la string générée, mais reste lisible!
			string? str = conditionText == null
				? SRC.ContractHelper.RaiseContractFailedEvent(kind, msg, null, exception)
				: SRC.ContractHelper.RaiseContractFailedEvent(kind, msg, conditionText, exception);
			if (str != null)
			{
				if (IsUnitTesting)
				{
					// throws une AssertionException si on a réussi a se connecter avec NUnit
					var ex = MapToNUnitAssertion(str, exception);
#if DEBUG
					// README: Si vous break-pointez ici, il faut remonter plus haut dans la callstack, et trouver la fonction invoque Contract.xxx(...)
					if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
					// note: à partir de VS 2015 Up2, [DebuggerNonUserCode] n'est plus respecté si la regkey AlwaysEnableExceptionCallbacksOutsideMyCode n'est pas égale à 1, pour améliorer les perfs.
					// cf "How to Suppress Ignorable Exceptions with DebuggerNonUserCode" dans https://blogs.msdn.microsoft.com/visualstudioalm/2016/02/12/using-the-debuggernonusercode-attribute-in-visual-studio-2015/
#endif
					if (ex != null) return ex;
					// sinon, on continue
				}
#if DEBUG
				else if (kind == SDC.ContractFailureKind.Assert && Debugger.IsAttached)
				{
					// uniquement si on F5 depuis VS, car sinon cela cause problèmes avec le TestRunner de R# (qui popup les assertion fail!)
					System.Diagnostics.Debug.Fail(str);
				}
#endif

				return new ContractException(kind, str, msg, conditionText, null);
			}
			//note: on doit quand même retourner quelque chose!
			return new ContractException(kind, "Contract Failed", msg, conditionText, null);
		}

		#endregion

	}

}

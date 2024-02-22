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

	/// <summary>Helper type to check for pre-requisites, invariants, assertions, ...</summary>
	[DebuggerNonUserCode]
	[PublicAPI]
	public static partial class Contract
	{

		private static readonly (ConstructorInfo? One, ConstructorInfo? Two) s_constructorNUnitException = GetAssertionExceptionCtor();

		public static bool IsUnitTesting { get; set; }

		private static (ConstructorInfo? One, ConstructorInfo? Two) GetAssertionExceptionCtor()
		{
			// check if we are inside a unit test runner (to mute all breakpoints and other intrusive actions that would block or crash an unattended CI)

			var nUnitAssert = Type.GetType("NUnit.Framework.AssertionException,nunit.framework");
			if (nUnitAssert != null)
			{
				// Convert all "soft" failures into NUnit assertions
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

		/// <summary>Test if a pre-condition is true, at the start of a method.</summary>
		/// <param name="condition">Condition that should never be false</param>
		/// <param name="userMessage">Message that describes the failed assertion (optional)</param>
		/// <param name="conditionText">Text of the condition (optional, injected by the compiler)</param>
		/// <remarks>No-op if <see cref="condition"/> is <c>true</c>. Otherwise, throws a ContractException, after attempting to breakpoint (if a debugger is attached)</remarks>
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

		/// <summary>Test if a condition is true, inside the body of a method.</summary>
		/// <param name="condition">Condition that should never be false</param>
		/// <param name="userMessage">Message that describes the failed assertion (optional)</param>
		/// <param name="conditionText">Text of the condition (optional, injected by the compiler)</param>
		/// <remarks>No-op if <see cref="condition"/> is <c>true</c>. Otherwise, throws a ContractException, after attempting to breakpoint (if a debugger is attached)</remarks>
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

		/// <summary>Test if a post-condition is true, at the end of a method.</summary>
		/// <param name="condition">Condition that should never be false</param>
		/// <param name="userMessage">Message that describes the failed assertion (optional)</param>
		/// <param name="conditionText">Text of the condition (optional, injected by the compiler)</param>
		/// <remarks>No-op if <see cref="condition"/> is <c>true</c>. Otherwise, throws a ContractException, after attempting to breakpoint (if a debugger is attached)</remarks>
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

		/// <summary>Test that an invariant is met.</summary>
		/// <param name="condition">Condition that should never be false</param>
		/// <param name="userMessage">Message that describes the failed assertion (optional)</param>
		/// <param name="conditionText">Text of the condition (optional, injected by the compiler)</param>
		/// <remarks>No-op if <see cref="condition"/> is <c>true</c>. Otherwise, throws a ContractException, after attempting to breakpoint (if a debugger is attached)</remarks>
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
		/// <param name="userMessage">Message that describes the failed assertion (optional)</param>
		/// <param name="exception">Optional exception linked to the issue</param>
		/// <remarks>Throws a <see cref="ContractException"/>, after attempting to breakpoint (if a debugger is attached)</remarks>
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
			[System.Diagnostics.CodeAnalysis.NotNull, AssertionCondition(AssertionConditionType.IS_NOT_NULL), NoEnumeration] string? value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null
		)
		{
			if (value == null) throw FailArgumentNull(paramName!, message);
		}

		/// <summary>The specified instance must not be null (assert: value != null)</summary>
		/// <exception cref="ArgumentNullException">if <paramref name="value"/> is null</exception>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotNull<TValue>(
			[System.Diagnostics.CodeAnalysis.NotNull, AssertionCondition(AssertionConditionType.IS_NOT_NULL), NoEnumeration] TValue? value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null
		)
			where TValue : class
		{
			if (value == null) throw FailArgumentNull(paramName!, message);
		}

		/// <summary>The specified instance must not be null (assert: value != null)</summary>
		/// <remarks>This method allow structs (that can never be null)</remarks>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotNullAllowStructs<TValue>(
			[System.Diagnostics.CodeAnalysis.NotNull, AssertionCondition(AssertionConditionType.IS_NOT_NULL), NoEnumeration] TValue? value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null
		)
		{
			if (value == null) throw FailArgumentNull(paramName!, message);
		}

		/// <summary>The specified pointer must not be null (assert: pointer != null)</summary>
		/// <exception cref="ArgumentNullException">if <paramref name="pointer"/> is null</exception>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void PointerNotNull(
			[System.Diagnostics.CodeAnalysis.AllowNull, System.Diagnostics.CodeAnalysis.NotNull, AssertionCondition(AssertionConditionType.IS_NOT_NULL)] void* pointer,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(pointer))] string? paramName = null)
		{
			if (pointer == null) throw FailArgumentNull(paramName!, message);
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
			[System.Diagnostics.CodeAnalysis.NotNull, AssertionCondition(AssertionConditionType.IS_NOT_NULL), NoEnumeration] T? value,
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
			[System.Diagnostics.CodeAnalysis.NotNull, AssertionCondition(AssertionConditionType.IS_NOT_NULL)] string? value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
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
			[System.Diagnostics.CodeAnalysis.NotNull, AssertionCondition(AssertionConditionType.IS_NOT_NULL)] string? value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
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
			[System.Diagnostics.CodeAnalysis.NotNull, AssertionCondition(AssertionConditionType.IS_NOT_NULL)] T[]? value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null
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
			[System.Diagnostics.CodeAnalysis.NotNull, AssertionCondition(AssertionConditionType.IS_NOT_NULL)] ICollection<T>? value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null
		)
		{
			if (value == null || value.Count == 0) throw FailCollectionNullOrEmpty(value, paramName!, message!);
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
		public static void NotNullOrEmpty(Slice buffer, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(buffer))] string? paramName = null)
		{
			if (buffer.Array == null | buffer.Count == 0) throw FailBufferNullOrEmpty(buffer.Array, paramName!, message);
		}

		/// <summary>The specified buffer must not be null or empty (assert: buffer.Array != null &amp;&amp; buffer.Count != 0)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotNullOrEmpty<T>(ArraySegment<T> buffer, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(buffer))] string? paramName = null)
		{
			if (buffer.Array == null || buffer.Count == 0) throw FailBufferNullOrEmpty(buffer.Array, paramName!, message);
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
			return ReportFailure(typeof(ArgumentException), ContractMessages.ValueIsForbidden, message, paramName, ContractMessages.ConditionArgNotEqualTo);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentExpected<T>(string paramName, T expected, string? message = null)
		{
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
		public static void Positive(int value, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value < 0) throw FailArgumentNotPositive(paramName!, message);
		}

		/// <summary>The specified value must not be a negative number (assert: value >= 0)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Positive(long value, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value < 0) throw FailArgumentNotPositive(paramName!, message);
		}

		/// <summary>The specified value must not be a negative number (assert: value >= 0)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Positive(double value, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (Math.Sign(value) != 1) throw FailArgumentNotPositive(paramName!, message);
		}

		/// <summary>The specified value must not be a negative number (assert: value >= 0)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Positive(float value, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (MathF.Sign(value) != 1) throw FailArgumentNotPositive(paramName!, message);
		}

		#endregion

		#region PowerOfTwo...

		/// <summary>The specified value must be a power of two (assert: NextPowerOfTwo(value) == value)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void PowerOfTwo(int value, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value < 0 || unchecked((value & (value - 1)) != 0)) throw FailArgumentNotPowerOfTwo(paramName!, message);
		}

		/// <summary>The specified value must be a power of two (assert: NextPowerOfTwo(value) == value)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void PowerOfTwo(uint value, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (unchecked((value & (value - 1)) != 0)) throw FailArgumentNotPowerOfTwo(paramName!, message);
		}

		/// <summary>The specified value must be a power of two (assert: NextPowerOfTwo(value) == value)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void PowerOfTwo(long value, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value < 0 || unchecked((value & (value - 1)) != 0)) throw FailArgumentNotPowerOfTwo(paramName!, message);
		}

		/// <summary>The specified value must be a power of two (assert: NextPowerOfTwo(value) == value)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void PowerOfTwo(ulong value, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (unchecked((value & (value - 1)) != 0)) throw FailArgumentNotPowerOfTwo(paramName!, message);
		}

		#endregion

		#region EqualTo...

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EqualTo(int value, int expected, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value != expected) throw FailArgumentExpected(paramName!, expected, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EqualTo(long value, long expected, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value != expected) throw FailArgumentExpected(paramName!, expected, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EqualTo(uint value, uint expected, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value != expected) throw FailArgumentExpected(paramName!, expected, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EqualTo(ulong value, ulong expected, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value != expected) throw FailArgumentExpected(paramName!, expected, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EqualTo(string? value, string? expected, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value != expected) throw FailArgumentExpected(paramName!, expected, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EqualTo<T>(T value, T expected, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
			where T : struct, IEquatable<T>
		{
			if (!value.Equals(expected)) throw FailArgumentExpected(paramName!, expected, message);
		}

		#endregion

		#region NotEqualTo...

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotEqualTo(int value, int forbidden, string? message = null, [InvokerParameterName] [CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value == forbidden) throw FailArgumentForbidden(paramName!, forbidden, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotEqualTo(long value, long forbidden, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value == forbidden) throw FailArgumentForbidden(paramName!, forbidden, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotEqualTo(uint value, uint forbidden, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value == forbidden) throw FailArgumentForbidden(paramName!, forbidden, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotEqualTo(ulong value, ulong forbidden, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value == forbidden) throw FailArgumentForbidden(paramName!, forbidden, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotEqualTo(string value, string forbidden, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value == forbidden) throw FailArgumentForbidden(paramName!, forbidden, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotEqualTo<T>(T value, T forbidden, string? message = null, [InvokerParameterName] [CallerArgumentExpression(nameof(value))] string? paramName = null)
			where T : struct, IEquatable<T>
		{
			if (value.Equals(forbidden)) throw FailArgumentForbidden(paramName!, forbidden, message);
		}

		#endregion

		#region GreaterThan...

		/// <summary>The specified value must not less than or equal to the specified lower bound (assert: value > threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterThan(int value, int threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value <= threshold) throw FailArgumentNotGreaterThan(valueExpression!, thresholdExpression!, threshold == 0, message);
		}

		/// <summary>The specified value must not less than or equal to the specified lower bound (assert: value > threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterThan(uint value, uint threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value <= threshold) throw FailArgumentNotGreaterThan(valueExpression!, thresholdExpression!, threshold == 0, message);
		}

		/// <summary>The specified value must not less than or equal to the specified lower bound (assert: value > threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterThan(long value, long threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value <= threshold) throw FailArgumentNotGreaterThan(valueExpression!, thresholdExpression!, threshold == 0, message);
		}

		/// <summary>The specified value must not less than or equal to the specified lower bound (assert: value > threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterThan(ulong value, ulong threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value <= threshold) throw FailArgumentNotGreaterThan(valueExpression!, thresholdExpression!, threshold == 0, message);
		}

		/// <summary>The specified value must not less than or equal to the specified lower bound (assert: value > threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterThan(float value, float threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (value <= threshold) throw FailArgumentNotGreaterThan(valueExpression!, thresholdExpression!, threshold == 0.0f, message);
		}

		/// <summary>The specified value must not less than or equal to the specified lower bound (assert: value > threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterThan(double value, double threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (value <= threshold) throw FailArgumentNotGreaterThan(valueExpression!, thresholdExpression!, threshold == 0.0d, message);
		}

		/// <summary>The specified value must not less than or equal to the specified lower bound (assert: value > threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterThan<T>(T value, T threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
			where T: struct, IComparable<T>
		{
			if (value.CompareTo(threshold) <= 0) throw FailArgumentNotGreaterThan(valueExpression!, thresholdExpression!, threshold.CompareTo(default) == 0, message);
		}

		#endregion
		
		#region GreaterOrEqual...

		/// <summary>The specified value must not less than the specified lower bound (assert: value >= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterOrEqual(int value, int threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value < threshold) throw FailArgumentNotGreaterOrEqual(valueExpression!, thresholdExpression!, threshold == 0, message);
		}

		/// <summary>The specified value must not less than the specified lower bound (assert: value >= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterOrEqual(uint value, uint threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value < threshold) throw FailArgumentNotGreaterOrEqual(valueExpression!, thresholdExpression!, false, message);
		}

		/// <summary>The specified value must not less than the specified lower bound (assert: value >= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterOrEqual(long value, long threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value < threshold) throw FailArgumentNotGreaterOrEqual(valueExpression!, thresholdExpression!, threshold == 0, message);
		}

		/// <summary>The specified value must not less than the specified lower bound (assert: value >= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterOrEqual(ulong value, ulong threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value < threshold) throw FailArgumentNotGreaterOrEqual(valueExpression!, thresholdExpression!, false, message);
		}

		/// <summary>The specified value must not less than the specified lower bound (assert: value >= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterOrEqual(float value, float threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (value < threshold) throw FailArgumentNotGreaterOrEqual(valueExpression!, thresholdExpression!, threshold == 0.0f, message);
		}

		/// <summary>The specified value must not less than the specified lower bound (assert: value >= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterOrEqual(double value, double threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (value < threshold) throw FailArgumentNotGreaterOrEqual(valueExpression!, thresholdExpression!, threshold == 0.0d, message);
		}

		/// <summary>The specified value must not less than the specified lower bound (assert: value >= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GreaterOrEqual<T>(T value, T threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
			where T : struct, IComparable<T>
		{
			if (value.CompareTo(threshold) < 0) throw FailArgumentNotGreaterOrEqual(valueExpression!, thresholdExpression!, threshold.CompareTo(default) == 0, message);
		}

		#endregion

		#region LessThan...

		/// <summary>The specified value must not greater than or equal to the specified upper bound</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessThan(int value, int threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value >= threshold) throw FailArgumentNotLessThan(valueExpression!, thresholdExpression!, message);
		}

		/// <summary>The specified value must not greater than or equal to the specified upper bound</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessThan(uint value, uint threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value >= threshold) throw FailArgumentNotLessThan(valueExpression!, thresholdExpression!, message);
		}

		/// <summary>The specified value must not greater than or equal to the specified upper bound (assert: value &lt; threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessThan(long value, long threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value >= threshold) throw FailArgumentNotLessThan(valueExpression!, thresholdExpression!, message);
		}

		/// <summary>The specified value must not greater than or equal to the specified upper bound (assert: value &lt; threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessThan(ulong value, ulong threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value >= threshold) throw FailArgumentNotLessThan(valueExpression!, thresholdExpression!, message);
		}

		/// <summary>The specified value must not greater than or equal to the specified upper bound (assert: value &lt; threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessThan(float value, float threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value >= threshold) throw FailArgumentNotLessThan(valueExpression!, thresholdExpression!, message);
		}

		/// <summary>The specified value must not greater than or equal to the specified upper bound (assert: value &lt; threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessThan(double value, double threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value >= threshold) throw FailArgumentNotLessThan(valueExpression!, thresholdExpression!, message);
		}

		/// <summary>The specified value must not greater than or equal to the specified upper bound (assert: value &lt; threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessThan<T>(T value, T threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
			where T : struct, IComparable<T>
		{
			if (value.CompareTo(threshold) >= 0) throw FailArgumentNotLessThan(valueExpression!, thresholdExpression!, message);
		}

		#endregion

		#region LessOrEqual...

		/// <summary>The specified value must not greater than the specified upper bound (assert: value &lt;= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessOrEqual(int value, int threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value > threshold) throw FailArgumentNotLessOrEqual(valueExpression!, thresholdExpression!, message);
		}

		/// <summary>The specified value must not greater than the specified upper bound (assert: value &lt;= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessOrEqual(uint value, uint threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value > threshold) throw FailArgumentNotLessOrEqual(valueExpression!, thresholdExpression!, message);
		}

		/// <summary>The specified value must not greater than the specified upper bound (assert: value &lt;= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessOrEqual(long value, long threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value > threshold) throw FailArgumentNotLessOrEqual(valueExpression!, thresholdExpression!, message);
		}

		/// <summary>The specified value must not greater than the specified upper bound (assert: value &lt;= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessOrEqual(ulong value, ulong threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value > threshold) throw FailArgumentNotLessOrEqual(valueExpression!, thresholdExpression!, message);
		}

		/// <summary>The specified value must not greater than the specified upper bound (assert: value &lt;= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessOrEqual(float value, float threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value > threshold) throw FailArgumentNotLessOrEqual(valueExpression!, thresholdExpression!, message);
		}

		/// <summary>The specified value must not greater than the specified upper bound (assert: value &lt;= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessOrEqual(double value, double threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value > threshold) throw FailArgumentNotLessOrEqual(valueExpression!, thresholdExpression!, message);
		}

		/// <summary>The specified value must not greater than the specified upper bound (assert: value &lt;= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LessOrEqual<T>(T value, T threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
			where T : struct, IComparable<T>
		{
			if (value.CompareTo(threshold) > 0) throw FailArgumentNotLessOrEqual(valueExpression!, thresholdExpression!, message);
		}

		#endregion

		#region Between...

		/// <summary>The specified value must not be outside the specified bounds (assert: min &lt;= value &lt;= max)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Between(int value, int minimumInclusive, int maximumInclusive, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(minimumInclusive))] string? minExpression = null, [InvokerParameterName, CallerArgumentExpression("maximumInclusive")] string? maxExpression = null)
		{
			if (value < minimumInclusive || value > maximumInclusive) throw FailArgumentOutOfBounds(valueExpression!, minExpression!, maxExpression!, message);
		}

		/// <summary>The specified value must not be outside the specified bounds (assert: min &lt;= value &lt;= max)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Between(uint value, uint minimumInclusive, uint maximumInclusive, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(minimumInclusive))] string? minExpression = null, [InvokerParameterName, CallerArgumentExpression("maximumInclusive")] string? maxExpression = null)
		{
			if (value < minimumInclusive || value > maximumInclusive) throw FailArgumentOutOfBounds(valueExpression!, minExpression!, maxExpression!, message);
		}

		/// <summary>The specified value must not be outside the specified bounds (assert: min &lt;= value &lt;= max)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Between(long value, long minimumInclusive, long maximumInclusive, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(minimumInclusive))] string? minExpression = null, [InvokerParameterName, CallerArgumentExpression("maximumInclusive")] string? maxExpression = null)
		{
			if (value < minimumInclusive || value > maximumInclusive) throw FailArgumentOutOfBounds(valueExpression!, minExpression!, maxExpression!, message);
		}

		/// <summary>The specified value must not be outside the specified bounds (assert: min &lt;= value &lt;= max)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Between(ulong value, ulong minimumInclusive, ulong maximumInclusive, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(minimumInclusive))] string? minExpression = null, [InvokerParameterName, CallerArgumentExpression("maximumInclusive")] string? maxExpression = null)
		{
			if (value < minimumInclusive || value > maximumInclusive) throw FailArgumentOutOfBounds(valueExpression!, minExpression!, maxExpression!, message);
		}

		/// <summary>The specified value must not be outside the specified bounds (assert: min &lt;= value &lt;= max)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Between(float value, float minimumInclusive, float maximumInclusive, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(minimumInclusive))] string? minExpression = null, [InvokerParameterName, CallerArgumentExpression("maximumInclusive")] string? maxExpression = null)
		{
			if (value < minimumInclusive || value > maximumInclusive) throw FailArgumentOutOfBounds(valueExpression!, minExpression!, maxExpression!, message);
		}

		/// <summary>The specified value must not be outside the specified bounds (assert: min &lt;= value &lt;= max)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Between(double value, double minimumInclusive, double maximumInclusive, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(minimumInclusive))] string? minExpression = null, [InvokerParameterName, CallerArgumentExpression("maximumInclusive")] string? maxExpression = null)
		{
			if (value < minimumInclusive || value > maximumInclusive) throw FailArgumentOutOfBounds(valueExpression!, minExpression!, maxExpression!, message);
		}

		#endregion

		#region Multiple...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNotMultiple(string paramName, string? message = null)
		{
			return ReportFailure(typeof(ArgumentException), ContractMessages.ValueMustBeMultiple, message, paramName, ContractMessages.ConditionArgMultiple);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Multiple(int value, int multiple, string? message = null, [InvokerParameterName] [CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value % multiple != 0) throw FailArgumentNotMultiple(paramName!, message);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Multiple(uint value, uint multiple, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value % multiple != 0) throw FailArgumentNotMultiple(paramName!, message);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Multiple(long value, long multiple, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value % multiple != 0) throw FailArgumentNotMultiple(paramName!, message);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Multiple(ulong value, ulong multiple, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value % multiple != 0) throw FailArgumentNotMultiple(paramName!, message);
		}

		#endregion

		#endregion

		#region Contract.DoesNotOverflow

		/// <summary>Vérifie qu'une couple index/count ne débord pas d'un buffer, et qu'il n'est pas null</summary>
		/// <param name="buffer">Buffer (qui ne doit pas être null)</param>
		/// <param name="index">Index (qui ne doit pas être négatif)</param>
		/// <param name="count">Taille (qui ne doit pas être négative)</param>
		/// <param name="message"></param>
		/// <param name="paramBuffer"></param>
		/// <param name="paramIndex"></param>
		/// <param name="paramCount"></param>
		[AssertionMethod]
		public static void DoesNotOverflow(
			[System.Diagnostics.CodeAnalysis.NotNull]
			[AssertionCondition(AssertionConditionType.IS_NOT_NULL)] string? buffer,
			int index,
			int count,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(buffer))] string? paramBuffer = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(index))] string? paramIndex = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(count))] string? paramCount = null
		)
		{
			if (buffer == null) throw FailArgumentNull(paramBuffer!, message);
			if (index < 0 || count < 0) throw FailArgumentNotNonNegative(index < 0 ? paramIndex! : paramCount!, message);
			if ((buffer.Length - index) < count) throw FailBufferTooSmall(paramCount!, message);
		}

		/// <summary>Vérifie qu'une couple index/count ne débord pas d'un buffer, et qu'il n'est pas null</summary>
		/// <param name="bufferLength">Taille du buffer</param>
		/// <param name="offset">Index (qui ne doit pas être négatif)</param>
		/// <param name="count">Taille (qui ne doit pas être négative)</param>
		/// <param name="message"></param>
		[AssertionMethod]
		public static void DoesNotOverflow(int bufferLength, int offset, int count, string? message = null)
		{
			if (offset < 0 || count < 0) throw FailArgumentNotNonNegative(offset < 0 ? nameof(offset) : nameof(count), message);
			if ((bufferLength - offset) < count) throw FailBufferTooSmall(nameof(count), message);
		}

		/// <summary>Vérifie qu'une couple index/count ne débord pas d'un buffer, et qu'il n'est pas null</summary>
		/// <param name="bufferLength">Taille du buffer</param>
		/// <param name="offset">Index (qui ne doit pas être négatif)</param>
		/// <param name="count">Taille (qui ne doit pas être négative)</param>
		/// <param name="message"></param>
		[AssertionMethod]
		public static void DoesNotOverflow(long bufferLength, long offset, long count, string? message = null)
		{
			if (offset < 0 || count < 0) throw FailArgumentNotNonNegative(offset < 0 ? nameof(offset) : nameof(count), message);
			if ((bufferLength - offset) < count) throw FailBufferTooSmall(nameof(count), message);
		}

		/// <summary>Vérifie qu'une couple index/count ne débord pas d'un buffer, et qu'il n'est pas null</summary>
		/// <param name="buffer">Buffer (qui ne doit pas être null)</param>
		/// <param name="offset">Index (qui ne doit pas être négatif)</param>
		/// <param name="count">Taille (qui ne doit pas être négative)</param>
		/// <param name="message"></param>
		/// <param name="paramBuffer"></param>
		/// <param name="paramOffset"></param>
		/// <param name="paramCount"></param>
		[AssertionMethod]
		public static void DoesNotOverflow<TElement>(
			[System.Diagnostics.CodeAnalysis.NotNull]
			[AssertionCondition(AssertionConditionType.IS_NOT_NULL)] TElement[]? buffer,
			int offset,
			int count,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(buffer))] string? paramBuffer = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(offset))] string? paramOffset = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(count))] string? paramCount = null
		)
		{
			if (buffer == null) throw FailArgumentNull(paramBuffer!, message);
			if (offset < 0 || count < 0) throw FailArgumentNotNonNegative(offset < 0 ? paramOffset! : paramCount!, message);
			if ((buffer.Length - offset) < count) throw FailBufferTooSmall(paramCount!, message);
		}

		/// <summary>Vérifie qu'une couple index/count ne débord pas d'un buffer, et qu'il n'est pas null</summary>
		/// <param name="buffer">Buffer (qui ne doit pas être null)</param>
		/// <param name="message"></param>
		/// <param name="paramName"></param>
		public static void DoesNotOverflow<TElement>(
			ArraySegment<TElement> buffer,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(buffer))] string? paramName = null
		)
		{
			if (buffer.Offset < 0 || buffer.Count < 0) throw FailArgumentNotNonNegative(paramName + (buffer.Offset < 0 ? ".Offset" : ".Count"), message);
			if (buffer.Count > 0)
			{
				if (buffer.Array == null) throw FailBufferNull(paramName!, message);
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
		/// <param name="paramBuffer"></param>
		/// <param name="paramOffset"></param>
		/// <param name="paramCount"></param>
		[AssertionMethod]
		public static void DoesNotOverflow<TElement>(
			[System.Diagnostics.CodeAnalysis.NotNull]
			[AssertionCondition(AssertionConditionType.IS_NOT_NULL)] ICollection<TElement>? buffer,
			int offset,
			int count,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(buffer))] string? paramBuffer = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(offset))] string? paramOffset = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(count))] string? paramCount = null
		)
		{
			if (buffer == null) throw FailArgumentNull(paramBuffer!, message);
			if (offset < 0 || count < 0) throw FailArgumentNotNonNegative(offset < 0 ? paramOffset! : paramCount!, message);
			if ((buffer.Count - offset) < count) throw FailBufferTooSmall(paramCount!, message);
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

		/// <summary>Throws an exception, following a failed assertion</summary>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static Exception ReportFailure(Type exceptionType, string msg, string? userMessage, string? paramName, string? conditionTxt)
		{
			if (conditionTxt != null && conditionTxt.IndexOf('{') >= 0)
			{ // Replace any occurence of "{0}" in the condition, by the name of the parameter
				conditionTxt = string.Format(conditionTxt, paramName ?? "value");
			}

			string? str = SRC.ContractHelper.RaiseContractFailedEvent(SDC.ContractFailureKind.Precondition, userMessage ?? msg, conditionTxt, null);
			// If this returns null, then the issue has already been handled (popup on the screen, ...)
			// But we still need to stop the execution!
#if DEBUG
			if (str != null)
			{
				// note: do not spam the logs if in the context a of unit test runner!
				if (!IsUnitTesting)
				{
					System.Diagnostics.Debug.Fail(str);
				}
			}
#endif
			string description = userMessage ?? str ?? msg;

			var exception = ThrowHelper.TryMapToKnownException(exceptionType, description, paramName);

			if (exception == null)
			{ // Is this a complex exception type ?
				exception = ThrowHelper.TryMapToComplexException(exceptionType, description, paramName);
			}

			if (exception == null)
			{ // still not know? we'll try to wrap it with a proxy exception
				exception = FallbackForUnknownException(description, paramName);
			}

			return exception;
		}

		private static Exception FallbackForUnknownException(string description, string? paramName)
		{
#if DEBUG
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break(); // README: If you breakpoint here, it means the caller has requested a type of Exception that we cannot instantiate! Please change the type to something easier
#endif
			return paramName != null
				? new ArgumentException(description, paramName)
				: new InvalidOperationException(description);
		}

		/// <summary>Signale l'échec d'une condition en déclenchant une ContractException</summary>
		/// <remarks>Si un debugger est attaché, un breakpoint est déclenché. Sinon, une ContractException est générée</remarks>
		[Pure]
		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static Exception RaiseContractFailure(SDC.ContractFailureKind kind, string? msg, string? conditionText = null, Exception? exception = null)
		{
			//note: currently in .NET Core 3.x, if conditionText == null, the formatted message will not have the "kind" section!
			// => the workaround is to pass the message itself, instead of the condition text, which will slightly change the generated string, but will still be readable!
			string? str = conditionText == null
				? SRC.ContractHelper.RaiseContractFailedEvent(kind, msg, null, exception)
				: SRC.ContractHelper.RaiseContractFailedEvent(kind, msg, conditionText, exception);
			if (str != null)
			{
				if (IsUnitTesting)
				{
					// throws an AssertionException if we were able to connect with NUnit
					var ex = MapToNUnitAssertion(str, exception);
#if DEBUG
					// README: If you breakpoint here, you are too deep and must go up the stack, until you find the method that invoked any of the Contract.xxx(...) helpers!
					if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
					// note: starting from VS 2015 Up2, [DebuggerNonUserCode] has no effect anymore, if the registry key AlwaysEnableExceptionCallbacksOutsideMyCode is not set to 1, for performance reasons.
					// cf "How to Suppress Ignorable Exceptions with DebuggerNonUserCode" dans https://blogs.msdn.microsoft.com/visualstudioalm/2016/02/12/using-the-debuggernonusercode-attribute-in-visual-studio-2015/
#endif
					if (ex != null) return ex;
					// if not null, continue
				}
#if DEBUG
				else if (kind == SDC.ContractFailureKind.Assert && Debugger.IsAttached)
				{
					// only when debugging from VS, or else it will cause issues with Resharper's TestRunner (that will display a popup for each failed assertion!)
					System.Diagnostics.Debug.Fail(str);
				}
#endif

				return new ContractException(kind, str, msg, conditionText, null);
			}
			//note: we still need to return something!
			return new ContractException(kind, "Contract Failed", msg, conditionText, null);
		}

		#endregion

	}

}

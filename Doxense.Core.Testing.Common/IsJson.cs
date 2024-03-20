﻿#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

// ReSharper disable MemberHidesStaticFromOuterClass
namespace SnowBank.Testing
{
	using System;
	using System.Collections.Generic;
	using Doxense.Serialization.Json;
	using JetBrains.Annotations;
	using NUnit.Framework.Constraints;

	/// <summary>JSON Assertions</summary>
	[PublicAPI]
	public static class IsJson
	{

		public static JsonConstraintExpression Not
		{
			get
			{
				var expr = new JsonConstraintExpression();
				expr.Append(new JsonNotOperator());
				return expr;
			}
		}

		/// <summary>Assert that the value is null or missing</summary>
		/// <remarks>Includes all variants of <see cref="JsonNull"/></remarks>
		public static JsonConstraint Null => new JsonTypeConstraint(JsonType.Null);

		/// <summary>Assert that the value is a JSON Object</summary>
		public static JsonConstraint Object => new JsonTypeConstraint(JsonType.Object);

		/// <summary>Assert that the value is a JSON Array</summary>
		public static JsonConstraint Array => new JsonTypeConstraint(JsonType.Array);

		/// <summary>Assert that the value is a JSON String</summary>
		public static JsonConstraint String => new JsonTypeConstraint(JsonType.String);

		/// <summary>Assert that the value is a JSON Number</summary>
		public static JsonConstraint Number => new JsonTypeConstraint(JsonType.Number);

		/// <summary>Assert that the value is a JSON Boolean</summary>
		public static JsonConstraint Boolean => new JsonTypeConstraint(JsonType.Boolean);

		/// <summary>Assert that the value is an explicit JSON Null literal</summary>
		/// <remarks>This only accept the singleton <see cref="JsonNull.Null"/>. If you want to also include <see cref="JsonNull.Missing"/>, you should use the <see cref="Null"/> assertion.</remarks>
		public static JsonConstraint ExplicitNull => new JsonEqualConstraint(JsonComparisonOperator.SameAs, JsonNull.Null);

		/// <summary>Assert that the value is missing</summary>
		/// <remarks>This only accept the singleton <see cref="JsonNull.Missing"/>. If you want to also include <see cref="JsonNull.Null"/>, you should use the <see cref="Null"/> assertion.</remarks>
		public static JsonConstraint Missing => new JsonEqualConstraint(JsonComparisonOperator.SameAs, JsonNull.Missing);

		/// <summary>Assert that the value is out of bounds of its parent array.</summary>
		public static JsonConstraint Error => new JsonEqualConstraint(JsonComparisonOperator.SameAs, JsonNull.Error);

		/// <summary>Assert that the value is <see cref="JsonBoolean.False"/></summary>
		public static JsonConstraint False => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonBoolean.False);

		/// <summary>Assert that the value is <see cref="JsonBoolean.True"/></summary>
		public static JsonConstraint True => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonBoolean.True);

		/// <summary>Assert that the value is <see cref="JsonNumber.Zero"/></summary>
		public static JsonConstraint Zero => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonNumber.Zero);

		/// <summary>Assert that the value is empty</summary>
		/// <remarks>Supports Objects, Arrays and Strings.</remarks>
		public static JsonConstraint Empty => new JsonEmptyConstraint();

		/// <summary>Assert that the value is read-only.</summary>
		public static JsonConstraint ReadOnly => new JsonReadOnlyConstraint();

		#region JsonValue...
		
		/// <summary>Assert that the value is a JSON Value equal to the expected value</summary>
		public static JsonConstraint EqualTo(JsonValue expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, expected);

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo(IEnumerable<JsonValue> expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		/// <summary>Assert that the value is strictly greater than the expected value</summary>
		public static JsonConstraint GreaterThan(JsonValue expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, expected);

		/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
		public static JsonConstraint GreaterThanOrEqualTo(JsonValue expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, expected);

		/// <summary>Assert that the value is strictly less than the expected value</summary>
		public static JsonConstraint LessThan(JsonValue expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, expected);

		/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
		public static JsonConstraint LessThanOrEqualTo(JsonValue expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, expected);

		#endregion

		#region Generic...
		
		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo<TValue>(TValue expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonValue.FromValue(expected));

		/// <summary>Assert that the value is strictly greater than the expected value</summary>
		public static JsonConstraint GreaterThan<TValue>(TValue expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, JsonValue.FromValue(expected));

		/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
		public static JsonConstraint GreaterThanOrEqualTo<TValue>(TValue expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonValue.FromValue(expected));

		/// <summary>Assert that the value is strictly less than the expected value</summary>
		public static JsonConstraint LessThan<TValue>(TValue expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, JsonValue.FromValue(expected));

		/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
		public static JsonConstraint LessThanOrEqualTo<TValue>(TValue expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonValue.FromValue(expected));

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo<TValue>(TValue[] expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo<TValue>(ReadOnlySpan<TValue> expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo<TValue>(IEnumerable<TValue> expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		#endregion

		#region Boolean...
		
		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(bool expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, expected ? JsonBoolean.True : JsonBoolean.False);

		#endregion

		#region Int32...
		
		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(int expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonNumber.Return(expected));

		/// <summary>Assert that the value is strictly greater than the expected value</summary>
		public static JsonConstraint GreaterThan(int expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(expected));

		/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
		public static JsonConstraint GreaterThanOrEqualTo(int expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(expected));

		/// <summary>Assert that the value is strictly less than the expected value</summary>
		public static JsonConstraint LessThan(int expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(expected));

		/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
		public static JsonConstraint LessThanOrEqualTo(int expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(expected));

		#endregion

		#region Int64...
		
		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(long expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonNumber.Return(expected));

		/// <summary>Assert that the value is strictly greater than the expected value</summary>
		public static JsonConstraint GreaterThan(long expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(expected));

		/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
		public static JsonConstraint GreaterThanOrEqualTo(long expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(expected));

		/// <summary>Assert that the value is strictly less than the expected value</summary>
		public static JsonConstraint LessThan(long expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(expected));

		/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
		public static JsonConstraint LessThanOrEqualTo(long expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(expected));

		#endregion

		#region Float...
		
		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(float expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonNumber.Return(expected));

		/// <summary>Assert that the value is strictly greater than the expected value</summary>
		public static JsonConstraint GreaterThan(float expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(expected));

		/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
		public static JsonConstraint GreaterThanOrEqualTo(float expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(expected));

		/// <summary>Assert that the value is strictly less than the expected value</summary>
		public static JsonConstraint LessThan(float expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(expected));

		/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
		public static JsonConstraint LessThanOrEqualTo(float expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(expected));

		#endregion

		#region Double...
		
		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(double expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonNumber.Return(expected));

		/// <summary>Assert that the value is strictly greater than the expected value</summary>
		public static JsonConstraint GreaterThan(double expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(expected));

		/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
		public static JsonConstraint GreaterThanOrEqualTo(double expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(expected));

		/// <summary>Assert that the value is strictly less than the expected value</summary>
		public static JsonConstraint LessThan(double expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(expected));

		/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
		public static JsonConstraint LessThanOrEqualTo(double expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(expected));

		#endregion

		#region Strings...

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(string? expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonString.Return(expected));

		/// <summary>Assert that the value is strictly greater than the expected value</summary>
		public static JsonConstraint GreaterThan(string? expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, JsonString.Return(expected));

		/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
		public static JsonConstraint GreaterThanOrEqualTo(string? expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonString.Return(expected));

		/// <summary>Assert that the value is strictly less than the expected value</summary>
		public static JsonConstraint LessThan(string? expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, JsonString.Return(expected));

		/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
		public static JsonConstraint LessThanOrEqualTo(string? expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonString.Return(expected));

		#endregion

		public enum JsonComparisonOperator
		{
			Equal,
			SameAs,
			GreaterThan,
			GreaterThanOrEqual,
			LessThan,
			LessThanOrEqual,
		}

		public abstract class JsonConstraint : Constraint
		{

			protected JsonConstraint(params object?[] args)
				: base(args)
			{ }

			public new JsonConstraintExpression And
			{
				get
				{
					var builder = this.Builder;
					if (builder == null)
					{
						builder = new ConstraintBuilder();
						builder.Append(this);
					}
					builder.Append(new JsonAndOperator());
					return new JsonConstraintExpression(builder);
				}
			}

			public new JsonConstraintExpression Or
			{
				get
				{
					var builder = this.Builder;
					if (builder == null)
					{
						builder = new ConstraintBuilder();
						builder.Append(this);
					}
					builder.Append(new JsonOrOperator());
					return new JsonConstraintExpression(builder);
				}
			}

		}

		internal sealed class JsonEqualConstraint : JsonConstraint
		{

			public JsonComparisonOperator Operator { get; }

			public JsonValue Expected { get; }

			public JsonEqualConstraint(JsonComparisonOperator op, JsonValue expected) : base(expected)
			{
				this.Operator = op;
				this.Expected = expected;
			}

			public override ConstraintResult ApplyTo<TActual>(TActual actual)
			{
				var obj = actual as JsonValue ?? (actual is null ? JsonNull.Null : null);
				if (obj is null)
				{ // the actual value is NOT a JsonValue
					return new JsonEqualConstraintResult(this, obj, false);
				}
				return this.Operator switch
				{
					JsonComparisonOperator.Equal => new JsonEqualConstraintResult(this, obj, obj.Equals(this.Expected)),
					JsonComparisonOperator.SameAs => new JsonEqualConstraintResult(this, obj, ReferenceEquals(obj, this.Expected)),
					JsonComparisonOperator.GreaterThan => new JsonEqualConstraintResult(this, obj, obj > this.Expected),
					JsonComparisonOperator.GreaterThanOrEqual => new JsonEqualConstraintResult(this, obj, obj >= this.Expected),
					JsonComparisonOperator.LessThan => new JsonEqualConstraintResult(this, obj, obj < this.Expected),
					JsonComparisonOperator.LessThanOrEqual => new JsonEqualConstraintResult(this, obj, obj <= this.Expected),
					_ => throw new NotSupportedException()
				};
			}

			public override string Description => this.Expected.ToJsonCompact();

			public class JsonEqualConstraintResult : ConstraintResult
			{

				public JsonValue ExpectedValue { get; }

				public JsonComparisonOperator Operator { get; }

				public JsonEqualConstraintResult(JsonEqualConstraint constraint, object? actual, bool hasSucceeded)
					: base(constraint, actual, hasSucceeded)
				{
					this.Operator = constraint.Operator;
					this.ExpectedValue = constraint.Expected;
				}

				private void WriteDifferences(MessageWriter writer, string? op, string message)
				{
					writer.WriteMessageLine(message);
					writer.Write("  Expected: ");
					if (op != null) writer.Write(op + " ");
					if (this.ActualValue is JsonValue value)
					{
						bool showType = value.Type != this.ExpectedValue.Type;

						if (showType) writer.Write($"<{this.ExpectedValue.Type}> ");
						writer.WriteLine(this.ExpectedValue.ToJsonCompact());

						writer.Write("  But was:  ");
						if (showType) writer.Write($"<{value.Type}> ");
						writer.WriteLine(value.ToJsonCompact());
					}
					else if (this.ActualValue is null)
					{
						writer.WriteLine(this.ExpectedValue.ToJsonCompact());
						writer.Write("  But was:  ");
						writer.WriteLine("<null>");
					}
					else
					{
						writer.WriteLine(this.ExpectedValue.ToJsonCompact());
						writer.Write("  But was:  ");
						writer.WriteActualValue(this.ActualValue);
					}
				}

				public override void WriteMessageTo(MessageWriter writer)
				{
					if (this.IsSuccess)
					{
						base.WriteMessageTo(writer);
						return;
					}

					switch (this.Operator)
					{
						case JsonComparisonOperator.Equal:
						{
							WriteDifferences(writer, null, "JSON value does not match the expected value");
							break;
						}
						case JsonComparisonOperator.SameAs:
						{
							WriteDifferences(writer, "===", "JSON value should be the same instance as the expected value");
							break;
						}
						case JsonComparisonOperator.GreaterThan:
						{
							WriteDifferences(writer, ">", "JSON value should be greater than the expected value");
							break;
						}
						case JsonComparisonOperator.GreaterThanOrEqual:
						{
							WriteDifferences(writer, ">=", "JSON value should be greater than or equal to the expected value");
							break;
						}
						case JsonComparisonOperator.LessThan:
						{
							WriteDifferences(writer, "<", "JSON value should be less than the expected value");
							break;
						}
						case JsonComparisonOperator.LessThanOrEqual:
						{
							WriteDifferences(writer, "<=", "JSON value should be less than or equal to the expected value");
							break;
						}
						default:
						{
							throw new NotImplementedException();
						}
					}
				}
			}

		}

		internal sealed class JsonTypeConstraint : JsonConstraint
		{

			public JsonType Expected { get; }

			public JsonTypeConstraint(JsonType expected) : base(expected)
			{
				this.Expected = expected;
			}

			public override ConstraintResult ApplyTo<TActual>(TActual actual)
			{
				if (actual is not JsonValue value)
				{
					if (actual is not null)
					{
						throw new ArgumentException("The actual value must be a JSON value. The value passed was of type " + typeof(TActual).Name, nameof(actual));
					}
					value = JsonNull.Null;
				}
				return new JsonTypeConstraintResult(this, value, value.Type == this.Expected);
			}

			public override string Description => this.Expected.ToString();

			public class JsonTypeConstraintResult : ConstraintResult
			{

				public JsonType ExpectedType { get; }

				public JsonValue ActualJsonValue { get; }

				public JsonTypeConstraintResult(JsonTypeConstraint constraint, JsonValue actual, bool hasSucceeded)
					: base(constraint, actual, hasSucceeded)
				{
					this.ExpectedType = constraint.Expected;
					this.ActualJsonValue = actual;
				}

				public override void WriteMessageTo(MessageWriter writer)
				{
					if (this.IsSuccess)
					{
						base.WriteMessageTo(writer);
						return;
					}

					writer.WriteMessageLine("JSON value is of a different type than expected.");
					writer.Write("  Expected: ");
					writer.WriteLine($"<{this.ExpectedType.ToString()}>");
					writer.Write("  But was:  ");
					writer.Write($"<{this.ActualJsonValue.Type}> ");
					writer.WriteLine(this.ActualJsonValue.ToJsonCompact());
				}

			}

		}

		internal sealed class JsonEmptyConstraint : JsonConstraint
		{

			public override ConstraintResult ApplyTo<TActual>(TActual actual)
			{
				var val = actual as JsonValue ?? (actual is null ? JsonNull.Null : null);
				return val switch
				{
					null => throw new ArgumentException("The actual value must be a JSON value. The value passed was of type " + typeof(TActual).Name, nameof(actual)),
					JsonArray arr => new JsonEmptyConstraintResult(this, val, arr.Count == 0),
					JsonObject obj => new JsonEmptyConstraintResult(this, val, obj.Count == 0),
					JsonString str => new JsonEmptyConstraintResult(this, val, str.Length == 0),
					_ => new JsonEmptyConstraintResult(this, val, false) // does not have the concept of length
				};
			}

			public override string Description => "<empty>";

			public class JsonEmptyConstraintResult : ConstraintResult
			{

				public JsonValue? ActualJsonValue { get; }

				public JsonEmptyConstraintResult(JsonEmptyConstraint constraint, JsonValue? actual, bool hasSucceeded)
					: base(constraint, actual, hasSucceeded)
				{
					this.ActualJsonValue = actual;
				}

				public override void WriteMessageTo(MessageWriter writer)
				{
					if (this.IsSuccess)
					{
						base.WriteMessageTo(writer);
						return;
					}

					if (this.ActualJsonValue is null)
					{
						writer.WriteMessageLine("Expected: <empty>");
						writer.Write("  But was:  ");
						writer.WriteActualValue(this.ActualValue);
					}
					else if (this.ActualJsonValue is JsonArray or JsonObject or JsonString)
					{
						writer.WriteMessageLine("Expected: <empty>");
						writer.Write("  But was:  ");
						writer.WriteActualValue(this.ActualJsonValue.ToJsonCompact());
					}
					else
					{
						writer.WriteMessageLine("Expected: <empty>");
						writer.Write("  But was:  ");
						writer.Write($"<{this.ActualJsonValue.Type}> ");
						writer.WriteLine(this.ActualJsonValue.ToJsonCompact());
					}
				}
			}

		}

		internal sealed class JsonReadOnlyConstraint : JsonConstraint
		{

			public override ConstraintResult ApplyTo<TActual>(TActual actual)
			{
				return ((object?) actual) switch
				{
					null => new JsonReadOnlyConstraintResult(this, JsonNull.Null, true),
					JsonValue val => new JsonReadOnlyConstraintResult(this, val, val.IsReadOnly),
					_ => throw new ArgumentException("The actual value must be a JSON value. The value passed was of type " + typeof(TActual).Name, nameof(actual)),
				};
			}

			public override string Description => "<read-only>";

			public class JsonReadOnlyConstraintResult : ConstraintResult
			{

				public JsonValue ActualJsonValue { get; }

				public JsonReadOnlyConstraintResult(JsonReadOnlyConstraint constraint, JsonValue actual, bool hasSucceeded)
					: base(constraint, actual, hasSucceeded)
				{
					this.ActualJsonValue = actual;
				}

				public override void WriteMessageTo(MessageWriter writer)
				{
					if (this.IsSuccess)
					{
						base.WriteMessageTo(writer);
						return;
					}

					writer.WriteMessageLine("Expected: <read-only>");
					writer.Write("  But was:  <mutable>");
					writer.WriteActualValue(this.ActualJsonValue.ToJsonCompact());
				}
			}

		}

		internal sealed class JsonNotConstraint : PrefixConstraint
		{

			public JsonNotConstraint(IConstraint baseConstraint)
				: base(baseConstraint, "not")
			{ }

			public override ConstraintResult ApplyTo<TActual>(TActual actual)
			{
				var constraintResult = this.BaseConstraint.ApplyTo(actual);
				return new ConstraintResult(this, constraintResult.ActualValue, !constraintResult.IsSuccess);
			}

		}

		internal sealed class JsonNotOperator : PrefixOperator
		{

			/// <summary>Constructs a new NotOperator</summary>
			public JsonNotOperator() => this.left_precedence = this.right_precedence = 1;

			/// <summary>Returns a NotConstraint applied to its argument.</summary>
			public override IConstraint ApplyPrefix(IConstraint constraint)
			{
				return new JsonNotConstraint(constraint);
			}

		}

		internal sealed class JsonAndConstraint : BinaryConstraint
		{

			public JsonAndConstraint(IConstraint left, IConstraint right) : base(left, right) { }

			public override string Description => this.Left.Description + " and " + this.Right.Description;

			public override ConstraintResult ApplyTo<TActual>(TActual actual)
			{
				var leftResult = this.Left.ApplyTo(actual);
				var rightResult = leftResult.IsSuccess ? this.Right.ApplyTo(actual) : new ConstraintResult(this.Right, actual);
				return new AndConstraintResult(this, actual, leftResult, rightResult);
			}

			private class AndConstraintResult : ConstraintResult
			{
				private readonly ConstraintResult LeftResult;
				private readonly ConstraintResult RightResult;

				public AndConstraintResult(JsonAndConstraint constraint, object? actual, ConstraintResult leftResult, ConstraintResult rightResult)
					: base(constraint, actual, leftResult.IsSuccess && rightResult.IsSuccess)
				{
					this.LeftResult = leftResult;
					this.RightResult = rightResult;
				}

				public override void WriteActualValueTo(MessageWriter writer)
				{
					if (this.IsSuccess)
					{
						base.WriteActualValueTo(writer);
					}
					else if (!this.LeftResult.IsSuccess)
					{
						this.LeftResult.WriteActualValueTo(writer);
					}
					else
					{
						this.RightResult.WriteActualValueTo(writer);
					}
				}

				public override void WriteAdditionalLinesTo(MessageWriter writer)
				{
					if (this.IsSuccess)
					{
						base.WriteAdditionalLinesTo(writer);
					}
					else if (!this.LeftResult.IsSuccess)
					{
						this.LeftResult.WriteAdditionalLinesTo(writer);
					}
					else
					{
						this.RightResult.WriteAdditionalLinesTo(writer);
					}
				}

			}

		}

		internal sealed class JsonAndOperator : BinaryOperator
		{
			public JsonAndOperator() => this.left_precedence = this.right_precedence = 2;

			public override IConstraint ApplyOperator(IConstraint left, IConstraint right)
			{
				return new JsonAndConstraint(left, right);
			}
		}

		internal sealed class JsonOrConstraint : BinaryConstraint
		{

			public JsonOrConstraint(IConstraint left, IConstraint right) : base(left, right) { }

			public override string Description => this.Left.Description + " or " + this.Right.Description;

			public override ConstraintResult ApplyTo<TActual>(TActual actual)
			{
				bool isSuccess = this.Left.ApplyTo(actual).IsSuccess || this.Right.ApplyTo(actual).IsSuccess;
				return new ConstraintResult(this, actual, isSuccess);
			}

		}

		internal sealed class JsonOrOperator : BinaryOperator
		{
			public JsonOrOperator() => this.left_precedence = this.right_precedence = 2;

			public override IConstraint ApplyOperator(IConstraint left, IConstraint right)
			{
				return new JsonOrConstraint(left, right);
			}
		}
		
		[PublicAPI]
		public sealed class JsonConstraintExpression : IResolveConstraint
		{

			private ResolvableConstraintExpression Expression { get; }

			public JsonConstraintExpression() => this.Expression = new ResolvableConstraintExpression();

			public JsonConstraintExpression(ConstraintBuilder builder) => this.Expression = new ResolvableConstraintExpression(builder);

			internal JsonConstraintExpression Append(ConstraintOperator op)
			{
				this.Expression.Append(op);
				return this;
			}

			internal JsonConstraintExpression Append(Constraint constraint)
			{
				this.Expression.Append(constraint);
				return this;
			}

			IConstraint IResolveConstraint.Resolve() => ((IResolveConstraint) this.Expression).Resolve();

			private JsonConstraintExpression AddTypeConstraint(JsonType expected) => this.Append(new JsonTypeConstraint(expected));

			private JsonConstraintExpression AddSameConstraint(JsonValue expected) => this.Append(new JsonEqualConstraint(JsonComparisonOperator.SameAs, expected));

			private JsonConstraintExpression AddEqualConstraint(JsonValue expected) => this.Append(new JsonEqualConstraint(JsonComparisonOperator.Equal, expected));

			private JsonConstraintExpression AddEqualConstraint(JsonComparisonOperator op, JsonValue expected) => this.Append(new JsonEqualConstraint(op, expected));

			public JsonConstraintExpression Not => this.Append(new JsonNotOperator());

			/// <summary>Combine two assertions that must both be true</summary>
			public JsonConstraintExpression And => this.Append(new JsonAndOperator());

			/// <summary>Combine two assertions with at least one that must be true</summary>
			public JsonConstraintExpression Or => this.Append(new JsonOrOperator());

			/// <summary>Assert that the value is null or missing</summary>
			/// <remarks>Includes all variants of <see cref="JsonNull"/></remarks>
			public JsonConstraintExpression Null => AddTypeConstraint(JsonType.Null);

			/// <summary>Assert that the value is a JSON Object</summary>
			public JsonConstraintExpression Object => AddTypeConstraint(JsonType.Object);

			/// <summary>Assert that the value is a JSON Array</summary>
			public JsonConstraintExpression Array => AddTypeConstraint(JsonType.Array);

			/// <summary>Assert that the value is a JSON String</summary>
			public JsonConstraintExpression String => AddTypeConstraint(JsonType.String);

			/// <summary>Assert that the value is a JSON Number</summary>
			public JsonConstraintExpression Number => AddTypeConstraint(JsonType.Number);

			/// <summary>Assert that the value is a JSON Boolean</summary>
			public JsonConstraintExpression Boolean => AddTypeConstraint(JsonType.Boolean);

			/// <summary>Assert that the value is an explicit JSON Null literal</summary>
			/// <remarks>This only accept the singleton <see cref="JsonNull.Null"/>. If you want to also include <see cref="JsonNull.Missing"/>, you should use the <see cref="Null"/> assertion.</remarks>
			public JsonConstraintExpression ExplicitNull => AddSameConstraint(JsonNull.Null);

			/// <summary>Assert that the value is missing</summary>
			/// <remarks>This only accept the singleton <see cref="JsonNull.Missing"/>. If you want to also include <see cref="JsonNull.Null"/>, you should use the <see cref="Null"/> assertion.</remarks>
			public JsonConstraintExpression Missing => AddSameConstraint(JsonNull.Missing);

			/// <summary>Assert that the value is out of bounds of its parent array.</summary>
			public JsonConstraintExpression Error => AddSameConstraint(JsonNull.Error);

			/// <summary>Assert that the value is empty</summary>
			/// <remarks>Supports Objects, Arrays and Strings.</remarks>
			public JsonConstraintExpression Empty => this.Append(new JsonEmptyConstraint());

			/// <summary>Assert that the value is read-only.</summary>
			public JsonConstraintExpression ReadOnly => this.Append(new JsonReadOnlyConstraint());

			/// <summary>Assert that the value is <see cref="JsonBoolean.False"/></summary>
			public JsonConstraintExpression False => AddEqualConstraint(JsonBoolean.False);

			/// <summary>Assert that the value is <see cref="JsonBoolean.True"/></summary>
			public JsonConstraintExpression True => AddEqualConstraint(JsonBoolean.True);

			/// <summary>Assert that the value is <see cref="JsonNumber.Zero"/></summary>
			public JsonConstraintExpression Zero => AddEqualConstraint(JsonNumber.Zero);

			#region JsonValue...

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(JsonValue expected) => AddEqualConstraint(expected);

			/// <summary>Assert that the value is a JSON Array with the expected content</summary>
			public JsonConstraintExpression EqualTo(IEnumerable<JsonValue> expected) => AddEqualConstraint(JsonArray.FromValues(expected));

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression GreaterThan(JsonValue expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThan, expected);

			/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
			public JsonConstraintExpression GreaterThanOrEqualTo(JsonValue expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, expected);

			/// <summary>Assert that the value is strictly less than the expected value</summary>
			public JsonConstraintExpression LessThan(JsonValue expected) => AddEqualConstraint(JsonComparisonOperator.LessThan, expected);

			/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
			public JsonConstraintExpression LessThanOrEqualTo(JsonValue expected) => AddEqualConstraint(JsonComparisonOperator.LessThanOrEqual, expected);

			#endregion

			#region Generic...

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo<TValue>(TValue expected) => AddEqualConstraint(JsonValue.FromValue(expected));

			/// <summary>Assert that the value is a JSON Array with the expected content</summary>
			public JsonConstraintExpression EqualTo<TValue>(TValue[] expected) => AddEqualConstraint(JsonArray.FromValues(expected));

			/// <summary>Assert that the value is a JSON Array with the expected content</summary>
			public JsonConstraintExpression EqualTo<TValue>(ReadOnlySpan<TValue> expected) => AddEqualConstraint(JsonArray.FromValues(expected));

			/// <summary>Assert that the value is a JSON Array with the expected content</summary>
			public JsonConstraintExpression EqualTo<TValue>(IEnumerable<TValue> expected) => AddEqualConstraint(JsonArray.FromValues(expected));

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression GreaterThan<TValue>(TValue expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThan, JsonValue.FromValue(expected));

			/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
			public JsonConstraintExpression GreaterThanOrEqualTo<TValue>(TValue expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonValue.FromValue(expected));

			/// <summary>Assert that the value is strictly less than the expected value</summary>
			public JsonConstraintExpression LessThan<TValue>(TValue expected) => AddEqualConstraint(JsonComparisonOperator.LessThan, JsonValue.FromValue(expected));

			/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
			public JsonConstraintExpression LessThanOrEqualTo<TValue>(TValue expected) => AddEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonValue.FromValue(expected));

			#endregion

			#region String...

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(string expected) => AddEqualConstraint(JsonString.Return(expected));

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression GreaterThan(string expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThan, JsonString.Return(expected));

			/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
			public JsonConstraintExpression GreaterThanOrEqualTo(string expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonString.Return(expected));

			/// <summary>Assert that the value is strictly less than the expected value</summary>
			public JsonConstraintExpression LessThan(string expected) => AddEqualConstraint(JsonComparisonOperator.LessThan, JsonString.Return(expected));

			/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
			public JsonConstraintExpression LessThanOrEqualTo(string expected) => AddEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonString.Return(expected));

			#endregion

			#region Boolean...

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(bool expected) => AddEqualConstraint(expected ? JsonBoolean.True : JsonBoolean.False);

			#endregion

			#region Int32...

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(int expected) => AddEqualConstraint(JsonNumber.Return(expected));

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression GreaterThan(int expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(expected));

			/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
			public JsonConstraintExpression GreaterThanOrEqualTo(int expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(expected));

			/// <summary>Assert that the value is strictly less than the expected value</summary>
			public JsonConstraintExpression LessThan(int expected) => AddEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(expected));

			/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
			public JsonConstraintExpression LessThanOrEqualTo(int expected) => AddEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(expected));

			#endregion

			#region Int64...

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(long expected) => AddEqualConstraint(JsonNumber.Return(expected));

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression GreaterThan(long expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(expected));

			/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
			public JsonConstraintExpression GreaterThanOrEqualTo(long expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(expected));

			/// <summary>Assert that the value is strictly less than the expected value</summary>
			public JsonConstraintExpression LessThan(long expected) => AddEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(expected));

			/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
			public JsonConstraintExpression LessThanOrEqualTo(long expected) => AddEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(expected));

			#endregion

			#region Single...

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(float expected) => AddEqualConstraint(JsonNumber.Return(expected));

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression GreaterThan(float expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(expected));

			/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
			public JsonConstraintExpression GreaterThanOrEqualTo(float expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(expected));

			/// <summary>Assert that the value is strictly less than the expected value</summary>
			public JsonConstraintExpression LessThan(float expected) => AddEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(expected));

			/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
			public JsonConstraintExpression LessThanOrEqualTo(float expected) => AddEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(expected));

			#endregion

			#region Double...

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(double expected) => AddEqualConstraint(JsonNumber.Return(expected));

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression GreaterThan(double expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(expected));

			/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
			public JsonConstraintExpression GreaterThanOrEqualTo(double expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(expected));

			/// <summary>Assert that the value is strictly less than the expected value</summary>
			public JsonConstraintExpression LessThan(double expected) => AddEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(expected));

			/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
			public JsonConstraintExpression LessThanOrEqualTo(double expected) => AddEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(expected));

			#endregion

		}

	}

}
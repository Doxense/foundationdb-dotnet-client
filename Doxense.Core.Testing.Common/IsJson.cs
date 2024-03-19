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

namespace Snowbank.Testing
{
	using System;
	using System.Collections.Generic;
	using Doxense.Serialization.Json;
	using NUnit.Framework.Constraints;

	/// <summary>JSON Assertions</summary>
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

		/// <summary>Assert that the value is a JSON String literal</summary>
		public static JsonConstraint String => new JsonTypeConstraint(JsonType.String);

		/// <summary>Assert that the value is a JSON Number literal</summary>
		public static JsonConstraint Number => new JsonTypeConstraint(JsonType.Number);

		/// <summary>Assert that the value is a JSON Boolean literal</summary>
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
		public static JsonEmptyConstraint Empty => new();

		/// <summary>Assert that the value is equal to the specified value</summary>
		public static JsonConstraint EqualTo(bool value) => new JsonEqualConstraint(JsonComparisonOperator.Equal, value ? JsonBoolean.True : JsonBoolean.False);

		#region JsonValue...
		
		/// <summary>Assert that the value is a JSON Value equal to the specified value</summary>
		public static JsonConstraint EqualTo(JsonValue value) => new JsonEqualConstraint(JsonComparisonOperator.Equal, value);

		/// <summary>Assert that the value is strictly greater than the specified value</summary>
		public static JsonConstraint GreaterThan(JsonValue value) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, value);

		/// <summary>Assert that the value is greater than, or equal to, the specified value</summary>
		public static JsonConstraint GreaterThanOrEqualTo(JsonValue value) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, value);

		/// <summary>Assert that the value is strictly less than the specified value</summary>
		public static JsonConstraint LessThan(JsonValue value) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, value);

		/// <summary>Assert that the value is less than, or equal to, the specified value</summary>
		public static JsonConstraint LessThanOrEqualTo(JsonValue value) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, value);

		/// <summary>Assert that the value is a JSON Array with the specified content</summary>
		public static JsonConstraint EqualTo(IEnumerable<JsonValue> values) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(values));

		#endregion

		#region Generic...
		
		/// <summary>Assert that the value is equal to the specified value</summary>
		public static JsonConstraint EqualTo<TValue>(TValue value) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonValue.FromValue(value));

		/// <summary>Assert that the value is strictly greater than the specified value</summary>
		public static JsonConstraint GreaterThan<TValue>(TValue value) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, JsonValue.FromValue(value));

		/// <summary>Assert that the value is greater than, or equal to, the specified value</summary>
		public static JsonConstraint GreaterThanOrEqualTo<TValue>(TValue value) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonValue.FromValue(value));

		/// <summary>Assert that the value is strictly less than the specified value</summary>
		public static JsonConstraint LessThan<TValue>(TValue value) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, JsonValue.FromValue(value));

		/// <summary>Assert that the value is less than, or equal to, the specified value</summary>
		public static JsonConstraint LessThanOrEqualTo<TValue>(TValue value) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonValue.FromValue(value));

		/// <summary>Assert that the value is a JSON Array with the specified content</summary>
		public static JsonConstraint EqualTo<TValue>(TValue[] values) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(values));

		/// <summary>Assert that the value is a JSON Array with the specified content</summary>
		public static JsonConstraint EqualTo<TValue>(ReadOnlySpan<TValue> values) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(values));

		/// <summary>Assert that the value is a JSON Array with the specified content</summary>
		public static JsonConstraint EqualTo<TValue>(IEnumerable<TValue> values) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(values));

		#endregion

		#region Int32...
		
		/// <summary>Assert that the value is equal to the specified value</summary>
		public static JsonConstraint EqualTo(int value) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonNumber.Return(value));

		/// <summary>Assert that the value is strictly greater than the specified value</summary>
		public static JsonConstraint GreaterThan(int value) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(value));

		/// <summary>Assert that the value is greater than, or equal to, the specified value</summary>
		public static JsonConstraint GreaterThanOrEqualTo(int value) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(value));

		/// <summary>Assert that the value is strictly less than the specified value</summary>
		public static JsonConstraint LessThan(int value) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(value));

		/// <summary>Assert that the value is less than, or equal to, the specified value</summary>
		public static JsonConstraint LessThanOrEqualTo(int value) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(value));

		#endregion

		#region Int64...
		
		/// <summary>Assert that the value is equal to the specified value</summary>
		public static JsonConstraint EqualTo(long value) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonNumber.Return(value));

		/// <summary>Assert that the value is strictly greater than the specified value</summary>
		public static JsonConstraint GreaterThan(long value) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(value));

		/// <summary>Assert that the value is greater than, or equal to, the specified value</summary>
		public static JsonConstraint GreaterThanOrEqualTo(long value) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(value));

		/// <summary>Assert that the value is strictly less than the specified value</summary>
		public static JsonConstraint LessThan(long value) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(value));

		/// <summary>Assert that the value is less than, or equal to, the specified value</summary>
		public static JsonConstraint LessThanOrEqualTo(long value) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(value));

		#endregion

		#region Float...
		
		/// <summary>Assert that the value is equal to the specified value</summary>
		public static JsonConstraint EqualTo(float value) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonNumber.Return(value));

		/// <summary>Assert that the value is strictly greater than the specified value</summary>
		public static JsonConstraint GreaterThan(float value) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(value));

		/// <summary>Assert that the value is greater than, or equal to, the specified value</summary>
		public static JsonConstraint GreaterThanOrEqualTo(float value) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(value));

		/// <summary>Assert that the value is strictly less than the specified value</summary>
		public static JsonConstraint LessThan(float value) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(value));

		/// <summary>Assert that the value is less than, or equal to, the specified value</summary>
		public static JsonConstraint LessThanOrEqualTo(float value) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(value));

		#endregion

		#region Double...
		
		/// <summary>Assert that the value is equal to the specified value</summary>
		public static JsonConstraint EqualTo(double value) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonNumber.Return(value));

		/// <summary>Assert that the value is strictly greater than the specified value</summary>
		public static JsonConstraint GreaterThan(double value) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(value));

		/// <summary>Assert that the value is greater than, or equal to, the specified value</summary>
		public static JsonConstraint GreaterThanOrEqualTo(double value) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(value));

		/// <summary>Assert that the value is strictly less than the specified value</summary>
		public static JsonConstraint LessThan(double value) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(value));

		/// <summary>Assert that the value is less than, or equal to, the specified value</summary>
		public static JsonConstraint LessThanOrEqualTo(double value) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(value));

		#endregion

		#region Strings...

		/// <summary>Assert that the value is equal to the specified value</summary>
		public static JsonConstraint EqualTo(string? value) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonString.Return(value));

		/// <summary>Assert that the value is strictly greater than the specified value</summary>
		public static JsonConstraint GreaterThan(string? value) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, JsonString.Return(value));

		/// <summary>Assert that the value is greater than, or equal to, the specified value</summary>
		public static JsonConstraint GreaterThanOrEqualTo(string? value) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonString.Return(value));

		/// <summary>Assert that the value is strictly less than the specified value</summary>
		public static JsonConstraint LessThan(string? value) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, JsonString.Return(value));

		/// <summary>Assert that the value is less than, or equal to, the specified value</summary>
		public static JsonConstraint LessThanOrEqualTo(string? value) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonString.Return(value));

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

			/// <summary>
			/// Returns a ConstraintExpression by appending And
			/// to the current constraint.
			/// </summary>
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

		}

		internal class JsonEqualConstraint : JsonConstraint
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
							WriteDifferences(writer, "=== ", "JSON value should be the same instance as the specified value");
							break;
						}
						case JsonComparisonOperator.GreaterThan:
						{
							WriteDifferences(writer, ">", "JSON value should not match the specified value");
							break;
						}
						case JsonComparisonOperator.GreaterThanOrEqual:
						{
							WriteDifferences(writer, ">=", "JSON value should not match the specified value");
							break;
						}
						case JsonComparisonOperator.LessThan:
						{
							WriteDifferences(writer, "<", "JSON value should not match the specified value");
							break;
						}
						case JsonComparisonOperator.LessThanOrEqual:
						{
							WriteDifferences(writer, "<=", "JSON value should not match the specified value");
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

		internal class JsonTypeConstraint : JsonConstraint
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

		public class JsonEmptyConstraint : JsonConstraint
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

		internal class JsonNotConstraint : PrefixConstraint
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

		internal class JsonNotOperator : PrefixOperator
		{

			/// <summary>Constructs a new NotOperator</summary>
			public JsonNotOperator() => this.left_precedence = this.right_precedence = 1;

			/// <summary>Returns a NotConstraint applied to its argument.</summary>
			public override IConstraint ApplyPrefix(IConstraint constraint)
			{
				return new JsonNotConstraint(constraint);
			}

		}

		internal class JsonAndConstraint : BinaryConstraint
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

		internal class JsonAndOperator : BinaryOperator
		{
			public JsonAndOperator() => this.left_precedence = this.right_precedence = 2;

			public override IConstraint ApplyOperator(IConstraint left, IConstraint right)
			{
				return new JsonAndConstraint(left, right);
			}
		}

		public class JsonConstraintExpression : IResolveConstraint
		{

			private ResolvableConstraintExpression Expression { get; }

			public JsonConstraintExpression()
			{
				this.Expression = new ResolvableConstraintExpression();
			}

			public JsonConstraintExpression(ConstraintBuilder builder)
			{
				this.Expression = new ResolvableConstraintExpression(builder);
			}


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

			public IConstraint Resolve()
			{
				return ((IResolveConstraint) this.Expression).Resolve();
			}

			public JsonConstraintExpression Not => this.Append(new JsonNotOperator());

			private JsonConstraintExpression AddTypeConstraint(JsonType expected) => this.Append(new JsonTypeConstraint(expected));

			public JsonConstraintExpression Null => AddTypeConstraint(JsonType.Null);

			public JsonConstraintExpression Object => AddTypeConstraint(JsonType.Object);

			public JsonConstraintExpression Array => AddTypeConstraint(JsonType.Array);

			public JsonConstraintExpression String => AddTypeConstraint(JsonType.String);

			public JsonConstraintExpression Number => AddTypeConstraint(JsonType.Number);

			public JsonConstraintExpression Boolean => AddTypeConstraint(JsonType.Boolean);

			public JsonConstraintExpression Empty => this.Append(new JsonEmptyConstraint());

			private JsonConstraintExpression AddSameConstraint(JsonValue expected) => this.Append(new JsonEqualConstraint(JsonComparisonOperator.SameAs, expected));

			public JsonConstraintExpression ExplicitNull => AddSameConstraint(JsonNull.Null);

			public JsonConstraintExpression Missing => AddSameConstraint(JsonNull.Missing);

			public JsonConstraintExpression Error => AddSameConstraint(JsonNull.Error);

			private JsonConstraintExpression AddEqualConstraint(JsonValue expected) => this.Append(new JsonEqualConstraint(JsonComparisonOperator.Equal, expected));

			public JsonConstraintExpression False => AddEqualConstraint(JsonBoolean.False);

			public JsonConstraintExpression True => AddEqualConstraint(JsonBoolean.True);

			public JsonConstraintExpression Zero => AddEqualConstraint(JsonNumber.Zero);

			public JsonConstraintExpression EqualTo(JsonValue expected) => AddEqualConstraint(expected);

			public JsonConstraintExpression EqualTo<TValue>(TValue expected) => AddEqualConstraint(JsonValue.FromValue(expected));

			public JsonConstraintExpression EqualTo(int expected) => AddEqualConstraint(JsonNumber.Return(expected));

			public JsonConstraintExpression EqualTo(long expected) => AddEqualConstraint(JsonNumber.Return(expected));

			public JsonConstraintExpression EqualTo(float expected) => AddEqualConstraint(JsonNumber.Return(expected));

			public JsonConstraintExpression EqualTo(double expected) => AddEqualConstraint(JsonNumber.Return(expected));

		}

	}

}

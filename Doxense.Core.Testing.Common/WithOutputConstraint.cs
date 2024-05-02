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

namespace Doxense
{
	using NUnit.Framework.Constraints;

	internal class WithOutputConstraint : Constraint
	{

		public object? Actual { get; }

		public string Literal { get; }

		public IConstraint Left { get; }

		public IConstraint Right { get; }

		public WithOutputConstraint(object? actual, string literal, IConstraint left, IConstraint right)
		{
			this.Actual = actual;
			this.Literal = literal;
			this.Left = left;
			this.Right = right;
		}

		public override string Description => $"{this.Literal} = {this.Actual}";

		public override ConstraintResult ApplyTo<TActual>(TActual actual)
		{
			var leftResult = this.Left.ApplyTo(actual);
			if (!leftResult.IsSuccess)
			{
				return leftResult;
			}

			var rightResult = this.Right.ApplyTo(this.Actual);
			if (!rightResult.IsSuccess)
			{
				return rightResult;
			}

			return new WithOutputConstraintResult(this, actual, this.Actual, leftResult, rightResult);
		}

	}

	internal class WithOutputConstraintResult : ConstraintResult
	{
		private ConstraintResult LeftResult { get; }
		private ConstraintResult RightResult { get; }


		public WithOutputConstraintResult(
			WithOutputConstraint constraint,
			object? leftActual,
			object? rightActual,
			ConstraintResult leftResult,
			ConstraintResult rightResult)
			: base((IConstraint) constraint, leftResult.IsSuccess ? rightActual : leftActual, leftResult.IsSuccess && rightResult.IsSuccess)
		{
			this.LeftResult = leftResult;
			this.RightResult = rightResult;
		}

		/// <summary>
		/// Write the actual value for a failing constraint test to a
		/// MessageWriter. The default implementation simply writes
		/// the raw value of actual, leaving it to the writer to
		/// perform any formatting.
		/// </summary>
		/// <param name="writer">The writer on which the actual value is displayed</param>
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

	internal class WithOutputOperator : BinaryOperator
	{

		public object? Actual { get; }

		public string Literal { get; }

		public WithOutputOperator(object? actual, string literal)
		{
			this.Actual = actual;
			this.Literal = literal;
		}

		public override IConstraint ApplyOperator(IConstraint left, IConstraint right)
		{
			return new WithOutputConstraint(this.Actual, this.Literal, left, right);
		}
	}

}

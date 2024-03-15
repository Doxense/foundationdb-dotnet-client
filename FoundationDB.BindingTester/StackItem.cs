#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace FoundationDB.Client.Testing
{
	using System;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;

	[DebuggerDisplay("{ToString(),nq}")]
	public sealed record StackItem
	{

		public StackItem(TestInstruction instr, IVarTuple value, int index)
		{
			this.Result = new () { Index = index, Value = value };
			this.Index = index;
			this.Instruction = instr;
		}

		public StackItem(TestInstruction instr, Task<IVarTuple> future, int index)
		{
			this.Future = new ValueTask<IVarTuple>(future);
			this.Index = index;
			this.Instruction = instr;
		}

		public StackItem(TestInstruction instr, ValueTask<IVarTuple> future, int index)
		{
			this.Future = future;
			this.Index = index;
			this.Instruction = instr;
		}

		public StackItem(TestInstruction instr, Exception error, int index)
		{
			this.Result = new() { Index = index, Value = MapError(error), Error = error, };
			this.Index = index;
			this.Instruction = instr;
		}

		internal static IVarTuple MapError(Exception e) => e switch
		{
			FdbException fdbEx => STuple.Create("ERROR", fdbEx.Code.ToString()), //BUGBUG: text version of error code, which case and format???
			_ => STuple.Create("ERROR", "Internal Error", e.Message)
		};

		public ValueTask<IVarTuple> Future { get; private set; }

		public StackResult? Result { get; private set; }

		public int Index { get; init; }

		public TestInstruction Instruction { get; init; }

		public bool IsCompleted => this.Result != null;

		public ValueTask<StackResult> Resolve()
		{
			if (this.Result != null)
			{
				return ValueTask.FromResult(this.Result);
			}

			return AwaitResult();

			async ValueTask<StackResult> AwaitResult()
			{
				IVarTuple tuple;
				Exception? error = null;
				try
				{
					tuple = await this.Future;
				}
				catch (Exception e)
				{
					error = e;
					tuple = MapError(e);
				}

				var res = new StackResult() { Value = tuple, Index = this.Index, Error = error, };
				this.Result = res;
				this.Future = default;
				return res;
			}
		}

		public bool TryResolve([MaybeNullWhen(false)] out StackResult res)
		{
			if (this.Result != null)
			{
				res = this.Result;
				return true;
			}

			if (this.Future.IsCompleted)
			{
				res = GetResult();
				return true;
			}

			res = default;
			return false;

			StackResult GetResult()
			{
				IVarTuple tuple;
				Exception? error = null;
				try
				{
					tuple = this.Future.GetAwaiter().GetResult();
				}
				catch (FdbException e)
				{
					error = e;
					tuple = STuple.Create("ERROR", e.Code.ToString()); //BUGBUG: text version of error code, which case and format???
				}
				catch (Exception e)
				{
					error = e;
					tuple = STuple.Create("ERROR", "Internal Error", e.Message);
				}

				var res = new StackResult() { Value = tuple, Index = this.Index, Error = error, };
				this.Result = res;
				this.Future = default;
				return res;
			}
		}

		public override string ToString() =>
			TryResolve(out var res)
				? $"{res.Index}: {res.Value} # {this.Instruction}"
				: $"{this.Index}: PENDING # {this.Instruction}";

	}

	public sealed record StackResult
	{
		public required IVarTuple Value { get; init; }

		public required int Index { get; init; }

		public Exception? Error { get; init; }

		public T? As<T>() => this.Value.Get<T>(0);

		public object? AsObject() => this.Value[0];

	}

}

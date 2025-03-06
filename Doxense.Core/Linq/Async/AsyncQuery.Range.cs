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

namespace SnowBank.Linq
{
	using System.Collections.Immutable;
	using System.Numerics;
	using SnowBank.Linq.Async.Iterators;

	public static partial class AsyncQuery
	{

		/// <summary>Generates a sequence of integral numbers within a specified range.</summary>
		/// <param name="start">The value of the first integer in the sequence.</param>
		/// <param name="count">The number of sequential integers to generate.</param>
		/// <param name="ct">Token used to cancel the execution of this query</param>
		/// <returns>An <see cref="IAsyncEnumerable{T}"/> that contains a range of sequential integral numbers.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 0</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> + <paramref name="count"/> -1 is larger than <see cref="int.MaxValue"/>.</exception>
		public static IAsyncLinqQuery<int> Range(int start, int count, CancellationToken ct = default)
		{
			if (count < 0 || (((long)start) + count - 1) > int.MaxValue)
			{
				ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
			}

			return new RangeIterator<int>(start, 1, count, ct);
		}

		/// <summary>Generates a sequence of integral numbers within a specified range.</summary>
		/// <param name="start">The value of the first element returned by the query.</param>
		/// <param name="delta">The value that is added to each value return by the query.</param>
		/// <param name="count">The number of elements returned by the query.</param>
		/// <param name="ct">Token used to cancel the execution of this query</param>
		/// <returns>An <see cref="IAsyncQuery{T}"/> that contains a range of sequential numbers.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 0</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> + <paramref name="count"/> -1 is larger than <see cref="int.MaxValue"/>.</exception>
		public static IAsyncLinqQuery<TNumber> Range<TNumber>(TNumber start, TNumber delta, int count, CancellationToken ct = default)
			where TNumber : INumberBase<TNumber>
		{
			if (count < 0)
			{
				ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
			}
			return new RangeIterator<TNumber>(start, delta, count, ct);
		}

		internal sealed class RangeIterator<TNumber> : AsyncLinqIterator<TNumber>
			where TNumber : INumberBase<TNumber>
		{

			public RangeIterator(TNumber start, TNumber delta, int count, CancellationToken ct)
			{
				this.Start = start;
				this.Delta = delta;
				this.Count = count;
				this.Cancellation = ct;
			}

			public TNumber Start { get; }

			public TNumber Delta { get; }

			public int Count { get; }

			private TNumber Cursor { get; set; } = TNumber.Zero;

			private int Remaining { get; set; }

			/// <inheritdoc />
			public override CancellationToken Cancellation { get; }

			/// <inheritdoc />
			protected override RangeIterator<TNumber> Clone() => new(this.Start, this.Delta, this.Count, this.Cancellation);

			/// <inheritdoc />
			protected override ValueTask<bool> OnFirstAsync()
			{
				this.Cursor = this.Start;
				this.Remaining = this.Count;
				return new(true);
			}

			/// <inheritdoc />
			protected override ValueTask<bool> OnNextAsync()
			{
				var remaining = this.Remaining;
				if (remaining <= 0)
				{
					return this.Completed();
				}

				var cursor = this.Cursor;
				this.Cursor = cursor + this.Delta;
				this.Remaining = remaining - 1;
				return new(this.Publish(cursor));
			}

			/// <inheritdoc />
			protected override ValueTask Cleanup()
			{
				this.Cursor = TNumber.Zero;
				this.Remaining = 0;
				return default;
			}

			/// <inheritdoc />
			public override Task<List<TNumber>> ToListAsync()
			{
				int count = this.Count;
				var res = new List<TNumber>(count);
				var cursor = this.Start;
				var delta = this.Delta;
				for(int i = 0; i < count; i++)
				{
					res.Add(cursor);
					cursor += delta;
				}
				return Task.FromResult(res);
			}

			/// <inheritdoc />
			public override Task<TNumber[]> ToArrayAsync()
			{
				int count = this.Count;
				if (count == 0) return Task.FromResult(Array.Empty<TNumber>());

				var res = new TNumber[count];
				var cursor = this.Start;
				var delta = this.Delta;
				for(int i = 0; i < res.Length; i++)
				{
					res[i] = cursor;
					cursor += delta;
				}
				return Task.FromResult(res);
			}

			/// <inheritdoc />
			public override Task<ImmutableArray<TNumber>> ToImmutableArrayAsync()
			{
				int count = this.Count;
				if (count == 0) return Task.FromResult(ImmutableArray<TNumber>.Empty);

				var builder = ImmutableArray.CreateBuilder<TNumber>(count);
				var cursor = this.Start;
				var delta = this.Delta;
				for(int i = 0; i < count; i++)
				{
					builder.Add(cursor);
					cursor += delta;
				}
				return Task.FromResult(builder.ToImmutable());
			}

			/// <inheritdoc />
			public override Task<int> CountAsync() => Task.FromResult(this.Count);

			/// <inheritdoc />
			public override Task<bool> AnyAsync() => Task.FromResult(this.Count > 0);

		}

	}

}

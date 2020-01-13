#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Linq.Async.Iterators
{
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Iterator that will generate the underlying async sequence "just in time" when it is itself iterated</summary>
	/// <typeparam name="TResult">Type of elements of the async sequence</typeparam>
	/// <typeparam name="TCollection">Concrete type of the async sequence</typeparam>
	public class DeferredAsyncIterator<TResult, TCollection> : AsyncIterator<TResult>
		where TCollection : IAsyncEnumerable<TResult>
	{

		public Func<CancellationToken, Task<TCollection>> Generator { get; }

		private IAsyncEnumerator<TResult> Inner { get; set; }

		public DeferredAsyncIterator([NotNull] Func<CancellationToken, Task<TCollection>> generator)
		{
			Contract.NotNull(generator, nameof(generator));
			this.Generator = generator;
		}

		protected override ValueTask Cleanup()
		{
			var inner = this.Inner;
			this.Inner = null;
			return inner?.DisposeAsync() ?? default;
		}

		protected override AsyncIterator<TResult> Clone()
		{
			return new DeferredAsyncIterator<TResult, TCollection>(this.Generator);
		}

		protected override async ValueTask<bool> OnFirstAsync()
		{
			var sequence = await this.Generator(m_ct);
			if (sequence == null) throw new InvalidOperationException("Deferred generator cannot return a null async sequence.");

			this.Inner = sequence.GetAsyncEnumerator(m_ct);
			Contract.Assert(this.Inner != null);

			return true;
		}

		protected override async ValueTask<bool> OnNextAsync()
		{
			var inner = this.Inner;
			if (inner == null) throw ThrowHelper.ObjectDisposedException(this);

			if (!(await inner.MoveNextAsync()))
			{
				return await Completed();
			}
			return Publish(this.Inner.Current);
		}

	}

}

#endif
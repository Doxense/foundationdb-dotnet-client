#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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

namespace FoundationDB.Linq
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Async;

	/// <summary>Generate items asynchronously, using a user-provided lambda</summary>
	/// <typeparam name="TOutput">Type of the items produced by this generator</typeparam>
	internal class FdbAnonymousAsyncGenerator<TOutput> : FdbAsyncIterator<TOutput>
	{
		// use a custom lambda that returns Maybe<TOutput> results, asynchronously
		// => as long as the result has a value, continue iterating
		// => if the result does not have a value, stop iterating
		// => if the lambda fails, or the result represents an error, throw the error down the chain and stop iterating

		// ITERABLE

		private readonly Func<long, CancellationToken, Task<Maybe<TOutput>>> m_generator;

		// ITERATOR

		private long m_index;

		public FdbAnonymousAsyncGenerator(Func<long, CancellationToken, Task<Maybe<TOutput>>> generator)
		{
			Contract.Requires(generator != null);
			m_generator = generator;
			m_index = -1;
		}

		protected override FdbAsyncIterator<TOutput> Clone()
		{
			return new FdbAnonymousAsyncGenerator<TOutput>(m_generator);
		}

		protected override Task<bool> OnFirstAsync(CancellationToken ct)
		{
			m_index = 0;
			return TaskHelpers.TrueTask;
		}

		protected override async Task<bool> OnNextAsync(CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();
			if (m_index < 0) return false;

			long index = m_index;
			var res = await m_generator(index, ct);

			if (res.HasFailed) res.ThrowForNonSuccess();
			if (res.IsEmpty) return Completed();
			m_index = checked(index + 1);
			return Publish(res.Value);
		}

		protected override void Cleanup()
		{
			m_index = -1;
		}
	}
}

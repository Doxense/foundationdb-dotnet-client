#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Async
{
	using JetBrains.Annotations;
	using System;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;

	/// <summary>Pump that takes items from a source, transform them, and outputs them</summary>
	public sealed class AsyncTransform<TInput, TOutput> : IAsyncTarget<TInput>, IDisposable
	{
		private readonly IAsyncTarget<Task<TOutput>> m_target;
		private readonly Func<TInput, CancellationToken, Task<TOutput>> m_transform;
		private readonly TaskScheduler? m_scheduler;
		private bool m_done;

		public AsyncTransform(Func<TInput, CancellationToken, Task<TOutput>> transform, IAsyncTarget<Task<TOutput>> target, TaskScheduler? scheduler = null)
		{
			Contract.NotNull(transform);
			Contract.NotNull(target);

			m_transform = transform;
			m_target = target;
			m_scheduler = scheduler;
		}

		/// <summary>Target of the transform</summary>
		public IAsyncTarget<Task<TOutput>> Target => m_target;

		/// <summary>Optional scheduler used to run the tasks</summary>
		public TaskScheduler? Scheduler => m_scheduler;

		#region IAsyncTarget<T>...

		public Task OnNextAsync(TInput value, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.CompletedTask;

			if (m_done) throw new InvalidOperationException("Cannot send any more values because this transform has already completed");

			try
			{

				// we start the task here, but do NOT wait for its completion!
				// It is the job of the target to handle that (and ordering)
				Task<TOutput> task;
				if (m_scheduler == null)
				{ // execute inline
					task = m_transform(value, ct);
				}
				else
				{ // execute in a scheduler
					task = Task.Factory.StartNew(
						(state) =>
						{
							var prms = (Tuple<AsyncTransform<TInput, TOutput>, TInput, CancellationToken>)state;
							return prms.Item1.m_transform(prms.Item2, prms.Item3);
						},
						Tuple.Create(this, value, ct),
						ct,
						TaskCreationOptions.PreferFairness,
						m_scheduler
					).Unwrap();
				}

				return m_target.OnNextAsync(task, ct);
			}
			catch(Exception e)
			{
				m_target.OnError(ExceptionDispatchInfo.Capture(e));
				return Task.FromException<object>(e);
			}
		}

		public void OnCompleted()
		{
			Dispose();
		}

		public void OnError(ExceptionDispatchInfo e)
		{
			if (!m_done)
			{
				m_target.OnError(e);
			}
		}

		#endregion

		#region IDisposable...

		public void Dispose()
		{
			if (!m_done)
			{
				m_done = true;
				m_target.OnCompleted();
			}
		}

		#endregion

	}

}

#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

//#define FULL_DEBUG

namespace Doxense.Async
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Buffer that holds a fixed number of Tasks, output them in arrival or completion order, and can rate-limit the producer</summary>
	/// <typeparam name="T"></typeparam>
	public class AsyncTaskBuffer<T> : AsyncProducerConsumerQueue<Task<T>>, IAsyncSource<T>
	{

		#region Private Members...

		/// <summary>How should we output results ? In arrival order or in completion order ? </summary>
		private readonly AsyncOrderingMode m_mode;

		/// <summary>Queue that holds items produced but not yet consumed</summary>
		/// <remarks>The queue can sometime go over the limit because the Complete/Error message are added without locking</remarks>
		protected LinkedList<Task<T>?> m_queue = new LinkedList<Task<T>?>();

		/// <summary>Only used in mode CompletionOrder</summary>
		private AsyncCancelableMutex m_completionLock = AsyncCancelableMutex.AlreadyDone;

		#endregion

		#region Constructors...

		public AsyncTaskBuffer(AsyncOrderingMode mode, int capacity)
			: base(capacity)
		{
			if (mode != AsyncOrderingMode.ArrivalOrder && mode != AsyncOrderingMode.CompletionOrder) throw new ArgumentOutOfRangeException(nameof(mode), "Unsupported ordering mode");

			m_mode = mode;
		}

		#endregion

		#region IAsyncTarget<T>...

		public override Task OnNextAsync(Task<T> task, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			LogProducer("Received task #" + task.Id + " (" + task.Status + ")");

			Task wait;
			lock (m_lock)
			{
				if (m_done) return Task.FromException<object>(new InvalidOperationException("Cannot send any more values because this buffer has already completed"));

				if (m_queue.Count < m_capacity)
				{ // quick path

					Enqueue_NeedsLocking(task);

					if (m_mode == AsyncOrderingMode.CompletionOrder)
					{ // we need to observe task completion to wake up the consumer as soon as one is ready !

						if (task.IsCompleted)
						{ // we can do it right now
							NotifyConsumerOfTaskCompletion_NeedsLocking();
						}
						else
						{
							ObserveTaskCompletion(task);
						}
					}

					return Task.CompletedTask;
				}

				// we are blocked, we will need to wait !
				wait = MarkProducerAsBlocked_NeedsLocking(ct);
			}

			// slow path
			return WaitForNextFreeSlotThenEnqueueAsync(task, wait, ct);
		}

		private void NotifyConsumerOfTaskCompletion_NeedsLocking()
		{
			Contract.Debug.Requires(m_mode == AsyncOrderingMode.CompletionOrder);

			if (!m_receivedLast && m_completionLock.Set(async: true))
			{
				LogProducer("Woke up blocked consumer because one task completed");
			}
		}

		/// <summary>Observe the completion of a task to wake up the consumer</summary>
		private void ObserveTaskCompletion(Task<T> task)
		{
			task.ContinueWith(
				(t, state) =>
				{
					LogProducer("Task #" + t.Id + " " + t.Status);
					var self = (AsyncTaskBuffer<T>)state;
					lock (self.m_lock)
					{
						self.NotifyConsumerOfTaskCompletion_NeedsLocking();
					}
				},
				this,
				TaskContinuationOptions.ExecuteSynchronously
			);
		}

		public override void OnCompleted()
		{
			lock (m_lock)
			{
				if (!m_done)
				{
					LogProducer("Completion received");
					m_done = true;
					m_queue.AddLast(new LinkedListNode<Task<T>?>(null));
					WakeUpBlockedConsumer_NeedsLocking();
					if (m_mode == AsyncOrderingMode.CompletionOrder) NotifyConsumerOfTaskCompletion_NeedsLocking();
				}
			}
		}

		public override void OnError(ExceptionDispatchInfo error)
		{
			lock (m_lock)
			{
				if (!m_done)
				{
					LogProducer("Error received: " + error.SourceException.Message);
					m_queue.AddLast(new LinkedListNode<Task<T>?>(Task.FromException<T>(error.SourceException)));
					WakeUpBlockedConsumer_NeedsLocking();
					if (m_mode == AsyncOrderingMode.CompletionOrder) NotifyConsumerOfTaskCompletion_NeedsLocking();
				}
			}
		}

		private void Enqueue_NeedsLocking(Task<T> task)
		{
			m_queue.AddLast(new LinkedListNode<Task<T>?>(task));
			WakeUpBlockedConsumer_NeedsLocking();
		}

		private async Task WaitForNextFreeSlotThenEnqueueAsync(Task<T> task, Task wait, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			await wait.ConfigureAwait(false);

			LogProducer("Woke up because one slot got freed");

			lock (m_lock)
			{
				Contract.Debug.Assert(m_queue.Count < m_capacity);
				Enqueue_NeedsLocking(task);

				if (m_mode == AsyncOrderingMode.CompletionOrder)
				{ // we need to observe task completion to wake up the consumer as soon as one is ready !
					LogConsumer("Task still pending after wait, and must be observed");
					ObserveTaskCompletion(task);
				}

			}
		}

		#endregion

		#region IAsyncSource<R>...

		public Task<Maybe<T>> ReceiveAsync(CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<Maybe<T>>(ct);

			LogConsumer("Looking for next value...");

			// in arrival order mode, we only look at the first task in the queue
			// in completion order mode, we look at the first completed task in the queue
			// we have to take special care of the "done" message that needs to be returned last !

			Task wait;
			lock (m_lock)
			{
				if (m_receivedLast)
				{
					// this is an error !
					throw new InvalidOperationException("Last item has already been received");
				}

				var queue = m_queue;
				var current = queue.First;
				if (current != null)
				{
					if (m_mode == AsyncOrderingMode.ArrivalOrder)
					{
						if (current.Value == null || current.Value.IsCompleted)
						{ // it's ready

							queue.RemoveFirst();
							if (current.Value != null) LogConsumer("First task #" + current.Value.Id + " was already " + current.Value.Status);
							return CompleteTask(current.Value);
						}

						// it is not yet completed!
						return WaitForTaskToCompleteAsync(current.Value, ct);
					}
					else
					{
						// note: if one is already completed, it will be return immediately !
						while (current != null)
						{
							var t = current.Value;
							if (t != null && t.IsCompleted)
							{
								queue.Remove(current);
								LogConsumer("Found task #" + t.Id + " that was already " + t.Status);
								return CompleteTask(t);
							}
							current = current.Next;
						}

						// in case of completion, it would be the last
						if (queue.First == queue.Last && queue.First.Value == null)
						{ // last one
							queue.Clear();
							m_receivedLast = true;
							LogConsumer("Received completion notification");
							return CompleteTask(null);
						}

						// no task ready !
						wait = MarkConsumerAsAwaitingCompletion_NeedsLocking(ct);
					}
				}
				else if (m_done)
				{
					return Maybe<T>.EmptyTask;
				}
				else
				{
					// queue is empty, we need to wait!
					wait = MarkConsumerAsBlocked_NeedsLocking(ct);
				}
			}

			return WaitForCompletionOrNextItemAsync(wait, ct);
		}

		private Task<Maybe<T>> CompleteTask(Task<T>? task)
		{
			var item = task == null ? Maybe.Nothing<T>() : Maybe.FromTask(task);
			WakeUpBlockedProducer_NeedsLocking();
			return Task.FromResult(item);
		}

		private async Task<Maybe<T>> WaitForTaskToCompleteAsync(Task<T> task, CancellationToken ct)
		{
			// we just need to wait for this task to complete, and return it

			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			try
			{
				var item = await task.ConfigureAwait(false);

				lock(m_lock)
				{
					Contract.Debug.Assert(m_queue.First.Value == task);
					LogConsumer("Notified that task #" + task + " completed");
					m_queue.RemoveFirst();
					WakeUpBlockedConsumer_NeedsLocking();
					return Maybe.Return(item);
				}
			}
			catch(Exception e)
			{
				LogConsumer("Notified that task #" + task + " failed");
				return Maybe.Error<T>(ExceptionDispatchInfo.Capture(e));
			}
		}

		private async Task<Maybe<T>> WaitForCompletionOrNextItemAsync(Task wait, CancellationToken ct)
		{
			// we wait for any activity (new task or one that completes)

			await wait.ConfigureAwait(false);

			// recursive call, but should be ok since we are (almost) sure to have a completed task in the queue
			return await ReceiveAsync(ct).ConfigureAwait(false);
		}

		protected Task MarkConsumerAsAwaitingCompletion_NeedsLocking(CancellationToken ct)
		{
			Contract.Debug.Requires(m_mode == AsyncOrderingMode.CompletionOrder);

			var cl = m_completionLock;
			if (cl.IsCompleted)
			{
				LogConsumer("Creating new task completion lock");
				cl = new AsyncCancelableMutex(ct);
				m_completionLock = cl;
			}
			LogConsumer("marked as waiting for task completion");
			return cl.Task;
		}

		#endregion

		#region IDisposable...

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				LogConsumer("Disposing consumer!");
				lock (m_lock)
				{
					m_done = true;
					m_producerLock.Abort();
					m_consumerLock.Abort();
					m_completionLock.Abort();
					m_queue.Clear();
					LogConsumer("Consumer has been disposed");
				}
			}
		}

		#endregion

	}
}

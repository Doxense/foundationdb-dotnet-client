﻿#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
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

namespace FoundationDB.Client
{
	using FoundationDB.Client.Utils;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	public static partial class Fdb
	{
		/// <summary>Helper class for bulk operations</summary>
		public static class Bulk
		{

			#region Writing...

			/// <summary>Insert a potentially large sequence of key/value pairs into the database, by using as many transactions as necessary, and automatically scaling the size of each batch</summary>
			/// <param name="data">Sequence of key/value pairs</param>
			/// <param name="progress">Notify of the progress on this instance (or null)</param>
			/// <param name="cancellationToken">Cancellation Token</param>
			/// <returns>Total number of values inserted in the database</returns>
			public static async Task<long> WriteAsync(IFdbDatabase db, IEnumerable<KeyValuePair<Slice, Slice>> data, IProgress<long> progress, CancellationToken cancellationToken)
			{
				if (db == null) throw new ArgumentNullException("db");
				if (data == null) throw new ArgumentNullException("data");

				cancellationToken.ThrowIfCancellationRequested();

				// We will batch keys into chunks (bounding by count and bytes), then attempt to insert that batch in the database.
				// Each transaction should try to never exceed ~1MB of size

				int maxBatchCount = 2 * 1000;
				int maxBatchSize = 1 * 1000 * 1000;

				var chunk = new List<KeyValuePair<Slice, Slice>>();

				long items = 0;
				using (var iterator = data.GetEnumerator())
				{
					if (progress != null) progress.Report(0);

					while (!cancellationToken.IsCancellationRequested)
					{
						chunk.Clear();
						int bytes = 0;

						while (iterator.MoveNext())
						{
							var pair = iterator.Current;
							chunk.Add(pair);
							bytes += pair.Key.Count + pair.Value.Count;

							if (chunk.Count >= maxBatchCount || bytes >= maxBatchSize)
							{ // chunk is big enough
								break;
							}
						}

						if (chunk.Count == 0)
						{ // no more data, we are done
							break;
						}

						await db.WriteAsync((tr) =>
						{
							foreach (var pair in chunk)
							{
								tr.Set(pair.Key, pair.Value);
							}
						}, cancellationToken).ConfigureAwait(false);

						items += chunk.Count;

						if (progress != null) progress.Report(items);
					}
				}

				cancellationToken.ThrowIfCancellationRequested();

				return items;
			}

			#endregion

			#region Batching...

			//REVIEW: what's a good value for these ?
			private const int DefaultInitialBatchSize = 100; 
			private const int DefaultMinimumBatchSize = 4;
			private const int DefaultMaximumBatchSize = 10000;


			public sealed class BatchOperationContext
			{
				/// <summary>Transaction corresponding to the current generation</summary>
				public IFdbReadOnlyTransaction Transaction { get; internal set; }

				/// <summary>Offset of the current batch from the start of the source sequence</summary>
				public long Position { get; internal set; }

				/// <summary>Generation number of the transaction (starting from 0 for the initial transaction)</summary>
				public int Generation { get; internal set; }

				/// <summary>Current batch size target</summary>
				public int Step { get; internal set; }

				/// <summary>Global timer, from the start of the bulk operation</summary>
				internal Stopwatch TotalTimer { get; set; }

				/// <summary>Timer started at the start of each transaction</summary>
				internal Stopwatch GenerationTimer { get; set; }

				/// <summary>Cooldown timer used for scaling up and down the step size</summary>
				public int Cooldown { get; internal set; }

				public TimeSpan ElapsedTotal { get { return this.TotalTimer.Elapsed; } }
				public TimeSpan ElapsedGeneration { get { return this.GenerationTimer.Elapsed; } }

				/// <summary>Returns true if all values processed up to this point used the same transaction, or false if more than one transaction was used.</summary>
				public bool IsTransactional { get { return this.Generation == 0; } }

				/// <summary>Returns true if at least one unretryable exception happened during the process of one batch</summary>
				public bool Failed { get; internal set; }

				/// <summary>If called, will stop immediately after processing this batch, even if the source sequence contains more elements</summary>
				public void Stop()
				{
					this.Abort = true;
				}

				public bool Abort { get; set; }

			}

			#region ForEach...

			/// <summary>Execute a potentially long read-only operation on batches of elements from a source sequence, using as many transactions as necessary, and automatically scaling the size of each batch to maximize the throughput.</summary>
			/// <typeparam name="TSource">Type of elements in the source sequence</typeparam>
			/// <typeparam name="TLocal">Type of the local immutable state that is flowed accross all batch operations</typeparam>
			/// <param name="db">Source database</param>
			/// <param name="source">Source sequence that will be split into batch. The size of each batch will scale up and down automatically depending on the speed of execution</param>
			/// <param name="localInit">Lambda function that is called once, and returns the initial state that will be passed to the first batch</param>
			/// <param name="body">Retryable lambda function that receives a batch of elements from the source sequence, the current context, and the previous state. If the transaction expires while this lambda function is running, it will be automatically retried with a new transaction, and a smaller batch.</param>
			/// <param name="localFinally">Lambda function that will be called after the last batch, and will be passed the last known state.</param>
			/// <param name="cancellationToken">Token used to cancel the operation</param>
			/// <returns>Task that completes when all the elements of <paramref name="source"/> have been processed, a non-retryable error occurs, or <paramref name="cancellationToken"/> is triggered</returns>
			public static Task ForEachAsync<TSource, TLocal>(
				IFdbDatabase db,
				IEnumerable<TSource> source,
				Func<TLocal> localInit,
				Func<TSource[], BatchOperationContext, TLocal, Task<TLocal>> body,
				Action<TLocal> localFinally,
				CancellationToken cancellationToken)
			{
				if (db == null) throw new ArgumentNullException("db");
				if (source == null) throw new ArgumentNullException("source");
				if (localInit == null) throw new ArgumentNullException("localInit");
				if (body == null) throw new ArgumentNullException("body");
				if (localFinally == null) throw new ArgumentNullException("localFinally");

				return RunBatchedOperationAsync<TSource, TLocal, object>(db, source, localInit, body, localFinally, DefaultInitialBatchSize, cancellationToken);
			}

			/// <summary>Execute a potentially long read-only operation on batches of elements from a source sequence, using as many transactions as necessary, and automatically scaling the size of each batch to maximize the throughput.</summary>
			/// <typeparam name="TSource">Type of elements in the source sequence</typeparam>
			/// <typeparam name="TLocal">Type of the local immutable state that is flowed accross all batch operations</typeparam>
			/// <param name="db">Source database</param>
			/// <param name="source">Source sequence that will be split into batch. The size of each batch will scale up and down automatically depending on the speed of execution</param>
			/// <param name="localInit">Lambda function that is called once, and returns the initial state that will be passed to the first batch</param>
			/// <param name="body">Retryable lambda function that receives a batch of elements from the source sequence, the current context, and the previous state. If the transaction expires while this lambda function is running, it will be automatically retried with a new transaction, and a smaller batch.</param>
			/// <param name="localFinally">Lambda function that will be called after the last batch, and will be passed the last known state.</param>
			/// <param name="cancellationToken">Token used to cancel the operation</param>
			/// <returns>Task that completes when all the elements of <paramref name="source"/> have been processed, a non-retryable error occurs, or <paramref name="cancellationToken"/> is triggered</returns>
			[Obsolete("EXPERIMENTAL: do not use yet!")]
			public static Task ForEachAsync<TSource, TLocal>(
				IFdbDatabase db,
				IEnumerable<TSource> source,
				Func<TLocal> localInit,
				Func<TSource[], BatchOperationContext, TLocal, TLocal> body,
				Action<TLocal> localFinally,
				CancellationToken cancellationToken)
			{
				if (db == null) throw new ArgumentNullException("db");
				if (source == null) throw new ArgumentNullException("source");
				if (localInit == null) throw new ArgumentNullException("localInit");
				if (body == null) throw new ArgumentNullException("body");
				if (localFinally == null) throw new ArgumentNullException("localFinally");

				//REVIEW: what is the point if the body is not async ?
				// > either is can read and generate past_version errors then it needs to be async
				// > either it's not async, then it could only Write/Clear, and in which case we need a writeable transaction ... ? (and who will commit and when ??)
				// It could maybe make sense if the source was an IFdbAsyncEnumerable<T> because you could not use Parallel.ForEach(...) for that

				return RunBatchedOperationAsync<TSource, TLocal, object>(db, source, localInit, body, localFinally, DefaultInitialBatchSize, cancellationToken);
			}

			/// <summary>Execute a potentially long read-only operation on batches of elements from a source sequence, using as many transactions as necessary, and automatically scaling the size of each batch to maximize the throughput.</summary>
			/// <typeparam name="TSource">Type of elements in the source sequence</typeparam>
			/// <param name="db">Source database</param>
			/// <param name="source">Source sequence that will be split into batch. The size of each batch will scale up and down automatically depending on the speed of execution</param>
			/// <param name="body">Retryable lambda function that receives a batch of elements from the source sequence, the current context, and the previous state. If the transaction expires while this lambda function is running, it will be automatically retried with a new transaction, and a smaller batch.</param>
			/// <param name="cancellationToken">Token used to cancel the operation</param>
			/// <returns>Task that completes when all the elements of <paramref name="source"/> have been processed, a non-retryable error occurs, or <paramref name="cancellationToken"/> is triggered</returns>
			public static Task ForEachAsync<TSource>(
				IFdbDatabase db,
				IEnumerable<TSource> source,
				Func<TSource[], BatchOperationContext, Task> body,
				CancellationToken cancellationToken)
			{
				if (db == null) throw new ArgumentNullException("db");
				if (source == null) throw new ArgumentNullException("source");
				if (body == null) throw new ArgumentNullException("body");

				return RunBatchedOperationAsync<TSource, object, object>(db, source, null, body, null, DefaultInitialBatchSize, cancellationToken);
			}

			/// <summary>Execute a potentially long read-only operation on batches of elements from a source sequence, using as many transactions as necessary, and automatically scaling the size of each batch to maximize the throughput.</summary>
			/// <typeparam name="TSource">Type of elements in the source sequence</typeparam>
			/// <param name="db">Source database</param>
			/// <param name="source">Source sequence that will be split into batch. The size of each batch will scale up and down automatically depending on the speed of execution</param>
			/// <param name="body">Retryable lambda function that receives a batch of elements from the source sequence, the current context, and the previous state. If the transaction expires while this lambda function is running, it will be automatically retried with a new transaction, and a smaller batch.</param>
			/// <param name="cancellationToken">Token used to cancel the operation</param>
			/// <returns>Task that completes when all the elements of <paramref name="source"/> have been processed, a non-retryable error occurs, or <paramref name="cancellationToken"/> is triggered</returns>
			[Obsolete("EXPERIMENTAL: do not use yet!")]
			public static Task ForEachAsync<TSource>(
				IFdbDatabase db,
				IEnumerable<TSource> source,
				Action<TSource[], BatchOperationContext> body,
				CancellationToken cancellationToken)
			{
				if (db == null) throw new ArgumentNullException("db");
				if (source == null) throw new ArgumentNullException("source");
				if (body == null) throw new ArgumentNullException("body");

				//REVIEW: what is the point if the body is not async ?
				// > either is can read and generate past_version errors then it needs to be async
				// > either it's not async, then it could only Write/Clear, and in which case we need a writeable transaction ... ? (and who will commit and when ??)
				// It could maybe make sense if the source was an IFdbAsyncEnumerable<T> because you could not use Parallel.ForEach(...) for that

				return RunBatchedOperationAsync<TSource, object, object>(db, source, null, body, null, DefaultInitialBatchSize, cancellationToken);
			}

			#endregion

			#region Aggregate...

			/// <summary>Execute a potentially long aggregation on batches of elements from a source sequence, using as many transactions as necessary, and automatically scaling the size of each batch to maximize the throughput.</summary>
			/// <typeparam name="TSource">Type of elements in the source sequence</typeparam>
			/// <typeparam name="TAggregate">Type of the local immutable aggregate that is flowed accross all batch operations</typeparam>
			/// <param name="db">Source database</param>
			/// <param name="source">Source sequence that will be split into batch. The size of each batch will scale up and down automatically depending on the speed of execution</param>
			/// <param name="localInit">Lambda function that is called once, and returns the initial state that will be passed to the first batch</param>
			/// <param name="body">Retryable lambda function that receives a batch of elements from the source sequence, the current context, and the previous state. If the transaction expires while this lambda function is running, it will be automatically retried with a new transaction, and a smaller batch.</param>
			/// <param name="cancellationToken">Token used to cancel the operation</param>
			/// <returns>Task that completes when all the elements of <paramref name="source"/> have been processed, a non-retryable error occurs, or <paramref name="cancellationToken"/> is triggered</returns>
			public static Task<TAggregate> AggregateAsync<TSource, TAggregate>(
				IFdbDatabase db,
				IEnumerable<TSource> source,
				Func<TAggregate> localInit,
				Func<TSource[], BatchOperationContext, TAggregate, Task<TAggregate>> body,
				CancellationToken cancellationToken)
			{
				if (db == null) throw new ArgumentNullException("db");
				if (source == null) throw new ArgumentNullException("source");
				if (localInit == null) throw new ArgumentNullException("localInit");
				if (body == null) throw new ArgumentNullException("body");

				Func<TAggregate, TAggregate> identity = (x) => x;
				return RunBatchedOperationAsync<TSource, TAggregate, TAggregate>(db, source, localInit, body, identity, DefaultInitialBatchSize, cancellationToken);
			}

			/// <summary>Execute a potentially long aggregation on batches of elements from a source sequence, using as many transactions as necessary, and automatically scaling the size of each batch to maximize the throughput.</summary>
			/// <typeparam name="TSource">Type of elements in the source sequence</typeparam>
			/// <typeparam name="TAggregate">Type of the local immutable aggregate that is flowed accross all batch operations</typeparam>
			/// <param name="db">Source database</param>
			/// <param name="source">Source sequence that will be split into batch. The size of each batch will scale up and down automatically depending on the speed of execution</param>
			/// <param name="localInit">Lambda function that is called once, and returns the initial state that will be passed to the first batch</param>
			/// <param name="body">Retryable lambda function that receives a batch of elements from the source sequence, the current context, and the previous state. If the transaction expires while this lambda function is running, it will be automatically retried with a new transaction, and a smaller batch.</param>
			/// <param name="cancellationToken">Token used to cancel the operation</param>
			/// <returns>Task that completes when all the elements of <paramref name="source"/> have been processed, a non-retryable error occurs, or <paramref name="cancellationToken"/> is triggered</returns>
			public static Task<TResult> AggregateAsync<TSource, TAggregate, TResult>(
				IFdbDatabase db,
				IEnumerable<TSource> source,
				Func<TAggregate> init,
				Func<TSource[], BatchOperationContext, TAggregate, Task<TAggregate>> body,
				Func<TAggregate, TResult> transform,
				CancellationToken cancellationToken)
			{
				if (db == null) throw new ArgumentNullException("db");
				if (source == null) throw new ArgumentNullException("source");
				if (init == null) throw new ArgumentNullException("init");
				if (body == null) throw new ArgumentNullException("body");
				if (transform == null) throw new ArgumentNullException("transform");

				return RunBatchedOperationAsync<TSource, TAggregate, TResult>(db, source, init, body, transform, DefaultInitialBatchSize, cancellationToken);
			}

			#endregion

			/// <summary>Runs a long duration bulk read</summary>
			private static async Task<TResult> RunBatchedOperationAsync<TSource, TLocal, TResult>(
				IFdbDatabase db,
				IEnumerable<TSource> source,
				Func<TLocal> localInit,
				Delegate body,
				Delegate localFinally,
				int initialBatchSize,
				CancellationToken cancellationToken
			)
			{
				Contract.Requires(db != null && source != null && initialBatchSize > 0 && body != null);

				var bodyAsyncWithContextAndState = body as Func<TSource[], BatchOperationContext, TLocal, Task<TLocal>>;
				var bodyWithContextAndState = body as Func<TSource[], BatchOperationContext, TLocal, TLocal>;
				var bodyAsyncWithContext = body as Func<TSource[], BatchOperationContext, Task>;
				var bodyWithContext = body as Action<TSource[], BatchOperationContext>;

				if (bodyAsyncWithContextAndState == null &&
					bodyAsyncWithContext == null &&
					bodyWithContextAndState == null &&
					bodyWithContext == null)
				{
					throw new ArgumentException(String.Format("Unsupported delegate type {0} for body", body.GetType().FullName), "body");
				}

				var localFinallyVoid = localFinally as Action<TLocal>;
				var localFinallyWithResult = localFinally as Func<TLocal, TResult>;
				if (localFinally != null && 
					localFinallyVoid == null &&
					localFinallyWithResult == null)
				{
					throw new ArgumentException(String.Format("Unsupported delegate type {0} for local finally", body.GetType().FullName), "localFinally");
				}

				int batchSize = initialBatchSize;
				var batch = new List<TSource>(batchSize);

				bool localInitialized = false;
				TLocal localValue = default(TLocal);
				TSource[] items = null;
				TResult result = default(TResult);

				using (var iterator = source.GetEnumerator())
				{
					var totalTimer = Stopwatch.StartNew();

					using (var trans = db.BeginReadOnlyTransaction(cancellationToken))
					{
						var ctx = new BatchOperationContext()
						{
							Transaction = trans,
							Step = batchSize,
							TotalTimer = totalTimer,
							GenerationTimer = Stopwatch.StartNew(),
						};

						try
						{
							if (localInit != null)
							{ // need to initialize the state
								//TODO: maybe defer only if there are things in the source sequence?
								localValue = localInit();
								localInitialized = true;
							}

							while (!ctx.Abort && !cancellationToken.IsCancellationRequested)
							{
								FillNextBatch<TSource>(iterator, batch, ctx.Step);

								if (batch.Count == 0)
								{
									break;
								}

								int offsetInCurrentBatch = 0;
								while (!ctx.Abort && !cancellationToken.IsCancellationRequested)
								{
									var r = Math.Min(ctx.Step, batch.Count - offsetInCurrentBatch);
									if (items == null || items.Length != r)
									{
										items = new TSource[r];
									}
									batch.CopyTo(offsetInCurrentBatch, items, 0, items.Length);

									FdbException error = null;
									try
									{
										var sw = Stopwatch.StartNew();
										if (bodyAsyncWithContextAndState != null)
										{
											localValue = await bodyAsyncWithContextAndState(items, ctx, localValue);
										}
										else if (bodyWithContextAndState != null)
										{
											localValue = bodyWithContextAndState(items, ctx, localValue);
										}
										else if (bodyAsyncWithContext != null)
										{
											await bodyAsyncWithContext(items, ctx);
										}
										else if (bodyWithContext != null)
										{
											bodyWithContext(items, ctx);
										}
										sw.Stop();

										if (!ctx.Abort)
										{
											offsetInCurrentBatch += items.Length;
											if (offsetInCurrentBatch >= batch.Count)
											{
												// scale up the batch size if everything was superquick !
												if (ctx.Cooldown > 0) ctx.Cooldown--;
												if (ctx.Cooldown <= 0 && sw.Elapsed.TotalSeconds < (5.0 - ctx.ElapsedGeneration.TotalSeconds) / 2)//REVIEW: magical number!
												{
													ctx.Step = Math.Min(ctx.Step * 2, DefaultMaximumBatchSize); //REVIEW: magical number!
													//REVIEW: magical number!
													ctx.Cooldown = 2;
												}
												break;
											}
										}
									}
									catch (Exception e)
									{
										error = e as FdbException;
										// if the callback uses task.Wait() or task.Result the exception may be wrapped in an AggregateException
										if (error == null && e is AggregateException)
										{
											error = (e as AggregateException).InnerException as FdbException;
										}
									}

									if (error != null)
									{
										if (error.Code == FdbError.PastVersion)
										{ // this generation lasted too long, we need to start a new one and try again...
											trans.Reset();
											ctx.GenerationTimer.Reset();
											ctx.Generation++;

											// scale back batch size
											if (ctx.Step > DefaultMinimumBatchSize)
											{
												ctx.Step = Math.Max(ctx.Step >> 1, DefaultMinimumBatchSize);
												//REVIEW: magical number!
												ctx.Cooldown = 10;
											}
										}
										else
										{ // the error may be retryable...
											await trans.OnErrorAsync(error.Code);
											ctx.GenerationTimer.Reset();
											ctx.Generation++;
											//REVIEW: magical number!
											if (ctx.Cooldown < 2) ctx.Cooldown = 2;

										}
									}
								}
								cancellationToken.ThrowIfCancellationRequested();

								ctx.Position += offsetInCurrentBatch;
								batch.Clear();
							}
							cancellationToken.ThrowIfCancellationRequested();
						}
						catch(Exception)
						{
							ctx.Failed = true;
							throw;
						}
						finally
						{
							if (localFinally != null && localInitialized)
							{ // we need to cleanup the state whatever happens
								if (localFinallyVoid != null)
								{
									localFinallyVoid(localValue);
								}
								else if (localFinallyWithResult != null)
								{
									result = localFinallyWithResult(localValue);
								}
							}
						}
					}
				}

				return result;
			}

			/// <summary>Tries to fill a batch with items from the source</summary>
			/// <typeparam name="T">Type of source items</typeparam>
			/// <param name="iterator">Source iterator</param>
			/// <param name="batch">Batch where the read items will be appended.</param>
			/// <param name="size">Maximum capacity of the batch</param>
			/// <returns>Number items added to the batch, or 0 if it was already full</returns>
			private static int FillNextBatch<T>(IEnumerator<T> iterator, List<T> batch, int size)
			{
				Contract.Requires(iterator != null && batch != null && size > 0);

				int count = 0;
				while (batch.Count < size && iterator.MoveNext())
				{
					batch.Add(iterator.Current);
					++count;
				}
				return count;
			}

			#endregion

		}
	}

}

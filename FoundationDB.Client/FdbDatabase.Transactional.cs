#region BSD Licence
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
	using FoundationDB.Async;
	using FoundationDB.Client.Native;
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Concurrent;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>FoundationDB Database</summary>
	/// <remarks>Wraps an FDBDatabase* handle</remarks>
	public partial class FdbDatabase
	{

		/// <summary>Set of retryable operations (read only)</summary>
		public class ReadOnlyTransactional
		{

			protected readonly FdbDatabase m_db;
			private readonly bool m_snapshot;

			internal ReadOnlyTransactional(FdbDatabase db, bool snapshot)
			{
				Contract.Requires(db != null);
				m_db = db;
				m_snapshot = snapshot;
			}

			public Task Read(Action<IFdbReadTransaction> action, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunReadAsync(
					db: m_db,
					snapshot: m_snapshot,
					asyncAction: (tr, _context) => TaskHelpers.Inline((Action<IFdbReadTransaction>)_context.State, arg1: tr, ct: _context.Token),
					state: action,
					ct: ct
				);
			}

			public Task ReadAsync(Func<IFdbReadTransaction, Task> asyncAction, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var _asyncAction = (Func<IFdbReadTransaction, Task>)_context.State;
						return _asyncAction(tr);
					},
					state: asyncAction,
					ct: ct
				);
			}

			public Task ReadAsync(Func<IFdbReadTransaction, CancellationToken, Task> asyncAction, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var _asyncAction = (Func<IFdbReadTransaction, CancellationToken, Task>)_context.State;
						return _asyncAction(tr, _context.Token);
					},
					state: asyncAction,
					ct: ct
				);
			}

			public Task<R> ReadAsync<R>(Func<IFdbReadTransaction, Task<R>> asyncAction, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync<R>(
					db: m_db,
					asyncAction: async (tr, _context) =>
					{
						var func = (Func<IFdbReadTransaction, Task<R>>)_context.State;
						_context.Result = await func(tr).ConfigureAwait(false);
					},
					state: asyncAction,
					ct: ct
				);
			}

			public Task Read<T1>(Action<IFdbReadTransaction, T1> asyncAction, T1 arg1, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var prms = (Tuple<Action<IFdbReadTransaction, T1>, T1>)_context.State;
						return TaskHelpers.Inline(prms.Item1, arg1: tr, arg2: prms.Item2, ct: _context.Token);
					},
					state: Tuple.Create(asyncAction, arg1),
					ct: ct
				);
			}

			public Task ReadAsync<T1>(Func<IFdbReadTransaction, T1, Task> asyncAction, T1 arg1, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var prms = (Tuple<Func<IFdbReadTransaction, T1, Task>, T1>)_context.State;
						return prms.Item1(tr, prms.Item2);
					},
					state: Tuple.Create(asyncAction, arg1),
					ct: ct
				);
			}

			public Task<R> ReadAsync<T1, R>(Func<IFdbReadTransaction, T1, Task<R>> asyncAction, T1 arg1, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync<R>(
					db: m_db,
					asyncAction: async (tr, _context) =>
					{
						var prms = (Tuple<Func<IFdbReadTransaction, T1, Task<R>>, T1>)_context.State;
						_context.Result = await prms.Item1(tr, prms.Item2).ConfigureAwait(false);
					},
					state: Tuple.Create(asyncAction, arg1),
					ct: ct
				);
			}

			public Task Read<T1, T2>(Action<IFdbReadTransaction, T1, T2> asyncAction, T1 arg1, T2 arg2, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var prms = (Tuple<Action<IFdbReadTransaction, T1, T2>, T1, T2>)_context.State;
						return TaskHelpers.Inline(prms.Item1, arg1: tr, arg2: prms.Item2, arg3: prms.Item3, ct: _context.Token);
					},
					state: Tuple.Create(asyncAction, arg1, arg2),
					ct: ct
				);
			}

			public Task ReadAsync<T1, T2>(Func<IFdbReadTransaction, T1, T2, Task> asyncAction, T1 arg1, T2 arg2, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var prms = (Tuple<Func<IFdbReadTransaction, T1, T2, Task>, T1, T2>)_context.State;
						return prms.Item1(tr, prms.Item2, prms.Item3);
					},
					state: Tuple.Create(asyncAction, arg1, arg2),
					ct: ct
				);
			}

			public Task<R> ReadAsync<T1, T2, R>(Func<IFdbReadTransaction, T1, T2, Task<R>> asyncAction, T1 arg1, T2 arg2, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync<R>(
					db: m_db,
					asyncAction: async (tr, _context) =>
					{
						var prms = (Tuple<Func<IFdbReadTransaction, T1, T2, Task<R>>, T1, T2>)_context.State;
						_context.Result = await prms.Item1(tr, prms.Item2, prms.Item3).ConfigureAwait(false);
					},
					state: Tuple.Create(asyncAction, arg1, arg2),
					ct: ct
				);
			}

			public Task<R> ReadAsync<T1, T2, R>(Func<IFdbReadTransaction, T1, T2, CancellationToken, Task<R>> asyncAction, T1 arg1, T2 arg2, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync<R>(
					db: m_db,
					asyncAction: async (tr, _context) =>
					{
						var prms = (Tuple<Func<IFdbReadTransaction, T1, T2, CancellationToken, Task<R>>, T1, T2>)_context.State;
						_context.Result = await prms.Item1(tr, prms.Item2, prms.Item3, _context.Token).ConfigureAwait(false);
					},
					state: Tuple.Create(asyncAction, arg1, arg2),
					ct: ct
				);
			}

			public Task Read<T1, T2, T3>(Action<IFdbReadTransaction, T1, T2, T3> asyncAction, T1 arg1, T2 arg2, T3 arg3, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var prms = (Tuple<Action<IFdbTransaction, T1, T2, T3>, T1, T2, T3>)_context.State;
						return TaskHelpers.Inline(prms.Item1, arg1: tr, arg2: prms.Item2, arg3: prms.Item3, arg4: prms.Item4, ct: _context.Token);
					},
					state: Tuple.Create(asyncAction, arg1, arg2, arg3),
					ct: ct
				);
			}

			public Task ReadAsync<T1, T2, T3>(Func<IFdbReadTransaction, T1, T2, T3, Task> asyncAction, T1 arg1, T2 arg2, T3 arg3, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var prms = (Tuple<Func<IFdbReadTransaction, T1, T2, T3, Task>, T1, T2, T3>)_context.State;
						return prms.Item1(tr, prms.Item2, prms.Item3, prms.Item4);
					},
					state: Tuple.Create(asyncAction, arg1, arg2, arg3),
					ct: ct
				);
			}

			public Task<R> ReadAsync<T1, T2, T3, R>(Func<IFdbReadTransaction, T1, T2, T3, Task<R>> asyncAction, T1 arg1, T2 arg2, T3 arg3, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync<R>(
					db: m_db,
					asyncAction: async (tr, _context) =>
					{
						var prms = (Tuple<Func<IFdbReadTransaction, T1, T2, T3, Task<R>>, T1, T2, T3>)_context.State;
						_context.Result = await prms.Item1(tr, prms.Item2, prms.Item3, prms.Item4).ConfigureAwait(false);
					},
					state: Tuple.Create(asyncAction, arg1, arg2, arg3),
					ct: ct
				);
			}

			public Task Read<T1, T2, T3, T4>(Action<IFdbReadTransaction, T1, T2, T3, T4> asyncAction, T1 arg1, T2 arg2, T3 arg3, T4 arg4, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var prms = (Tuple<Action<IFdbReadTransaction, T1, T2, T3, T4>, T1, T2, T3, T4>)_context.State;
						return TaskHelpers.Inline(prms.Item1, arg1: tr, arg2: prms.Item2, arg3: prms.Item3, arg4: prms.Item4, arg5: prms.Item5, ct: _context.Token);
					},
					state: Tuple.Create(asyncAction, arg1, arg2, arg3, arg4),
					ct: ct
				);
			}

			public Task ReadAsync<T1, T2, T3, T4>(Func<IFdbReadTransaction, T1, T2, T3, T4, Task> asyncAction, T1 arg1, T2 arg2, T3 arg3, T4 arg4, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var prms = (Tuple<Func<IFdbReadTransaction, T1, T2, T3, T4, Task>, T1, T2, T3, T4>)_context.State;
						return prms.Item1(tr, prms.Item2, prms.Item3, prms.Item4, prms.Item5);
					},
					state: Tuple.Create(asyncAction, arg1, arg2, arg3, arg4),
					ct: ct
				);
			}

			public Task<R> ReadAsync<T1, T2, T3, T4, R>(Func<IFdbReadTransaction, T1, T2, T3, T4, Task<R>> asyncAction, T1 arg1, T2 arg2, T3 arg3, T4 arg4, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync<R>(
					db: m_db,
					asyncAction: async (tr, _context) =>
					{
						var prms = (Tuple<Func<IFdbTransaction, T1, T2, T3, T4, Task<R>>, T1, T2, T3, T4>)_context.State;
						_context.Result = await prms.Item1(tr, prms.Item2, prms.Item3, prms.Item4, prms.Item5).ConfigureAwait(false);
					},
					state: Tuple.Create(asyncAction, arg1, arg2, arg3, arg4),
					ct: ct
				);
			}

		}

		/// <summary>Set of retryable operations (read and write)</summary>
		public sealed class ReadWriteTransactional : ReadOnlyTransactional
		{
			private ReadOnlyTransactional m_snapshotTransactional;

			internal ReadWriteTransactional(FdbDatabase db)
				: base(db, false)
			{ }

			/// <summary>Snapshot read operations</summary>
			public ReadOnlyTransactional Snapshot
			{
				get { return m_snapshotTransactional ?? (m_snapshotTransactional = new ReadOnlyTransactional(m_db, true)); }
			}

			#region Changing...

			public Task Change(Action<IFdbTransaction> action, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) => TaskHelpers.Inline((Action<IFdbTransaction>)_context.State, arg1: tr, ct: _context.Token),
					state: action,
					ct: ct
				);
			}

			public Task ChangeAsync(Func<IFdbTransaction, Task> asyncAction, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var _asyncAction = (Func<IFdbTransaction, Task>)_context.State;
						return _asyncAction(tr);
					},
					state: asyncAction,
					ct: ct
				);
			}

			public Task ChangeAsync(Func<IFdbTransaction, CancellationToken, Task> asyncAction, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var _asyncAction = (Func<IFdbTransaction, CancellationToken, Task>)_context.State;
						return _asyncAction(tr, _context.Token);
					},
					state: asyncAction,
					ct: ct
				);
			}

			public Task<R> ChangeAsync<R>(Func<IFdbTransaction, Task<R>> asyncAction, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync<R>(
					db: m_db,
					asyncAction: async (tr, _context) =>
					{
						var func = (Func<IFdbTransaction, Task<R>>)_context.State;
						_context.Result = await func(tr).ConfigureAwait(false);
					},
					state: asyncAction,
					ct: ct
				);
			}

			public Task Change<T1>(Action<IFdbTransaction, T1> asyncAction, T1 arg1, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var prms = (Tuple<Action<IFdbTransaction, T1>, T1>)_context.State;
						return TaskHelpers.Inline(prms.Item1, arg1: tr, arg2: prms.Item2, ct: _context.Token);
					},
					state: Tuple.Create(asyncAction, arg1),
					ct: ct
				);
			}

			public Task ChangeAsync<T1>(Func<IFdbTransaction, T1, Task> asyncAction, T1 arg1, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var prms = (Tuple<Func<IFdbTransaction, T1, Task>, T1>)_context.State;
						return prms.Item1(tr, prms.Item2);
					},
					state: Tuple.Create(asyncAction, arg1),
					ct: ct
				);
			}

			public Task<R> ChangeAsync<T1, R>(Func<IFdbTransaction, T1, Task<R>> asyncAction, T1 arg1, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync<R>(
					db: m_db,
					asyncAction: async (tr, _context) =>
					{
						var prms = (Tuple<Func<IFdbTransaction, T1, Task<R>>, T1>)_context.State;
						_context.Result = await prms.Item1(tr, prms.Item2).ConfigureAwait(false);
					},
					state: Tuple.Create(asyncAction, arg1),
					ct: ct
				);
			}

			public Task Change<T1, T2>(Action<IFdbTransaction, T1, T2> asyncAction, T1 arg1, T2 arg2, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var prms = (Tuple<Action<IFdbTransaction, T1, T2>, T1, T2>)_context.State;
						return TaskHelpers.Inline(prms.Item1, arg1: tr, arg2: prms.Item2, arg3: prms.Item3, ct: _context.Token);
					},
					state: Tuple.Create(asyncAction, arg1, arg2),
					ct: ct
				);
			}

			public Task ChangeAsync<T1, T2>(Func<IFdbTransaction, T1, T2, Task> asyncAction, T1 arg1, T2 arg2, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var prms = (Tuple<Func<IFdbTransaction, T1, T2, Task>, T1, T2>)_context.State;
						return prms.Item1(tr, prms.Item2, prms.Item3);
					},
					state: Tuple.Create(asyncAction, arg1, arg2),
					ct: ct
				);
			}

			public Task<R> ChangeAsync<T1, T2, R>(Func<IFdbTransaction, T1, T2, Task<R>> asyncAction, T1 arg1, T2 arg2, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync<R>(
					db: m_db,
					asyncAction: async (tr, _context) =>
					{
						var prms = (Tuple<Func<IFdbTransaction, T1, T2, Task<R>>, T1, T2>)_context.State;
						_context.Result = await prms.Item1(tr, prms.Item2, prms.Item3).ConfigureAwait(false);
					},
					state: Tuple.Create(asyncAction, arg1, arg2),
					ct: ct
				);
			}

			public Task<R> ChangeAsync<T1, T2, R>(Func<IFdbTransaction, T1, T2, CancellationToken, Task<R>> asyncAction, T1 arg1, T2 arg2, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync<R>(
					db: m_db,
					asyncAction: async (tr, _context) =>
					{
						var prms = (Tuple<Func<IFdbTransaction, T1, T2, CancellationToken, Task<R>>, T1, T2>)_context.State;
						_context.Result = await prms.Item1(tr, prms.Item2, prms.Item3, _context.Token).ConfigureAwait(false);
					},
					state: Tuple.Create(asyncAction, arg1, arg2),
					ct: ct
				);
			}

			public Task Change<T1, T2, T3>(Action<IFdbTransaction, T1, T2, T3> asyncAction, T1 arg1, T2 arg2, T3 arg3, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var prms = (Tuple<Action<IFdbTransaction, T1, T2, T3>, T1, T2, T3>)_context.State;
						return TaskHelpers.Inline(prms.Item1, arg1: tr, arg2: prms.Item2, arg3: prms.Item3, arg4: prms.Item4, ct: _context.Token);
					},
					state: Tuple.Create(asyncAction, arg1, arg2, arg3),
					ct: ct
				);
			}

			public Task ChangeAsync<T1, T2, T3>(Func<IFdbTransaction, T1, T2, T3, Task> asyncAction, T1 arg1, T2 arg2, T3 arg3, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var prms = (Tuple<Func<IFdbTransaction, T1, T2, T3, Task>, T1, T2, T3>)_context.State;
						return prms.Item1(tr, prms.Item2, prms.Item3, prms.Item4);
					},
					state: Tuple.Create(asyncAction, arg1, arg2, arg3),
					ct: ct
				);
			}

			public Task<R> ChangeAsync<T1, T2, T3, R>(Func<IFdbTransaction, T1, T2, T3, Task<R>> asyncAction, T1 arg1, T2 arg2, T3 arg3, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync<R>(
					db: m_db,
					asyncAction: async (tr, _context) =>
					{
						var prms = (Tuple<Func<IFdbTransaction, T1, T2, T3, Task<R>>, T1, T2, T3>)_context.State;
						_context.Result = await prms.Item1(tr, prms.Item2, prms.Item3, prms.Item4).ConfigureAwait(false);
					},
					state: Tuple.Create(asyncAction, arg1, arg2, arg3),
					ct: ct
				);
			}

			public Task Change<T1, T2, T3, T4>(Action<IFdbTransaction, T1, T2, T3, T4> asyncAction, T1 arg1, T2 arg2, T3 arg3, T4 arg4, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var prms = (Tuple<Action<IFdbTransaction, T1, T2, T3, T4>, T1, T2, T3, T4>)_context.State;
						return TaskHelpers.Inline(prms.Item1, arg1: tr, arg2: prms.Item2, arg3: prms.Item3, arg4: prms.Item4, arg5: prms.Item5, ct: _context.Token);
					},
					state: Tuple.Create(asyncAction, arg1, arg2, arg3, arg4),
					ct: ct
				);
			}

			public Task ChangeAsync<T1, T2, T3, T4>(Func<IFdbTransaction, T1, T2, T3, T4, Task> asyncAction, T1 arg1, T2 arg2, T3 arg3, T4 arg4, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync(
					db: m_db,
					asyncAction: (tr, _context) =>
					{
						var prms = (Tuple<Func<IFdbTransaction, T1, T2, T3, T4, Task>, T1, T2, T3, T4>)_context.State;
						return prms.Item1(tr, prms.Item2, prms.Item3, prms.Item4, prms.Item5);
					},
					state: Tuple.Create(asyncAction, arg1, arg2, arg3, arg4),
					ct: ct
				);
			}

			public Task<R> ChangeAsync<T1, T2, T3, T4, R>(Func<IFdbTransaction, T1, T2, T3, T4, Task<R>> asyncAction, T1 arg1, T2 arg2, T3 arg3, T4 arg4, CancellationToken ct = default(CancellationToken))
			{
				return FdbOperationContext.RunWriteAsync<R>(
					db: m_db,
					asyncAction: async (tr, _context) =>
					{
						var prms = (Tuple<Func<IFdbTransaction, T1, T2, T3, T4, Task<R>>, T1, T2, T3, T4>)_context.State;
						_context.Result = await prms.Item1(tr, prms.Item2, prms.Item3, prms.Item4, prms.Item5).ConfigureAwait(false);
					},
					state: Tuple.Create(asyncAction, arg1, arg2, arg3, arg4),
					ct: ct
				);
			}

			#endregion

		}

	}

}

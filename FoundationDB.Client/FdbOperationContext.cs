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
	using FoundationDB.Client.Utils;
	using System;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	public class FdbOperationContext
	{
		/// <summary>The database used by the operation</summary>
		public FdbDatabase Db { get; private set; }

		/// <summary>The state of the operation (or null)</summary>
		public object State { get; set; }

		/// <summary>Result of the operation (or null)</summary>
		public object Result { get; set; }

		/// <summary>Cancellation token associated with the operation</summary>
		public CancellationToken Token { get; private set; }

		/// <summary>If set to true, will abort and not commit the transaction. If false, will try to commit the transaction (and retry on failure)</summary>
		public bool Abort { get; set; }

		/// <summary>Current attempt number (0 for first, 1+ for retries)</summary>
		public int Retries { get; private set; }

		/// <summary>Date at wich the operation was first started</summary>
		public DateTime StartedUtc { get; private set; }

		/// <summary>Time spent since the start of the first attempt</summary>
		public Stopwatch Duration { get; private set; }

		public bool Committed { get; private set; }

		internal FdbOperationContext(FdbDatabase db, object state)
		{
			Contract.Requires(db != null);

			this.Db = db;
			this.State = state;
			this.Duration = new Stopwatch();
		}

		public async Task Execute(Func<FdbTransaction, FdbOperationContext, Task> asyncAction, CancellationToken ct)
		{
			try
			{
				this.Committed = false;
				this.Token = ct;
				this.Retries = 0;
				this.StartedUtc = DateTime.UtcNow;
				this.Duration.Start();

				while (!this.Committed && !this.Token.IsCancellationRequested)
				{
					using (var trans = this.Db.BeginTransaction())
					{
						FdbException e = null;
						try
						{
							// call the user provided lambda
							await asyncAction(trans, this).ConfigureAwait(false);

							if (this.Abort)
							{
								break;
							}
							// commit the transaction
							await trans.CommitAsync(this.Token).ConfigureAwait(false);

							// we are done
							this.Committed = true;
						}
						catch (FdbException x)
						{
							x = e;
						}

						if (e != null)
						{
							await trans.OnErrorAsync(e.Code).ConfigureAwait(false);
						}

						if (this.Duration.Elapsed.TotalSeconds >= 1)
						{
							if (Logging.On) Logging.Info(String.Format("fdb WARNING: long transaction ({0} sec elapsed in transaction lambda function ({1} retries, {2})", this.Duration.Elapsed.TotalSeconds.ToString("N1"), this.Retries.ToString(), this.Committed ? "committed" : "not yet committed"));
						}

						this.Retries++;
					}
				}
				this.Token.ThrowIfCancellationRequested();

				if (this.Abort)
				{
					throw new OperationCanceledException(this.Token);
				}

			}
			finally
			{
				this.Duration.Stop();
				this.Token = CancellationToken.None;
			}
		}

	}

}

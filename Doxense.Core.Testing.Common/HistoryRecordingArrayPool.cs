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

namespace SnowBank.Testing
{
	using System.Buffers;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using Doxense.Serialization;

	/// <summary>Extension methods for <see cref="HistoryRecordingArrayPool{T}"/></summary>
	public static class HistoryRecordingArrayPool
	{

		/// <summary>Creates a new history recording array pool</summary>
		public static HistoryRecordingArrayPool<T> Create<T>() => new();

	}

	/// <summary>Implementation of <see cref="ArrayPool{T}"/> that records all rented and returned arrays</summary>
	/// <remarks>This pool <b>DOES NOT</b> reuse returned arrays, and is not appropriate for testing concurrency or performance.</remarks>
	public class HistoryRecordingArrayPool<T> : ArrayPool<T>
	{

		public Action<(int Token, int Size, T[] Array)> OnRent { get; init; } = ((_) => { });

		public Action<(int Token, T[] Array, T[] Snapshot, bool Clear)> OnReturn { get; } = ((_) => { });

		public Dictionary<T[], int> NotReturned { get; } = [ ];

		public List<(int Token, int Size, T[] Array, StackTrace Callstack)> Rented { get; } = [ ];

		public List<(int Token, int Size, T[] Array, T[] Snapshot, bool Cleared, StackTrace Callstack)> Returned { get; } = [ ];

		/// <inheritdoc />
		public override T[] Rent(int minimumLength)
		{
			lock (this)
			{
				var token = this.Rented.Count;
				var array = new T[minimumLength];
				this.OnRent((token, minimumLength, array));
				this.NotReturned.Add(array, token);
				this.Rented.Add((token, minimumLength, array, new StackTrace()));
				return array;
			}
		}

		/// <inheritdoc />
		public override void Return(T[] array, bool clearArray = false)
		{
			lock (this)
			{
				Assert.That(array, Is.Not.Null.Or.Empty);
				if (!this.NotReturned.Remove(array, out var token))
				{
					Assert.Fail("Attempted to return a buffer not originally from this pool");
				}

				// create a snapshot of the array as it was when returned
				var snapshot = array.ToArray();

				this.OnReturn((token, array, snapshot, clearArray));

				if (clearArray)
				{
					Array.Clear(array);
				}

				this.Returned.Add((token, this.Rented[token].Size, array, snapshot, clearArray, new StackTrace()));
				this.NotReturned.Remove(array);
			}
		}

		public void Dump()
		{
			SimpleTest.Log($"# ArrayPool<{typeof(T).GetFriendlyName()}>: {this.NotReturned:N0} pending");

			SimpleTest.Log($"# > Rented: {this.Rented.Count:N0}");
			foreach (var rent in this.Rented)
			{
				SimpleTest.Log($"#   - {rent.Token}, [{rent.Size:N0}]; {(this.NotReturned.ContainsKey(rent.Array) ? "PENDING" : "returned")}");
			}

			SimpleTest.Log($"Returned: {this.Returned.Count:N0}");
			foreach (var ret in this.Returned)
			{
				SimpleTest.Log($"#   - {ret.Token}, [{ret.Array.Length:N0}]");
			}
		}

	}

}

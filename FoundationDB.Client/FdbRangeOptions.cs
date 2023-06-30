#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Client
{
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Container class for options in a Range query</summary>
	[DebuggerDisplay("Limit={Limit}, Reverse={Reverse}, TargetBytes={TargetBytes}, Mode={Mode}, Read={Read}")]
	[PublicAPI]
	public sealed class FdbRangeOptions
	{
		#region Public Properties...

		/// <summary>Maximum number of items to return</summary>
		public int? Limit { get; set; }

		/// <summary>If true, results are returned in reverse order (from last to first)</summary>
		public bool? Reverse { get; set; }

		/// <summary>Maximum number of bytes to read</summary>
		public int? TargetBytes { get; set; }

		/// <summary>Streaming mode</summary>
		public FdbStreamingMode? Mode { get; set; }

		/// <summary>Read mode (only keys, only values, or both)</summary>
		public FdbReadMode? Read { get; set; }

		#endregion

		#region Constructors...

		/// <summary>Create a new empty set of options</summary>
		public FdbRangeOptions()
		{ }

		public FdbRangeOptions(int limit)
		{
			this.Limit = limit;
		}

		public FdbRangeOptions(int limit, bool reverse)
		{
			this.Limit = limit;
			this.Reverse = reverse;
		}

		/// <summary>Create a new set of options</summary>
		public FdbRangeOptions(int? limit, bool? reverse = null, int? targetBytes = null, FdbStreamingMode? mode = null, FdbReadMode? read = null)
		{
			this.Limit = limit;
			this.Reverse = reverse;
			this.TargetBytes = targetBytes;
			this.Mode = mode;
			this.Read = read;
		}

		/// <summary>Copy an existing set of options</summary>
		/// <param name="options"></param>
		public FdbRangeOptions(FdbRangeOptions options)
		{
			Contract.Debug.Requires(options != null);
			this.Limit = options.Limit;
			this.Reverse = options.Reverse;
			this.TargetBytes = options.TargetBytes;
			this.Mode = options.Mode;
			this.Read = options.Read;
		}

		#endregion

		/// <summary>Add all missing values from the provided defaults</summary>
		/// <param name="options">Options provided by the caller (can be null)</param>
		/// <param name="limit">Default value for Limit if not provided</param>
		/// <param name="targetBytes">Default TargetBytes for limit if not provided</param>
		/// <param name="mode">Default value for Streaming mode if not provided</param>
		/// <param name="read">Default value for Read mode if not provided</param>
		/// <param name="reverse">Default value for Reverse if not provided</param>
		/// <returns>Options with all the values filled</returns>
		public static FdbRangeOptions EnsureDefaults(FdbRangeOptions? options, int? limit, int? targetBytes, FdbStreamingMode mode, FdbReadMode read, bool reverse)
		{
			Contract.Debug.Requires((limit ?? 0) >= 0 && (targetBytes ?? 0) >= 0);

			if (options == null)
			{
				options = new FdbRangeOptions()
				{
					Limit = limit,
					TargetBytes = targetBytes,
					Mode = mode,
					Reverse = reverse,
					Read = read,
				};
			}
			else if (options.Limit == null || options.TargetBytes == null || options.Mode == null || options.Reverse == null || options.Read == null)
			{
				options = new FdbRangeOptions
				{
					Limit = options.Limit ?? limit,
					TargetBytes = options.TargetBytes ?? targetBytes,
					Mode = options.Mode ?? mode,
					Read = options.Read ?? read,
					Reverse = options.Reverse ?? reverse
				};
			}

			Contract.Debug.Ensures(options.Mode != null && options.Reverse != null);
			Contract.Debug.Ensures((options.Limit ?? 0) >= 0, "Limit cannot be negative");
			Contract.Debug.Ensures((options.TargetBytes ?? 0) >= 0, "TargetBytes cannot be negative");
			Contract.Debug.Ensures(options.Mode.HasValue && Enum.IsDefined(typeof(FdbStreamingMode), options.Mode.Value), "Streaming mode must be valid");
			Contract.Debug.Ensures(options.Read.HasValue && Enum.IsDefined(typeof(FdbReadMode), options.Read.Value), "Reading mode must be valid");

			return options;
		}

		/// <summary>Throws if values are not legal</summary>
		public void EnsureLegalValues()
		{
			EnsureLegalValues(this.Limit ?? 0, this.TargetBytes ?? 0, this.Mode ?? FdbStreamingMode.Iterator, this.Read ?? FdbReadMode.Both, 0);
		}

		internal static void EnsureLegalValues(int limit, int targetBytes, FdbStreamingMode mode, FdbReadMode read, int iteration)
		{
			if (limit < 0) throw InvalidOptionValue("Range Limit cannot be negative.");
			if (targetBytes < 0) throw InvalidOptionValue("Range TargetBytes cannot be negative.");
			if (mode < FdbStreamingMode.WantAll || mode > FdbStreamingMode.Serial) throw InvalidOptionValue("Range StreamingMode must be valid.");
			if (read < FdbReadMode.Both || read > FdbReadMode.Values) throw InvalidOptionValue("Range ReadMode must be valid.");
			if (iteration < 0) throw InvalidOptionValue("Iteration counter cannot be negative.");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static FdbException InvalidOptionValue(string message)
		{
			return new FdbException(FdbError.InvalidOptionValue, message);
		}

		/// <summary>Return the default range options</summary>
		[Pure]
		public static FdbRangeOptions FromDefault()
		{
			return new FdbRangeOptions();
		}

		[Pure]
		public FdbRangeOptions WithLimit(int limit)
		{
			return this.Limit == limit ? this : new FdbRangeOptions(this) { Limit = limit };
		}

		[Pure]
		public FdbRangeOptions WithTargetBytes(int targetBytes)
		{
			return this.TargetBytes == targetBytes ? this : new FdbRangeOptions(this) { TargetBytes = targetBytes };
		}

		[Pure]
		public FdbRangeOptions WithStreamingMode(FdbStreamingMode mode)
		{
			return this.Mode == mode ? this : new FdbRangeOptions(this) { Mode = mode };
		}

		[Pure]
		public FdbRangeOptions Reversed()
		{
			return this.Reverse.GetValueOrDefault() ? this : new FdbRangeOptions(this) { Reverse = true };
		}

		[Pure]
		public FdbRangeOptions OnlyKeys()
		{
			return this.Read == FdbReadMode.Keys ? this : new FdbRangeOptions(this) { Read = FdbReadMode.Keys };
		}

		[Pure]
		public FdbRangeOptions OnlyValues()
		{
			return this.Read == FdbReadMode.Values ? this : new FdbRangeOptions(this) { Read = FdbReadMode.Values };
		}

	}

}

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

namespace FoundationDB.Client
{
	using System.Buffers;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;

	/// <summary>Container class for options in a Range query</summary>
	[DebuggerDisplay("Limit={Limit}, Reverse={Reverse}, TargetBytes={TargetBytes}, Mode={Mode}, Read={Read}")]
	[PublicAPI]
	public sealed record FdbRangeOptions
	{

		#region Public Properties...

		/// <summary>Maximum number of items to return</summary>
		public int? Limit { get; init; }

		/// <summary>If <see langword="true"/> , results are returned in reverse order (from last to first)</summary>
		public bool Reverse { get; init; }

		/// <summary>Maximum number of bytes to read</summary>
		public int? TargetBytes { get; init; }

		/// <summary>Streaming mode</summary>
		public FdbStreamingMode? Mode { get; init; }

		/// <summary>Read only the keys, only the values, or both (default)</summary>
		public FdbReadMode? Read { get; init; }

		/// <summary>If specified, pool used to allocate the buffer that will hold the keys and values</summary>
		/// <remarks>If a pool is provided, then the <see cref="FdbRangeChunk"/> instance <b>MUST</b> be disposed, and the keys and values <b>CANNOT</b> be exposed outside the scope of the read operation</remarks>
		public ArrayPool<byte>? Pool { get; init; }

		#endregion

		#region Singletons...

		public static readonly FdbRangeOptions Default = new() { };

		public static readonly FdbRangeOptions Reversed = new() { Reverse = true };

		public static readonly FdbRangeOptions ValuesOnly = new() { Read = FdbReadMode.Values };

		public static readonly FdbRangeOptions KeysOnly = new() { Read = FdbReadMode.Keys };

		public static readonly FdbRangeOptions OnlyOne = new() { Limit = 1 };

		public static readonly FdbRangeOptions OnlyOneReversed = new() { Limit = 1, Reverse = true };

		public static readonly FdbRangeOptions WantAll = new() { Mode = FdbStreamingMode.WantAll };

		public static readonly FdbRangeOptions WantAllValuesOnly = new() { Mode = FdbStreamingMode.WantAll, Read = FdbReadMode.Values };

		public static readonly FdbRangeOptions WantAllKeysOnly = new() { Mode = FdbStreamingMode.WantAll, Read = FdbReadMode.Keys };

		public static readonly FdbRangeOptions WantAllReversed = new() { Mode = FdbStreamingMode.WantAll, Reverse = true };

		public static readonly FdbRangeOptions WantAllReversedValuesOnly = new() { Mode = FdbStreamingMode.WantAll, Read = FdbReadMode.Values, Reverse = true };

		public static readonly FdbRangeOptions WantAllReversedKeysOnly = new() { Mode = FdbStreamingMode.WantAll, Read = FdbReadMode.Keys, Reverse = true };

		#endregion

		/// <summary>Add all missing values from the provided defaults</summary>
		/// <param name="options">Options provided by the caller (can be null)</param>
		/// <param name="mode">Default value for Streaming mode if not provided</param>
		/// <returns>Options with all the values filled</returns>
		public static FdbRangeOptions EnsureDefaults(FdbRangeOptions? options, FdbStreamingMode mode, FdbReadMode read)
		{
			if (options == null)
			{
				if (mode == FdbStreamingMode.Iterator && read == FdbReadMode.Both)
				{
					options = FdbRangeOptions.Default;
				}
				else if (mode == FdbStreamingMode.WantAll && read == FdbReadMode.Both)
				{
					options = FdbRangeOptions.Default;
				}
				else
				{
					options = new()
					{
						Mode = mode,
						Read = read,
					};
				}
			}

			if ((options.Mode == null && mode != FdbStreamingMode.Iterator)
			  | (options.Read == null && read != FdbReadMode.Both))
			{
				// the default is Iterator, so only change if that is not what we want
				options = options with
				{
					Mode = options.Mode ?? mode,
					Read = options.Read ?? read,
				};
			}

			Contract.Debug.Ensures((options.Limit ?? 0) >= 0, "Limit cannot be negative");
			Contract.Debug.Ensures((options.TargetBytes ?? 0) >= 0, "TargetBytes cannot be negative");
			Contract.Debug.Ensures(options.Mode == null || Enum.IsDefined(typeof(FdbStreamingMode), options.Mode.Value), "Streaming mode must be valid");
			Contract.Debug.Ensures(options.Read == null || Enum.IsDefined(typeof(FdbReadMode), options.Read.Value), "Reading mode must be valid");

			return options;
		}

		/// <summary>Throws if values are not legal</summary>
		public void EnsureLegalValues(int iteration)
		{
			EnsureLegalValues(this.Limit, this.TargetBytes, this.Mode, this.Read, iteration);
		}

		internal static void EnsureLegalValues(int? limit, int? targetBytes, FdbStreamingMode? mode, FdbReadMode? read, int iteration)
		{
			if (limit < 0) throw InvalidOptionValue("Range Limit cannot be negative.");
			if (targetBytes < 0) throw InvalidOptionValue("Range TargetBytes cannot be negative.");
			if (mode is < FdbStreamingMode.WantAll or > FdbStreamingMode.Serial) throw InvalidOptionValue("Range StreamingMode must be valid.");
			if (read is < FdbReadMode.Both or > FdbReadMode.Values) throw InvalidOptionValue("Range ReadMode must be valid.");
			if (iteration < 0) throw InvalidOptionValue("Iteration counter cannot be negative.");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static FdbException InvalidOptionValue(string message)
		{
			return new FdbException(FdbError.InvalidOptionValue, message);
		}

		/// <summary>Return the default range options</summary>
		[Pure]
		public static FdbRangeOptions FromDefault() => new();

		[Pure]
		public FdbRangeOptions WithLimit(int limit)
			=> this.Limit == limit ? this : this with { Limit = limit };

		[Pure]
		public FdbRangeOptions WithTargetBytes(int targetBytes)
			=> this.TargetBytes == targetBytes ? this : this with { TargetBytes = targetBytes };

		[Pure]
		public FdbRangeOptions WithStreamingMode(FdbStreamingMode mode)
			=> this.Mode == mode ? this : this with { Mode = mode };

		[Pure]
		public FdbRangeOptions InReverse()
			=> this.Reverse ? this
			: ReferenceEquals(this, FdbRangeOptions.Default) ? FdbRangeOptions.WantAllReversed
			: ReferenceEquals(this, FdbRangeOptions.WantAll) ? FdbRangeOptions.WantAllReversed
			: ReferenceEquals(this, FdbRangeOptions.WantAllValuesOnly) ? FdbRangeOptions.WantAllReversedValuesOnly
			: ReferenceEquals(this, FdbRangeOptions.WantAllKeysOnly) ? FdbRangeOptions.WantAllReversedKeysOnly
			: this with { Reverse = true };

		[Pure]
		public FdbRangeOptions OnlyKeys()
			=> this.Read == FdbReadMode.Keys ? this : this with { Read = FdbReadMode.Keys };

		[Pure]
		public FdbRangeOptions OnlyValues()
			=> this.Read == FdbReadMode.Values ? this : this with { Read = FdbReadMode.Values };

	}

}

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

	/// <summary>Container class for options in a Range query</summary>
	[DebuggerDisplay("Limit={Limit}, Reverse={Reverse}, TargetBytes={TargetBytes}, Mode={Mode}")]
	public sealed class FdbRangeOptions
	{
		#region Public Properties...

		/// <summary>Maximum number of items to return</summary>
		public int? Limit { get; set; }

		/// <summary>If true, results are returned in reverse order (from last to rist)</summary>
		public bool? Reverse { get; set; }

		/// <summary>Maximum number of bytes to read</summary>
		public int? TargetBytes { get; set; }

		/// <summary>Streaming mode</summary>
		public FdbStreamingMode? Mode { get; set; }

		#endregion

		#region Constructors...

		/// <summary>Create a new empty set of options</summary>
		public FdbRangeOptions()
		{ }

		/// <summary>Create a new set of options</summary>
		public FdbRangeOptions(int? limit, bool? reverse = null, int? targetBytes = null, FdbStreamingMode? mode = null)
		{
			this.Limit = limit;
			this.Reverse = reverse;
			this.TargetBytes = targetBytes;
			this.Mode = mode;
		}

		/// <summary>Copy an existing set of options</summary>
		/// <param name="options"></param>
		public FdbRangeOptions(FdbRangeOptions options)
		{
			this.Limit = options.Limit;
			this.Reverse = options.Reverse;
			this.TargetBytes = options.TargetBytes;
			this.Mode = options.Mode;
		}

		#endregion

		/// <summary>Add all missing values from the provided defaults</summary>
		/// <param name="options">Options provided by the caller (can be null)</param>
		/// <param name="limit">Default value for Limit if not provided</param>
		/// <param name="targetBytes">Default TargetBytes for limit if not provided</param>
		/// <param name="mode">Default value for StreamingMode if not provided</param>
		/// <param name="reverse">Default value for Reverse if not provided</param>
		/// <returns>Options with all the values filled</returns>
		public static FdbRangeOptions EnsureDefaults(FdbRangeOptions options, int limit, int targetBytes, FdbStreamingMode mode, bool reverse)
		{
			Contract.Requires(limit >= 0 && targetBytes >= 0 && Enum.IsDefined(typeof(FdbStreamingMode), mode));

			if (options == null)
			{
				options = new FdbRangeOptions()
				{
					Limit = limit,
					TargetBytes = targetBytes,
					Mode = mode,
					Reverse = reverse
				};
			}
			else if (options.Limit == null || options.TargetBytes == null || options.Mode == null || options.Reverse == null)
			{
				options = new FdbRangeOptions()
				{
					Limit = options.Limit ?? limit,
					TargetBytes = options.TargetBytes ?? targetBytes,
					Mode = options.Mode ?? mode,
					Reverse = options.Reverse ?? reverse
				};
			}

			Contract.Ensures(options != null && options.Limit != null && options.TargetBytes != null && options.Mode != null && options.Reverse != null);
			Contract.Ensures(options.Limit >= 0, null, "Limit cannot be negative");
			Contract.Ensures(options.TargetBytes >= 0, null, "TargetBytes cannot be negative");
			Contract.Ensures(options.Mode.HasValue && Enum.IsDefined(typeof(FdbStreamingMode), options.Mode.Value), null, "Streaming mode must be valid");

			return options;
		}

		/// <summary>Throws if values are not legal</summary>
		public void EnsureLegalValues()
		{
			if (this.Limit < 0) throw new FdbException(FdbError.InvalidOptionValue, "Range Limit cannot be negative");
			if (this.TargetBytes < 0) throw new FdbException(FdbError.InvalidOptionValue, "Range TargetBytes cannot be negative");
			if (this.Mode < FdbStreamingMode.WantAll || this.Mode > FdbStreamingMode.Serial) throw new FdbException(FdbError.InvalidOptionValue, "Range StreaminMode must be valid");
		}

		public FdbRangeOptions WithLimit(int limit)
		{
			return this.Limit == limit ? this : new FdbRangeOptions(this) { Limit = limit };
		}

		public FdbRangeOptions WithTargetBytes(int targetBytes)
		{
			return this.TargetBytes == targetBytes ? this : new FdbRangeOptions(this) { TargetBytes = targetBytes };
		}

		public FdbRangeOptions WithStreamingMode(FdbStreamingMode mode)
		{
			return this.Mode == mode ? this : new FdbRangeOptions(this) { Mode = mode };
		}

		public FdbRangeOptions Reversed()
		{
			return this.Reverse.GetValueOrDefault() ? this : new FdbRangeOptions(this) { Reverse = true };
		}
	}


}

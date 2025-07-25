#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace FoundationDB.Client.Tests
{

	[DebuggerDisplay("Path={Path}, Key={Key}")]
	public sealed class FakeSubspace : KeySubspace
	{

		public FakeSubspace(FdbPath path, Slice prefix, ISubspaceContext? context = null)
			: base(prefix, context ?? SubspaceContext.Default)
		{
			this.Path = path;
		}

		public FdbPath Path { get; }

		public Slice Prefix { get; }

		/// <summary>Returns a user-friendly description of this directory</summary>
		public override string ToString(string? format, IFormatProvider? provider = null)
		{
			switch (format ?? "")
			{
				case "" or "D" or "d" or "P" or "p":
				{
					return this.Path.ToString();
				}
				case "K" or "k":
				{
					return FdbKey.Dump(this.GetPrefix());
				}
				case "X" or "x":
				{
					return this.GetPrefix().ToString(format);
				}
				case "G" or "g":
				{
					return $"FakeSubspace(path={this.Path}, key={FdbKey.Dump(this.GetPrefix())})";
				}
				default:
				{
					throw new FormatException("Unsupported format");
				}
			}
		}

	}

}

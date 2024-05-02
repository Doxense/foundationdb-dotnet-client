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

namespace Doxense.Serialization.Json
{
	using System.Diagnostics;

	/// <summary>Structure that holds the cached serialization metadata for a field or property of a class or struct</summary>
	[DebuggerDisplay("Name={Name}, Type={Type}")]
	public record CrystalJsonMemberDefinition : ICrystalMemberDefinition
	{
		/// <summary>Name of the member</summary>
		public required string Name { get; init; }

		/// <summary>Declared type of the member</summary>
		public required Type Type { get; init; }

		/// <summary>Optional <see cref="JsonPropertyAttribute"/> attribute that was applied to this member</summary>
		public JsonPropertyAttribute? Attributes { get; init; }

		/// <summary>Default value for this member (when it is missing)</summary>
		public object? DefaultValue { get; init; }

		/// <summary>Flag set to <see langword="true"/> when the member is read-only or init-only</summary>
		public bool ReadOnly { get; init; }

		/// <summary>Func that can return the value of this member in an instance of the containing type</summary>
		public required Func<object, object?> Getter { get; init; }

		/// <summary>Func that can change the value of this member in an instance of the containing type</summary>
		public Action<object, object?>? Setter { get; init; }

		/// <summary>Delegate that can serialize values of this member into JSON</summary>
		public required CrystalJsonTypeVisitor Visitor { get; init; }

		/// <summary>Delegate that can bind JSON values to a type that is assignable to this member</summary>
		public required CrystalJsonTypeBinder Binder { get; init; }

		/// <summary>Returns <see langword="true"/> if a possible value for this member is the default value for this member's type (<see langword="null"/> for ref types or Nullable&lt;T&gt;, <see langword="0"/> for numbers, <see langword="false"/> for booleans, ...)</summary>
		public bool IsDefaultValue(object? value) => this.DefaultValue?.Equals(value) ?? (value is null);

	}

}

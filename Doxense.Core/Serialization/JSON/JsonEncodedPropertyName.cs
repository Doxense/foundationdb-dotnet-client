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
	using System.Diagnostics.CodeAnalysis;
	using System.Runtime.CompilerServices;
	using Doxense.Web;

	/// <summary>Cache for the various encoded forms of a JSON property name</summary>
	[DebuggerDisplay("{Value}")]
	public readonly struct JsonEncodedPropertyName : IEquatable<JsonEncodedPropertyName>, IEquatable<string>, IFormattable
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonEncodedPropertyName(string value)
		{
			this.Value = value;
			this.JsonLiteral = JsonEncoding.Encode(value);
			this.JavaScriptLiteral = JavaScriptEncoding.EncodePropertyName(value);
		}

		/// <summary>Original non-encoded name of the property (ex: `<c>fooBar</c>` or `<c>hello'world"!!!</c>`)</summary>
		public readonly string Value;

		/// <summary>Name encoded as the field name of a JSON Object (ex: `<c>"fooBar"</c>` or `<c>"hello'world\"!!!"</c>`)</summary>
		/// <remarks>Includes the double-quotes</remarks>
		public readonly string JsonLiteral;

		/// <summary>Name encoded as the field name of a Javascript Object (ex: <c>fooBar</c> (no quotes) or <c>'hello\'world"!!!'</c> (with quotes))</summary>
		/// <remarks>Includes the single quotes <i>if required</i></remarks>
		public readonly string JavaScriptLiteral;

		/// <inheritdoc />
		public override string ToString() => this.Value;

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => this.Value;

		/// <inheritdoc />
		public override int GetHashCode() => this.Value.GetHashCode();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj is JsonEncodedPropertyName name && name.Value == this.Value;

		/// <inheritdoc />
		public bool Equals(JsonEncodedPropertyName other) => other.Value == this.Value;

		/// <inheritdoc />
		public bool Equals(string? other) => other == this.Value;

		public bool Equals(ReadOnlySpan<char> other) => other.SequenceEqual(this.Value);

	}

}

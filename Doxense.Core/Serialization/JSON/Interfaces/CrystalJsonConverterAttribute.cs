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

namespace Doxense.Serialization.Json
{

	/// <summary>Marker attribute to enable JSON source code generation</summary>
	/// <remarks>
	/// <para>This attribute should be applied on a partial class, that will act as a container for all generated types.</para>
	/// <para>All "root" data types should be included via the <see cref="CrystalJsonSerializableAttribute"/> attribute</para>
	/// <para>Sample: <code>
	/// [CrystalJsonConverter]
	/// [CrystalJsonSerializable(typeof(User))]
	/// [CrystalJsonSerializable(typeof(Product))]
	/// // ... one for each "top level" type, nested/linked types are automatically discovered
	/// public static partial class ApplicationSerializers
	/// {
	///		// generated code will be inserted here
	/// }
	/// </code></para>
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class)]
	public sealed class CrystalJsonConverterAttribute : Attribute
	{

		//TODO: default settings!

	}

	/// <summary>Attribute that will generate a converter for the specified type</summary>
	/// <remarks>Any nested type, or types referenced by the members will also be included in the source code generation</remarks>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public sealed class CrystalJsonSerializableAttribute : Attribute
	{

		public CrystalJsonSerializableAttribute(Type type)
		{
			this.Types = [ type ];
		}

		public CrystalJsonSerializableAttribute(params Type[] types)
		{
			this.Types = types;
		}

		public Type[] Types { get; set; }

	}

}

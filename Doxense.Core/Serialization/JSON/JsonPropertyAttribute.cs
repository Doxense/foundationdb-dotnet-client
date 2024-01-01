﻿#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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
	using System;

	/// <summary>Attribute that controls how a field or property is serialized into JSON</summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	public class JsonPropertyAttribute : Attribute
	{
		public JsonPropertyAttribute()
		{ }

		public JsonPropertyAttribute(string propertyName)
		{
			this.PropertyName = propertyName;
		}

		/// <summary>Name of the property</summary>
		/// <remarks>If not specified, will use the exact name of the field or property</remarks>
		public string? PropertyName { get; set; }

		/// <summary>Default value for this property</summary>
		/// <remarks>If not specified, will use the default value for the member type (null, false, 0, ...)</remarks>
		public object? DefaultValue { get; set; }

		/// <summary>Defines if this field should be always serialized as strings (true), as integers (false), or according to the runtime json settings (null)</summary>
		public JsonEnumFormat EnumFormat { get; set; }

	}

	public enum JsonEnumFormat
	{
		Inherits = 0,
		Number,
		String
	}
}

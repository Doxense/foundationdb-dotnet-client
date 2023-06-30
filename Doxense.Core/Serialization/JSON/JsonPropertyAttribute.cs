#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
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


namespace Doxense.Serialization.Json.CodeGen
{
	using Microsoft.CodeAnalysis;


	/// <summary>Metadata about the container type that will host the generated code for one or more types</summary>
	public sealed record CrystalJsonContainerMetadata
	{
		public required string Name { get; init; }
		
		public required TypeRef Symbol { get; init; }
		
		public required AttributeData? Attribute { get; init; }

		public required ImmutableEquatableArray<CrystalJsonTypeMetadata> IncludedTypes { get; init; }
		
	}
	
	/// <summary>Metadata about a serialized type</summary>
	public sealed record CrystalJsonTypeMetadata
	{
		
		/// <summary>Symbol for the serialized type</summary>
		public required TypeRef Symbol { get; init; }

		/// <summary>Friendly name of the type, used as the prefix of the generated converters (ex: "User", "Account", "Order", ...)</summary>
		public string Name => this.Symbol.Name;

		/// <summary>Namespace of the type (ex: "Contoso.Backend.Models")</summary>
		public string NameSpace => this.Symbol.NameSpace;

		/// <summary>For objects, list of included members in this type</summary>
		public required ImmutableEquatableArray<CrystalJsonMemberMetadata> Members { get; init; }
		
	}

	/// <summary>Metadata about a member (field or property) of a serialized type</summary>
	public sealed record CrystalJsonMemberMetadata
	{
		
		/// <summary>Name, as serialized in the JSON output</summary>
		public required string Name { get; init; }
		
		/// <summary>Type of the member</summary>
		public required TypeRef Type { get; init; }
		
		/// <summary>Name of the member in the container type</summary>
		public required string MemberName { get; init; }

		/// <summary><c>true</c> if the member is a field, <c>false</c> if it is a property</summary>
		public required bool IsField { get; init; } // true = field, false = prop

		/// <summary><c>true</c> if the member is read-only</summary>
		/// <remarks>For properties, this means there is not SetMethod.</remarks>
		public required bool IsReadOnly { get; init; }

		/// <summary><c>true</c> if the member is init-only</summary>
		public required bool IsInitOnly { get; init; }
		
		public required bool IsKey { get; init; }
		
	}
	
}

#if NETSTANDARD2_0

namespace System.Diagnostics.CodeAnalysis
{

	[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
	internal sealed class SetsRequiredMembersAttribute : Attribute
	{ }

}

namespace System.Runtime.CompilerServices
{
	using System.ComponentModel;

	/// <summary>Reserved to be used by the compiler for tracking metadata.
	/// This class should not be used by developers in source code.</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static class IsExternalInit
	{
	}
	
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	internal sealed class RequiredMemberAttribute : Attribute
	{ }

	[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
	internal sealed class CompilerFeatureRequiredAttribute : Attribute
	{
		public CompilerFeatureRequiredAttribute(string featureName) => this.FeatureName = featureName;
		public string FeatureName { get; }
		public bool IsOptional { get; init; }
		public const string RefStructs = nameof(RefStructs);
		public const string RequiredMembers = nameof(RequiredMembers);
	}

}
#endif

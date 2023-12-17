// add various attribute shims that are missing from .NET 6.0, but required for the latest c# language versions

#if !NET8_0_OR_GREATER

namespace System.Diagnostics.CodeAnalysis
{

	[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
	internal sealed class SetsRequiredMembersAttribute : Attribute
	{ }

}

namespace System.Runtime.CompilerServices
{
	using System.ComponentModel;

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

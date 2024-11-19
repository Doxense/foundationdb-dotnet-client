
namespace Doxense.Serialization.Json.CodeGen
{
	using Microsoft.CodeAnalysis;

	/// <summary>
	/// An equatable value representing type identity.
	/// </summary>
	[DebuggerDisplay("Name = {Name}")]
	public sealed class TypeRef : IEquatable<TypeRef>
	{
		public TypeRef(ITypeSymbol type)
		{
			this.Name = type.Name;
			this.NameSpace = type.ContainingNamespace.ToDisplayString();
			this.FullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			this.IsValueType = type.IsValueType;
			this.TypeKind = type.TypeKind;
			this.SpecialType = type.OriginalDefinition.SpecialType;
		}

		public string Name { get; }

		public string NameSpace { get; }
		
		/// <summary>
		/// Fully qualified assembly name, prefixed with "global::", e.g. global::System.Numerics.BigInteger.
		/// </summary>
		public string FullyQualifiedName { get; }

		public bool IsValueType { get; }
		public TypeKind TypeKind { get; }
		public SpecialType SpecialType { get; }

		public bool CanBeNull => !this.IsValueType || this.SpecialType is SpecialType.System_Nullable_T;

		public bool Equals(TypeRef? other) => other != null && this.FullyQualifiedName == other.FullyQualifiedName;
		public override bool Equals(object? obj) => this.Equals(obj as TypeRef);
		public override int GetHashCode() => this.FullyQualifiedName.GetHashCode();
	}
}

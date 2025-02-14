#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System.Runtime.CompilerServices;

	public readonly record struct ObservableJsonPath
	{

		public static readonly ObservableJsonPath Root = default;

		public ObservableJsonPath()
		{
			this.Parent = null;
			this.Key = null;
			this.Index = null;
		}

		public ObservableJsonPath(ObservableJsonValue parent, string key)
		{
			Contract.Debug.Requires(parent != null && key != null);
			this.Parent = parent;
			this.Key = key;
			this.Index = null;
		}

		public ObservableJsonPath(ObservableJsonValue parent, Index index)
		{
			Contract.Debug.Requires(parent != null);
			this.Parent = parent;
			this.Key = null;
			this.Index = index;
		}

		/// <summary>Parent of this value, or <see langword="null"/> if this is the root of the document</summary>
		public readonly ObservableJsonValue? Parent;

		/// <summary>Name of the field that contains this value in its parent object, or <see langword="null"/> if it was not part of an object</summary>
		public readonly string? Key;

		/// <summary>Position of this value in its parent array, or <see langword="null"/> if it was not part of an array</summary>
		public readonly Index? Index;
		//REVIEW: maybe not nullable? if Key != null then Index should be ignored (0), and if Key == null, then it is present?

		public bool IsRoot() => this.Parent == null && this.Key == null && this.Index == null;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void NotifyParent(ObservableJsonValue child)
		{
			this.Parent?.NotifyChildChanged(child, this.Key, this.Index);
		}

		public override string ToString()
		{
			return ObservableJson.ComputePath(this.Parent, this.Key, this.Index) ?? "";
		}

	}

}

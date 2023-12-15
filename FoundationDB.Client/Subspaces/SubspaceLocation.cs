#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace FoundationDB.Client
{
	using System;
	using System.Diagnostics;
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Serialization.Encoders;
	using JetBrains.Annotations;

	/// <summary>Represents the path to a specific subspace in the database</summary>
	/// <remarks>A path can be <see cref="ISubspaceLocation{TSubspace}.Resolve">resolved</see> into the actual <see cref="IKeySubspace"/> implementation that will be valid within the context of a transaction.</remarks>
	public interface ISubspaceLocation : IEquatable<ISubspaceLocation>
	{

		// Path and/or Prefix can be empty!
		// - Path: empty, Prefix: empty => no prefix at all (root of the database!)
		// - Path: empty, Prefix: not empty => a traditional subspace located under the root of the database (not using the Directory Layer)
		// - Path: not empty, Prefix: empty => the content of a directory subspace (no subspaces)
		// - Path: not empty, Prefix: not empty => a directory subspace that has been divided into another local subspace

		/// <summary>Path of the <see cref="FdbDirectorySubspace">directory subspace</see> that contains the subspace.</summary>
		/// <remarks>
		/// This path is always absolute. Relative paths are not allowed.
		/// Can be <see cref="FdbPath.Root"/> if this subspace is located at the root of the database (ie: not using the Directory Layer)</remarks>
		FdbPath Path { get; }

		/// <summary>Optional key prefix of the subspace, added immediately after the prefix of the <see cref="FdbDirectorySubspace">subspace</see> resolved by the <see cref="Path"/>.</summary>
		Slice Prefix { get; }

		/// <summary>Encoding used to serialize keys in this subspace</summary>
		/// <remarks>The default is to use the <see cref="TuPack"/> encoding, but it can be any other custom encoding.</remarks>
		IKeyEncoding Encoding { get; }

		ValueTask<IKeySubspace?> Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null);
	}

	/// <summary>Represents the path to a typed subspace in the database</summary>
	/// <typeparam name="TSubspace">Type of the <see cref="IKeySubspace"/> implementation that this path will resolve into.</typeparam>
	public interface ISubspaceLocation<TSubspace> : ISubspaceLocation
		where TSubspace : class, IKeySubspace
	{
		/// <summary>Return the actual subspace that corresponds to this location</summary>
		/// <param name="tr">Current transaction</param>
		/// <param name="directory"><see cref="FdbDirectoryLayer">DirectoryLayer</see> instance to use for the resolve. If null, uses the default database directory layer.</param>
		/// <returns>Key subspace using the resolved key prefix of this location in the context of the current transaction, or null if the directory does not exist</returns>
		/// <remarks>
		/// The instance resolved for this transaction SHOULD NOT be used in the context of a different transaction, because its location in the Directory Layer may have been changed concurrently!
		/// Re-using cached subspace instances MAY lead to DATA CORRUPTION if not used carefully! The best practice is to re-<see cref="Resolve"/>() the subspace again in each new transaction opened.
		/// </remarks>
		new ValueTask<TSubspace?> Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null);
	}

	/// <summary>Default implementation of a subspace location</summary>
	/// <typeparam name="TSubspace">Type of the concrete <see cref="IKeySubspace"/> implementation that this location will resolve to</typeparam>
	[DebuggerDisplay("Path={Path}, Prefix={Prefix}, Encoding={Encoding}")]
	public abstract class SubspaceLocation<TSubspace> : ISubspaceLocation<TSubspace>
		where TSubspace : class, IKeySubspace
	{

		/// <inheritdoc />
		public FdbPath Path { get; } // can be empty

		/// <inheritdoc />
		public Slice Prefix { get; } // can be empty

		/// <inheritdoc />
		public abstract IKeyEncoding Encoding { get; }

		protected SubspaceLocation(in FdbPath path, in Slice prefix)
		{
			this.Path = path;
			this.Prefix = prefix.IsNull ? Slice.Empty : prefix;
		}

		public override string ToString()
		{
			if (this.Path.Count == 0)
			{
				return this.Prefix.ToString();
			}
			if (this.Prefix.Count == 0)
			{
				return "[" + this.Path.ToString() + "]";
			}
			return "[" + this.Path.ToString() + "]+" + this.Prefix.ToString();
		}

		public bool IsTopLevel => this.Path.Count == 0;

		async ValueTask<IKeySubspace?> ISubspaceLocation.Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory)
		{
			return await Resolve(tr, directory);
		}

		public abstract ValueTask<TSubspace?> Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null);

		public override bool Equals(object obj) => obj is ISubspaceLocation path && Equals(path);

		public abstract bool Equals(ISubspaceLocation other);

		public abstract override int GetHashCode();

	}

	/// <summary>Path to a subspace that can represent binary keys only</summary>
	/// <remarks>Instance of this type can be <see cref="ISubspaceLocation{TSubspace}.Resolve">resolved</see> into an actual <see cref="IDynamicKeySubspace"/> valid for a specific transaction</remarks>
	public sealed class BinaryKeySubspaceLocation : SubspaceLocation<IBinaryKeySubspace>
	{

		public override IKeyEncoding Encoding => BinaryEncoding.Instance;

		public BinaryKeySubspaceLocation(in Slice suffix) : base(default, suffix)
		{ }

		public BinaryKeySubspaceLocation(in FdbPath path, in Slice suffix)
			: base(path, suffix)
		{ }

		public override int GetHashCode() => HashCodes.Combine(this.Path.GetHashCode(), this.Prefix.GetHashCode(), 0x12345678);

		public override bool Equals(ISubspaceLocation other) =>
			object.ReferenceEquals(other, this)
			|| (other is BinaryKeySubspaceLocation bin && bin.Path == this.Path && bin.Prefix == other.Prefix);

		public override ValueTask<IBinaryKeySubspace?> Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
		{
			if (this.IsTopLevel)
			{ // not contained in a directory subspace
				return new ValueTask<IBinaryKeySubspace?>(new BinaryKeySubspace(this.Prefix, SubspaceContext.Default));
			}
			return ResolveWithDirectory(tr, directory);
		}

		private async ValueTask<IBinaryKeySubspace?> ResolveWithDirectory(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory)
		{
			// located inside a directory subspace!
			var folder = await (directory ?? tr.Context.Database.DirectoryLayer).TryOpenCachedAsync(tr, this.Path);
			if (folder == null) return null;
			if (this.Prefix.Count == 0) return folder;
			return new BinaryKeySubspace(folder.GetPrefix() + this.Prefix, folder.Context);
		}

		public BinaryKeySubspaceLocation this[Slice prefix] => prefix.Count != 0 ? new BinaryKeySubspaceLocation(this.Path, this.Prefix + prefix) : this;

	}

	public interface IDynamicKeySubspaceLocation : ISubspaceLocation<IDynamicKeySubspace>
	{
		IDynamicKeyEncoder Encoder { get; }
	}

	/// <summary>Path to a subspace that can represent dynamic keys of any size and type</summary>
	/// <remarks>Instance of this type can be <see cref="ISubspaceLocation{TSubspace}.Resolve">resolved</see> into an actual <see cref="IDynamicKeySubspace"/> valid for a specific transaction</remarks>
	public sealed class DynamicKeySubspaceLocation : SubspaceLocation<IDynamicKeySubspace>, IDynamicKeySubspaceLocation
	{

		public IDynamicKeyEncoder Encoder { get; }

		public override IKeyEncoding Encoding => this.Encoder.Encoding;

		public DynamicKeySubspaceLocation(in FdbPath path, in Slice suffix, IDynamicKeyEncoder encoder)
			: base(path, suffix)
		{
			Contract.NotNull(encoder);
			this.Encoder = encoder;
		}

		public DynamicKeySubspaceLocation(in Slice prefix, IDynamicKeyEncoder encoder)
			: base(default, prefix)
		{
			Contract.NotNull(encoder);
			this.Encoder = encoder;
		}

		public static readonly DynamicKeySubspaceLocation Root = new DynamicKeySubspaceLocation(Slice.Empty, TuPack.Encoding.GetDynamicKeyEncoder());

		public static DynamicKeySubspaceLocation Create(Slice prefix) => new DynamicKeySubspaceLocation(prefix, TuPack.Encoding.GetDynamicKeyEncoder());

		public static DynamicKeySubspaceLocation Create(Slice prefix, IDynamicKeyEncoder? encoder) => new DynamicKeySubspaceLocation(prefix, encoder ?? TuPack.Encoding.GetDynamicKeyEncoder());

		public static DynamicKeySubspaceLocation Create(Slice prefix, IDynamicKeyEncoding? encoding) => new DynamicKeySubspaceLocation(prefix, (encoding ?? TuPack.Encoding).GetDynamicKeyEncoder());

		public override int GetHashCode() => HashCodes.Combine(this.Path.GetHashCode(), this.Prefix.GetHashCode(), 0x12344321);

		public override bool Equals(ISubspaceLocation? other) =>
			object.ReferenceEquals(other, this)
			|| (other is DynamicKeySubspaceLocation dyn && dyn.Encoding == this.Encoding && dyn.Path == this.Path && dyn.Prefix == other.Prefix);

		public override ValueTask<IDynamicKeySubspace?> Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
		{
			Contract.NotNull(tr);

			if (this.IsTopLevel)
			{ // not contained in a directory subspace
				return new ValueTask<IDynamicKeySubspace?>(new DynamicKeySubspace(this.Prefix, this.Encoder, SubspaceContext.Default));
			}

			return ResolveWithDirectory(tr, directory);
		}

		private async ValueTask<IDynamicKeySubspace?> ResolveWithDirectory(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory)
		{
			// located inside a directory subspace!
			var folder = await (directory ?? tr.Context.Database.DirectoryLayer).TryOpenCachedAsync(tr, this.Path);
			if (folder == null) return null;
			return this.Prefix.Count == 0 ? folder : new DynamicKeySubspace(folder.GetPrefix() + this.Prefix, folder.KeyEncoder, folder.Context);
		}

		public DynamicKeySubspaceLocation this[Slice prefix] => prefix.Count != 0 ? new DynamicKeySubspaceLocation(this.Path, this.Prefix + prefix, this.Encoder) : this;

		public DynamicKeySubspaceLocation this[byte[] prefix] => this[prefix.AsSlice()];

		public DynamicKeySubspaceLocation this[ReadOnlySpan<byte> prefix] => prefix.Length != 0 ? new DynamicKeySubspaceLocation(this.Path, this.Prefix.Concat(prefix), this.Encoder) : this;

		public DynamicKeySubspaceLocation this[IVarTuple tuple] => new DynamicKeySubspaceLocation(this.Path, this.Encoding.GetDynamicKeyEncoder().Pack(this.Prefix, tuple), this.Encoder);

		public DynamicKeySubspaceLocation ByKey<T1>(T1 item1) => new DynamicKeySubspaceLocation(this.Path, this.Encoding.GetDynamicKeyEncoder().Pack(this.Prefix, STuple.Create(item1)), this.Encoder);

		public DynamicKeySubspaceLocation ByKey<T1, T2>(T1 item1, T2 item2) => new DynamicKeySubspaceLocation(this.Path, this.Encoding.GetDynamicKeyEncoder().Pack(this.Prefix, STuple.Create(item1, item2)), this.Encoder);

		public DynamicKeySubspaceLocation ByKey<T1, T2, T3>(T1 item1, T2 item2, T3 item3) => new DynamicKeySubspaceLocation(this.Path, this.Encoding.GetDynamicKeyEncoder().Pack(this.Prefix, STuple.Create(item1, item2, item3)), this.Encoder);

		//TODO: more?
	}

	/// <summary>Path to a subspace that can represent keys of a specific type</summary>
	/// <typeparam name="T1">Type of the key</typeparam>
	/// <remarks>Instance of this type can be <see cref="ISubspaceLocation{TSubspace}.Resolve">resolved</see> into an actual <see cref="ITypedKeySubspace{T1}"/> valid for a specific transaction</remarks>
	public sealed class TypedKeySubspaceLocation<T1> : SubspaceLocation<ITypedKeySubspace<T1>>
	{

		/// <summary>Encoder used by keys stored at that location</summary>
		public IKeyEncoder<T1> Encoder { get; }

		/// <inheritdoc/>
		public override IKeyEncoding Encoding => this.Encoder.Encoding;

		public TypedKeySubspaceLocation(Slice suffix, IKeyEncoder<T1> encoder)
			: this(default, suffix, encoder)
		{ }

		public TypedKeySubspaceLocation(FdbPath path, Slice suffix, IKeyEncoder<T1> encoder)
			: base(path, suffix)
		{
			Contract.NotNull(encoder);
			this.Encoder = encoder;
		}

		public override bool Equals(object obj) => obj is ISubspaceLocation path && Equals(path);

		public override int GetHashCode() => HashCodes.Combine(this.Path.GetHashCode(), this.Prefix.GetHashCode(), 0x1111);

		public override bool Equals(ISubspaceLocation other) =>
			object.ReferenceEquals(other, this)
			|| (other is TypedKeySubspaceLocation<T1> typed && typed.Encoding == this.Encoding && typed.Path == this.Path && typed.Prefix == other.Prefix);
		
		/// <inheritdoc/>
		public override ValueTask<ITypedKeySubspace<T1>?> Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
		{
			Contract.NotNull(tr);

			if (this.IsTopLevel)
			{ // not contained in a directory subspace
				return new ValueTask<ITypedKeySubspace<T1>?>(new TypedKeySubspace<T1>(this.Prefix, this.Encoder, SubspaceContext.Default));
			}

			// we have to use the DirectoryLayer to resolve the subspace
			return ResolveWithDirectory(tr, directory);
		}

		private async ValueTask<ITypedKeySubspace<T1>?> ResolveWithDirectory(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory)
		{
			Contract.Debug.Requires(this.Path.Count != 0);

			var folder = await (directory ?? tr.Context.Database.DirectoryLayer).TryOpenCachedAsync(tr, this.Path);
			if (folder == null) return null;
			return new TypedKeySubspace<T1>(folder.GetPrefix() + this.Prefix, this.Encoder, folder.Context);
		}

	}

	/// <summary>Path to a subspace that can represent composite keys of a specific type</summary>
	/// <typeparam name="T1">Type of the first key</typeparam>
	/// <typeparam name="T2">Type of the second key</typeparam>
	/// <remarks>Instance of this type can be <see cref="ISubspaceLocation{TSubspace}.Resolve">resolved</see> into an actual <see cref="ITypedKeySubspace{T1, T2}"/> valid for a specific transaction</remarks>
	public sealed class TypedKeySubspaceLocation<T1, T2> : SubspaceLocation<ITypedKeySubspace<T1, T2>>
	{
		public ICompositeKeyEncoder<T1, T2> Encoder { get; }

		/// <inheritdoc/>
		public override IKeyEncoding Encoding => this.Encoder.Encoding;

		public TypedKeySubspaceLocation(Slice suffix, ICompositeKeyEncoder<T1, T2> encoder)
			: this(default, suffix, encoder)
		{ }

		public TypedKeySubspaceLocation(FdbPath path, Slice suffix, ICompositeKeyEncoder<T1, T2> encoder)
			: base(path, suffix)
		{
			Contract.NotNull(encoder);
			this.Encoder = encoder;
		}

		public override int GetHashCode() => HashCodes.Combine(this.Path.GetHashCode(), this.Prefix.GetHashCode(), 0x2222);

		public override bool Equals(ISubspaceLocation other) =>
			object.ReferenceEquals(other, this)
			|| (other is TypedKeySubspaceLocation<T1, T2> typed && typed.Encoding == this.Encoding && typed.Path == this.Path && typed.Prefix == other.Prefix);

		/// <inheritdoc/>
		public override ValueTask<ITypedKeySubspace<T1, T2>?> Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
		{
			Contract.NotNull(tr);

			if (this.IsTopLevel)
			{ // not contained in a directory subspace
				return new ValueTask<ITypedKeySubspace<T1, T2>?>(new TypedKeySubspace<T1, T2>(this.Prefix, this.Encoder, SubspaceContext.Default));
			}

			// we have to use the DirectoryLayer to resolve the subspace
			return ResolveWithDirectory(tr, directory);
		}

		private async ValueTask<ITypedKeySubspace<T1, T2>?> ResolveWithDirectory(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory)
		{
			Contract.Debug.Requires(this.Path.Count != 0);

			var folder = await (directory ?? tr.Context.Database.DirectoryLayer).TryOpenCachedAsync(tr, this.Path);
			if (folder == null) return null;
			return new TypedKeySubspace<T1, T2>(folder.GetPrefix() + this.Prefix, this.Encoder, folder.Context);
		}

		public TypedKeySubspaceLocation<T2> this[T1 item1] => new TypedKeySubspaceLocation<T2>(this.Path, this.Encoder.EncodePartialKey(this.Prefix, item1), this.Encoding.GetKeyEncoder<T2>());

	}

	/// <summary>Path to a subspace that can represent composite keys of a specific type</summary>
	/// <typeparam name="T1">Type of the first key</typeparam>
	/// <typeparam name="T2">Type of the second key</typeparam>
	/// <typeparam name="T3">Type of the third key</typeparam>
	/// <remarks>Instance of this type can be <see cref="ISubspaceLocation{TSubspace}.Resolve">resolved</see> into an actual <see cref="ITypedKeySubspace{T1, T2, T3}"/> valid for a specific transaction</remarks>
	public sealed class TypedKeySubspaceLocation<T1, T2, T3> : SubspaceLocation<ITypedKeySubspace<T1, T2, T3>>
	{

		public ICompositeKeyEncoder<T1, T2, T3> Encoder { get; }

		public override IKeyEncoding Encoding => this.Encoder.Encoding;

		public TypedKeySubspaceLocation(Slice suffix, ICompositeKeyEncoder<T1, T2, T3> encoder)
			: this(default, suffix, encoder)
		{ }

		public TypedKeySubspaceLocation(FdbPath path, Slice suffix, ICompositeKeyEncoder<T1, T2, T3> encoder)
			: base(path, suffix)
		{
			Contract.NotNull(encoder);
			this.Encoder = encoder;
		}

		public override int GetHashCode() => HashCodes.Combine(this.Path.GetHashCode(), this.Prefix.GetHashCode(), 0x3333);

		public override bool Equals(ISubspaceLocation other) =>
			object.ReferenceEquals(other, this)
			|| (other is TypedKeySubspaceLocation<T1, T2, T3> typed && typed.Encoding == this.Encoding && typed.Path == this.Path && typed.Prefix == other.Prefix);

		public override ValueTask<ITypedKeySubspace<T1, T2, T3>?> Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
		{
			if (this.IsTopLevel)
			{ // not contained in a directory subspace
				return new ValueTask<ITypedKeySubspace<T1, T2, T3>?>(new TypedKeySubspace<T1, T2, T3>(this.Prefix, this.Encoder, SubspaceContext.Default));
			}

			// we have to use the DirectoryLayer to resolve the subspace
			return ResolveWithDirectory(tr, directory);
		}

		private async ValueTask<ITypedKeySubspace<T1, T2, T3>?> ResolveWithDirectory(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory)
		{
			Contract.Debug.Requires(tr != null && this.Path.Count != 0);

			var folder = await (directory ?? tr.Context.Database.DirectoryLayer).TryOpenCachedAsync(tr, this.Path);
			if (folder == null) return null;
			return new TypedKeySubspace<T1, T2, T3>(folder.GetPrefix() + this.Prefix, this.Encoder, folder.Context);
		}

		public TypedKeySubspaceLocation<T2, T3> this[T1 item1] => new TypedKeySubspaceLocation<T2, T3>(this.Path, this.Prefix + this.Encoder.EncodeKeyParts(1, (item1, default, default)), this.Encoding.GetKeyEncoder<T2, T3>());

		public TypedKeySubspaceLocation<T3> this[T1 item1, T2 item2] => new TypedKeySubspaceLocation<T3>(this.Path, this.Prefix + this.Encoder.EncodeKeyParts(2, (item1, item2, default)), this.Encoding.GetKeyEncoder<T3>());

	}

	/// <summary>Path to a subspace that can represent composite keys of a specific type</summary>
	/// <typeparam name="T1">Type of the first key</typeparam>
	/// <typeparam name="T2">Type of the second key</typeparam>
	/// <typeparam name="T3">Type of the third key</typeparam>
	/// <typeparam name="T4">Type of the fourth key</typeparam>
	/// <remarks>Instance of this type can be <see cref="ISubspaceLocation{TSubspace}.Resolve">resolved</see> into an actual <see cref="ITypedKeySubspace{T1, T2, T3, T4}"/> valid for a specific transaction</remarks>
	public sealed class TypedKeySubspaceLocation<T1, T2, T3, T4> : SubspaceLocation<ITypedKeySubspace<T1, T2, T3, T4>>
	{

		public ICompositeKeyEncoder<T1, T2, T3, T4> Encoder { get; }

		public override IKeyEncoding Encoding => this.Encoder.Encoding;

		public TypedKeySubspaceLocation(Slice suffix, ICompositeKeyEncoder<T1, T2, T3, T4> encoder)
			: this(default, suffix, encoder)
		{ }

		public TypedKeySubspaceLocation(FdbPath path, Slice suffix, ICompositeKeyEncoder<T1, T2, T3, T4> encoder)
			: base(path, suffix)
		{
			Contract.NotNull(encoder);
			this.Encoder = encoder;
		}

		public override int GetHashCode() => HashCodes.Combine(this.Path.GetHashCode(), this.Prefix.GetHashCode(), 0x4444);

		public override bool Equals(ISubspaceLocation other) =>
			object.ReferenceEquals(other, this)
			|| (other is TypedKeySubspaceLocation<T1, T2, T3, T4> typed && typed.Encoding == this.Encoding && typed.Path == this.Path && typed.Prefix == other.Prefix);

		public override ValueTask<ITypedKeySubspace<T1, T2, T3, T4>?> Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
		{
			if (this.IsTopLevel)
			{ // not contained in a directory subspace
				return new ValueTask<ITypedKeySubspace<T1, T2, T3, T4>?>(new TypedKeySubspace<T1, T2, T3, T4>(this.Prefix, this.Encoder, SubspaceContext.Default));
			}

			// we have to use the DirectoryLayer to resolve the subspace
			return ResolveWithDirectory(tr, directory);
		}

		private async ValueTask<ITypedKeySubspace<T1, T2, T3, T4>?> ResolveWithDirectory(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory)
		{
			Contract.Debug.Requires(tr != null && this.Path.Count != 0);

			var folder = await (directory ?? tr.Context.Database.DirectoryLayer).TryOpenCachedAsync(tr, this.Path);
			if (folder == null) return null;
			return new TypedKeySubspace<T1, T2, T3, T4>(folder.GetPrefix() + this.Prefix, this.Encoder, folder.Context);
		}

		public TypedKeySubspaceLocation<T2, T3, T4> this[T1 item1] => new TypedKeySubspaceLocation<T2, T3, T4>(this.Path, this.Prefix + this.Encoder.EncodeKeyParts(1, (item1, default, default, default)), this.Encoding.GetKeyEncoder<T2, T3, T4>());

		public TypedKeySubspaceLocation<T3, T4> this[T1 item1, T2 item2] => new TypedKeySubspaceLocation<T3, T4>(this.Path, this.Prefix + this.Encoder.EncodeKeyParts(2, (item1, item2, default, default)), this.Encoding.GetKeyEncoder<T3, T4>());

		public TypedKeySubspaceLocation<T4> this[T1 item1, T2 item2, T3 item3] => new TypedKeySubspaceLocation<T4>(this.Path, this.Prefix + this.Encoder.EncodeKeyParts(3, (item1, item2, item3, default)), this.Encoding.GetKeyEncoder<T4>());
	}

	public static class SubspaceLocationExtensions
	{

		/// <summary>Return a binary version of the current path</summary>
		/// <param name="self">Existing subspace path</param>
		/// <returns>A <see cref="BinaryKeySubspaceLocation"/> that points to a <see cref="IBinaryKeySubspace">binary key subspace</see>.</returns>
		[Pure]
		public static BinaryKeySubspaceLocation AsBinary(this ISubspaceLocation self)
		{
			Contract.NotNull(self);

			if (self is BinaryKeySubspaceLocation bsp)
			{
				return bsp;
			}

			return new BinaryKeySubspaceLocation(self.Path, self.Prefix);
		}

		/// <summary>Return a dynamic version of the current location</summary>
		/// <param name="self">Existing subspace location</param>
		/// <param name="encoding">If specified, change the encoding use by the current location. If <c>null</c>, inherit the current encoding</param>
		/// <returns>A <see cref="DynamicKeySubspaceLocation"/> that points to a <see cref="IDynamicKeySubspace">dynamic key subspace</see></returns>
		[Pure]
		public static DynamicKeySubspaceLocation AsDynamic(this ISubspaceLocation self, IKeyEncoding? encoding = null)
		{
			Contract.NotNull(self);

			if (self is DynamicKeySubspaceLocation ksp)
			{
				if (encoding == null || encoding == ksp.Encoding) return ksp;
			}

			return new DynamicKeySubspaceLocation(self.Path, self.Prefix, (encoding ?? self.Encoding).GetDynamicKeyEncoder());
		}

		/// <summary>Return a directory version of the current location</summary>
		/// <param name="self">Existing subspace location</param>
		/// <returns>A <see cref="FdbDirectorySubspaceLocation"/> that points to the same location as <paramref name="self"/>.</returns>
		/// <exception cref="ArgumentException">If the location has a non-zero <see cref="ISubspaceLocation.Prefix"/></exception>
		[Pure]
		public static FdbDirectorySubspaceLocation AsDirectory(this ISubspaceLocation self)
		{
			Contract.NotNull(self);

			if (self is FdbDirectorySubspaceLocation dsl)
			{
				return dsl;
			}

			if (self.Prefix.Count != 0) throw new ArgumentException($"Cannot convert location '{self}' into a directory location, because it has a non-empty prefix.");
			return new FdbDirectorySubspaceLocation(self.Path);
		}

		/// <summary>Return a dynamic version of the current path</summary>
		/// <param name="self">Existing subspace path</param>
		/// <param name="encoder">Custom encoder used by subspace</param>
		/// <returns>A <see cref="DynamicKeySubspaceLocation"/> that will encode dynamic keys</returns>
		[Pure]
		public static DynamicKeySubspaceLocation UsingEncoder(this ISubspaceLocation self, IDynamicKeyEncoder encoder)
		{
			Contract.NotNull(self);
			Contract.NotNull(encoder);

			if (self is DynamicKeySubspaceLocation ksp)
			{
				if (encoder == ksp.Encoding.GetDynamicKeyEncoder()) return ksp;
			}

			return new DynamicKeySubspaceLocation(self.Path, self.Prefix, encoder);
		}

		/// <summary>Returned a typed version of the current path</summary>
		/// <typeparam name="T1">Type of the key</typeparam>
		/// <param name="self">Existing subspace path</param>
		/// <param name="encoding">If specified, change the encoding use by the current path. If <c>null</c>, inherit the current encoding</param>
		public static TypedKeySubspaceLocation<T1> AsTyped<T1>(this ISubspaceLocation self, IKeyEncoding? encoding = null)
		{
			Contract.NotNull(self);

			if (self is TypedKeySubspaceLocation<T1> tsp)
			{
				if (encoding == null || encoding == tsp.Encoder.Encoding) return tsp;
			}

			return new TypedKeySubspaceLocation<T1>(self.Path, self.Prefix, (encoding ?? self.Encoding).GetKeyEncoder<T1>());
		}

		/// <summary>Returned a typed version of the current path</summary>
		/// <typeparam name="T1">Type of the key</typeparam>
		/// <param name="self">Existing subspace path</param>
		/// <param name="encoder">Custom encoder used by subspace</param>
		public static TypedKeySubspaceLocation<T1> UsingEncoder<T1>(this ISubspaceLocation self, IKeyEncoder<T1> encoder)
		{
			Contract.NotNull(self);
			Contract.NotNull(encoder);

			if (self is TypedKeySubspaceLocation<T1> tsp)
			{
				if (encoder == tsp.Encoder) return tsp;
			}

			return new TypedKeySubspaceLocation<T1>(self.Path, self.Prefix, encoder);
		}

		/// <summary>Returned a typed version of the current path</summary>
		/// <typeparam name="T1">Type of the first part of the key</typeparam>
		/// <typeparam name="T2">Type of the second part of the key</typeparam>
		/// <param name="self">Existing subspace path</param>
		/// <param name="encoding">If specified, change the encoding use by the current path. If <c>null</c>, inherit the current encoding</param>
		public static TypedKeySubspaceLocation<T1, T2> AsTyped<T1, T2>(this ISubspaceLocation self, IKeyEncoding? encoding = null)
		{
			Contract.NotNull(self);

			if (self is TypedKeySubspaceLocation<T1, T2> tsp)
			{
				if (encoding == null || encoding == tsp.Encoder.Encoding) return tsp;
			}

			return new TypedKeySubspaceLocation<T1, T2>(self.Path, self.Prefix, (encoding ?? self.Encoding).GetKeyEncoder<T1, T2>());
		}

		/// <summary>Returned a typed version of the current path</summary>
		/// <typeparam name="T1">Type of the first part of the key</typeparam>
		/// <typeparam name="T2">Type of the second part of the key</typeparam>
		/// <param name="self">Existing subspace path</param>
		/// <param name="encoder">Custom encoder used by subspace</param>
		public static TypedKeySubspaceLocation<T1, T2> UsingEncoder<T1, T2>(this ISubspaceLocation self, ICompositeKeyEncoder<T1, T2> encoder)
		{
			Contract.NotNull(self);
			Contract.NotNull(encoder);

			if (self is TypedKeySubspaceLocation<T1, T2> tsp)
			{
				if (encoder == tsp.Encoder) return tsp;
			}

			return new TypedKeySubspaceLocation<T1, T2>(self.Path, self.Prefix, encoder);
		}

		/// <summary>Returned a typed version of the current path</summary>
		/// <typeparam name="T1">Type of the first part of the key</typeparam>
		/// <typeparam name="T2">Type of the second part of the key</typeparam>
		/// <typeparam name="T3">Type of the third part of the key</typeparam>
		/// <param name="self">Existing subspace path</param>
		/// <param name="encoding">If specified, change the encoding use by the current path. If <c>null</c>, inherit the current encoding</param>
		public static TypedKeySubspaceLocation<T1, T2, T3> AsTyped<T1, T2, T3>(this ISubspaceLocation self, IKeyEncoding? encoding = null)
		{
			Contract.NotNull(self);

			if (self is TypedKeySubspaceLocation<T1, T2, T3> tsp)
			{
				if (encoding == null || encoding == tsp.Encoder.Encoding) return tsp;
			}

			return new TypedKeySubspaceLocation<T1, T2, T3>(self.Path, self.Prefix, (encoding ?? self.Encoding).GetKeyEncoder<T1, T2, T3>());
		}

		/// <summary>Returned a typed version of the current path</summary>
		/// <typeparam name="T1">Type of the first part of the key</typeparam>
		/// <typeparam name="T2">Type of the second part of the key</typeparam>
		/// <typeparam name="T3">Type of the third part of the key</typeparam>
		/// <param name="self">Existing subspace path</param>
		/// <param name="encoder">Custom encoder used by subspace</param>
		public static TypedKeySubspaceLocation<T1, T2, T3> UsingEncoder<T1, T2, T3>(this ISubspaceLocation self, ICompositeKeyEncoder<T1, T2, T3> encoder)
		{
			Contract.NotNull(self);

			if (self is TypedKeySubspaceLocation<T1, T2, T3> tsp)
			{
				if (encoder == tsp.Encoder) return tsp;
			}

			return new TypedKeySubspaceLocation<T1, T2, T3>(self.Path, self.Prefix, encoder);
		}

	}

	public static class SubspaceLocation
	{

		/// <summary>Represent the root directory of the Directory Layer</summary>
		public static readonly DynamicKeySubspaceLocation Root = new DynamicKeySubspaceLocation(FdbPath.Root, Slice.Empty, TuPack.Encoding.GetDynamicKeyEncoder());

		/// <summary>Represent a location without any prefix, and outside the jurisdiction of the Directory Layer</summary>
		public static readonly DynamicKeySubspaceLocation Empty = new DynamicKeySubspaceLocation(Slice.Empty, TuPack.Encoding.GetDynamicKeyEncoder());

		#region FromPath...

		[Pure]
		public static DynamicKeySubspaceLocation FromPath(FdbPath path)
		{
			return new DynamicKeySubspaceLocation(path, default, TuPack.Encoding.GetDynamicKeyEncoder());
		}

		[Pure]
		public static DynamicKeySubspaceLocation FromPath(FdbPath path, IDynamicKeyEncoder encoder)
		{
			return new DynamicKeySubspaceLocation(path, default, encoder ?? TuPack.Encoding.GetDynamicKeyEncoder());
		}

		[Pure]
		public static DynamicKeySubspaceLocation FromPath(FdbPath path, IDynamicKeyEncoding encoding)
		{
			return new DynamicKeySubspaceLocation(path, default, (encoding ?? TuPack.Encoding).GetDynamicKeyEncoder());
		}

		[Pure]
		public static TypedKeySubspaceLocation<T1> FromPath<T1>(FdbPath path, IKeyEncoder<T1> encoder)
		{
			return new TypedKeySubspaceLocation<T1>(path, default, encoder ?? TuPack.Encoding.GetKeyEncoder<T1>());
		}

		[Pure]
		public static TypedKeySubspaceLocation<T1, T2> FromPath<T1, T2>(FdbPath path, ICompositeKeyEncoder<T1, T2> encoder)
		{
			return new TypedKeySubspaceLocation<T1, T2>(path, default, encoder ?? TuPack.Encoding.GetKeyEncoder<T1, T2>());
		}

		[Pure]
		public static TypedKeySubspaceLocation<T1, T2, T3> FromPath<T1, T2, T3>(FdbPath path, ICompositeKeyEncoder<T1, T2, T3> encoder)
		{
			return new TypedKeySubspaceLocation<T1, T2, T3>(path, default, encoder ?? TuPack.Encoding.GetKeyEncoder<T1, T2, T3>());
		}

		[Pure]
		public static TypedKeySubspaceLocation<T1, T2, T3, T4> FromPath<T1, T2, T3, T4>(FdbPath path, ICompositeKeyEncoder<T1, T2, T3, T4> encoder)
		{
			return new TypedKeySubspaceLocation<T1, T2, T3, T4>(path, default, encoder ?? TuPack.Encoding.GetKeyEncoder<T1, T2, T3, T4>());
		}

		#endregion

		#region FromKey...

		/// <summary>Create a location that uses a fixed prefix given by a tuple</summary>
		[Pure]
		public static DynamicKeySubspaceLocation FromKey(IVarTuple items, IDynamicKeyEncoding? encoding = null)
		{
			var encoder = (encoding ?? TuPack.Encoding).GetDynamicKeyEncoder();
			return new DynamicKeySubspaceLocation(encoder.Pack(items), encoder);
		}

		/// <summary>Create a location that uses a fixed prefix given by a tuple</summary>
		[Pure]
		public static DynamicKeySubspaceLocation FromKey(IVarTuple items, IDynamicKeyEncoder? encoder)
		{
			encoder ??= TuPack.Encoding.GetDynamicKeyEncoder();
			return new DynamicKeySubspaceLocation(encoder.Pack(items), encoder);
		}

		/// <summary>Create a location that uses a fixed binary prefix</summary>
		[Pure]
		public static DynamicKeySubspaceLocation FromKey(ReadOnlySpan<byte> key, IDynamicKeyEncoding? encoding = null)
		{
			return new DynamicKeySubspaceLocation(Slice.Copy(key), (encoding ?? TuPack.Encoding).GetDynamicKeyEncoder());
		}

		/// <summary>Create a location that uses a fixed binary prefix</summary>
		[Pure]
		public static DynamicKeySubspaceLocation FromKey(Slice key, IDynamicKeyEncoding? encoding = null)
		{
			if (key.IsNull) throw new ArgumentException("Key cannot be nil.", nameof(key));
			return new DynamicKeySubspaceLocation(key, (encoding ?? TuPack.Encoding).GetDynamicKeyEncoder());
		}

		/// <summary>Create a location that uses a fixed binary prefix</summary>
		[Pure]
		public static DynamicKeySubspaceLocation FromKey(Slice key, IDynamicKeyEncoder? encoder)
		{
			if (key.IsNull) throw new ArgumentException("Key cannot be nil.", nameof(key));
			return new DynamicKeySubspaceLocation(key, encoder ?? TuPack.Encoding.GetDynamicKeyEncoder());
		}

		[Pure]
		public static TypedKeySubspaceLocation<T1> FromKey<T1>(Slice key, IKeyEncoder<T1> encoder)
		{
			if (key.IsNull) throw new ArgumentException("Key cannot be nil.", nameof(key));
			return new TypedKeySubspaceLocation<T1>(key, encoder);
		}

		[Pure]
		public static TypedKeySubspaceLocation<T1, T2> FromKey<T1, T2>(Slice key, ICompositeKeyEncoder<T1, T2> encoder)
		{
			if (key.IsNull) throw new ArgumentException("Key cannot be nil.", nameof(key));
			return new TypedKeySubspaceLocation<T1, T2>(key, encoder);
		}

		[Pure]
		public static TypedKeySubspaceLocation<T1, T2, T3> FromKey<T1, T2, T3>(Slice key, ICompositeKeyEncoder<T1, T2, T3> encoder)
		{
			if (key.IsNull) throw new ArgumentException("Key cannot be nil.", nameof(key));
			return new TypedKeySubspaceLocation<T1, T2, T3>(key, encoder);
		}

		[Pure]
		public static TypedKeySubspaceLocation<T1, T2, T3, T4> FromKey<T1, T2, T3, T4>(Slice key, ICompositeKeyEncoder<T1, T2, T3, T4> encoder)
		{
			if (key.IsNull) throw new ArgumentException("Key cannot be nil.", nameof(key));
			return new TypedKeySubspaceLocation<T1, T2, T3, T4>(key, encoder);
		}

		#endregion

	}

}

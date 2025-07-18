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

namespace FoundationDB.Client
{
	/// <summary>Represents the path to a specific subspace in the database</summary>
	/// <remarks>A path can be <see cref="ISubspaceLocation{TSubspace}.Resolve">resolved</see> into the actual <see cref="IKeySubspace"/> implementation that will be valid within the context of a transaction.</remarks>
	[PublicAPI]
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
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		IKeyEncoding Encoding { get; }

		/// <summary>Resolves the subspace for this location, if it exists</summary>
		/// <param name="tr">Transaction used for this operation.</param>
		/// <param name="directory">Optional <see cref="FdbDirectoryLayer">Directory Layer</see> to use; otherwise, uses the global instance for the transaction's database.</param>
		/// <returns>Resolved subspace if found; otherwise, <see langword="null"/></returns>
		/// <remarks>The subspace may be cached, and be obsolete. The cache will add deferred value checks to the transaction, that will cause it to conflict and retry, if the directory has been deleted or has moved.</remarks>
		ValueTask<IKeySubspace?> TryResolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null);

		/// <summary>Resolves the subspace for this location, or throws if it does not exist.</summary>
		/// <param name="tr">Transaction used for this operation.</param>
		/// <param name="directory">Optional <see cref="FdbDirectoryLayer">Directory Layer</see> to use; otherwise, uses the global instance for the transaction's database.</param>
		/// <returns>Resolved subspace if found</returns>
		/// <exception cref="InvalidOperationException">If this location does not exist in the database.</exception>
		/// <remarks>The subspace may be cached, and be obsolete. The cache will add deferred value checks to the transaction, that will cause it to conflict and retry, if the directory has been deleted or has moved.</remarks>
		ValueTask<IKeySubspace> Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null);

	}

	/// <summary>Represents the path to a typed subspace in the database</summary>
	/// <typeparam name="TSubspace">Type of the <see cref="IKeySubspace"/> implementation that this path will resolve into.</typeparam>
	[PublicAPI]
	public interface ISubspaceLocation<TSubspace> : ISubspaceLocation
		where TSubspace : class, IKeySubspace
	{

		/// <summary>Returns the actual subspace that corresponds to this location, if it exists.</summary>
		/// <param name="tr">Current transaction</param>
		/// <param name="directory"><see cref="FdbDirectoryLayer">DirectoryLayer</see> instance to use for the resolve. If null, uses the default database directory layer.</param>
		/// <returns>Key subspace using the resolved key prefix of this location in the context of the current transaction, or <c>null</c> if the directory does not exist</returns>
		/// <remarks>
		/// <para>The instance resolved for this transaction <b>SHOULD NOT</b> be used in the context of a different transaction, because its location in the Directory Layer may have been changed concurrently!</para>
		/// <para>Re-using cached subspace instances <b>MAY</b> lead to <b>DATA CORRUPTION</b> if not used carefully! The best practice is to call <see cref="TryResolve"/>() every time it is needed by a new transaction.</para>
		/// </remarks>
		/// <seealso cref="Resolve"/>
		new ValueTask<TSubspace?> TryResolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null);

		/// <summary>Returns the actual subspace that corresponds to this location, or throws if it does not exist.</summary>
		/// <param name="tr">Current transaction</param>
		/// <param name="directory"><see cref="FdbDirectoryLayer">DirectoryLayer</see> instance to use for the resolve. If null, uses the default database directory layer.</param>
		/// <returns>Key subspace using the resolved key prefix of this location in the context of the current transaction</returns>
		/// <exception cref="InvalidOperationException">If this location does not exist in the database.</exception>
		/// <remarks>
		/// <para>The instance resolved for this transaction <b>SHOULD NOT</b> be used in the context of a different transaction, because its location in the Directory Layer may have been changed concurrently!</para>
		/// <para>Re-using cached subspace instances <b>MAY</b> lead to <b>DATA CORRUPTION</b> if not used carefully! The best practice is to call <see cref="Resolve"/>() every time it is needed by a new transaction.</para>
		/// </remarks>
		/// <seealso cref="TryResolve"/>
		new ValueTask<TSubspace> Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null);

	}

	/// <summary>Default implementation of a subspace location</summary>
	/// <typeparam name="TSubspace">Type of the concrete <see cref="IKeySubspace"/> implementation that this location will resolve to</typeparam>
	[DebuggerDisplay("Path={Path}, Prefix={Prefix}")]
	[PublicAPI]
	public abstract class SubspaceLocation<TSubspace> : ISubspaceLocation<TSubspace>, IFdbLayer<TSubspace, FdbDirectoryLayer?>
		where TSubspace : class, IKeySubspace
	{

		/// <inheritdoc />
		public FdbPath Path { get; } // can be empty

		/// <inheritdoc />
		public Slice Prefix { get; } // can be empty

		/// <inheritdoc />
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public abstract IKeyEncoding Encoding { get; }

		protected SubspaceLocation(FdbPath path, Slice prefix)
		{
			this.Path = path;
			this.Prefix = prefix.IsNull ? Slice.Empty : prefix;
		}

		/// <inheritdoc />
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

		async ValueTask<IKeySubspace?> ISubspaceLocation.TryResolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory)
		{
			return await TryResolve(tr, directory).ConfigureAwait(false);
		}

		/// <inheritdoc cref="ISubspaceLocation{T}.TryResolve"/>
		public abstract ValueTask<TSubspace?> TryResolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null);

		async ValueTask<IKeySubspace> ISubspaceLocation.Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory)
		{
			return await Resolve(tr, directory).ConfigureAwait(false);
		}

		/// <inheritdoc cref="ISubspaceLocation{T}.Resolve"/>
		public virtual async ValueTask<TSubspace> Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
		{
			var subspace = await TryResolve(tr, directory).ConfigureAwait(false);
			if (subspace == null)
			{
				throw new InvalidOperationException($"The required location '{this.Path}' does not exist in the database.");
			}
			return subspace;
		}

		/// <inheritdoc />
		string IFdbLayer.Name => nameof(SubspaceLocation<>);

		/// <inheritdoc />
		public override bool Equals(object? obj) => obj is ISubspaceLocation path && Equals(path);

		/// <inheritdoc />
		public abstract bool Equals(ISubspaceLocation? other);

		/// <inheritdoc />
		public abstract override int GetHashCode();

	}

	/// <summary>Path to a subspace that can represent binary keys only</summary>
	/// <remarks>Instance of this type can be <see cref="ISubspaceLocation{TSubspace}.Resolve">resolved</see> into an actual <see cref="IDynamicKeySubspace"/> valid for a specific transaction</remarks>
	[PublicAPI]
	public sealed class BinaryKeySubspaceLocation : SubspaceLocation<IBinaryKeySubspace>
	{

		/// <inheritdoc />
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public override IKeyEncoding Encoding => BinaryEncoding.Instance;

		public BinaryKeySubspaceLocation(Slice suffix) : base(default, suffix)
		{ }

		public BinaryKeySubspaceLocation(FdbPath path, Slice suffix)
			: base(path, suffix)
		{ }

		/// <inheritdoc />
		public override int GetHashCode() => HashCode.Combine(this.Path.GetHashCode(), this.Prefix.GetHashCode(), 0x12345678);

		/// <inheritdoc />
		public override bool Equals(ISubspaceLocation? other) =>
			ReferenceEquals(other, this)
			|| (other is BinaryKeySubspaceLocation bin && bin.Path == this.Path && bin.Prefix == other.Prefix);

		/// <inheritdoc />
		public override ValueTask<IBinaryKeySubspace?> TryResolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
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
			var folder = await (directory ?? tr.Context.Database.DirectoryLayer).TryOpenCachedAsync(tr, this.Path).ConfigureAwait(false);
			if (folder == null) return null;
			if (this.Prefix.Count == 0) return folder;
			return new BinaryKeySubspace(folder.GetPrefix() + this.Prefix, folder.Context);
		}

		public BinaryKeySubspaceLocation this[Slice prefix] => prefix.Count != 0 ? new BinaryKeySubspaceLocation(this.Path, this.Prefix + prefix) : this;

	}

	/// <summary>Path to a subspace that can represent dynamic keys of any size and type</summary>
	[PublicAPI]
	public interface IDynamicKeySubspaceLocation : ISubspaceLocation<IDynamicKeySubspace>
	{

		/// <summary>Instance that is used to encode keys in this subspace</summary>
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		IDynamicKeyEncoder Encoder { get; }

	}

	/// <summary>Path to a subspace that can represent dynamic keys of any size and type</summary>
	/// <remarks>Instance of this type can be <see cref="ISubspaceLocation{TSubspace}.Resolve">resolved</see> into an actual <see cref="IDynamicKeySubspace"/> valid for a specific transaction</remarks>
	[PublicAPI]
	public sealed class DynamicKeySubspaceLocation : SubspaceLocation<IDynamicKeySubspace>, IDynamicKeySubspaceLocation
	{

		/// <inheritdoc />
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public IDynamicKeyEncoder Encoder { get; }

		/// <inheritdoc />
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public override IKeyEncoding Encoding => this.Encoder.Encoding;

		public DynamicKeySubspaceLocation(FdbPath path, Slice suffix)
			: base(path, suffix)
		{
#pragma warning disable CS0618 // Type or member is obsolete
			this.Encoder = TuPack.Encoding.GetDynamicKeyEncoder();
#pragma warning restore CS0618 // Type or member is obsolete
		}

		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public DynamicKeySubspaceLocation(FdbPath path, Slice suffix, IDynamicKeyEncoder encoder)
			: base(path, suffix)
		{
			Contract.NotNull(encoder);
			this.Encoder = encoder;
		}

		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public DynamicKeySubspaceLocation(Slice prefix, IDynamicKeyEncoder encoder)
			: base(default, prefix)
		{
			Contract.NotNull(encoder);
			this.Encoder = encoder;
		}

		public static readonly DynamicKeySubspaceLocation Root
#pragma warning disable CS0618 // Type or member is obsolete
			= new(Slice.Empty, TuPack.Encoding.GetDynamicKeyEncoder());
#pragma warning restore CS0618 // Type or member is obsolete

		public static DynamicKeySubspaceLocation Create(Slice prefix)
#pragma warning disable CS0618 // Type or member is obsolete
			=> new(prefix, TuPack.Encoding.GetDynamicKeyEncoder());
#pragma warning restore CS0618 // Type or member is obsolete

		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static DynamicKeySubspaceLocation Create(Slice prefix, IDynamicKeyEncoder encoder)
			=> new(prefix, encoder);

		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static DynamicKeySubspaceLocation Create(Slice prefix, IDynamicKeyEncoding encoding)
			=> new(prefix, encoding.GetDynamicKeyEncoder());

		/// <inheritdoc />
		public override int GetHashCode() => HashCode.Combine(this.Path.GetHashCode(), this.Prefix.GetHashCode(), 0x12344321);

		/// <inheritdoc />
		public override bool Equals(ISubspaceLocation? other) =>
			ReferenceEquals(other, this) ||
			(other is DynamicKeySubspaceLocation dyn
#pragma warning disable CS0618 // Type or member is obsolete
			 && dyn.Encoding == this.Encoding
#pragma warning restore CS0618 // Type or member is obsolete
			 && dyn.Path == this.Path
			 && dyn.Prefix == other.Prefix
		 );

		/// <inheritdoc />
		public override ValueTask<IDynamicKeySubspace?> TryResolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
		{
			Contract.NotNull(tr);

			if (this.IsTopLevel)
			{ // not contained in a directory subspace
#pragma warning disable CS0618 // Type or member is obsolete
				return new(new DynamicKeySubspace(this.Prefix, this.Encoder, SubspaceContext.Default));
#pragma warning restore CS0618 // Type or member is obsolete
			}

			return ResolveWithDirectory(tr, directory);
		}

		private async ValueTask<IDynamicKeySubspace?> ResolveWithDirectory(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory)
		{
			// located inside a directory subspace!
			var folder = await (directory ?? tr.Context.Database.DirectoryLayer).TryOpenCachedAsync(tr, this.Path).ConfigureAwait(false);
			if (folder == null) return null;
			return this.Prefix.Count == 0
				? folder
#pragma warning disable CS0618 // Type or member is obsolete
				: new DynamicKeySubspace(folder.GetPrefix() + this.Prefix, folder.KeyEncoder, folder.Context);
#pragma warning restore CS0618 // Type or member is obsolete
		}

		public DynamicKeySubspaceLocation this[Slice prefix]
#pragma warning disable CS0618 // Type or member is obsolete
			=> prefix.Count != 0 ? new(this.Path, this.Prefix + prefix, this.Encoder) : this;
#pragma warning restore CS0618 // Type or member is obsolete

		public DynamicKeySubspaceLocation this[byte[] prefix]
			=> this[prefix.AsSlice()];

#pragma warning disable CS0618 // Type or member is obsolete

		public DynamicKeySubspaceLocation this[ReadOnlySpan<byte> prefix]
			=> prefix.Length != 0 ? new(this.Path, this.Prefix.Concat(prefix), this.Encoder) : this;

		public DynamicKeySubspaceLocation this[IVarTuple tuple]
			=> new(this.Path, this.Encoding.GetDynamicKeyEncoder().Pack(this.Prefix, tuple), this.Encoder);

		public DynamicKeySubspaceLocation ByKey<T1>(T1 item1)
			=> new(this.Path, this.Encoding.GetDynamicKeyEncoder().Pack(this.Prefix, STuple.Create(item1)), this.Encoder);

		public DynamicKeySubspaceLocation ByKey<T1, T2>(T1 item1, T2 item2)
			=> new(this.Path, this.Encoding.GetDynamicKeyEncoder().Pack(this.Prefix, STuple.Create(item1, item2)), this.Encoder);

		public DynamicKeySubspaceLocation ByKey<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
			=> new(this.Path, this.Encoding.GetDynamicKeyEncoder().Pack(this.Prefix, STuple.Create(item1, item2, item3)), this.Encoder);

		public DynamicKeySubspaceLocation ByKey<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
			=> new(this.Path, this.Encoding.GetDynamicKeyEncoder().Pack(this.Prefix, STuple.Create(item1, item2, item3, item4)), this.Encoder);

#pragma warning restore CS0618 // Type or member is obsolete

	}

#pragma warning disable CS0618 // Type or member is obsolete

	/// <summary>Path to a subspace that can represent keys of a specific type</summary>
	/// <typeparam name="T1">Type of the key</typeparam>
	/// <remarks>Instance of this type can be <see cref="ISubspaceLocation{TSubspace}.Resolve">resolved</see> into an actual <see cref="ITypedKeySubspace{T1}"/> valid for a specific transaction</remarks>
	[PublicAPI]
	public sealed class TypedKeySubspaceLocation<T1> : SubspaceLocation<ITypedKeySubspace<T1>>
	{


		/// <summary>Encoder used by keys stored at that location</summary>
		public IKeyEncoder<T1> Encoder { get; }

		/// <inheritdoc/>
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
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

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj is ISubspaceLocation path && Equals(path);

		/// <inheritdoc />
		public override int GetHashCode() => HashCode.Combine(this.Path.GetHashCode(), this.Prefix.GetHashCode(), 0x1111);

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] ISubspaceLocation? other) =>
			ReferenceEquals(other, this)
			|| (other is TypedKeySubspaceLocation<T1> typed && typed.Encoding == this.Encoding && typed.Path == this.Path && typed.Prefix == other.Prefix);
		
		/// <inheritdoc/>
		public override ValueTask<ITypedKeySubspace<T1>?> TryResolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
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

			var folder = await (directory ?? tr.Context.Database.DirectoryLayer).TryOpenCachedAsync(tr, this.Path).ConfigureAwait(false);
			if (folder == null) return null;
			return new TypedKeySubspace<T1>(folder.GetPrefix() + this.Prefix, this.Encoder, folder.Context);
		}

	}

	/// <summary>Path to a subspace that can represent composite keys of a specific type</summary>
	/// <typeparam name="T1">Type of the first key</typeparam>
	/// <typeparam name="T2">Type of the second key</typeparam>
	/// <remarks>Instance of this type can be <see cref="ISubspaceLocation{TSubspace}.Resolve">resolved</see> into an actual <see cref="ITypedKeySubspace{T1, T2}"/> valid for a specific transaction</remarks>
	[PublicAPI]
	public sealed class TypedKeySubspaceLocation<T1, T2> : SubspaceLocation<ITypedKeySubspace<T1, T2>>
	{
		public ICompositeKeyEncoder<T1, T2> Encoder { get; }

		/// <inheritdoc/>
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
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

		/// <inheritdoc/>
		public override int GetHashCode() => HashCode.Combine(this.Path.GetHashCode(), this.Prefix.GetHashCode(), 0x2222);

		/// <inheritdoc/>
		public override bool Equals([NotNullWhen(true)] ISubspaceLocation? other) =>
			ReferenceEquals(other, this)
			|| (other is TypedKeySubspaceLocation<T1, T2> typed && typed.Encoding == this.Encoding && typed.Path == this.Path && typed.Prefix == other.Prefix);

		/// <inheritdoc/>
		public override ValueTask<ITypedKeySubspace<T1, T2>?> TryResolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
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

			var folder = await (directory ?? tr.Context.Database.DirectoryLayer).TryOpenCachedAsync(tr, this.Path).ConfigureAwait(false);
			if (folder == null) return null;
			return new TypedKeySubspace<T1, T2>(folder.GetPrefix() + this.Prefix, this.Encoder, folder.Context);
		}

		public TypedKeySubspaceLocation<T2> this[T1 item1] => new(this.Path, this.Encoder.EncodePartialKey(this.Prefix, item1), this.Encoding.GetKeyEncoder<T2>());

	}

	/// <summary>Path to a subspace that can represent composite keys of a specific type</summary>
	/// <typeparam name="T1">Type of the first key</typeparam>
	/// <typeparam name="T2">Type of the second key</typeparam>
	/// <typeparam name="T3">Type of the third key</typeparam>
	/// <remarks>Instance of this type can be <see cref="ISubspaceLocation{TSubspace}.Resolve">resolved</see> into an actual <see cref="ITypedKeySubspace{T1, T2, T3}"/> valid for a specific transaction</remarks>
	[PublicAPI]
	public sealed class TypedKeySubspaceLocation<T1, T2, T3> : SubspaceLocation<ITypedKeySubspace<T1, T2, T3>>
	{

		public ICompositeKeyEncoder<T1, T2, T3> Encoder { get; }

		/// <inheritdoc/>
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
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

		/// <inheritdoc/>
		public override int GetHashCode() => HashCode.Combine(this.Path.GetHashCode(), this.Prefix.GetHashCode(), 0x3333);

		/// <inheritdoc/>
		public override bool Equals([NotNullWhen(true)] ISubspaceLocation? other) =>
			ReferenceEquals(other, this)
			|| (other is TypedKeySubspaceLocation<T1, T2, T3> typed && typed.Encoding == this.Encoding && typed.Path == this.Path && typed.Prefix == other.Prefix);

		/// <inheritdoc/>
		public override ValueTask<ITypedKeySubspace<T1, T2, T3>?> TryResolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
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

			var folder = await (directory ?? tr.Context.Database.DirectoryLayer).TryOpenCachedAsync(tr, this.Path).ConfigureAwait(false);
			if (folder == null) return null;
			return new TypedKeySubspace<T1, T2, T3>(folder.GetPrefix() + this.Prefix, this.Encoder, folder.Context);
		}

		public TypedKeySubspaceLocation<T2, T3> this[T1 item1] => new(this.Path, this.Prefix + this.Encoder.EncodeKeyParts(1, (item1, default!, default!)), this.Encoding.GetKeyEncoder<T2, T3>());

		public TypedKeySubspaceLocation<T3> this[T1 item1, T2 item2] => new(this.Path, this.Prefix + this.Encoder.EncodeKeyParts(2, (item1, item2, default!)), this.Encoding.GetKeyEncoder<T3>());

	}

	/// <summary>Path to a subspace that can represent composite keys of a specific type</summary>
	/// <typeparam name="T1">Type of the first key</typeparam>
	/// <typeparam name="T2">Type of the second key</typeparam>
	/// <typeparam name="T3">Type of the third key</typeparam>
	/// <typeparam name="T4">Type of the fourth key</typeparam>
	/// <remarks>Instance of this type can be <see cref="ISubspaceLocation{TSubspace}.Resolve">resolved</see> into an actual <see cref="ITypedKeySubspace{T1, T2, T3, T4}"/> valid for a specific transaction</remarks>
	[PublicAPI]
	public sealed class TypedKeySubspaceLocation<T1, T2, T3, T4> : SubspaceLocation<ITypedKeySubspace<T1, T2, T3, T4>>
	{

		public ICompositeKeyEncoder<T1, T2, T3, T4> Encoder { get; }

		/// <inheritdoc/>
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
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

		/// <inheritdoc/>
		public override int GetHashCode() => HashCode.Combine(this.Path.GetHashCode(), this.Prefix.GetHashCode(), 0x4444);

		/// <inheritdoc/>
		public override bool Equals([NotNullWhen(true)] ISubspaceLocation? other) =>
			ReferenceEquals(other, this)
			|| (other is TypedKeySubspaceLocation<T1, T2, T3, T4> typed && typed.Encoding == this.Encoding && typed.Path == this.Path && typed.Prefix == other.Prefix);

		/// <inheritdoc/>
		public override ValueTask<ITypedKeySubspace<T1, T2, T3, T4>?> TryResolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
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

			var folder = await (directory ?? tr.Context.Database.DirectoryLayer).TryOpenCachedAsync(tr, this.Path).ConfigureAwait(false);
			if (folder == null) return null;
			return new TypedKeySubspace<T1, T2, T3, T4>(folder.GetPrefix() + this.Prefix, this.Encoder, folder.Context);
		}

		public TypedKeySubspaceLocation<T2, T3, T4> this[T1 item1] => new(this.Path, this.Prefix + this.Encoder.EncodeKeyParts(1, (item1, default!, default!, default!)), this.Encoding.GetKeyEncoder<T2, T3, T4>());

		public TypedKeySubspaceLocation<T3, T4> this[T1 item1, T2 item2] => new(this.Path, this.Prefix + this.Encoder.EncodeKeyParts(2, (item1, item2, default!, default!)), this.Encoding.GetKeyEncoder<T3, T4>());

		public TypedKeySubspaceLocation<T4> this[T1 item1, T2 item2, T3 item3] => new(this.Path, this.Prefix + this.Encoder.EncodeKeyParts(3, (item1, item2, item3, default!)), this.Encoding.GetKeyEncoder<T4>());
	}

#pragma warning restore CS0618 // Type or member is obsolete

	/// <summary>Extension methods for <see cref="ISubspaceLocation"/></summary>
	[PublicAPI]
	public static class SubspaceLocationExtensions
	{

		/// <summary>Returns a binary version of the current location</summary>
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

			return new(self.Path, self.Prefix);
		}

		/// <summary>Returns a location that will always add the given prefix to all the keys, relative to the current location</summary>
		/// <param name="self">Existing subspace path</param>
		/// <param name="prefix">Prefix added between the resolved path prefix, and the rest of the key</param>
		/// <remarks>
		/// <para>If, for example, location <c>"/Foo/Bar"</c> will resolve to the prefix <c>(123,)</c>, call <c>WithPrefix(TuPack.EncodeKey(0))</c> will create a new location that will use <c>(123, 0)</c> for all keys, without having to explicitly repeat <c>subspace.GetKey(0, ...)</c> everytime.</para>
		/// </remarks>
		public static BinaryKeySubspaceLocation WithPrefix(this ISubspaceLocation self, Slice prefix)
		{
			return prefix.Count == 0 ? self.AsBinary() : new(self.Path, self.Prefix + prefix);
		}

		/// <summary>Returns a dynamic version of the current location</summary>
		/// <param name="self">Existing subspace location</param>
		/// <returns>A <see cref="DynamicKeySubspaceLocation"/> that points to a <see cref="IDynamicKeySubspace">dynamic key subspace</see></returns>
		[Pure]
		public static DynamicKeySubspaceLocation AsDynamic(this ISubspaceLocation self)
		{
			Contract.NotNull(self);

			if (self is DynamicKeySubspaceLocation ksp)
			{
				return ksp;
			}

#pragma warning disable CS0618 // Type or member is obsolete
			return new(self.Path, self.Prefix, TupleKeyEncoder.Instance);
#pragma warning restore CS0618 // Type or member is obsolete
		}

		/// <summary>Return a dynamic version of the current location</summary>
		/// <param name="self">Existing subspace location</param>
		/// <param name="encoding">If specified, change the encoding use by the current location. If <c>null</c>, inherit the current encoding</param>
		/// <returns>A <see cref="DynamicKeySubspaceLocation"/> that points to a <see cref="IDynamicKeySubspace">dynamic key subspace</see></returns>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static DynamicKeySubspaceLocation AsDynamic(this ISubspaceLocation self, IKeyEncoding encoding)
		{
			Contract.NotNull(self);
			Contract.NotNull(encoding);

			if (self is DynamicKeySubspaceLocation ksp)
			{
				if (encoding == ksp.Encoding) return ksp;
			}

			return new(self.Path, self.Prefix, encoding.GetDynamicKeyEncoder());
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

			return self.Prefix.Count == 0
				? new FdbDirectorySubspaceLocation(self.Path)
				: throw new ArgumentException($"Cannot convert location '{self}' into a directory location, because it has a non-empty prefix.");
		}

		/// <summary>Return a dynamic version of the current path</summary>
		/// <param name="self">Existing subspace path</param>
		/// <param name="encoder">Custom encoder used by subspace</param>
		/// <returns>A <see cref="DynamicKeySubspaceLocation"/> that will encode dynamic keys</returns>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static DynamicKeySubspaceLocation UsingEncoder(this ISubspaceLocation self, IDynamicKeyEncoder encoder)
		{
			Contract.NotNull(self);
			Contract.NotNull(encoder);

			if (self is DynamicKeySubspaceLocation ksp)
			{
				if (encoder == ksp.Encoding.GetDynamicKeyEncoder()) return ksp;
			}

			return new(self.Path, self.Prefix, encoder);
		}

		/// <summary>Returned a typed version of the current path</summary>
		/// <typeparam name="T1">Type of the key</typeparam>
		/// <param name="self">Existing subspace path</param>
		public static TypedKeySubspaceLocation<T1> AsTyped<T1>(this ISubspaceLocation self)
		{
			Contract.NotNull(self);

			if (self is TypedKeySubspaceLocation<T1> tsp)
			{
				return tsp;
			}

#pragma warning disable CS0618 // Type or member is obsolete
			return new(self.Path, self.Prefix, TuPack.Encoding.GetKeyEncoder<T1>());
#pragma warning restore CS0618 // Type or member is obsolete
		}

		/// <summary>Returned a typed version of the current path</summary>
		/// <typeparam name="T1">Type of the key</typeparam>
		/// <param name="self">Existing subspace path</param>
		/// <param name="encoding">If specified, change the encoding use by the current path. If <c>null</c>, inherit the current encoding</param>
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static TypedKeySubspaceLocation<T1> AsTyped<T1>(this ISubspaceLocation self, IKeyEncoding encoding)
		{
			Contract.NotNull(self);
			Contract.NotNull(encoding);

			if (self is TypedKeySubspaceLocation<T1> tsp)
			{
				if (encoding == tsp.Encoder.Encoding) return tsp;
			}

			return new(self.Path, self.Prefix, encoding.GetKeyEncoder<T1>());
		}

		/// <summary>Returned a typed version of the current path</summary>
		/// <typeparam name="T1">Type of the key</typeparam>
		/// <param name="self">Existing subspace path</param>
		/// <param name="encoder">Custom encoder used by subspace</param>
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
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
		public static TypedKeySubspaceLocation<T1, T2> AsTyped<T1, T2>(this ISubspaceLocation self)
		{
			Contract.NotNull(self);

			if (self is TypedKeySubspaceLocation<T1, T2> tsp)
			{
				return tsp;
			}

#pragma warning disable CS0618 // Type or member is obsolete
			return new(self.Path, self.Prefix, TuPack.Encoding.GetKeyEncoder<T1, T2>());
#pragma warning restore CS0618 // Type or member is obsolete
		}

		/// <summary>Returned a typed version of the current path</summary>
		/// <typeparam name="T1">Type of the first part of the key</typeparam>
		/// <typeparam name="T2">Type of the second part of the key</typeparam>
		/// <param name="self">Existing subspace path</param>
		/// <param name="encoding">If specified, change the encoding use by the current path. If <c>null</c>, inherit the current encoding</param>
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static TypedKeySubspaceLocation<T1, T2> AsTyped<T1, T2>(this ISubspaceLocation self, IKeyEncoding encoding)
		{
			Contract.NotNull(self);
			Contract.NotNull(encoding);

			if (self is TypedKeySubspaceLocation<T1, T2> tsp)
			{
				if (encoding == tsp.Encoder.Encoding) return tsp;
			}

			return new(self.Path, self.Prefix, encoding.GetKeyEncoder<T1, T2>());
		}

		/// <summary>Returned a typed version of the current path</summary>
		/// <typeparam name="T1">Type of the first part of the key</typeparam>
		/// <typeparam name="T2">Type of the second part of the key</typeparam>
		/// <param name="self">Existing subspace path</param>
		/// <param name="encoder">Custom encoder used by subspace</param>
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static TypedKeySubspaceLocation<T1, T2> UsingEncoder<T1, T2>(this ISubspaceLocation self, ICompositeKeyEncoder<T1, T2> encoder)
		{
			Contract.NotNull(self);
			Contract.NotNull(encoder);

			if (self is TypedKeySubspaceLocation<T1, T2> tsp)
			{
				if (encoder == tsp.Encoder) return tsp;
			}

			return new(self.Path, self.Prefix, encoder);
		}

		/// <summary>Returned a typed version of the current path</summary>
		/// <typeparam name="T1">Type of the first part of the key</typeparam>
		/// <typeparam name="T2">Type of the second part of the key</typeparam>
		/// <typeparam name="T3">Type of the third part of the key</typeparam>
		/// <param name="self">Existing subspace path</param>
		public static TypedKeySubspaceLocation<T1, T2, T3> AsTyped<T1, T2, T3>(this ISubspaceLocation self)
		{
			Contract.NotNull(self);

			if (self is TypedKeySubspaceLocation<T1, T2, T3> tsp)
			{
				return tsp;
			}

#pragma warning disable CS0618 // Type or member is obsolete
			return new(self.Path, self.Prefix, TuPack.Encoding.GetKeyEncoder<T1, T2, T3>());
#pragma warning restore CS0618 // Type or member is obsolete
		}

		/// <summary>Returned a typed version of the current path</summary>
		/// <typeparam name="T1">Type of the first part of the key</typeparam>
		/// <typeparam name="T2">Type of the second part of the key</typeparam>
		/// <typeparam name="T3">Type of the third part of the key</typeparam>
		/// <param name="self">Existing subspace path</param>
		/// <param name="encoding">If specified, change the encoding use by the current path. If <c>null</c>, inherit the current encoding</param>
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static TypedKeySubspaceLocation<T1, T2, T3> AsTyped<T1, T2, T3>(this ISubspaceLocation self, IKeyEncoding encoding)
		{
			Contract.NotNull(self);
			Contract.NotNull(encoding);

			if (self is TypedKeySubspaceLocation<T1, T2, T3> tsp)
			{
				if (encoding == tsp.Encoder.Encoding) return tsp;
			}

			return new(self.Path, self.Prefix, encoding.GetKeyEncoder<T1, T2, T3>());
		}

		/// <summary>Returned a typed version of the current path</summary>
		/// <typeparam name="T1">Type of the first part of the key</typeparam>
		/// <typeparam name="T2">Type of the second part of the key</typeparam>
		/// <typeparam name="T3">Type of the third part of the key</typeparam>
		/// <param name="self">Existing subspace path</param>
		/// <param name="encoder">Custom encoder used by subspace</param>
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static TypedKeySubspaceLocation<T1, T2, T3> UsingEncoder<T1, T2, T3>(this ISubspaceLocation self, ICompositeKeyEncoder<T1, T2, T3> encoder)
		{
			Contract.NotNull(self);
			Contract.NotNull(encoder);

			if (self is TypedKeySubspaceLocation<T1, T2, T3> tsp)
			{
				if (encoder == tsp.Encoder) return tsp;
			}

			return new(self.Path, self.Prefix, encoder);
		}

	}

	/// <summary>Factory methods for creating <see cref="ISubspaceLocation">locations</see> in the keyspace of a FoundationDB database.</summary>
	[PublicAPI]
	public static class SubspaceLocation
	{

		/// <summary>Represent the root directory of the Directory Layer</summary>
		public static readonly DynamicKeySubspaceLocation Root
#pragma warning disable CS0618 // Type or member is obsolete
			= new(FdbPath.Root, Slice.Empty, TuPack.Encoding.GetDynamicKeyEncoder());
#pragma warning restore CS0618 // Type or member is obsolete

		/// <summary>Represent a location without any prefix, and outside the jurisdiction of the Directory Layer</summary>
		public static readonly DynamicKeySubspaceLocation Empty
#pragma warning disable CS0618 // Type or member is obsolete
			= new(Slice.Empty, TuPack.Encoding.GetDynamicKeyEncoder());
#pragma warning restore CS0618 // Type or member is obsolete

		#region FromPath...

		[Pure]
		public static DynamicKeySubspaceLocation FromPath(FdbPath path)
		{
#pragma warning disable CS0618 // Type or member is obsolete
			return new(path, default, TuPack.Encoding.GetDynamicKeyEncoder());
#pragma warning restore CS0618 // Type or member is obsolete
		}

		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static DynamicKeySubspaceLocation FromPath(FdbPath path, IDynamicKeyEncoder? encoder)
		{
			return new(path, default, encoder ?? TuPack.Encoding.GetDynamicKeyEncoder());
		}

		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static DynamicKeySubspaceLocation FromPath(FdbPath path, IDynamicKeyEncoding? encoding)
		{
			return new(path, default, (encoding ?? TuPack.Encoding).GetDynamicKeyEncoder());
		}

		[Pure]
		public static TypedKeySubspaceLocation<T1> FromPath<T1>(FdbPath path)
		{
#pragma warning disable CS0618 // Type or member is obsolete
			return new(path, Slice.Empty, TuPack.Encoding.GetKeyEncoder<T1>());
#pragma warning restore CS0618 // Type or member is obsolete
		}

		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static TypedKeySubspaceLocation<T1> FromPath<T1>(FdbPath path, IKeyEncoder<T1>? encoder)
		{
			return new(path, default, encoder ?? TuPack.Encoding.GetKeyEncoder<T1>());
		}

		[Pure]
		public static TypedKeySubspaceLocation<T1, T2> FromPath<T1, T2>(FdbPath path)
		{
#pragma warning disable CS0618 // Type or member is obsolete
			return new(path, default, TuPack.Encoding.GetKeyEncoder<T1, T2>());
#pragma warning restore CS0618 // Type or member is obsolete
		}

		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static TypedKeySubspaceLocation<T1, T2> FromPath<T1, T2>(FdbPath path, ICompositeKeyEncoder<T1, T2>? encoder)
		{
			return new(path, default, encoder ?? TuPack.Encoding.GetKeyEncoder<T1, T2>());
		}

		[Pure]
		public static TypedKeySubspaceLocation<T1, T2, T3> FromPath<T1, T2, T3>(FdbPath path)
		{
#pragma warning disable CS0618 // Type or member is obsolete
			return new(path, default, TuPack.Encoding.GetKeyEncoder<T1, T2, T3>());
#pragma warning restore CS0618 // Type or member is obsolete
		}

		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static TypedKeySubspaceLocation<T1, T2, T3> FromPath<T1, T2, T3>(FdbPath path, ICompositeKeyEncoder<T1, T2, T3>? encoder)
		{
			return new(path, default, encoder ?? TuPack.Encoding.GetKeyEncoder<T1, T2, T3>());
		}

		[Pure]
		public static TypedKeySubspaceLocation<T1, T2, T3, T4> FromPath<T1, T2, T3, T4>(FdbPath path)
		{
#pragma warning disable CS0618 // Type or member is obsolete
			return new(path, default, TuPack.Encoding.GetKeyEncoder<T1, T2, T3, T4>());
#pragma warning restore CS0618 // Type or member is obsolete
		}

		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static TypedKeySubspaceLocation<T1, T2, T3, T4> FromPath<T1, T2, T3, T4>(FdbPath path, ICompositeKeyEncoder<T1, T2, T3, T4>? encoder)
		{
			return new(path, default, encoder ?? TuPack.Encoding.GetKeyEncoder<T1, T2, T3, T4>());
		}

		#endregion

		#region FromKey...

		/// <summary>Creates a location that uses a fixed prefix given by a tuple</summary>
		[Pure]
		public static DynamicKeySubspaceLocation FromKey(IVarTuple items)
		{
#pragma warning disable CS0618 // Type or member is obsolete
			var encoder = TuPack.Encoding.GetDynamicKeyEncoder();
			return new(encoder.Pack(items), encoder);
#pragma warning restore CS0618 // Type or member is obsolete
		}

		/// <summary>Creates a location that uses a fixed prefix given by a tuple</summary>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static DynamicKeySubspaceLocation FromKey(IVarTuple items, IDynamicKeyEncoding encoding)
		{
			Contract.NotNull(encoding);
			var encoder = encoding.GetDynamicKeyEncoder();
			return new(encoder.Pack(items), encoder);
		}

		/// <summary>Creates a location for dynamic keys, that uses a fixed prefix given by a tuple</summary>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static DynamicKeySubspaceLocation FromKey(IVarTuple items, IDynamicKeyEncoder? encoder)
		{
			encoder ??= TuPack.Encoding.GetDynamicKeyEncoder();
			return new(encoder.Pack(items), encoder);
		}

		/// <summary>Creates a location for dynamic keys, that uses a fixed binary prefix</summary>
		[Pure]
		public static DynamicKeySubspaceLocation FromKey(ReadOnlySpan<byte> key)
		{
#pragma warning disable CS0618 // Type or member is obsolete
			return new(Slice.FromBytes(key), TuPack.Encoding.GetDynamicKeyEncoder());
#pragma warning restore CS0618 // Type or member is obsolete
		}

		/// <summary>Creates a location for dynamic keys, that uses a fixed binary prefix</summary>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static DynamicKeySubspaceLocation FromKey(ReadOnlySpan<byte> key, IDynamicKeyEncoding encoding)
		{
			Contract.NotNull(encoding);
			return new(Slice.FromBytes(key), encoding.GetDynamicKeyEncoder());
		}

		/// <summary>Creates a location for dynamic keys, that uses a fixed binary prefix</summary>
		[Pure]
		public static DynamicKeySubspaceLocation FromKey(Slice key)
		{
			if (key.IsNull) throw new ArgumentException("Key cannot be nil.", nameof(key));
#pragma warning disable CS0618 // Type or member is obsolete
			return new(key, TuPack.Encoding.GetDynamicKeyEncoder());
#pragma warning restore CS0618 // Type or member is obsolete
		}

		/// <summary>Creates a location for dynamic keys, that uses a fixed binary prefix</summary>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static DynamicKeySubspaceLocation FromKey(Slice key, IDynamicKeyEncoding encoding)
		{
			if (key.IsNull) throw new ArgumentException("Key cannot be nil.", nameof(key));
			return new(key, encoding.GetDynamicKeyEncoder());
		}

		/// <summary>Creates a location for dynamic keys, that uses a fixed binary prefix and dynamic keys</summary>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static DynamicKeySubspaceLocation FromKey(Slice key, IDynamicKeyEncoder encoder)
		{
			if (key.IsNull) throw new ArgumentException("Key cannot be nil.", nameof(key));
			return new(key, encoder);
		}

		/// <summary>Creates a location for typed keys, that uses a fixed binary prefix</summary>
		[Pure]
		public static TypedKeySubspaceLocation<T1> FromKey<T1>(Slice key)
		{
			if (key.IsNull) throw new ArgumentException("Key cannot be nil.", nameof(key));
#pragma warning disable CS0618 // Type or member is obsolete
			return new(key, TuPack.Encoding.GetKeyEncoder<T1>());
#pragma warning restore CS0618 // Type or member is obsolete
		}

		/// <summary>Creates a location for typed keys, that uses a fixed binary prefix</summary>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static TypedKeySubspaceLocation<T1> FromKey<T1>(Slice key, IKeyEncoder<T1> encoder)
		{
			if (key.IsNull) throw new ArgumentException("Key cannot be nil.", nameof(key));
			return new(key, encoder);
		}

		/// <summary>Creates a location for typed keys, that uses a fixed binary prefix</summary>
		[Pure]
		public static TypedKeySubspaceLocation<T1, T2> FromKey<T1, T2>(Slice key)
		{
			if (key.IsNull) throw new ArgumentException("Key cannot be nil.", nameof(key));
#pragma warning disable CS0618 // Type or member is obsolete
			return new(key, TuPack.Encoding.GetKeyEncoder<T1, T2>());
#pragma warning restore CS0618 // Type or member is obsolete
		}

		/// <summary>Creates a location for typed keys, that uses a fixed binary prefix</summary>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static TypedKeySubspaceLocation<T1, T2> FromKey<T1, T2>(Slice key, ICompositeKeyEncoder<T1, T2> encoder)
		{
			if (key.IsNull) throw new ArgumentException("Key cannot be nil.", nameof(key));
			return new(key, encoder);
		}

		/// <summary>Creates a location for typed keys, that uses a fixed binary prefix</summary>
		[Pure]
		public static TypedKeySubspaceLocation<T1, T2, T3> FromKey<T1, T2, T3>(Slice key)
		{
			if (key.IsNull) throw new ArgumentException("Key cannot be nil.", nameof(key));
#pragma warning disable CS0618 // Type or member is obsolete
			return new(key, TuPack.Encoding.GetKeyEncoder<T1, T2, T3>());
#pragma warning restore CS0618 // Type or member is obsolete
		}

		/// <summary>Creates a location for typed keys, that uses a fixed binary prefix</summary>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static TypedKeySubspaceLocation<T1, T2, T3> FromKey<T1, T2, T3>(Slice key, ICompositeKeyEncoder<T1, T2, T3> encoder)
		{
			if (key.IsNull) throw new ArgumentException("Key cannot be nil.", nameof(key));
			return new(key, encoder);
		}

		/// <summary>Creates a location for typed keys, that uses a fixed binary prefix</summary>
		[Pure]
		public static TypedKeySubspaceLocation<T1, T2, T3, T4> FromKey<T1, T2, T3, T4>(Slice key)
		{
			if (key.IsNull) throw new ArgumentException("Key cannot be nil.", nameof(key));
#pragma warning disable CS0618 // Type or member is obsolete
			return new(key, TuPack.Encoding.GetKeyEncoder<T1, T2, T3, T4>());
#pragma warning restore CS0618 // Type or member is obsolete
		}

		/// <summary>Creates a location for typed keys, that uses a fixed binary prefix</summary>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static TypedKeySubspaceLocation<T1, T2, T3, T4> FromKey<T1, T2, T3, T4>(Slice key, ICompositeKeyEncoder<T1, T2, T3, T4> encoder)
		{
			if (key.IsNull) throw new ArgumentException("Key cannot be nil.", nameof(key));
			return new(key, encoder);
		}

		#endregion

	}

}

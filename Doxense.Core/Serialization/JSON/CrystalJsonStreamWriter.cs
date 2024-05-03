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
	using System.IO;
	using System.Text;

	/// <summary>Classe capable d'écrire des fragments de JSON, en mode stream</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public sealed class CrystalJsonStreamWriter : IDisposable //TODO: IAsyncDisposable !
	{
		// On utilise un CrystalJsonWriter classique, qui va écrire dans un MemoryStream qui sert de tampon, de manière classique.
		// Le writer écrit dans un TextWriter, qui lui même flush périodiquement dans le MemoryStream. Jusque la, tout reste en non-async et ne bloque jamais.
		// Régulièrement, ou lors que le tampon est assez gros, on le flush sur le stream, et cela de manière async.

		/// <summary>Limite de taille du tampon au delà de laquelle on va faire un flush implicite</summary>
		/// <remarks>Ce n'est qu'une limite indicatif, et non pas une limite absolue! Si un item génère 1GB de JSON d'un seul coup, le tampon devra quand même grossir jusqu'à cette taille.</remarks>
		private const int AUTOFLUSH_THRESHOLD = 256 * 1024;

		/// <summary>Stream sous-jacent (dans lequel on flush le tampon de manière asynchrone)</summary>
		private readonly Stream m_stream;
		/// <summary>Tampon mémoire</summary>
		private readonly MemoryStream m_scratch;
		/// <summary>JSON writer qui écrit dans le tampon</summary>
		private readonly CrystalJsonWriter m_writer;
		/// <summary>Si true, il faut disposer m_stream lorsque cette instance est elle même disposée</summary>
		private readonly bool m_ownStream;
		/// <summary>Si true, cette instance a déjà été disposée</summary>
		private bool m_disposed;

		/// <summary>Dernier type visité (m_visitor contient le delegate en cache)</summary>
		private Type? m_lastType;
		/// <summary>Visiteur en cache pour des éléments du même type que m_lastType</summary>
		private CrystalJsonTypeVisitor? m_visitor;

		public CrystalJsonStreamWriter(Stream output, CrystalJsonSettings? settings, CrystalJsonTypeResolver? resolver = null, bool ownStream = false)
		{
			Contract.NotNull(output);

			m_stream = output;
			m_ownStream = ownStream;
			settings ??= CrystalJsonSettings.JsonCompact;
			resolver ??= CrystalJson.DefaultResolver;

			m_scratch = new MemoryStream(65536);
			m_writer = new CrystalJsonWriter(new StreamWriter(m_scratch, Encoding.UTF8), settings, resolver);
		}

		public CrystalJsonWriter.NodeType CurrentNode => m_writer.CurrentState.Node;

		/// <summary>Retourne une estimation de la position dans le stream source, pour information</summary>
		/// <remarks>ATTENTION: cette valeur ne peut être considérée comme valide que juste après un Flush!</remarks>
		public long? PositionHint
		{
			get
			{
				if (m_disposed) ThrowDisposed();
				//note: s'il n'y a pas eu de flush récent, il peut y avoir des caractères encore en cache dans Writer (pas flushés dans le scratch buffer)
				return m_stream.Position + m_scratch.Length;
			}
		}

		/// <summary>Ecrit un fragment de document en une seule passe, et flush le stream</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="item"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public void WriteFragment<T>(T item, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var visitor = GetVisitor(typeof(T));
			visitor(item, typeof(T), item != null ? item.GetType() : null, m_writer);
			m_writer.Buffer.WriteLine();

			FlushInternal(true);
		}

		/// <summary>Ecrit un fragment de document en une seule passe, et flush le stream</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="item"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public Task WriteFragmentAsync<T>(T item, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var visitor = GetVisitor(typeof(T));
			visitor(item, typeof(T), item != null ? item.GetType() : null, m_writer);
			m_writer.Buffer.WriteLine();

			return FlushInternalAsync(true, cancellationToken);
		}

		#region Objects...

		/// <summary>Ecrit un document top-level de type object, et flush le stream</summary>
		/// <param name="handler">Handler appelé, chargé d'écrire le contenu de l'objet</param>
		/// <param name="cancellationToken"></param>
		/// <returns>Task qui se termine quand l'objet a été écrit entièrement, et après voir flushé le buffer sur le stream</returns>
		/// <remarks>Cette méthode ne doit idéalement être utilisé que pour des documents top-level. Pour des sous-objets, utilisez <see cref="BeginObjectFragment"/>.</remarks>
		public void WriteObjectFragment([InstantHandle] Action<ObjectStream> handler, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			using (var obj = BeginObjectFragment())
			{
				handler(obj);
			}
			FlushInternal(true);
		}

		/// <summary>Ecrit un document top-level de type object, et flush le stream</summary>
		/// <param name="handler">Handler appelé, chargé d'écrire le contenu de l'objet</param>
		/// <param name="cancellationToken"></param>
		/// <returns>Task qui se termine quand l'objet a été écrit entièrement, et après voir flushé le buffer sur le stream</returns>
		/// <remarks>Cette méthode ne doit idéalement être utilisé que pour des documents top-level. Pour des sous-objets, utilisez <see cref="BeginObjectFragment"/>.</remarks>
		public async Task WriteObjectFragmentAsync([InstantHandle] Func<ObjectStream, Task> handler, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			using (var obj = BeginObjectFragment())
			{
				await handler(obj).ConfigureAwait(false);
			}
			await FlushInternalAsync(true, cancellationToken).ConfigureAwait(false);
		}

		[PublicAPI]
		public sealed class ObjectStream : IDisposable
		{
			private readonly CrystalJsonStreamWriter m_parent;
			private readonly CancellationToken m_ct;
			private readonly CrystalJsonWriter.State m_state;

			internal ObjectStream(CrystalJsonStreamWriter parent, CrystalJsonWriter.State state, CancellationToken cancellationToken)
			{
				m_parent = parent;
				m_ct = cancellationToken;
				m_state = state;
			}

			public CrystalJsonWriter Writer => m_parent.m_writer;

			public CancellationToken Cancellation => m_ct;

			private void VisitProperty(string name, CrystalJsonTypeVisitor? visitor, Type type, object? item)
			{
				var writer = m_parent.m_writer;
				if (writer.MarkNext())
				{
					writer.Buffer.Write(",\r\n ");
				}
				else
				{
					writer.Buffer.Write(' ');
				}
				writer.WritePropertyName(name);

				if (item == null || visitor == null)
				{
					writer.WriteNull();
				}
				else
				{
					visitor(item, type, item.GetType(), writer);
				}
			}

			public void WriteFieldNull(string name)
			{
				m_parent.EnsureInObjectMode();
				VisitProperty(name, null, typeof(object), null);
			}

			public void WriteField<T>(string name, T value)
			{
				m_parent.EnsureInObjectMode();
				VisitProperty(name, CrystalJsonVisitor.GetVisitorForType(typeof(T), atRuntime: false), typeof(T), value);
			}

			public void WriteField<T>(string name, T? value)
				where T : struct
			{
				m_parent.EnsureInObjectMode();
				if (value.HasValue)
				{
					VisitProperty(name, CrystalJsonVisitor.GetVisitorForType(typeof(T), atRuntime: false), typeof(T), value.Value);
				}
				else
				{
					VisitProperty(name, null, typeof(object), null);
				}
			}

			[Pure]
			public ArrayStream BeginArrayStream(string name)
			{
				m_ct.ThrowIfCancellationRequested();
				m_parent.EnsureInObjectMode();
				var writer = m_parent.m_writer;
				if (writer.MarkNext())
				{
					writer.Buffer.Write(",\r\n ");
				}
				else
				{
					writer.Buffer.Write(' ');
				}
				writer.WritePropertyName(name);

				return m_parent.BeginArrayFragment(m_ct);
			}

			[Pure]
			public ObjectStream BeginObjectStream(string name)
			{
				m_ct.ThrowIfCancellationRequested();
				m_parent.EnsureInObjectMode();
				var writer = m_parent.m_writer;
				if (writer.MarkNext())
				{
					writer.Buffer.Write(",\r\n ");
				}
				else
				{
					writer.Buffer.Write(' ');
				}
				writer.WritePropertyName(name);

				return m_parent.BeginObjectFragment(m_ct);
			}

			public void Dispose()
			{
				var writer = m_parent.m_writer;
				var state = writer.CurrentState;
				if (state.Node != CrystalJsonWriter.NodeType.Object) throw new InvalidOperationException("Invalid writer state: object mode expected");

				if (state.Tail)
				{
					writer.Buffer.WriteLine();
				}
				writer.Buffer.WriteLine("}");
				writer.PopState(m_state);
			}

		}

		/// <summary>Démarre manuellement un object, quelque soit le niveau de profondeur actuel</summary>
		/// <returns>Sous-stream dans lequel écrire le fragment, et qu'il faut Dispose() lorsqu'il est terminé</returns>
		[Pure]
		public ObjectStream BeginObjectFragment(CancellationToken cancellationToken = default)
		{
			var state = m_writer.PushState(CrystalJsonWriter.NodeType.Object);
			m_writer.Buffer.WriteLine("{");
			return new ObjectStream(this, state, cancellationToken);
		}

		private void EnsureInObjectMode()
		{
			if (m_disposed) ThrowDisposed();

			if (m_writer.CurrentState.Node != CrystalJsonWriter.NodeType.Object)
			{
				throw new InvalidOperationException("Invalid writer state: can only write fields in object mode. Did you forget to call BeginObject() first?");
			}
		}

		#endregion

		#region Arrays...

		/// <summary>Ecrit un document top-level de type array, et flush le stream</summary>
		/// <param name="handler">Handler appelé, chargé d'écrire le contenu de l'array</param>
		/// <param name="cancellationToken"></param>
		/// <returns>Task qui se termine quand l'array a été écrite entièrement, et après voir flushé le buffer sur le stream</returns>
		/// <remarks>Cette méthode ne doit idéalement être utilisé que pour des documents top-level. Pour des sous-objets, utilisez <see cref="BeginArrayFragment"/>.</remarks>
		public void WriteArrayFragment([InstantHandle] Action<ArrayStream> handler, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (var array = BeginArrayFragment(cancellationToken))
			{
				handler(array);
			}
			FlushInternal(true);
		}

		/// <summary>Write a top-level JSON array</summary>
		/// <param name="state">Value that will be passed as argument to <paramref name="handler"/></param>
		/// <param name="handler">Handler that will write the content of the array to the stream</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <remarks>
		/// <para>The stream will be flushed after this call</para>
		/// <para>This method should only be used for top-level documents. If you want to output a collection of child objects, please use <see cref="BeginArrayFragment"/>.</para>
		/// </remarks>
		public void WriteArrayFragment<TState>(TState state, [InstantHandle] Action<TState, ArrayStream> handler, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (var array = BeginArrayFragment(cancellationToken))
			{
				handler(state, array);
			}
			FlushInternal(true);
		}

		/// <summary>Ecrit un document top-level de type array, et flush le stream</summary>
		/// <param name="handler">Handler appelé, chargé d'écrire le contenu de l'array</param>
		/// <param name="cancellationToken"></param>
		/// <returns>Task qui se termine quand l'array a été écrite entièrement, et après voir flushé le buffer sur le stream</returns>
		/// <remarks>Cette méthode ne doit idéalement être utilisé que pour des documents top-level. Pour des sous-objets, utilisez <see cref="BeginArrayFragment"/>.</remarks>
		public async Task WriteArrayFragmentAsync([InstantHandle] Func<ArrayStream, Task> handler, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (var array = BeginArrayFragment(cancellationToken))
			{
				await handler(array).ConfigureAwait(false);
			}
			await FlushInternalAsync(true, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>Ecrit un document top-level de type array, copie le contenu entier de la séquence d'élément une array spécifié, et flush le stream</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="items">Séquence de l'intégralité des éléments constituant l'array</param>
		/// <param name="cancellationToken"></param>
		public void WriteArrayFragment<T>(IEnumerable<T> items, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (var array = BeginArrayFragment(cancellationToken))
			{
				array.WriteBatch(items);
			}
			FlushInternal(true);
		}

		/// <summary>Ecrit un document top-level de type array, copie le contenu entier de la séquence d'élément une array spécifié, et flush le stream</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="items">Séquence de l'intégralité des éléments constituant l'array</param>
		/// <param name="cancellationToken"></param>
		public async Task WriteArrayFragmentAsync<T>(IEnumerable<T> items, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (var array = BeginArrayFragment(cancellationToken))
			{
				await array.WriteBatchAsync(items).ConfigureAwait(false);
			}
			await FlushInternalAsync(true, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>Ecrit un document top-level de type array, copie le contenu entier de la séquence de valeur JSON, et flush le stream</summary>
		/// <param name="items">Séquence de l'intégralité des éléments constituant l'array</param>
		/// <param name="cancellationToken"></param>
		public void WriteArrayFragment(IEnumerable<JsonValue> items, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (var array = BeginArrayFragment(cancellationToken))
			{
				array.WriteBatch(items);
			}
			FlushInternal(true);
		}

		/// <summary>Ecrit un document top-level de type array, copie le contenu entier de la séquence de valeur JSON, et flush le stream</summary>
		/// <param name="items">Séquence de l'intégralité des éléments constituant l'array</param>
		/// <param name="cancellationToken"></param>
		public async Task WriteArrayFragmentAsync(IEnumerable<JsonValue> items, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (var array = BeginArrayFragment(cancellationToken))
			{
				await array.WriteBatchAsync(items).ConfigureAwait(false);
			}
			await FlushInternalAsync(true, cancellationToken).ConfigureAwait(false);
		}

		[PublicAPI]
		public sealed class ArrayStream : IDisposable
		{
			private readonly CrystalJsonStreamWriter m_parent;
			private readonly CancellationToken m_ct;
			private readonly CrystalJsonWriter.State m_state;

			internal ArrayStream(CrystalJsonStreamWriter parent, CrystalJsonWriter.State state, CancellationToken cancellationToken)
			{
				m_parent = parent;
				m_ct = cancellationToken;
				m_state = state;
			}

			public CrystalJsonWriter Writer => m_parent.m_writer;

			public CancellationToken Cancellation => m_ct;

			private void VisitValue(JsonValue value)
			{
				var writer = m_parent.m_writer;
				if (writer.MarkNext())
				{
					writer.Buffer.Write(",\r\n ");
				}
				else
				{
					writer.Buffer.Write(' ');
				}
				value.JsonSerialize(writer);
			}

			private void VisitArrayItem(CrystalJsonTypeVisitor visitor, Type type, object? item)
			{
				var writer = m_parent.m_writer;
				if (writer.MarkNext())
				{
					writer.Buffer.Write(",\r\n ");
				}
				else
				{
					writer.Buffer.Write(' ');
				}
				visitor(item, type, item != null ? item.GetType() : typeof(object), writer);
			}

			/// <summary>Ajoute un JsonValue dans un tableau ouvert</summary>
			public void WriteItem(JsonValue item)
			{
				m_ct.ThrowIfCancellationRequested();
				m_parent.EnsureInArrayMode();

				VisitValue(item);

				if (m_parent.ShouldFlush)
				{
					m_parent.FlushInternal(false);
				}
			}

			/// <summary>Ajoute un JsonValue dans un tableau ouvert</summary>
			public async Task WriteItemAsync(JsonValue item)
			{
				m_ct.ThrowIfCancellationRequested();
				m_parent.EnsureInArrayMode();

				VisitValue(item);

				if (m_parent.ShouldFlush)
				{
					await m_parent.FlushInternalAsync(false, m_ct).ConfigureAwait(false);
				}
			}

			/// <summary>Ajoute un élément dans un tableau ouvert</summary>
			/// <typeparam name="T">Type de l'élément</typeparam>
			/// <param name="item">Element ajouté</param>
			/// <returns></returns>
			public void WriteItem<T>(T item)
			{
				m_ct.ThrowIfCancellationRequested();
				m_parent.EnsureInArrayMode();

				VisitArrayItem(m_parent.GetVisitor(typeof(T)), typeof(T), item);

				if (m_parent.ShouldFlush)
				{
					m_parent.FlushInternal(false);
				}
			}

			/// <summary>Ajoute un élément dans un tableau ouvert</summary>
			/// <typeparam name="T">Type de l'élément</typeparam>
			/// <param name="item">Element ajouté</param>
			/// <returns></returns>
			public async Task WriteItemAsync<T>(T item)
			{
				m_ct.ThrowIfCancellationRequested();
				m_parent.EnsureInArrayMode();

				VisitArrayItem(m_parent.GetVisitor(typeof(T)), typeof(T), item);

				if (m_parent.ShouldFlush)
				{
					await m_parent.FlushInternalAsync(false, m_ct).ConfigureAwait(false);
				}
			}

			/// <summary>Ajoute une liste de JsonValue dans un tableau ouvert</summary>
			public void WriteBatch(IEnumerable<JsonValue> items)
			{
				Contract.NotNull(items);
				m_parent.EnsureInArrayMode();

				foreach (var item in items)
				{
					VisitValue(item);

					if (m_parent.ShouldFlush)
					{
						m_parent.FlushInternal(false);
					}
				}
			}

			/// <summary>Ajoute une liste de JsonValue dans un tableau ouvert</summary>
			public async Task WriteBatchAsync(IEnumerable<JsonValue> items)
			{
				Contract.NotNull(items);
				m_parent.EnsureInArrayMode();

				foreach (var item in items)
				{
					VisitValue(item);

					if (m_parent.ShouldFlush)
					{
						await m_parent.FlushInternalAsync(false, m_ct).ConfigureAwait(false);
					}
				}
			}

			/// <summary>Ajoute plusieurs éléments dans un tableau ouvert</summary>
			public void WriteBatch<T>(IEnumerable<T> items)
			{
				Contract.NotNull(items);
				m_parent.EnsureInArrayMode();

				var type = typeof(T);
				var visitor = m_parent.GetVisitor(type);
				foreach (var item in items)
				{
					m_ct.ThrowIfCancellationRequested();
					VisitArrayItem(visitor, type, item);

					if (m_parent.ShouldFlush)
					{
						m_parent.FlushInternal(false);
					}
				}
			}

			/// <summary>Ajoute plusieurs éléments dans un tableau ouvert</summary>
			public async Task WriteBatchAsync<T>(IEnumerable<T> items)
			{
				Contract.NotNull(items);
				m_parent.EnsureInArrayMode();

				var type = typeof(T);
				var visitor = m_parent.GetVisitor(type);
				foreach (var item in items)
				{
					m_ct.ThrowIfCancellationRequested();
					VisitArrayItem(visitor, type, item);

					if (m_parent.ShouldFlush)
					{
						await m_parent.FlushInternalAsync(false, m_ct).ConfigureAwait(false);
					}
				}
			}

			/// <summary>Ajoute plusieurs éléments dans un tableau ouvert, en appliquant une transformation sur chaque élément</summary>
			/// <typeparam name="TInput">Type des éléments de la séquence source</typeparam>
			/// <typeparam name="TOutput">Type des éléments transformés</typeparam>
			/// <param name="items">Séquence des éléments constituants le batch</param>
			/// <param name="selector">Transformation appliquée à chaque éléments de <paramref name="items"/></param>
			/// <returns></returns>
			public void WriteBatch<TInput, TOutput>(IEnumerable<TInput> items, [InstantHandle] Func<TInput, TOutput> selector)
			{
				Contract.NotNull(items);
				m_parent.EnsureInArrayMode();

				var type = typeof(TOutput);
				var visitor = m_parent.GetVisitor(type);

				foreach (var item in items)
				{
					m_ct.ThrowIfCancellationRequested();
					VisitArrayItem(visitor, type, selector(item));

					if (m_parent.ShouldFlush)
					{
						m_parent.FlushInternal(false);
					}
				}
			}

			/// <summary>Ajoute plusieurs éléments dans un tableau ouvert, en appliquant une transformation sur chaque élément</summary>
			/// <typeparam name="TInput">Type des éléments de la séquence source</typeparam>
			/// <typeparam name="TOutput">Type des éléments transformés</typeparam>
			/// <param name="items">Séquence des éléments constituants le batch</param>
			/// <param name="selector">Transformation appliquée à chaque éléments de <paramref name="items"/></param>
			/// <returns></returns>
			public async Task WriteBatchAsync<TInput, TOutput>(IEnumerable<TInput> items, [InstantHandle] Func<TInput, TOutput> selector)
			{
				Contract.NotNull(items);
				m_parent.EnsureInArrayMode();

				var type = typeof(TOutput);
				var visitor = m_parent.GetVisitor(type);

				foreach (var item in items)
				{
					m_ct.ThrowIfCancellationRequested();
					VisitArrayItem(visitor, type, selector(item));

					if (m_parent.ShouldFlush)
					{
						await m_parent.FlushInternalAsync(false, m_ct).ConfigureAwait(false);
					}
				}
			}

			public void Dispose()
			{
				var writer = m_parent.m_writer;
				var state = writer.CurrentState;
				if (state.Node != CrystalJsonWriter.NodeType.Array) throw new InvalidOperationException("Invalid writer state: array mode expected");

				if (state.Tail)
				{
					writer.Buffer.WriteLine();
				}
				writer.Buffer.WriteLine("]");
				writer.PopState(m_state);
			}

		}

		/// <summary>Démarre manuellement une array, quelque soit le niveau de profondeur actuel</summary>
		/// <returns>Sous-stream, qu'il faut Dispose() une fois que l'array est terminée</returns>
		[Pure]
		public ArrayStream BeginArrayFragment(CancellationToken cancellationToken = default)
		{
			var state = m_writer.PushState(CrystalJsonWriter.NodeType.Array);
			m_writer.Buffer.WriteLine("[");
			return new ArrayStream(this, state, cancellationToken);
		}

		private void EnsureInArrayMode()
		{
			if (m_disposed) ThrowDisposed();

			if (m_writer.CurrentState.Node != CrystalJsonWriter.NodeType.Array)
			{
				throw new InvalidOperationException("Invalid writer state: can only write batched entries in Array mode. Did you forget to call BeginArray() first?");
			}
		}

		#endregion

		#region Buffer management...

		public Task FlushAsync(CancellationToken ct)
		{
			if (m_disposed) ThrowDisposed();
			return FlushInternalAsync(true, ct);
		}

		public void Flush()
		{
			if (m_disposed) ThrowDisposed();
			FlushInternal(true);
		}

		private CrystalJsonTypeVisitor GetVisitor(Type declaredType)
		{
			Contract.Debug.Requires(declaredType != null);

			if (declaredType != m_lastType)
			{
				m_lastType = declaredType;
				m_visitor = CrystalJsonVisitor.GetVisitorForType(declaredType);
			}
			Contract.Debug.Ensures(m_visitor != null && m_lastType != null);
			return m_visitor;
		}

		/// <summary>Indique si le tampon mémoire dépasse la limite de taille acceptable</summary>
		private bool ShouldFlush => !m_disposed && m_scratch.Length >= AUTOFLUSH_THRESHOLD;

		/// <summary>Fait en sorte que toutes les données écrites jusqu'a présent soient flushées dans le stream de destination</summary>
		private async Task FlushInternalAsync(bool flushStream, CancellationToken cancellationToken)
		{
			// Flush le TextWriter pour être sûr que tous les caractères écrits arrivent dans le scratch stream!
#if NET8_0_OR_GREATER
			await m_writer.Buffer.FlushAsync(cancellationToken).ConfigureAwait(false);
#else
			//BUGBUG: .NET 6 does not have an overload that takes a cancellation token :(
			await m_writer.Buffer.FlushAsync().ConfigureAwait(false);
#endif

			// flush le scratch buffer dans le stream de destination si nécessaire
			if (m_scratch.Length > 0)
			{
				try
				{
					m_scratch.Seek(0, SeekOrigin.Begin);
					//note: MemoryStream.CopyToAsync() est optimisé pour écrire en une seul passe, et ignore la taille du buffer
					await m_scratch.CopyToAsync(m_stream, 4096, cancellationToken).ConfigureAwait(false);
				}
				//TODO: si erreur, il faudrait nuker ??
				finally
				{
					m_scratch.SetLength(0);
				}
			}
			// force le stream à écrire sur le disque, si demandé par l'appelant
			if (flushStream)
			{
				//note: même si c'est un FileStream, il n'y a AUCUNE garantie que les données seront durablement sauvées! (write cache de l'OS)
				await m_stream.FlushAsync(cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>Fait en sorte que toutes les données écrites jusqu'a présent soient flushées dans le stream de destination</summary>
		/// <param name="flushStream"></param>
		private void FlushInternal(bool flushStream)
		{
			// Flush le TextWriter pour être sûr que tous les caractères écrits arrivent dans le scratch stream!
			m_writer.Buffer.Flush();

			// flush le scratch buffer dans le stream de destination si nécessaire
			if (m_scratch.Length != 0)
			{
				try
				{
					m_scratch.Seek(0, SeekOrigin.Begin);
					//note: MemoryStream.CopyTo() est optimisé pour écrire en une seul passe, et ignore la taille du buffer
					m_scratch.CopyTo(m_stream, 4096);
				}
				finally
				{
					// si la copie throw, Dispose() va être appelé juste après, et ne doit pas retry une deuxième fois!
					// => on force le buffer a 0, pour que Dispose() ne Flush() rien
					m_scratch.SetLength(0);
				}
			}

			// force le stream à écrire sur le disque, si demandé par l'appelant
			if (flushStream)
			{
				//note: même si c'est un FileStream, il n'y a AUCUNE garantie que les données seront durablement sauvées! (write cache de l'OS)
				m_stream.Flush();
			}
		}

		#endregion

		#region IDisposable...

		public void Dispose()
		{
			if (!m_disposed)
			{
				m_disposed = true;
				//note: on ne peut pas fermer la hiérachie automatiquement, car Dispose() peut être appelé suite à une erreur I/O sur le stream lui-même !
				try
				{
					FlushInternal(true);
				}
				finally
				{
					if (m_ownStream)
					{
						m_stream.Dispose();
					}
				}
			}
		}

		//note: en prévision de IAsyncDisposable
		public async Task DisposeAsync(CancellationToken ct) //REVIEW: pas sur pour le CT...
		{
			if (!m_disposed)
			{
				m_disposed = true;
				//note: on ne peut pas fermer la hiérachie automatiquement, car Dispose() peut être appelé suite à une erreur I/O sur le stream lui-même !
				try
				{
					await FlushInternalAsync(true, ct).ConfigureAwait(true);
				}
				finally
				{
					if (m_ownStream)
					{
						await m_stream.DisposeAsync().ConfigureAwait(false);
					}
				}
			}
		}

		[ContractAnnotation("=> halt")]
		private void ThrowDisposed()
		{
			throw new ObjectDisposedException(this.GetType().Name);
		}

		#endregion

		#region Static Helpers...

		[Pure]
		public static CrystalJsonStreamWriter Create(string path, CrystalJsonSettings? settings = null, CrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNullOrEmpty(path);

			Stream? stream = null;
			CrystalJsonStreamWriter? sw = null;
			bool failed = true;
			try
			{
				stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 0x400, FileOptions.SequentialScan);
				sw = new CrystalJsonStreamWriter(stream, settings, resolver, true);
				failed = false;
				return sw;
			}
			finally
			{
				if (failed)
				{
					sw?.Dispose();
					stream?.Dispose();
				}
			}
		}

		[Pure]
		public static CrystalJsonStreamWriter Create(Stream stream, CrystalJsonSettings? settings = null, CrystalJsonTypeResolver? resolver = null, bool ownStream = false)
		{
			Contract.NotNull(stream);

			CrystalJsonStreamWriter? sw = null;
			bool failed = true;
			try
			{
				sw = new CrystalJsonStreamWriter(stream, settings, resolver, ownStream);
				failed = false;
				return sw;
			}
			finally
			{
				if (failed)
				{
					sw?.Dispose();
				}
			}
		}

		#endregion

	}

}

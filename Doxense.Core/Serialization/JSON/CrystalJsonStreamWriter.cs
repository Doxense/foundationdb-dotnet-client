#region Copyright Doxense 2014-2021
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Classe capable d'�crire des fragments de JSON, en mode stream</summary>
	public sealed class CrystalJsonStreamWriter : IDisposable //TODO: IAsyncDisposable !
	{
		// On utiliser un CrystalJsonWriter classique, qui va �crire dans un MemoryStream qui sert de tampon, de mani�re classique.
		// Le writer �crit dans un TextWriter, qui lui m�me flush p�riodiquement dans le MemoryStream. Jusque la, tout reste en non-async et ne bloque jamais.
		// R�guli�rement, ou lors que le tampon est assez gros, on le flush sur le stream, et cela de mani�re async.

		/// <summary>Limite de taille du tampon au del� de laquelle on va faire un flush implicite</summary>
		/// <remarks>Ce n'est qu'une limite indicatif, et non pas une limite absolue! Si un item g�n�re 1GB de JSON d'un seul coup, le tampon devra quand m�me grossir jusqu'� cette taille.</remarks>
		private const int AUTOFLUSH_THRESHOLD = 256 * 1024;

		/// <summary>Stream sous-jacent (dans lequel on flush le tampon de mani�re asynchrone)</summary>
		private readonly Stream m_stream;
		/// <summary>Tampon m�moire</summary>
		private readonly MemoryStream m_scratch;
		/// <summary>JSON writer qui �crit dans le tampon</summary>
		private readonly CrystalJsonWriter m_writer;
		/// <summary>Si true, il faut disposer m_stream lorsque cette instance est elle m�me dispos�e</summary>
		private readonly bool m_ownStream;
		/// <summary>Si true, cette instance a d�j� �t� dispos�e</summary>
		private bool m_disposed;

		/// <summary>Dernier type visit� (m_visitor contient le delegate en cache)</summary>
		private Type? m_lastType;
		/// <summary>Visiteur en cache pour des �l�ments du m�me type que m_lastType</summary>
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
		/// <remarks>ATTENTION: cette valeur ne peut �tre consid�r�e comme valide que juste apr�s un Flush!</remarks>
		public long? PositionHint
		{
			get
			{
				if (m_disposed) ThrowDisposed();
				//note: s'il n'y a pas eu de flush r�cent, il peut y avoir des caract�res encore en cache dans Writer (pas flush�s dans le scratch buffer)
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
		/// <param name="handler">Handler appel�, charg� d'�crire le contenu de l'objet</param>
		/// <param name="cancellationToken"></param>
		/// <returns>Task qui se termine quand l'objet a �t� �crit enti�rement, et apr�s voir flush� le buffer sur le stream</returns>
		/// <remarks>Cette m�thode ne doit id�alement �tre utilis� que pour des documents top-level. Pour des sous-objets, utilisez <see cref="BeginObjectFragment"/>.</remarks>
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
		/// <param name="handler">Handler appel�, charg� d'�crire le contenu de l'objet</param>
		/// <param name="cancellationToken"></param>
		/// <returns>Task qui se termine quand l'objet a �t� �crit enti�rement, et apr�s voir flush� le buffer sur le stream</returns>
		/// <remarks>Cette m�thode ne doit id�alement �tre utilis� que pour des documents top-level. Pour des sous-objets, utilisez <see cref="BeginObjectFragment"/>.</remarks>
		public async Task WriteObjectFragmentAsync([InstantHandle] Func<ObjectStream, Task> handler, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			using (var obj = BeginObjectFragment())
			{
				await handler(obj).ConfigureAwait(false);
			}
			await FlushInternalAsync(true, cancellationToken).ConfigureAwait(false);
		}

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

		/// <summary>D�marre manuellement un object, quelque soit le niveau de profondeur actuel</summary>
		/// <returns>Sous-stream dans lequel �crire le fragment, et qu'il faut Dispose() lorsqu'il est termin�</returns>
		[Pure]
		public ObjectStream BeginObjectFragment(CancellationToken cancellationToken = default(CancellationToken))
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
		/// <param name="handler">Handler appel�, charg� d'�crire le contenu de l'array</param>
		/// <param name="cancellationToken"></param>
		/// <returns>Task qui se termine quand l'array a �t� �crite enti�rement, et apr�s voir flush� le buffer sur le stream</returns>
		/// <remarks>Cette m�thode ne doit id�alement �tre utilis� que pour des documents top-level. Pour des sous-objets, utilisez <see cref="BeginArrayFragment"/>.</remarks>
		public void WriteArrayFragment([InstantHandle] Action<ArrayStream> handler, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (var array = BeginArrayFragment(cancellationToken))
			{
				handler(array);
			}
			FlushInternal(true);
		}

		/// <summary>Ecrit un document top-level de type array, et flush le stream</summary>
		/// <param name="handler">Handler appel�, charg� d'�crire le contenu de l'array</param>
		/// <param name="cancellationToken"></param>
		/// <returns>Task qui se termine quand l'array a �t� �crite enti�rement, et apr�s voir flush� le buffer sur le stream</returns>
		/// <remarks>Cette m�thode ne doit id�alement �tre utilis� que pour des documents top-level. Pour des sous-objets, utilisez <see cref="BeginArrayFragment"/>.</remarks>
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
		/// <param name="handler">Handler appel�, charg� d'�crire le contenu de l'array</param>
		/// <param name="cancellationToken"></param>
		/// <returns>Task qui se termine quand l'array a �t� �crite enti�rement, et apr�s voir flush� le buffer sur le stream</returns>
		/// <remarks>Cette m�thode ne doit id�alement �tre utilis� que pour des documents top-level. Pour des sous-objets, utilisez <see cref="BeginArrayFragment"/>.</remarks>
		public async Task WriteArrayFragmentAsync([InstantHandle] Func<ArrayStream, Task> handler, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (var array = BeginArrayFragment(cancellationToken))
			{
				await handler(array).ConfigureAwait(false);
			}
			await FlushInternalAsync(true, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>Ecrit un document top-level de type array, copie le contenu entier de la s�quence d'�l�ment une array sp�cifi�, et flush le stream</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="items">S�quence de l'int�gralit� des �l�ments constituant l'array</param>
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

		/// <summary>Ecrit un document top-level de type array, copie le contenu entier de la s�quence d'�l�ment une array sp�cifi�, et flush le stream</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="items">S�quence de l'int�gralit� des �l�ments constituant l'array</param>
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

		/// <summary>Ecrit un document top-level de type array, copie le contenu entier de la s�quence de valeur JSON, et flush le stream</summary>
		/// <param name="items">S�quence de l'int�gralit� des �l�ments constituant l'array</param>
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

		/// <summary>Ecrit un document top-level de type array, copie le contenu entier de la s�quence de valeur JSON, et flush le stream</summary>
		/// <param name="items">S�quence de l'int�gralit� des �l�ments constituant l'array</param>
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

			public CrystalJsonWriter Writer
			{
				get { return m_parent.m_writer; }
			}

			public CancellationToken Cancellation
			{
				get { return m_ct; }
			}

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

			/// <summary>Ajoute un �l�ment dans un tableau ouvert</summary>
			/// <typeparam name="T">Type de l'�l�ment</typeparam>
			/// <param name="item">Element ajout�</param>
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

			/// <summary>Ajoute un �l�ment dans un tableau ouvert</summary>
			/// <typeparam name="T">Type de l'�l�ment</typeparam>
			/// <param name="item">Element ajout�</param>
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

			/// <summary>Ajoute plusieurs �l�ments dans un tableau ouvert</summary>
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

			/// <summary>Ajoute plusieurs �l�ments dans un tableau ouvert</summary>
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

			/// <summary>Ajoute plusieurs �l�ments dans un tableau ouvert, en appliquant une transformation sur chaque �l�ment</summary>
			/// <typeparam name="TInput">Type des �l�ments de la s�quence source</typeparam>
			/// <typeparam name="TOutput">Type des �l�ments transform�s</typeparam>
			/// <param name="items">S�quence des �l�ments constituants le batch</param>
			/// <param name="selector">Transformation appliqu�e � chaque �l�ments de <paramref name="items"/></param>
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

			/// <summary>Ajoute plusieurs �l�ments dans un tableau ouvert, en appliquant une transformation sur chaque �l�ment</summary>
			/// <typeparam name="TInput">Type des �l�ments de la s�quence source</typeparam>
			/// <typeparam name="TOutput">Type des �l�ments transform�s</typeparam>
			/// <param name="items">S�quence des �l�ments constituants le batch</param>
			/// <param name="selector">Transformation appliqu�e � chaque �l�ments de <paramref name="items"/></param>
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

		/// <summary>D�marre manuellement une array, quelque soit le niveau de profondeur actuel</summary>
		/// <returns>Sous-stream, qu'il faut Dispose() une fois que l'array est termin�e</returns>
		[Pure]
		public ArrayStream BeginArrayFragment(CancellationToken cancellationToken = default(CancellationToken))
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

		/// <summary>Indique si le tampon m�moire d�passe la limite de taille acceptable</summary>
		private bool ShouldFlush
		{
			get { return !m_disposed && m_scratch.Length >= AUTOFLUSH_THRESHOLD; }
		}

		/// <summary>Fait en sorte que toutes les donn�es �crites jusqu'a pr�sent soient flush�es dans le stream de destination</summary>
		private async Task FlushInternalAsync(bool flushStream, CancellationToken cancellationToken)
		{
			// Flush le TextWriter pour �tre s�r que tous les caract�res �crits arrivent dans le scratch stream!
			m_writer.Buffer.Flush();

			// flush le scratch buffer dans le stream de destination si n�cessaire
			if (m_scratch.Length > 0)
			{
				try
				{
					m_scratch.Seek(0, SeekOrigin.Begin);
					//note: MemoryStream.CopyToAsync() est optimis� pour �crire en une seul passe, et ignore la taille du buffer
					await m_scratch.CopyToAsync(m_stream, 4096, cancellationToken);
				}
				//TODO: si erreur, il faudrait nuker ??
				finally
				{
					m_scratch.SetLength(0);
				}
			}
			// force le stream � �crire sur le disque, si demand� par l'appelant
			if (flushStream)
			{
				//note: m�me si c'est un FileStream, il n'y a AUCUNE garantie que les donn�es seront durablement sauv�es! (write cache de l'OS)
				await m_stream.FlushAsync(cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>Fait en sorte que toutes les donn�es �crites jusqu'a pr�sent soient flush�es dans le stream de destination</summary>
		/// <param name="flushStream"></param>
		private void FlushInternal(bool flushStream)
		{
			// Flush le TextWriter pour �tre s�r que tous les caract�res �crits arrivent dans le scratch stream!
			m_writer.Buffer.Flush();

			// flush le scratch buffer dans le stream de destination si n�cessaire
			if (m_scratch.Length != 0)
			{
				try
				{
					m_scratch.Seek(0, SeekOrigin.Begin);
					//note: MemoryStream.CopyTo() est optimis� pour �crire en une seul passe, et ignore la taille du buffer
					m_scratch.CopyTo(m_stream, 4096);
				}
				finally
				{
					// si la copie throw, Dispose() va �tre appel� juste apr�s, et ne doit pas retry une deuxi�me fois!
					// => on force le buffer a 0, pour que Dispose() ne Flush() rien
					m_scratch.SetLength(0);
				}
			}

			// force le stream � �crire sur le disque, si demand� par l'appelant
			if (flushStream)
			{
				//note: m�me si c'est un FileStream, il n'y a AUCUNE garantie que les donn�es seront durablement sauv�es! (write cache de l'OS)
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
				//note: on ne peut pas fermer la hi�rachie automatiquement, car Dispose() peut �tre appel� suite � une erreur I/O sur le stream lui-m�me !
				try
				{
					FlushInternal(true);
				}
				finally
				{
					if (m_ownStream)
					{
						m_stream?.Dispose();
					}
				}
			}
		}

		//note: en pr�vision de IAsyncDisposable
		public async Task DisposeAsync(CancellationToken ct) //REVIEW: pas sur pour le CT...
		{
			if (!m_disposed)
			{
				m_disposed = true;
				//note: on ne peut pas fermer la hi�rachie automatiquement, car Dispose() peut �tre appel� suite � une erreur I/O sur le stream lui-m�me !
				try
				{
					await FlushInternalAsync(true, ct).ConfigureAwait(true);
				}
				finally
				{
					if (m_ownStream)
					{
						m_stream?.Dispose();
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

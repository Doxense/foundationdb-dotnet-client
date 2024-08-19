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

namespace Doxense.Reactive.Disposables
{
	using System.Diagnostics;
	using System.Runtime;
	using System.Runtime.CompilerServices;
	using Doxense.Serialization;

	/// <summary>Classe qui encapsule un objet, en interceptant éventuellement le Dispose()</summary>
	/// <typeparam name="T">Type de l'objet encapsulé</typeparam>
	[DebuggerDisplay("Value={m_value}, Disposed={Disposed}")]
	public sealed class Disposable<T> : IDisposable<T>
	{

		#region Private Members...

		/// <summary>Placeholder utilisé lorsqu'il ne faut rien faire</summary>
		private static readonly Action<T> s_nop = (_) => { };

		/// <summary>Placeholder utilisé lorsqu'il faut Disposer l'objet</summary>
		private static readonly Action<T> s_dispose = (t) => (t as IDisposable)?.Dispose();

		/// <summary>Valeur encapsulée</summary>
		/// <remarks>La référence vers la valeur persiste après le Dispose() ! (au cas où l'objet serait mit dans un dictionary, il faut que les Equals/GetHashcode fonctionnent toujours!)</remarks>
		private readonly T m_value;

		/// <summary>Action effectuée lors du Dispose: soit s_nop (s'il ne faut rien faire), soit s_dispose (s'il faut Disposer l'objet encapsulé) soit une Action custom fournie par l'appelant, soit null si Dispose() a déja été appelé</summary>
		/// <remarks>Est mit à null par Dispose() pour éviter de garder des références vers les objets</remarks>
		private Delegate? m_action;

		/// <summary>Etat supplémentaire (optionnel)</summary>
		/// <remarks>Passé en paramètre à l'action (si elle est du bon type). Est mit à null par Dispose() pour éviter de garder des références vers les objets</remarks>
		private object? m_state;

		/// <summary>0 = alive, 1 = disposed</summary>
		private int m_disposed;

		#endregion

		#region Constructors...

		/// <summary>Encapsule un IDisposable (qui est disposé lorsque cet objet est disposé)</summary>
		/// <param name="value">Valeur encapsulée (qui peut implémenter IDisposable ou non)</param>
		/// <remarks>La valeur est Dispose() si elle implémente IDisposable</remarks>
		[TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
		public Disposable(T value)
			: this(value, false)
		{ }

		/// <summary>Encapsule un IDisposable, en précisant s'il doit être disposé en même temps que cet objet, ou non</summary>
		/// <param name="value">Valeur encapsulée</param>
		/// <param name="preventDisposition">Si true, la valeur ne sera pas Dispose(). Si false, elle sera Dispose() en même temps que l'objet</param>
		/// <remarks>La valeur est Dispose() si elle implémente IDisposable ET si preventDisposition est égal à false.</remarks>
		public Disposable(T value, bool preventDisposition)
		{
			m_value = value;
			m_action = !preventDisposition && value is IDisposable ? s_dispose : s_nop;
		}

		/// <summary>Encapsule un IDisposable, avec une action spécifique exécutée pour le Disposer</summary>
		/// <param name="value">Valeur encapsulée</param>
		/// <param name="onDispose">Action qui sera exécutée (une seule fois) lorsque le wrappeur sera Disposé. La valeur encapsulée est passée en paramètre</param>
		public Disposable(T value, Action<T> onDispose)
		{
			Contract.NotNull(onDispose);
			m_value = value;
			m_action = onDispose;
		}

		/// <summary>Encapsule un IDisposable, avec une action spécifique exécutée pour le Disposer</summary>
		/// <param name="value">Valeur encapsulée</param>
		/// <param name="onDispose">Action qui sera exécutée (une seule fois) lorsque le wrappeur sera Disposé. La valeur encapsulée est passée en paramètre</param>
		/// <param name="state">Argument qui sera passé comme second argument à <paramref name="onDispose"/></param>
		public Disposable(T value, Action<T, object?> onDispose, object? state)
		{
			Contract.NotNull(onDispose);
			m_value = value;
			m_action = onDispose;
			m_state = state;
		}

		#endregion

		#region Public Properties...

		/// <summary>Valeure encapsulée (peut être null!)</summary>
		public T Value
		{
			[TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
			get
			{
				ThrowIfDisposed();
				return m_value;
			}
		}

		/// <summary>Indique si l'objet a été dispose (true), ou s'il est toujours vivant (false)</summary>
		public bool Disposed => m_disposed != 0;

		#endregion

		#region Operators...

		public static implicit operator T?(Disposable<T>? disposable)
		{
			if (disposable == null) return default(T);
			return disposable.Value;
		}

		#endregion

		#region IDisposable Interface...

		public void Dispose()
		{
			if (Interlocked.CompareExchange(ref m_disposed, 1, 0) == 0)
			{
				var action = m_action;
				var state = m_state;
				m_action = null;
				m_state = null;
				if (action != null)
				{
					// le delegate peut être un Action<T> ou un Action<T, object> suivant les cas...
					if (action is Action<T> simple)
					{
						simple(m_value);
					}
					else
					{
						((Action<T, object?>) action)(m_value, state);
					}
				}
			}
		}

		private void ThrowIfDisposed()
		{
			if (m_disposed != 0) throw FailObjectAlreadyDisposed();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Exception FailObjectAlreadyDisposed()
		{
			return ThrowHelper.ObjectDisposedException($"Cannot use already disposed object of type {typeof(T).GetFriendlyName()}");
		}

		#endregion

		#region IEquatable<...>

		public override int GetHashCode()
		{
			return m_value == null ? -1 : m_value.GetHashCode();
		}

		public override bool Equals(object? obj)
		{
			if (obj == null) return false;
			{
				if (obj is Disposable<T> dispOfT) return EqualityComparer<T>.Default.Equals(m_value, dispOfT.m_value);
			}
			if (obj is T value) return EqualityComparer<T>.Default.Equals(m_value, value);
			return false;
		}

		public override string ToString()
		{
			return m_value?.ToString() ?? string.Empty;
		}

		#endregion

	}

	/// <summary>Class helper pour créer ou combiner des IDisposable entre eux</summary>
	/// <remarks>Remplace System.Disposables.Disposable de Rx (fonctionnalités similaire, drop-in replacement)</remarks>
	public static class Disposable
	{

		/// <summary>Crée un IDisposable qui execute une action lorsqu'il est Dispose()</summary>
		/// <param name="action"></param>
		/// <returns></returns>
		[Pure]
		public static IDisposable Create(Action action)
		{
			return new AnonymousDisposable(action);
		}

		/// <summary>Crée un IDisposable qui execute une action avec un état, lorsqu'il est Dispose()</summary>
		[Pure]
		public static IDisposable<T> Create<T>(T value, Action<T> action)
		{
			return new Disposable<T>(value, action);
		}

		/// <summary>Retourne un IDisposable "vide" qui ne fait rien lorsqu'il est Dispose (placeholder)</summary>
		[Pure]
		public static IDisposable Empty()
		{
			return new NullDisposable();
		}

	}

}

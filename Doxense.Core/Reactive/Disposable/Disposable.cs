#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

#nullable enable

namespace Doxense.Reactive.Disposables
{
	using System;
	using System.Diagnostics;
	using System.Runtime;
	using System.Threading;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Serialization;
	using JetBrains.Annotations;

	/// <summary>Classe qui encapsule un objet, en interceptant �ventuellement le Dispose()</summary>
	/// <typeparam name="T">Type de l'objet encapsul�</typeparam>
	[DebuggerDisplay("Value={m_value}, Disposed={Disposed}")]
	public sealed class Disposable<T> : IDisposable<T>
	{
		#region Private Members...

		/// <summary>Placeholder utilis� lorsqu'il ne faut rien faire</summary>
		private static readonly Action<T> s_nop = (t) => { };

		/// <summary>Placeholder utilis� lorsqu'il faut Disposer l'objet</summary>
		private static readonly Action<T> s_dispose = (t) => (t as IDisposable)?.Dispose();

		/// <summary>Valeur encapsul�e</summary>
		/// <remarks>La r�f�rence vers la valeur persiste apr�s le Dispose() ! (au cas o� l'objet serait mit dans un dictionary, il faut que les Equals/GetHashcode fonctionnent toujours!)</remarks>
		private readonly T m_value;

		/// <summary>Action effectu�e lors du Dispose: soit s_nop (s'il ne faut rien faire), soit s_dispose (s'il faut Disposer l'objet encapsul�) soit une Action custom fournie par l'appelant, soit null si Dispose() a d�ja �t� appel�</summary>
		/// <remarks>Est mit � null par Dispose() pour �viter de garder des r�f�rences vers les objets</remarks>
		private Delegate? m_action;

		/// <summary>Etat suppl�mentaire (optionnel)</summary>
		/// <remarks>Pass� en param�tre � l'action (si elle est du bon type). Est mit � null par Dispose() pour �viter de garder des r�f�rences vers les objets</remarks>
		private object? m_state;

		/// <summary>0 = alive, 1 = disposed</summary>
		private int m_disposed;

		#endregion

		#region Constructors...

		/// <summary>Encapsule un IDisposable (qui est dispos� lorsque cet objet est dispos�)</summary>
		/// <param name="value">Valeur encapsul�e (qui peut impl�menter IDisposable ou non)</param>
		/// <remarks>La valeur est Dispose() si elle impl�mente IDisposable</remarks>
		[TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
		public Disposable(T value)
			: this(value, false)
		{ }

		/// <summary>Encapsule un IDisposable, en pr�cisant s'il doit �tre dispos� en m�me temps que cet objet, ou non</summary>
		/// <param name="value">Valeur encapsul�e</param>
		/// <param name="preventDisposition">Si true, la valeur ne sera pas Dispose(). Si false, elle sera Dispose() en m�me temps que l'objet</param>
		/// <remarks>La valeur est Dispose() si elle impl�mente IDisposable ET si preventDisposition est �gal � false.</remarks>
		public Disposable(T value, bool preventDisposition)
		{
			m_value = value;
			m_action = !preventDisposition && value is IDisposable ? s_dispose : s_nop;
		}

		/// <summary>Encapsule un IDisposable, avec une action sp�cifique ex�cut�e pour le Disposer</summary>
		/// <param name="value">Valeur encapsul�e</param>
		/// <param name="onDispose">Action qui sera ex�cut�e (une seule fois) lorsque le wrappeur sera Dispos�. La valeur encapsul�e est pass�e en param�tre</param>
		public Disposable(T value, Action<T> onDispose)
		{
			Contract.NotNull(onDispose);
			m_value = value;
			m_action = onDispose;
		}

		/// <summary>Encapsule un IDisposable, avec une action sp�cifique ex�cut�e pour le Disposer</summary>
		/// <param name="value">Valeur encapsul�e</param>
		/// <param name="onDispose">Action qui sera ex�cut�e (une seule fois) lorsque le wrappeur sera Dispos�. La valeur encapsul�e est pass�e en param�tre</param>
		/// <param name="state">Argument qui sera pass� comme second argument � <paramref name="onDispose"/></param>
		public Disposable(T value, Action<T, object?> onDispose, object? state)
		{
			Contract.NotNull(onDispose);
			m_value = value;
			m_action = onDispose;
			m_state = state;
		}

		#endregion

		#region Public Properties...

		/// <summary>Valeure encapsul�e (peut �tre null!)</summary>
		public T Value
		{
			[TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
			get
			{
				ThrowIfDisposed();
				return m_value;
			}
		}

		/// <summary>Indique si l'objet a �t� dispose (true), ou s'il est toujours vivant (false)</summary>
		public bool Disposed => m_disposed != 0;

		#endregion

		#region Operators...

		public static implicit operator T(Disposable<T> disposable)
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
					// le delegate peut �tre un Action<T> ou un Action<T, object> suivant les cas...
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
			return m_value == null ? string.Empty : m_value.ToString();
		}

		#endregion

	}

	/// <summary>Class helper pour cr�er ou combiner des IDisposable entre eux</summary>
	/// <remarks>Remplace System.Disposables.Disposable de Rx (fonctionnalit�s similaire, drop-in replacement)</remarks>
	public static class Disposable
	{

		/// <summary>Cr�e un IDisposable qui execute une action lorsqu'il est Dispose()</summary>
		/// <param name="action"></param>
		/// <returns></returns>
		[Pure]
		public static IDisposable Create(Action action)
		{
			return new AnonymousDisposable(action);
		}

		/// <summary>Cr�e un IDisposable qui execute une action avec un �tat, lorsqu'il est Dispose()</summary>
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

#region Copyright Doxense 2012-2021
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
	using System.Threading;
	using Doxense.Diagnostics.Contracts;

	/// <summary>Retourne un objet exécutant une Action lorsqu'il est Dispose()</summary>
	/// <remarks>Version simplifiée de System.Disposables.AnonymousDisposable de Rx</remarks>
	internal sealed class AnonymousDisposable : IDisposable
	{
		/// <summary>Action effectuée lors du Dispose() s'il n'a pas déjà été fait</summary>
		/// <remarks>Est mit à null par le premier Dispose pour éviter de garder une référence vers l'objet</remarks>
		private Action? m_onDispose;

		public AnonymousDisposable(Action onDispose)
		{
			Contract.NotNull(onDispose);
			m_onDispose = onDispose;
		}

		public void Dispose()
		{
			var onDispose = Interlocked.Exchange(ref m_onDispose, null);
			onDispose?.Invoke();
		}
	}

}

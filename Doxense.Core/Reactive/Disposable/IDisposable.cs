#region Copyright Doxense 2012-2016
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Reactive.Disposables
{
	using System;

	/// <summary>Classe qui encapsule un objet, en interceptant éventuellement le Dispose()</summary>
	/// <typeparam name="T">Type de l'objet encapsulé</typeparam>
	public interface IDisposable<out T> : IDisposable
	{
		/// <summary>Value that is accessible until this instance gets disposed</summary>
		/// <exception cref="ObjectDisposedException">If this property is called after this instance has been disposed</exception>
		T Value { get; }

		/// <summary>Hint that indicates wether the container has been disposed or not.</summary>
		/// <remarks>It is NOT safe to check the value of this flag before reading <see cref="Value"/> due to race conditions between threads!</remarks>
		bool Disposed { get; }
	}
}

#region Copyright Doxense 2003-2023
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Threading
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using JetBrains.Annotations;

	public static class ReaderWriterLockSlimExtensions
	{

		#region ReaderWriterLockSlim ExtensionMethods...

		/// <summary>Retourne un token Disposable correspond à un lock de type Read</summary>
		/// <param name="self">Lock sur lequel prendre le Read (ou null)</param>
		/// <returns>Token disposable</returns>
		/// <remarks>Si le lock est null, un token "vide" sera retourné (permet d'activer ou non le lock sur un composant en allouant ou non le ReaderWriterLockSlim !)</remarks>
		/// <example>using (lock.GetReadLock() { ... DO STUFF .. }</example>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ReadLockDisposable GetReadLock(this ReaderWriterLockSlim? self)
		{
			self?.EnterReadLock();
			return new ReadLockDisposable(self);
		}

		/// <summary>Retourne un token Disposable correspond à un lock de type UpgradableRead</summary>
		/// <param name="self">Lock sur lequel prendre le Upgradable (ou null)</param>
		/// <returns>Token disposable</returns>
		/// <remarks>Si le lock est null, un token "vide" sera retourné (permet d'activer ou non le lock sur un composant en allouant ou non le ReaderWriterLockSlim !)</remarks>
		/// <example>using (lock.GetUpgradableReadLock() { ... DO STUFF .. }</example>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static UpgradeableReadLockDisposable GetUpgradableReadLock(this ReaderWriterLockSlim? self)
		{
			self?.EnterUpgradeableReadLock();
			return new UpgradeableReadLockDisposable(self);
		}

		/// <summary>Retourne un token Disposable correspond à un lock de type Write</summary>
		/// <param name="self">Lock sur lequel prendre le Write (ou null)</param>
		/// <returns>Token disposable</returns>
		/// <remarks>Si le lock est null, un token "vide" sera retourné (permet d'activer ou non le lock sur un composant en allouant ou non le ReaderWriterLockSlim !)</remarks>
		/// <example>using (lock.GetWriteLock() { ... DO STUFF .. }</example>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static WriteLockDisposable GetWriteLock(this ReaderWriterLockSlim? self)
		{
			self?.EnterWriteLock();
			return new WriteLockDisposable(self);
		}

		public readonly struct ReadLockDisposable : IDisposable
		{
			private readonly ReaderWriterLockSlim? m_lock;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public ReadLockDisposable(ReaderWriterLockSlim? rwLock)
			{
				m_lock = rwLock;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Dispose()
			{
				m_lock?.ExitReadLock();
			}
		}

		public readonly struct UpgradeableReadLockDisposable : IDisposable
		{
			private readonly ReaderWriterLockSlim? m_lock;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public UpgradeableReadLockDisposable(ReaderWriterLockSlim? rwLock)
			{
				m_lock = rwLock;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Dispose()
			{
				m_lock?.ExitUpgradeableReadLock();
			}
		}

		public readonly struct WriteLockDisposable : IDisposable
		{
			private readonly ReaderWriterLockSlim? m_lock;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public WriteLockDisposable(ReaderWriterLockSlim? rwLock)
			{
				m_lock = rwLock;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Dispose()
			{
				m_lock?.ExitWriteLock();
			}
		}

		#endregion

	}

}

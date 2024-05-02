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

namespace Doxense.Threading
{
	using System.Runtime.CompilerServices;

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

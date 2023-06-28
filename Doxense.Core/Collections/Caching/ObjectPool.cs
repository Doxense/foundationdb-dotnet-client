﻿#region Copyright Doxense 2010-2022
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

//NOTE: Adaptation de Roslyn.Utilities.ObjectPool<T>, cf http://source.roslyn.codeplex.com/#Microsoft.CodeAnalysis/InternalUtilities/ObjectPool%601.cs

namespace Doxense.Collections.Caching
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using Doxense.Diagnostics.Contracts;

	/// <summary>
	/// Generic implementation of object pooling pattern with predefined pool size limit. The main
	/// purpose is that limited number of frequently used objects can be kept in the pool for
	/// further recycling.
	///
	/// Notes:
	/// 1) it is not the goal to keep all returned objects. Pool is not meant for storage. If there
	///    is no space in the pool, extra returned objects will be dropped.
	///
	/// 2) it is implied that if object was obtained from a pool, the caller will return it back in
	///    a relatively short time. Keeping checked out objects for long durations is ok, but
	///    reduces usefulness of pooling. Just new up your own.
	///
	/// Not returning objects to the pool in not detrimental to the pool's work, but is a bad practice.
	/// Rationale:
	///    If there is no intent for reusing the object, do not use pool - just use "new".
	/// </summary>
	public class ObjectPool<T> where T : class
	{

		private struct Element
		{
			internal T? Value;
		}

		// storage for the pool objects.
		private readonly Element[] m_items;

		// factory is stored for the lifetime of the pool. We will call this only when pool needs to
		// expand. compared to "new T()", Func gives more flexibility to implementers and faster
		// than "new T()".
		private readonly Func<T> m_factory;

		public ObjectPool(Func<T> factory)
			: this(factory, Environment.ProcessorCount * 2)
		{ }

		public ObjectPool(Func<T> factory, int size)
		{
			Contract.NotNull(factory);
			Contract.Positive(size);

			m_factory = factory;
			m_items = new Element[size];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private T CreateInstance()
		{
			return m_factory();
		}

		/// <summary>
		/// Produces an instance.
		/// </summary>
		/// <remarks>
		/// Search strategy is a simple linear probing which is chosen for it cache-friendliness.
		/// Note that Free will try to store recycled objects close to the start thus statistically
		/// reducing how far we will typically search.
		/// </remarks>
		public T Allocate()
		{
			var items = m_items;

			for (int i = 0; i < items.Length; i++)
			{
				// Note that the read is optimistically not synchronized. That is intentional.
				// We will interlock only when we have a candidate. in a worst case we may miss some
				// recently returned objects. Not a big deal.
				var inst = items[i].Value;
				if (inst != null)
				{
					if (inst == Interlocked.CompareExchange(ref items[i].Value, null, inst))
					{
						return inst;
					}
				}
			}

			return CreateInstance();
		}

		/// <summary>
		/// Returns objects to the pool.
		/// </summary>
		/// <remarks>
		/// Search strategy is a simple linear probing which is chosen for it cache-friendliness.
		/// Note that Free will try to store recycled objects close to the start thus statistically
		/// reducing how far we will typically search in Allocate.
		/// </remarks>
		public void Free(T obj)
		{
			var items = m_items;
			for (int i = 0; i < items.Length; i++)
			{
				if (items[i].Value == null)
				{
					// Intentionally not using interlocked here.
					// In a worst case scenario two objects may be stored into same slot.
					// It is very unlikely to happen and will only mean that one of the objects will get collected.
					items[i].Value = obj;
					break;
				}
			}
		}

	}

}

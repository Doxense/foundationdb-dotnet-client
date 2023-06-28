#region Copyright (c) 2005-2023 Doxense SAS
// See License.MD for license information
#endregion

namespace Doxense.Memory
{
	using System;
	using System.Buffers;
	using System.Runtime.CompilerServices;
	using JetBrains.Annotations;

	public sealed class SliceBufferWriter : IBufferWriter<byte>
	{

		private SliceWriter Buffer;

		private bool InUse;

		private ArrayPool<byte> Pool { get; }

		public SliceBufferWriter() : this(0)
		{ }

		public SliceBufferWriter(int capacity, ArrayPool<byte>? pool = null)
		{
			this.Pool = pool ?? ArrayPool<byte>.Shared;
			this.Buffer = new SliceWriter(capacity, this.Pool);
			this.InUse = true;
		}

		public void Release(bool clearArray = false)
		{
			var buffer = this.Buffer.Buffer;
			this.Buffer = default;
			this.InUse = false;
			this.Pool.Return(buffer, clearArray);
		}

		/// <summary>Reset this instance to be used for another time</summary>
		public void Reset(int sizeHint = 0)
		{
			if (this.InUse) throw new InvalidOperationException("Writer instance is still in use.");
			this.Buffer = new SliceWriter(sizeHint, this.Pool);
			this.InUse = true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Advance(int count)
		{
			EnsureInUse();
			this.Buffer.Advance(count);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Memory<byte> GetMemory(int sizeHint = 0)
		{
			EnsureInUse();
			return this.Buffer.GetMemory(sizeHint);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Span<byte> GetSpan(int sizeHint = 0)
		{
			EnsureInUse();
			return this.Buffer.GetSpan(sizeHint);
		}

		public Slice ToSlice()
		{
			EnsureInUse();
			return this.Buffer.ToSlice();
		}

		public Span<byte> ToSpan()
		{
			EnsureInUse();
			return this.Buffer.Buffer.AsSpan(this.Buffer.Position);
		}

		public Memory<byte> ToMemory()
		{
			EnsureInUse();
			return this.Buffer.Buffer.AsMemory(this.Buffer.Position);
		}

		[Pure]
		public Slice CaptureBuffer()
		{
			EnsureInUse();
			var buffer = this.Buffer;
			this.Buffer = default;
			this.InUse = false;
			return buffer.ToSlice();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void EnsureInUse()
		{
			if (this.InUse) throw BufferIsUnavailable();
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception BufferIsUnavailable()
		{
			return new Exception("Writer instance has been returned to the pool, and cannot expose its underlying buffer.");
		}

		[ThreadStatic]
		private static SliceBufferWriter? CachedInstance;

		/// <summary>Alloue une instance d'un writer</summary>
		/// <param name="sizeHint">Taille total estimée nécessaire</param>
		/// <returns>Instance prête à l'emploi</returns>
		[Pure]
		public static SliceBufferWriter Rent(int sizeHint = 0)
		{
			var instance = CachedInstance;
			if (instance != null)
			{
				instance.Reset(sizeHint);
				return instance;
			}

			return new SliceBufferWriter(sizeHint);
		}

		/// <summary>Retourne un writer dans le pool</summary>
		/// <remarks>ATTENTION: le buffer exposé par ce writer NE DOIT PLUS ETRE UTILISE' !!!</remarks>
		public static void Return(SliceBufferWriter writer)
		{
			if (writer != null)
			{
				writer.Release();
				CachedInstance = writer;
			}
		}
	}
}

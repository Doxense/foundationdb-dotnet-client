//TODO: Copyright

namespace FoundationDb.Client.Utils
{
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Text;

	/// <summary>Mini classe capable d'écrire des données binaire dans un buffer qui se resize automatiquement</summary>
	/// <remarks>IMPORTANT: Cette class n'effectue pas de vérification des paramètres, qui est à la charge de l'appelant ! (le but étant justement d'avoir une structure très simple et rapide)</remarks>
	[DebuggerDisplay("Position={0}, Capacity={this.Buffer == null ? -1 : this.Buffer.Length}")]
	public sealed class FdbBufferWriter
	{

		#region Constants...

		/// <summary>Null/Empty/Void</summary>
		private const byte TypeNil = (byte)0;

		/// <summary>ASCII String</summary>
		private const byte TypeStringAscii = (byte)1;

		/// <summary>UTF-8 String</summary>
		private const byte TypeStringUtf8 = (byte)2;

		/// <summary>Base value for integer types (20 +/- n)</summary>
		private const int TypeIntegerBase = 20;

		/// <summary>Empty buffer</summary>
		private static readonly byte[] Empty = new byte[0];

		#endregion

		// Wrap a byte buffer in a structure that will automatically grow in size, if necessary
		// Valid data always start at offset 0, and this.Position is equal to the current size as well as the offset of the next available free spot

		/// <summary>Buffer holding the data</summary>
		public byte[] Buffer;

		/// <summary>Position in the buffer ( == number of already written bytes)</summary>
		public int Position;

		#region Constructors...

		public FdbBufferWriter()
		{ }

		public FdbBufferWriter(int capacity)
		{
			this.Buffer = new byte[capacity];
		}

		public FdbBufferWriter(byte[] buffer)
		{
			this.Buffer = buffer;
		}

		public FdbBufferWriter(byte[] buffer, int index)
		{
			this.Buffer = buffer;
			this.Position = index;
		}

		#endregion

		#region Public Properties...

		/// <summary>Returns true is the buffer contains at least some data</summary>
		public bool HasData
		{
#if !NET_4_0
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
			get { return this.Position > 0; }
		}

		#endregion

		public byte[] GetBytes()
		{
			var bytes = new byte[this.Position];
			if (this.Position > 0)
			{
				System.Buffer.BlockCopy(this.Buffer, 0, bytes, 0, this.Position);
			}
			return bytes;
		}

#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public ArraySegment<byte> ToArraySegment()
		{
			return new ArraySegment<byte>(this.Buffer, 0, this.Position);
		}

#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public ArraySegment<byte> ToArraySegment(int size)
		{
			Contract.Requires(size >= 0 && size <= this.Position);
			return new ArraySegment<byte>(this.Buffer, 0, size);
		}

		/// <summary>Truncate the buffer by setting the cursor to the specified position.</summary>
		/// <param name="position">New size of the buffer</param>
		/// <remarks>If the buffer was smaller, it will be resized and filled with zeroes. If it was biffer, the cursor will be set to the specified position, but previous data will not be deleted.</remarks>
		public void SetLength(int position)
		{
			Contract.Requires(position >= 0);

			if (this.Position < position)
			{
				int missing = position - this.Position;
				EnsureBytes(missing);
				Array.Clear(this.Buffer, this.Position, missing);
			}
			this.Position = position;
		}

		/// <summary>Delete the first N bytes of the buffer, and shift the remaining to the front</summary>
		/// <param name="bytes">Number of bytes to remove at the head of the buffer</param>
		/// <returns>New size of the buffer (or 0 if it is empty)</returns>
		/// <remarks>This should be called after every successfull write to the underlying stream, to update the buffer.</remarks>
		public int Flush(int bytes)
		{
			Contract.Requires(bytes > 0, null, "bytes > 0");
			Contract.Requires(bytes <= this.Position, null, "bytes <= this.Position");

			if (bytes < this.Position)
			{ // Il y aura des données à garder, on les copie au début du stream
				System.Buffer.BlockCopy(this.Buffer, bytes, this.Buffer, 0, this.Position - bytes);
				return this.Position -= bytes;
			}
			else
			{
				return this.Position = 0;
			}
		}

		/// <summary>Empties the current buffer after a succesfull write</summary>
		/// <remarks>Shrink the buffer if a lot of memory is wated</remarks>
		public void Reset()
		{
			if (this.Position > 0)
			{
				// reduce size ?
				// If the buffer exceeds 4K and we used less than 1/8 of it the last time, we will "shrink" the buffer
				if (this.Buffer.Length > 4096 && this.Position * 8 <= Buffer.Length)
				{ // Shrink it
					Buffer = new byte[NextPowerOfTwo(this.Position)];
				}
				else
				{ // Clear it
					Array.Clear(Buffer, 0, this.Position);
				}
				this.Position = 0;
			}
		}

		/// <summary>Append a byte array to the end of the buffer</summary>
		/// <param name="data"></param>
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public void WriteBytes(byte[] data)
		{
			//Contract.NotNull(data, WellKnownParameters.data);
			WriteBytes(data, 0, data.Length);
		}

#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public void UnsafeWriteBytes(byte[] bytes)
		{
			Contract.Requires(bytes != null && this.Buffer != null && this.Position + bytes.Length <= this.Buffer.Length);

			System.Buffer.BlockCopy(bytes, 0, this.Buffer, this.Position, bytes.Length);
			this.Position += bytes.Length;
		}

		/// <summary>Append a chunk of a byte array to the end of the buffer</summary>
		/// <param name="data"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		public void WriteBytes(byte[] data, int offset, int count)
		{
			Contract.Requires(data != null);
			Contract.Requires(offset >= 0);
			Contract.Requires(count >= 0);
			Contract.Requires(offset + count <= data.Length);

			if (count > 0)
			{
				EnsureBytes(count);
				System.Buffer.BlockCopy(data, offset, this.Buffer, this.Position, count);
				this.Position += count;
			}
		}

		/// <summary>Append a segment of bytes to the end of the buffer</summary>
		/// <param name="data"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		public void WriteBytes(ArraySegment<byte> data)
		{
			Contract.Requires(data.Array != null);
			Contract.Requires(data.Offset >= 0);
			Contract.Requires(data.Count >= 0);
			Contract.Requires(data.Offset + data.Count <= data.Array.Length);

			if (data.Count > 0)
			{
				EnsureBytes(data.Count);
				System.Buffer.BlockCopy(data.Array, data.Offset, this.Buffer, this.Position, data.Count);
				this.Position += data.Count;
			}
		}

#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public void UnsafeWriteBytes(byte[] bytes, int offset, int count)
		{
			Contract.Requires(bytes != null && offset >= 0 && count >= 0 && offset + count <= bytes.Length && this.Buffer != null & Position + count <= this.Buffer.Length);

			System.Buffer.BlockCopy(bytes, offset, this.Buffer, this.Position, count);
			this.Position += count;
		}

		public unsafe void WriteBytes(byte* data, int count)
		{
			Contract.Requires(data != null);
			Contract.Requires(count >= 0);

			if (count > 0)
			{
				EnsureBytes(count);
				System.Runtime.InteropServices.Marshal.Copy(new IntPtr(data), this.Buffer, this.Position, count);
				this.Position += count;
			}
		}

		/// <summary>Writes a NIL byte (\x00) into the buffer</summary>
		public void WriteNil()
		{
			WriteByte(TypeNil);
		}

		/// <summary>Writes a binary string (ASCII)</summary>
		public void WriteAsciiString(byte[] value)
		{
			if (value == null)
			{
				WriteByte(TypeNil);
				return;
			}

			int n = value.Length;
			//TODO: detect NULs!

			EnsureBytes(n + 2);
			var buffer = this.Buffer;
			int p = this.Position;
			buffer[p++] = TypeStringAscii;
			if (n > 0)
			{
				System.Buffer.BlockCopy(value, 0, buffer, p, n);
				p += n;
			}
			buffer[p++] = TypeNil;
			this.Position = p;
		}

		/// <summary>Writes a string containing only ASCII chars</summary>
		public void WriteAsciiString(string value)
		{
			if (value == null)
			{
				WriteByte(TypeNil);
			}
			else
			{
				WriteAsciiString(value.Length == 0 ? Empty : Encoding.Default.GetBytes(value));
			}
		}

		/// <summary>Writes a string encoded in UTF-8</summary>
		public void WriteUtf8String(string value)
		{
			var bytes = Encoding.UTF8.GetBytes(value);
			int n = bytes.Length;
			EnsureBytes(n + 2);
			var buffer = this.Buffer;
			int p = this.Position;
			buffer[p] = 2;
			System.Buffer.BlockCopy(bytes, 0, buffer, p + 1, n);
			buffer[p + n + 1] = 0;
			this.Position = p + n + 2;
		}

		/// <summary>Advance the cursor of the buffer without writing anything</summary>
		/// <param name="skip">Number of bytes to skip</param>
		/// <returns>Position of the cursor BEFORE moving it. Can be used as a marker to go back later and fill some value</returns>
		/// <remarks>Will fill the skipped bytes with 0xFF</remarks>
		public int Skip(int skip)
		{
			Contract.Requires(skip > 0);

			int before = Position;
			EnsureBytes(skip);
			for (int i = 0; i < skip; i++)
			{
				Buffer[before + i] = 0xFF;
			}
			Position = before + skip;
			return before;
		}

		/// <summary>Add a byte to the end of the buffer, and advance the cursor</summary>
		/// <param name="value">Byte, 8 bits</param>
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public void WriteByte(byte value)
		{
			EnsureBytes(1);
			Buffer[Position++] = value;
		}

#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public void UnsafeWriteByte(byte value)
		{
			Contract.Requires(Buffer != null & Position < Buffer.Length);
			Buffer[Position++] = value;
		}

		/// <summary>Ecrit un Int64 à la fin, et avance le curseur</summary>
		/// <param name="value">Signed QWORD, 64 bits, High Endian</param>
		public void WriteInt64(long value)
		{
			if (value <= 255)
			{
				if (value == 0)
				{ // zero
					WriteByte((byte)TypeIntegerBase);
					return;
				}

				if (value > 0)
				{ // 1..255: frequent for array index
					EnsureBytes(2);
					UnsafeWriteByte(TypeIntegerBase + 1);
					UnsafeWriteByte((byte)value);
					return;
				}

				if (value > -256)
				{ // -255..-1
					EnsureBytes(2);
					UnsafeWriteByte(TypeIntegerBase - 1);
					UnsafeWriteByte((byte)(255 + value));
					return;
				}
			}

			WriteInt64Slow(value);
		}

		private void WriteInt64Slow(long value)
		{
			// we are only called for values <= -256 or >= 256

			// determine the number of bytes needed to encode the absolute value
			int bytes = FdbBufferWriter.NumberOfBytes(value);

			EnsureBytes(bytes + 1);

			var buffer = this.Buffer;
			int p = this.Position;

			ulong v;
			if (value > 0)
			{ // simple case
				buffer[p++] = (byte)(TypeIntegerBase + bytes);
				v = (ulong)value;
			}
			else
			{ // we will encode the one's complement of the absolute value
				// -1 => 0xFE
				// -256 => 0xFFFE
				// -65536 => 0xFFFFFE
				buffer[p++] = (byte)(TypeIntegerBase - bytes);
				v = (ulong)((1 << (bytes << 3)) - 1 + value);
			}

			// TODO: unroll ?
			while(bytes-- > 0)
			{
				buffer[p++] = (byte)v;
				//TODO: we don't need the last '>>='
				v >>= 8;
			}
			this.Position = p;
		}

		/// <summary>Ecrit un Int64 à la fin, et avance le curseur</summary>
		/// <param name="value">Signed QWORD, 64 bits, High Endian</param>
		public void WriteUInt64(ulong value)
		{
			if (value <= 255)
			{
				if (value == 0)
				{ // 0
					WriteByte((byte)TypeIntegerBase);
				}
				else
				{ // 1..255
					EnsureBytes(2);
					UnsafeWriteByte(TypeIntegerBase + 1);
					UnsafeWriteByte((byte)value);
				}
				return;

			}

			// >= 256
			WriteUInt64Slow(value);
		}

		public void WriteUInt64Slow(ulong value)
		{
			// We are only called for values >= 256

			// determine the number of bytes needed to encode the value
			int bytes = FdbBufferWriter.NumberOfBytes(value);

			EnsureBytes(bytes + 1);

			var buffer = this.Buffer;
			int p = this.Position;

			// simple case (ulong can only be positive)
			buffer[p++] = (byte)(TypeIntegerBase + bytes);

			// TODO: unroll ?
			while (bytes-- > 0)
			{
				buffer[p++] = (byte)value;
				//TODO: we don't need the last '>>='
				value >>= 8;
			}
			this.Position = p;
		}
		/// <summary>Vérifie qu'il y a au moins 'count' octets de libre en fin du buffer</summary>
		/// <param name="count">Nombre d'octets qui vont être écrit dans le buffer juste après</param>
		/// <remarks>Si le buffer n'est pas assez grand, il est resizé pour accomoder le nombre d'octets désirés</remarks>
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public void EnsureBytes(int count)
		{
			Contract.Requires(count >= 0);

			if (Buffer == null || Position + count > Buffer.Length)
			{
				// note: double la taille du buffer
				GrowBuffer(ref Buffer, Position + count);
			}
		}

#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public void EnsureOffsetAndSize(int offset, int count)
		{
			Contract.Requires(offset >= 0);
			Contract.Requires(count >= 0);

			if (this.Buffer == null || offset + count > this.Buffer.Length)
			{
				GrowBuffer(ref this.Buffer, offset + count);
			}
		}

		/// <summary>Resize a buffer by doubling its capacity</summary>
		/// <param name="buffer">Reference to the variable holding the buffer to create/resize</param>
		/// <param name="minimumCapacity">Capacité minimum du buffer (si vide initialement) ou 0 pour "autogrowth"</param>
		/// <remarks>The buffer will be resized to the maximum betweeb the previous size multiplied by 2, and <paramref name="minimumCapacity"/>. The capacity will always be rounded to a multiple of 16 to reduce memory fragmentation</remarks>
		public static void GrowBuffer(ref byte[] buffer, int minimumCapacity = 0)
		{
			Contract.Requires(minimumCapacity >= 0);

			// essayes de doubler la taille du buffer, ou prendre le minimum demandé
			int newSize = Math.Max(minimumCapacity, buffer == null ? 0 : buffer.Length << 1);

			// round to the next multiple of 16 bytes (to reduce fragmentation)
			if (newSize < 16)
			{
				newSize = 16;
			}
			else if ((newSize & 0xF) != 0)
			{
				newSize = ((newSize + 15) >> 4) << 4;
			}

			Array.Resize(ref buffer, newSize);
		}

		/// <summary>Round a number to the next power of 2</summary>
		/// <param name="x">Positive integer that will be rounded up (if not already a power of 2)</param>
		/// <returns>Smallest power of 2 that is greater then or equal to <paramref name="x"/></returns>
		/// <remarks>Will return 1 for <paramref name="x"/> = 0 (because 0 is not a power 2 !), and will throws for <paramref name="x"/> &lt; 0</remarks>
		/// <exception cref="System.ArgumentOutOfRangeException">If <paramref name="x"/> is a negative number</exception>
		public static int NextPowerOfTwo(int x)
		{
			// cf http://en.wikipedia.org/wiki/Power_of_two#Algorithm_to_round_up_to_power_of_two

			// special case
			if (x == 0) return 1;
			if (x < 0) throw new ArgumentOutOfRangeException("x", x, "Cannot compute the next power of two for negative numbers");
			//TODO: check for overflow at if x > 2^30 ?

			--x;
			x |= (x >> 1);
			x |= (x >> 2);
			x |= (x >> 4);
			x |= (x >> 8);
			x |= (x >> 16);
			return x + 1;
		}

		/// <summary>Lookup table used to compute the index of the most significant bit</summary>
		private static readonly int[] MultiplyDeBruijnBitPosition = new int[32]
		{
		  0, 9, 1, 10, 13, 21, 2, 29, 11, 14, 16, 18, 22, 25, 3, 30,
		  8, 12, 20, 28, 15, 17, 24, 7, 19, 27, 23, 6, 26, 5, 4, 31
		};

		/// <summary>Returns the minimum number of bytes needed to represent a value</summary>
		/// <remarks>Note: will returns 1 even for <param name="v"/> == 0</remarks>
		public static int NumberOfBytes(uint v)
		{
			return (MostSignificantBit(v) + 8) >> 3;
		}

		public static int NumberOfBytes(long v)
		{
			return v >= 0 ? NumberOfBytes((ulong)v) : v != long.MinValue ? NumberOfBytes((ulong)-v) : 8;
		}

		/// <summary>Returns the minimum number of bytes needed to represent a value</summary>
		/// <returns></returns>
		public static int NumberOfBytes(ulong v)
		{
			int msb = 0;

			if (v > 0xFFFFFFFF)
			{ // for 64-bit values, shift everything by 32 bits to the right
				msb += 32;
				v >>= 32;
			}
			msb += MostSignificantBit((uint)v);
			return (msb + 8) >> 3;
		}

		/// <summary>Returns the position of the most significant bit (0-based) in a 32-bit integer</summary>
		/// <param name="value">32-bit integer</param>
		/// <returns>Index of the most significant bit (0-based)</returns>
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public static int MostSignificantBit(uint v)
		{
			// from: http://graphics.stanford.edu/~seander/bithacks.html#IntegerLogDeBruijn

			v |= v >> 1; // first round down to one less than a power of 2 
			v |= v >> 2;
			v |= v >> 4;
			v |= v >> 8;
			v |= v >> 16;

			var r = (v * 0x07C4ACDDU) >> 27;
			return MultiplyDeBruijnBitPosition[r];
		}

	}

}

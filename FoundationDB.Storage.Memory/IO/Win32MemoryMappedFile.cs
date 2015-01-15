#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.IO
{
	using FoundationDB.Storage.Memory.Utils;
	using Microsoft.Win32.SafeHandles;
	using System;
	using System.Diagnostics.Contracts;
	using System.IO;
	using System.Runtime.InteropServices;
	using System.Security;
	using System.Security.AccessControl;

	[SuppressUnmanagedCodeSecurity]
	internal static class UnsafeNativeMethods
	{

		[StructLayout(LayoutKind.Sequential)]
		public sealed class SECURITY_ATTRIBUTES
		{
			public int nLength;
			public IntPtr lpSecurityDescriptor;
			public int bInheritHandle;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct SYSTEM_INFO
		{
			internal int dwOemId;
			internal int dwPageSize;
			internal IntPtr lpMinimumApplicationAddress;
			internal IntPtr lpMaximumApplicationAddress;
			internal IntPtr dwActiveProcessorMask;
			internal int dwNumberOfProcessors;
			internal int dwProcessorType;
			internal uint dwAllocationGranularity;
			internal short wProcessorLevel;
			internal short wProcessorRevision;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		internal class MEMORYSTATUSEX
		{
			internal uint dwLength = ((uint)Marshal.SizeOf(typeof(UnsafeNativeMethods.MEMORYSTATUSEX)));
			internal uint dwMemoryLoad;
			internal ulong ullTotalPhys;
			internal ulong ullAvailPhys;
			internal ulong ullTotalPageFile;
			internal ulong ullAvailPageFile;
			internal ulong ullTotalVirtual;
			internal ulong ullAvailVirtual;
			internal ulong ullAvailExtendedVirtual;
		}

		[Flags]
		public enum FileMapProtection : uint
		{
			PageReadonly = 0x02,
			PageReadWrite = 0x04,
			PageWriteCopy = 0x08,
			PageExecuteRead = 0x20,
			PageExecuteReadWrite = 0x40,
			SectionCommit = 0x8000000,
			SectionImage = 0x1000000,
			SectionNoCache = 0x10000000,
			SectionReserve = 0x4000000,
		}

		[Flags]
		public enum FileMapAccess : uint
		{
			FileMapCopy = 0x0001,
			FileMapWrite = 0x0002,
			FileMapRead = 0x0004,
			FileMapAllAccess = 0x001f,
			FileMapExecute = 0x0020,
		}

		[SecurityCritical, DllImport("kernel32.dll", SetLastError = true)]
		private static extern void GetSystemInfo(ref SYSTEM_INFO lpSystemInfo);

		[SecurityCritical, DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

		[SecurityCritical, DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern SafeMemoryMappedFileHandle CreateFileMapping(SafeFileHandle hFile, SECURITY_ATTRIBUTES lpAttributes, FileMapProtection fProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

		[SecurityCritical, DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
		public static extern SafeMemoryMappedViewHandle MapViewOfFile(SafeMemoryMappedFileHandle handle, FileMapAccess dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap);

		/// <summary>Gets the granularity for the starting address at which virtual memory can be allocated.</summary>
		[SecurityCritical]
		public static uint GetSystemPageAllocationGranularity()
		{
			var sysInfo = new SYSTEM_INFO();
			GetSystemInfo(ref sysInfo);
			return sysInfo.dwAllocationGranularity;
		}

		/// <summary>Gets the total size of the user mode portion of the virtual address space of the calling process, in bytes.</summary>
		[SecurityCritical]
		public static ulong GetTotalVirtualAddressSpaceSize()
		{
			var memStatusEx = new MEMORYSTATUSEX();
			GlobalMemoryStatusEx(memStatusEx);
			return memStatusEx.ullTotalVirtual;
		}
	}

	internal unsafe sealed class Win32MemoryMappedFile : IDisposable
	{
		private readonly SafeMemoryMappedFileHandle m_mapHandle;
		private readonly SafeMemoryMappedViewHandle m_viewHandle;
		private readonly FileStream m_file;
		private readonly ulong m_size;
		private readonly byte* m_baseAddress;
		private bool m_disposed;

		private Win32MemoryMappedFile(FileStream fs, SafeMemoryMappedFileHandle handle, ulong size)
		{
			Contract.Requires(fs != null && handle != null && !handle.IsInvalid && !handle.IsClosed);
			m_mapHandle = handle;
			m_file = fs;
			m_size = size;

			// verify that it fits on 32 bit OS...
			if (IntPtr.Size == 4 && size > uint.MaxValue)
			{  // won't work with 32-bit pointers
				throw new InvalidOperationException("Memory mapped file size is too big to be opened on a 32-bit system.");
			}

			// verifiy that it will fit in the virtual address space of the process
			var totalVirtual = UnsafeNativeMethods.GetTotalVirtualAddressSpaceSize();
			if (size > totalVirtual)
			{
				throw new InvalidOperationException("Memory mapped file size is too big to fit in the current process virtual address space");
			}

			SafeMemoryMappedViewHandle view = null;
			byte* baseAddress = null;
			try
			{
				view = UnsafeNativeMethods.MapViewOfFile(m_mapHandle, UnsafeNativeMethods.FileMapAccess.FileMapRead, 0, 0, new UIntPtr(size));
				if (view.IsInvalid) throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
				view.Initialize(size);
				m_viewHandle = view;

				view.AcquirePointer(ref baseAddress);
				m_baseAddress = baseAddress;
			}
			catch
			{
				if (baseAddress != null) view.ReleasePointer();
				if (view != null) view.Dispose();
				m_file = null;
				m_viewHandle = null;
				m_mapHandle = null;
				m_baseAddress = null;
				throw;
			}
		}

		[SecurityCritical]
		public static Win32MemoryMappedFile OpenRead(string path)
		{
			Contract.Requires(!string.IsNullOrEmpty(path));


			if (!File.Exists(path))
			{
				throw new FileNotFoundException("Memory mapped file not found", path);
			}

			FileStream fs = null;
			SafeMemoryMappedFileHandle handle = null;
			try
			{
				// Open the file
				fs = new FileStream(path, FileMode.Open, FileSystemRights.ListDirectory, FileShare.None, 0x1000, FileOptions.SequentialScan);
				Contract.Assert(fs != null);
				ulong capacity = checked((ulong)fs.Length);
				if (capacity == 0) throw new ArgumentException("Cannot memory map an empty file");

				// Create the memory mapping
				uint dwMaximumSizeLow = (uint)(capacity & 0xffffffffL);
				uint dwMaximumSizeHigh = (uint)(capacity >> 32);
				handle = UnsafeNativeMethods.CreateFileMapping(fs.SafeFileHandle, null /*TODO?*/, UnsafeNativeMethods.FileMapProtection.PageReadonly, dwMaximumSizeHigh, dwMaximumSizeLow, null);
				int errorCode = Marshal.GetLastWin32Error();
				if (handle.IsInvalid || errorCode == 183)
				{
					throw Marshal.GetExceptionForHR(errorCode);
				}

				return new Win32MemoryMappedFile(fs, handle, capacity);
			}
			catch
			{
				if (handle != null) handle.Dispose();
				if (fs != null) fs.Dispose();
				throw;
			}
		}

		public string Name
		{
			get { return m_file.Name; }
		}

		public ulong Length
		{
			get { return m_size; }
		}

		private void EnsureReadable(ulong offset, ulong size)
		{
			if (m_disposed) throw new ObjectDisposedException(this.GetType().Name, "Memory mapped file has already been closed");
			if (offset > m_size) throw new ArgumentException("Offset is outside the bounds of the memory mapped file");
			if (checked(offset + size) > m_size) throw new ArgumentException("Size is outside the bounds of the memory mapped file");
		}

		public unsafe UnmanagedSliceReader CreateReader(ulong offset, ulong size)
		{
			EnsureReadable(offset, size);

			byte* start = m_baseAddress + offset;

			return UnmanagedSliceReader.FromAddress(start, size);
		}

		#region IDisposable...

		public void Dispose()
		{
			if (!m_disposed)
			{
				m_disposed = true;

				if (m_viewHandle != null)
				{
					if (m_baseAddress != null) m_viewHandle.ReleasePointer();
					m_viewHandle.Dispose();
				}
				if (m_mapHandle != null) m_mapHandle.Dispose();
				if (m_file != null) m_file.Dispose();
			}
		}

		#endregion
	}

}

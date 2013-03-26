//TODO: Copyright

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FoundationDb.Client.Utils
{

	internal static class Contract
	{

		[DebuggerStepThrough]
		[Conditional("DEBUG")]
#if NET_4_5
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public static void Requires(bool condition)
		{
			if (!condition) RaiseContractFailure(true, null, "A pre-requisite was not met");
		}

		[DebuggerStepThrough]
		[Conditional("DEBUG")]
#if NET_4_5
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public static void Requires(bool condition, string test, string message)
		{
			if (!condition) RaiseContractFailure(true, test, message);
		}

		[DebuggerStepThrough]
		public static void RaiseContractFailure(bool assertion, string test, string message)
		{
			if (assertion)
			{
#if DEBUG
				if (Debugger.IsAttached) Debugger.Break();
#endif
				Debug.Fail(message, test);
			}
			else
			{
				throw new InvalidOperationException(message);
			}
		}

	}
}

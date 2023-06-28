#region Copyright Doxense SAS 2013-2016
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Async
{
	using System;

	/// <summary>Order in which items in a buffer are being output</summary>
	public enum AsyncOrderingMode
	{
		/// <summary>Outputs the results, respecting the arrival order.</summary>
		/// <remarks>If B arrives after, but completes before A, then output order will be "A, B"</remarks>
		ArrivalOrder = 0,
		/// <summary>Outputs the results in completion order.</summary>
		/// <remarks>If B arrives after, but completes before A, then output order will be "B, A"</remarks>
		CompletionOrder,
	}

}

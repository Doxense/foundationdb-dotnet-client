#region Copyright (c) 2023-2023 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Aspire.Hosting.ApplicationModel
{
	using System;

	/// <summary>Represents a FoundationDB resource that requires a cluster file.</summary>
	public interface IFdbResource : IResourceWithConnectionString
	{

	}

}

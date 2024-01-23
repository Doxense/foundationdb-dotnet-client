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

	/// <summary>Specify the versionning rules for the FoundationDB docker image</summary>
	public enum FdbVersionPolicy
	{
		/// <summary>Use the exact version specified</summary>
		Exact = 0,

		/// <summary>Select the latest version published to the docker registry.</summary>
		/// <remarks>Please note that there is no guarantee that the latest version is stable or is compatible with the selected API level.</remarks>
		Latest,

		/// <summary>Select the latest compatible version published to the docker registry, that is greater than or equal to the version requested.</summary>
		/// <remarks>
		/// <para>For example, if version <c>6.0.3</c> is requested, but <c>7.3.5</c> is currently available, it will be used instead.</para>
		/// <para>If a newer version is available, but is known to break compatiblity (by removing support for the API level selected), then it will not be included in the selection process.</para>
		/// </remarks>
		LatestMajor,

		/// <summary>Select the latest compatible minor version published to the docker registry, that is greater than or equal to the version requested.</summary>
		/// <remarks>
		/// <para>For example, if version <c>6.0.3</c> is requested, but <c>6.2.7</c> is the latest <c>6.x</c> version available, it will be used even if there is a more recent <c>7.x</c> version.</para>
		/// <para>If a newer version is available, but is known to break compatiblity (by removing support for the API level selected), then it will not be included in the selection process.</para>
		/// </remarks>
		LatestMinor,
		
		/// <summary>Select the latest stable patch version available for the minor version requested.</summary>
		/// <remarks>
		/// <para>For example, if version <c>6.0.3</c> is requested, but <c>6.0.7</c> is the latest <c>6.0.x</c> version available, it will be used even if there is a more recent <c>6.1.x</c> or <c>7.x</c> version.</para>
		/// <para>If a newer version is available, but is known to break compatiblity (by removing support for the API level selected), then it will not be included in the selection process.</para>
		/// </remarks>
		LatestPatch,

	}

}

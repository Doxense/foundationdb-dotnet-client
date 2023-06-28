#region Copyright Doxense 2010-2015
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;

	public interface IJsonDynamic
	{
		JsonValue GetJsonValue();
	}

}

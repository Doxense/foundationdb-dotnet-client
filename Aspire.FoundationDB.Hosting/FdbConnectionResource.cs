#region Copyright (c) 2023-2023 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Aspire.Hosting.ApplicationModel
{

    public class FdbConnectionResource : Resource, IFdbResource
    {

        public FdbConnectionResource(string name) : base(name) { }

        public int ApiVersion { get; set; }

        public string ClusterFile { get; set; }

        public string? GetConnectionString()
        {
            return "file://" + this.ClusterFile;
        }

    }

}
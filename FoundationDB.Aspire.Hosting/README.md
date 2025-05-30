#### Using Aspire

It is possible to add a FoundationDB cluster resource to your Aspire application model, and pass a reference to this cluster to the projects that need it.

For local development, a local FoundationDB node will be started using the `foundationdb/foundationdb` Docker image, and all projects that use the cluster reference will have a temporary Cluster file pointing to the local instance.

Note: you will need to install Docker on your development machine, as explained in https://learn.microsoft.com/en-us/dotnet/aspire/get-started/add-aspire-existing-app#prerequisites

In the Program.cs of you AppHost project:
```c#
private static void Main(string[] args)
{
    var builder = DistributedApplication.CreateBuilder(args);

    // Define a locally hosted FoundationDB cluster
    var fdb = builder
        .AddFoundationDb("fdb", apiVersion: 720, root: "/Sandbox/MySuperApp", clusterVersion: "7.2.5", rollForward: FdbVersionPolicy.Exact);

    // Project that needs a reference to this cluster
    var backend = builder
        .AddProject<Projects.AwesomeWebApiBackend>("backend")
        //...
        .WithReference(fdb); // register the fdb cluster connection

    // ...
}
```

For testing/staging/production, or "non local" development, it is also possible to configure a FoundationDB connection resource that will pass the specified Cluster file to the projects that reference the cluster resource.

In the Program.cs of your AppHost project:
```c#
private static void Main(string[] args)
{
    var builder = DistributedApplication.CreateBuilder(args);

    // Define an external FoundationDB cluster connection
    var fdb = builder
        .AddFoundationDbCluster("fdb", apiVersion: 720, root: "/Sandbox/MySuperApp", clusterFile: "/SOME/PATH/TO/testing.cluster")		;

    // Project that needs a reference to this cluster
    var backend = builder
        .AddProject<Projects.AwesomeWebApiBackend>("backend")
        //...
        .WithReference(fdb); // register the fdb cluster connection

    // ...
}
```

Then, in the Program.cs, or where you are declaring your services with the DI, use the following extension method to add support for FoundationDB:

```c#
var builder = WebApplication.CreateBuilder(args);

// setup Aspire services...
builder.AddServiceDefaults();
//...

// hookup the FoundationDB component
builder.AddFoundationDb("fdb"); // "fdb" is the same name we used in AddFoundationDb(...) or AddFoundationDbCLuster(...) in the AppHost above.

// ...rest of the startup logic....
```

This will automatically register an instance of the `IFdbDatabaseProvider` service, automatically configured to connect the FDB local or external cluster defined in the AppHost.

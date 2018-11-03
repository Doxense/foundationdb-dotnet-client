This folder contains all the shared framework libraries and tools.

To build the project using a "shared framework" assembly reference, you must create a fake .csproj that includes all the source files from the library and that defines the `USE_SHARED_FRAMEWORK` constant.

To build the library in "standalone mode" (no external dependency), this constant must not be defined. This is the default build for the NuGet Package.

The difference between the two modes is that the standalone version has its own copy of the shared types (Slice, Uuid128, ...), while the "shared framework" version will import these from another assembly and will be usable by an application that references both.


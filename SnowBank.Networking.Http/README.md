SnowBank SDK - Core Library
===========================

> Core library for the FoundationDB .NET Client and SnowBank SDK

This package contains the low-level components that are used by other packages.

- Slice: better version of `ReadOnlyMemory<byte>`, that allows easier usage of binary keys and values.
- Tuples: implementation of tuples that is closer to Python, and makes it easier to work with composite keys.
- JSON: Supercharged implementation of a JSON parser, binder, DOM and code generator.
- Various interfaces that can be shared between all the other packages, while the implementation is in other packages.

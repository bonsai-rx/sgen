# Bonsai - Serializer Generator

Tools for defining record data types in the [Bonsai visual reactive programming language](https://bonsai-rx.org) with [JSON Schema](https://json-schema.org/). Each type is specified once in a schema, and the generated C# code includes operators to create, serialize and deserialize records of that type in a workflow.

## Packages

- [`Bonsai.Sgen`](https://www.nuget.org/packages/Bonsai.Sgen) is a .NET tool that generates C# types and operators from JSON Schema files.
- [`bonsai-sgen`](https://pypi.org/project/bonsai-sgen/) is a Python package of Pydantic base classes for models that export schemas compatible with `Bonsai.Sgen`. Its `bonsai-sgen` command exports the schemas of a module as JSON Schema files, or generates the C# types directly.

Both packages are released together, and each release of `bonsai-sgen` matches the release of `Bonsai.Sgen` with the same version number.

## Additional Documentation

For installation steps, examples and the reference of both packages, refer to the [official Bonsai.Sgen documentation](https://bonsai-rx.org/sgen).

## Feedback & Contributing

`Bonsai.Sgen` and `bonsai-sgen` are released as open source under the [MIT license](https://github.com/bonsai-rx/sgen/blob/main/LICENSE). Bug reports and contributions are welcome at [the GitHub repository](https://github.com/bonsai-rx/sgen).

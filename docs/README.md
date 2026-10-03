# Serializer Generator Tool

`Bonsai.Sgen` is a code generator tool for the [Bonsai visual reactive programming language](https://bonsai-rx.org). It uses [JSON Schema](https://json-schema.org/) to specify [record data types](https://en.wikipedia.org/wiki/Record_(computer_science)), and generates operators to create and manipulate these records. It builds on [NJsonSchema](https://github.com/RicoSuter/NJsonSchema), adding further customization of the generated code and features specific to Bonsai.

## Getting Started

1. Install `Bonsai.Sgen` as a local tool:

    ```cmd
    dotnet new tool-manifest
    ```

    ```cmd
    dotnet tool install --local Bonsai.Sgen
    ```

2. Generate YAML or JSON serialization classes from a schema file into the `Extensions` folder of the project:

    ```cmd
    dotnet bonsai.sgen schema.json -o Extensions --serializer yaml
    ```

    ```cmd
    dotnet bonsai.sgen schema.json -o Extensions --serializer json
    ```

3. Add the package reference for the chosen serializer to the `Extensions.csproj` file:

    ```xml
    <PackageReference Include="YamlDotNet" Version="16.3.0" />
    ```

    ```xml
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
    ```

4. When using the JSON serializer, also install `Newtonsoft.Json` in the Bonsai environment.

## Additional Documentation

For additional documentation and examples, refer to the [official Bonsai.Sgen documentation](https://bonsai-rx.org/sgen/articles/basic-usage.html).

## Feedback & Contributing

`Bonsai.Sgen` is released as open source under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are welcome at [the GitHub repository](https://github.com/bonsai-rx/sgen).
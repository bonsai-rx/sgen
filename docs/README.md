# Serializer Generator Tool

`Bonsai.Sgen` is a code generator tool for the [Bonsai](https://bonsai-rx.org/) programming language. It leverages [JSON Schema](https://json-schema.org/) as a standard to specify [record data types](https://en.wikipedia.org/wiki/Record_(computer_science)), and automatically generates operators to create and manipulate these objects. It builds on top of  [NJsonSchema](https://github.com/RicoSuter/NJsonSchema) by providing further customization of the generated code as well as Bonsai-specific features.

## Getting Started

1. Install `Bonsai.Sgen` as a local tool:

    ```cmd
    dotnet new tool-manifest
    ```

    ```cmd
    dotnet tool install --local Bonsai.Sgen
    ```

2. Generate YAML or JSON serialization classes from a schema file to your project `Extensions` folder:

    ```cmd
    dotnet bonsai.sgen schema.json -o Extensions --serializer yaml
    ```

    ```cmd
    dotnet bonsai.sgen schema.json -o Extensions --serializer json
    ```

3. Add the necessary package references to your `Extensions.csproj` file:

    ```xml
    <PackageReference Include="YamlDotNet" Version="16.3.0" />
    ```

    ```xml
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
    ```

> [!IMPORTANT]
> 
> Make sure the above packages are installed in your Bonsai environment as well. `YamlDotNet` should be built-in, whereas `Newtonsoft.Json` may have to be installed. To find `Newtonsoft.Json`, go to the package manager, click "Show advanced", and it will usually be the first package in the list.

## Additional Documentation

For additional documentation and examples, refer to the [Sgen documentation pages](https://bonsai-rx.org/sgen/articles/basic-usage.html).

## Feedback & Contributing

`Bonsai.Sgen` is released as open source under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are welcome at [the GitHub repository](https://github.com/bonsai-rx/sgen).
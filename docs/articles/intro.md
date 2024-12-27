---
uid: intro
---

## What is Bonsai.Sgen?

`Bonsai.Sgen` is a code generator tool for the [Bonsai programming language](https://bonsai-rx.org/). It leverages [`json-schema`](https://json-schema.org/) as a standard to represent [record-like](https://en.wikipedia.org/wiki/Record_(computer_science)) structures, and automatically generates Bonsai-compatible isomorphic operators to create and manipulate these objects.

## Getting started

1. Navigate to the [Bonsai.Sgen NuGet tool package](https://www.nuget.org/packages/Bonsai.Sgen/)
2. Click `.NET CLI (Local)` and copy the two suggested commands. E.g.:

    ```cmd
    dotnet new tool-manifest # if you are setting up this repo
    dotnet tool install --local Bonsai.Sgen
    ```

3. To view the tool help reference documentation, run:

    ```cmd
    dotnet bonsai.sgen --help
    ```

4. To generate YAML serialization classes from a schema file:

    ```cmd
    dotnet bonsai.sgen --schema schema.json --serializer YamlDotNet
    ```

5. To generate JSON serialization classes from a schema file:

    ```cmd
    dotnet bonsai.sgen --schema schema.json --serializer NewtonsoftJson
    ```

6. Copy the generated class file to your project `Extensions` folder.

7. Add the necessary package references to your `Extensions.csproj` file. For example:

    ```xml
    <ItemGroup>
        <PackageReference Include="Bonsai.Core" Version="2.8.0" />
        <PackageReference Include="YamlDotNet" Version="13.7.1" />
    </ItemGroup>
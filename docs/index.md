# Getting started

`Bonsai.Sgen` is a code generator tool for the [Bonsai visual reactive programming language](https://bonsai-rx.org). It uses [JSON Schema](https://json-schema.org/) to specify record data types, and generates operators to create and manipulate these records. The [Data definition](articles/data-definition.md) article describes how to define record data types for use in a workflow.

> [!TIP]
> Familiarity with [Scripting Extensions](https://bonsai-rx.org/docs/articles/scripting-extensions.html) is recommended before using this tool.

## Installation

`Bonsai.Sgen` is installed as a local tool of the repository that holds the workflow:

```powershell
dotnet new tool-manifest
dotnet tool install --local Bonsai.Sgen
```

## Code generation

[!INCLUDE [](articles/example-person.md)]

The generated code depends on the library of the chosen serializer, so the `Extensions.csproj` file needs the matching package reference:

```xml
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

`Newtonsoft.Json` also has to be installed in the Bonsai environment from the package manager. The [Package references](articles/basic-usage.md#package-references) section describes the requirements of the YAML serializer.

## Saving and loading

The generated operators create `Person` objects, convert them to JSON text and back, and can be combined with file operators to save and load records:

:::workflow
![(de)serialization](~/workflows/simple-serialization-example.bonsai)
:::

The [Basic usage](articles/basic-usage.md) and [Advanced usage](articles/advanced-usage.md) articles cover schemas with several types, enumerations, lists, nullable and required fields, and unions.

## Python models

The [`bonsai-sgen`](https://pypi.org/project/bonsai-sgen/) package provides base classes for [Pydantic](https://docs.pydantic.dev/latest/) models that follow the conventions of `Bonsai.Sgen`. Models written as Python classes export their schemas, or generate the C# types directly, through the `bonsai-sgen` command. The [Pydantic usage](articles/pydantic-usage.md) article describes how to define and export these models.

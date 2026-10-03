# Basic usage

`Bonsai.Sgen` can generate many kinds of models, with different relationships between types.

> [!TIP]
> Familiarity with [Scripting Extensions](https://bonsai-rx.org/docs/articles/scripting-extensions.html) is recommended before using this tool.

## Single object

The first example models the `Person` record type from the [Data definition](data-definition.md) article.

[!INCLUDE [](example-person.md)]

## Multiple objects

Projects often need more than one record type. A single schema file can define several types, and `Bonsai.Sgen` generates all of them at once:

[person-and-dog.json](~/workflows/person-and-dog.json)

```json
{
  "title": "PersonAndPet",
  "$defs": {
    "Person": {
      "title": "Person",
      "type": "object",
      "properties": {
        "age": { "type": "integer" },
        "first_name": { "type": "string" },
        "last_name": { "type": "string" },
        "date_of_birth": { "type": "string", "format": "date-time" }
      }
    },
    "Dog": {
      "title": "Dog",
      "type": "object",
      "properties": {
        "name": { "type": "string" },
        "breed": { "type": "string" },
        "age": { "type": "integer" }
      }
    }
  },
  "type": "object",
  "properties": {
    "owner": { "$ref": "#/$defs/Person" },
    "pet": { "$ref": "#/$defs/Dog" }
  }
}
```

```powershell
dotnet bonsai.sgen person-and-dog.json -o Extensions --serializer yaml
```

:::workflow
![Person And Dog](~/workflows/person-and-dog.bonsai)
:::

Some points to note in this example:

- The schema file contains the two definitions `Person` and `Dog`, each of which generates an operator of the same name.
- The schema root, titled `PersonAndPet`, combines the two objects into a single record.
- The `--serializer` flag selects YAML for the generated [serialization and deserialization operators](#serialization-and-deserialization).
- The generated classes are in a different namespace from the previous example. By default, the namespace is derived from the name of the schema file, which prevents name clashes between schemas, such as between `PersonAndDog.Person` here and `Person.Person` in the previous example.

> [!TIP]
> The `--namespace` flag sets the namespace of the generated code explicitly.

## Nested objects

Generated types can hold other generated types, as the `PersonAndPet` record holds a `Person` and a `Dog`. Workflows can create, compose and manipulate these nested objects directly:

:::workflow
![Person And Dog Nested Building](~/workflows/person-and-dog-nested-building.bonsai)
:::

## Enums

`Bonsai.Sgen` also supports the generation of enums using the [`enum`](https://json-schema.org/understanding-json-schema/reference/enum) keyword in the JSON Schema. For example, a `Pet` enum can replace the `Dog` object of the previous example:

[person-and-pet-enum.json](~/workflows/person-and-pet-enum.json)

```json
(...)

{
  "Pet": {
    "title": "Pet",
    "type": "string",
    "enum": ["Dog", "Cat", "Fish", "Bird", "Reptile"]
  }
},
"type": "object",
"properties": {
  "owner": {"$ref": "#/$defs/Person"},
  "pet": {"$ref": "#/$defs/Pet"}
}
```

In a workflow, the generated enums behave as ordinary [`Enum`](https://learn.microsoft.com/en-us/dotnet/api/system.enum) types:

:::workflow
![Person and Pets](~/workflows/person-and-pet-enum.bonsai)
:::

> [!TIP]
> The `x-enumNames` annotation specifies the names of the generated enum members, for example to name integer values:
>
> ```json
> {
>   "MyIntEnum": {
>     "enum": [0, 1, 2, 3, 4],
>     "title": "MyIntEnum",
>     "type": "integer",
>     "x-enumNames": ["None", "One", "Two", "Three", "Four"]
>   }
> }
> ```

## Lists

`Bonsai.Sgen` also generates lists from the `array` type:

```json
"pets": {
  "type": "array",
  "items": {"$ref": "#/$defs/Pet"}
}
```

An `array` generates a [`List<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1) property, which can be created and manipulated like any other list:

:::workflow
![Person and Pets](~/workflows/person-and-pets-enum.bonsai)
:::

## Dates and times

JSON Schema represents dates and times as strings with the `date` or `date-time` format:

```json
"date_of_birth": { "type": "string", "format": "date-time" }
```

Both formats generate a [`DateTimeOffset`](https://learn.microsoft.com/en-us/dotnet/api/system.datetimeoffset) property. A `date-time` value follows [RFC 3339](https://www.rfc-editor.org/rfc/rfc3339), which requires the offset from UTC that `DateTimeOffset` stores. A `date` value has no offset, but .NET Framework has no type that holds only a date, so the generated property uses `DateTimeOffset` as well. A date stored as a `DateTime` converts implicitly to `DateTimeOffset`.

## Nullable types

JSON Schema represents a missing value with the `null` type. `Bonsai.Sgen` generates a nullable type for a property that accepts either `null` or one other type:

```json
"pet": {
  "anyOf": [
    {"$ref": "#/$defs/Pet"},
    {"type": "null"}
  ]
}
```

For a value type, the generated property is a [nullable value type](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/nullable-value-types), whose `HasValue` property tests whether a value is present, and only then can its `Value` property be read.

For a reference type, the generated property keeps its type, since reference types always accept `null` in the generated C# code. An `ExpressionTransform` operator with the expression `it == null` tests whether the value is missing:

:::workflow
![Nullable pet](~/workflows/person-and-pet-enum-nullable.bonsai)
:::

## Required fields

The JSON Schema [`required`](https://json-schema.org/learn/getting-started-step-by-step#define-required-properties) keyword lists the fields an object must contain, and the `default` keyword gives a field its initial value. A field with a `default` value starts with that value, both in a new object and when the field is missing from deserialized data. Any other field starts with the default value of its C# type, such as `0` or `null`. Only the JSON deserializer checks `required`, so an object created in a workflow or read from YAML has to be populated correctly before it is used.

> [!NOTE]
> Languages and libraries use the terms `nullable` and `required` in different ways. This tool uses the following definitions:
>
> - `nullable` means the field can be `null` or of type `T`.
> - `required` means the field must be present in the object at deserialization time.
> - A field can be `nullable` and `required` at the same time. It must then be present in the object, but its value can be `null`.
> - A field can be `not required` and `nullable`. This does not mean its default value is `null`. The field accepts `null`, and the object provides a default value, but the declaration says nothing about what that default value is.
> - A field can be `not required` and `not nullable`. The field should then declare a `default` value that is not `null`.

## Serialization and deserialization

A schema guarantees that every record can be serialized and deserialized, so objects convert in both directions between C# types and a text format suited to specification and logging. When the `--serializer` option is specified, `Bonsai.Sgen` generates serialization and deserialization operators for every type in the schema. Two formats are supported: `json`, using [`Newtonsoft.Json`](https://github.com/JamesNK/Newtonsoft.Json), and `yaml`, using [`YamlDotNet`](https://github.com/aaubry/YamlDotNet).

The `SerializeToJson` and `SerializeToYaml` operators convert an object of any type generated in the namespace into a `string`. The `DeserializeFromJson` and `DeserializeFromYaml` operators convert a `string` back into an object, and throw an exception if the string fails validation.

:::workflow
![(de)serialization](~/workflows/serialization-example.bonsai)
:::

### Package references

The generated code depends on the library of the chosen serializer, so the `Extensions.csproj` file needs the matching package reference:

```xml
<PackageReference Include="YamlDotNet" Version="16.3.0" />
```

```xml
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

The Bonsai environment needs the same package. `YamlDotNet` is already included with the Bonsai editor, whereas `Newtonsoft.Json` has to be installed from the package manager.

> [!TIP]
> The package manager lists only Bonsai packages by default. Check "Show advanced" to search for other packages, such as `Newtonsoft.Json`.
---
uid: basic-usage
---


## Automatic generation of Bonsai code using Bonsai.Sgen

We will expand this example later on, but for now, let's see how we can use `Bonsai.Sgen` to automatically generate the Bonsai code for the `Person` object.

First, we need to define the schema of the object in a JSON file:

[person](~/workflows/person.json)

```json
{
  "title": "Person",
  "type": "object",
  "properties": {
    "Age": { "type": "integer" },
    "FirstName": { "type": "string" },
    "LastName": { "type": "string" },
    "DOB": { "type": "string", "format": "date-time" }
  }
}
```

Second, we need to run the `Bonsai.Sgen` tool to generate the Bonsai code:

```cmd
dotnet bonsai.sgen --schema docs/workflows/person.json --output docs/workflows/Extensions/PersonSgen.cs
```

Finally, we can use the generated code in our Bonsai workflow:

:::workflow
![Person as BonsaiSgen](~/workflows/person-example-bonsai-sgen.bonsai)
:::

As you can probably tell, the `Bonsai.Sgen` approach is much more concise and less error-prone than the previous ones. It allows you to focus on the data structure itself and not on the boilerplate code required to create it in Bonsai. Moreover, as we will see later, the tool also automatically generates serialization and deserialization boilerplate code for the object, which can be very useful when working with external data sources.

Finally, if one considers the `json-schema` as the "source of truth" for the data structure representation, it is possible to generate multiple representations of the object in different languages, ensuring interoperability. This can be very useful when working in a multi-language environment (e.g. running experiment in Bonsai and analysis in Python) and when sharing data structures across different projects.

## Multiple objects

The previous example shows how a single record can be modelled. In practice, projects often require modelling different types of objects. This is where `Bonsai.Sgen` shines, as it allows you to generate multiple objects from a single schema file:

[person_and_dog](~/workflows/person-and-dog.json)

```json
{
  "title": "PersonAndPet",
  "definitions": {
    "Person": {
      "title": "Person",
      "type": "object",
      "properties": {
        "Age": { "type": "integer" },
        "FirstName": { "type": "string" },
        "LastName": { "type": "string" },
        "DOB": { "type": "string", "format": "date-time" }
      }
    },
    "Dog": {
      "title": "Dog",
      "type": "object",
      "properties": {
        "Name": { "type": "string" },
        "Breed": { "type": "string" },
        "Age": { "type": "integer" }
      }
    }
  },
  "type": "object",
  "properties": {
    "owner": { "$ref": "#/definitions/Person" },
    "pet": { "$ref": "#/definitions/Dog" }
  }
}
```

```cmd
dotnet bonsai.sgen --schema docs/workflows/person_and_dog.json --output docs/workflows/Extensions/PersonAndDogSgen.cs --namespace PersonAndDog
```

:::workflow
![PersonAndDog as BonsaiSgen](~/workflows/person-and-dog-example-bonsai-sgen.bonsai)
:::

A few things worth noting in this example:

- The schema file contains two definitions: `Person` and `Dog` that give rise to two operators (`Person` and `Dog`) in the generated code.
- A third definition `PersonAndPet` is used to combine the two objects into a single record. This can be omitted as we will see later by using the `x-abstract` property.
- The `--namespace` flag is used to specify the namespace of the generated code. This is useful to prevent name clashes between different schemas (e.g. `PersonAndDog.Person` and `Person` from the previous example).
- Both `Person` and `Dog` `json-schema` objects are passed as references. This is extremely important as it allows the reuse of the same definition in multiple places. This is particularly useful when working with complex data structures that share common fields. If definitions are passed in-line (i.e. redefined each time), Bonsai.Sgen may not be able to correctly identify them as the same object.

## Nested objects

The previous example highlights the simplicity of generating Bonsai code for simple objects. However, the real power of `Bonsai.Sgen` comes when dealing with more complex data structures, such as nested objects. Moreover, Bonsai syntax lends itself quite nicely to represent these nested structures as well as build them:


:::workflow
![PersonAndDog building as BonsaiSgen](~/workflows/person-and-dog-nested-building-example-bonsai-sgen.bonsai)
:::

## Enums

`Bonsai.Sgen` also supports the generation of enums using the `enum` type in the `json-schema`:

We can replace the `Pet` object in the previous example with an [`enum`](https://json-schema.org/understanding-json-schema/reference/enum):

[Pet as Enum](~/workflows/person-and-pet-enum.json).

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
    "owner": {"$ref": "#/definitions/Person"},
    "pet": {"$ref": "#/definitions/Pet"}
  }
```

In Bonsai, they can be manipulated as [`Enum`](https://learn.microsoft.com/en-us/dotnet/api/system.enum?view=net-9.0) types:

:::workflow
![Person and Pets](~/workflows/person-and-pet-enum.bonsai)
:::

> [!TIP]
> In certain cases, it may be useful to use `x-enum-names` to specify the rendered names of the enum values.
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

`Bonsai.Sgen` also supports the generation of lists using the `array` type in the `json-schema`:

```json
    "pets": {
      "type": "array",
      "items": {"$ref": "#/definitions/Pet"}
    }
```

`json-schema` `array`s will be rendered as [`List<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1?view=net-9.0) in the generated code and can be manipulated (and created) as such.

:::workflow
![Person and Pets](~/workflows/person-and-pets-enum.bonsai)
:::

## Nullable types

`json-schema` supports the `null` type, which can be used to represent nullable types. The standard is a bit loose in this regard, but `Bonsai.Sgen` will generate a nullable-T if the json-schema represents it using the `oneOf` keyword:

```json
    "pet": {
      "oneOf": [
        {"$ref": "#/definitions/Pet"},
        {"type": "null"}
      ]
    }
```

For value types, the generated code will render a [Nullable value type](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/nullable-value-types) type. This type will expose two properties: `HasValue` and `Value`, that can be used to test and manipulate the type, respectively.

For reference types, the generated code will not render a nullable type since reference types are already nullable in C#. A consumer can test for `null` to determine if the value is present by simply using an `ExpressionTransform` operator with `it == null`.:

:::workflow
![Nullable pet](~/workflows/person-and-pets-enum-nullable.bonsai)
:::


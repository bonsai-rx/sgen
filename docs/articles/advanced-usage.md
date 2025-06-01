# Advanced usage

## Unions

In the previous examples, we have seen how create object properties of a single type. However, in practice, data structures' fields can often be represented by one of several types. We have actually seen a special case of this behavior in the previous nullable example, where a field can be either a value of a given type `T` or `null` (or an Union between type `T` and `null`).

`json-schema` allows union types using the `oneOf` keyword. For example:

```json
{
  "title": "MyPet",
  "type": "object",
  "properties": {
    "FooProperty": {
      "oneOf": [
        { "type": "string" },
        { "type": "number" }
      ]
    }
  }
}
```

Running `Bonsai.Sgen` on this schema generates the following type signature for `FooProperty`:

```csharp
public object FooProperty
```

While `oneOf` is supported, statically typed languages like `C#` require the exact type at compile time. Thus, the property is "up-cast" to `object`, and users must down-cast it to the correct type at runtime.

## Tagged-Unions

Unions types can be made type-aware by using [`tagged unions`](https://en.wikipedia.org/wiki/Tagged_union) (or `discriminated unions`). The syntax for tagged unions is not part of the `json-schema` specification, but it is supported by the [`OpenAPI` standard](https://swagger.io/docs/specification/v3_0/data-models/inheritance-and-polymorphism/#discriminator), which is a superset of `json-schema`. The key idea behind tagged unions is to add a `discriminator` field to the schema that specifies the property that will be used to determine the type of the object at runtime.

For example, a `Pet` object that can be either a `Dog` or a `Cat` can be represented as follows:

[person](~/workflows/person-and-discriminated-pets.json)

```json
"Pet": {
  "discriminator": {
    "mapping": {
      "cat": "#/definitions/Cat",
      "dog": "#/definitions/Dog"
    },
    "propertyName": "pet_type"
  },
  "oneOf": [
    { "$ref": "#/definitions/Dog" },
    { "$ref": "#/definitions/Cat" }
  ]
}
```

In `C#`, `Bonsai.Sgen` will generate a root type `Pet` that will be inherited by the `Dog` and `Cat` types (since in the worst case scenario, the discriminated property must be shared). The `Pet` type will have a `pet_type` property that will be used to downcast to the proper type at runtime. At this point we can open our example in `Bonsai` and see how the `Pet` type is represented in the workflow.

As you can see below, we still get a `Pet` type. Better than `object` but still not a `Dog` or `Cat` type. Fortunately, `Bonsai.Sgen` will generate an operator that can be used to filter and downcast the `Pet` objects to the correct type at runtime. These are called `Match<T>` operators. `MatchPet` can be used to select the desired target type which will allow us access to the properties of the `Dog` or `Cat` subtypes. Conversely, we can also upcast a `Dog` or `Cat` to a `Pet` by leaving the `MatchPet` operator's `Type` property empty.

:::workflow
![Discriminated Unions](~/workflows/person-pet-discriminated-union.bonsai)
:::

> [!Important]
> In is strongly recommended to use references with the `oneOf` syntax. Not only does this decision make your `json-schema` significantly smaller, it will also help `Bonsai.Sgen` generate the correct class hierarchy if multiple unions are present in the schema. If you use inline objects, `Bonsai.Sgen` will likely have to generate a new root class for each union, which can lead to a lot of duplicated code and a more complex object hierarchy.



## Extending generated code with `partial` classes

Generated classes are marked as [`partial`](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/partial-classes-and-methods), allowing you to extend them without modifying the generated code directly. This can be done by placing the new `.cs` file in the [`Extensions`](https://bonsai-rx.org/docs/articles/scripting-extensions.html) folder of your project.

For example, to add an operator for summing `Cat` objects:

```csharp
namespace PersonAndDiscriminatedPets
{
    partial class Cat
    {
        public static Cat operator +(Cat c1, Cat c2)
        {
            return new Cat
            {
                CanMeow = c1.CanMeow || c2.CanMeow,
                Age = c1.Age + c2.Age
            };
        }
    }
}
```

In Bonsai, use the `Add` operator to sum `Cat` objects:

:::workflow
![Discriminated Unions](~/workflows/sum-cats.bonsai)
:::

## Supported tags

- `x-abstract`: Marks a class as abstract, preventing it from being generated as an operator in Bonsai.

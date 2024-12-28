---
uid: advanced-usage
---


## Unions

In the previous examples, we have seen how create object properties of a single type. However, in practice, data structures' fields can often be represented by one of several types. We have actually seen a special case of this behavior in the previous nullable example, where a field can be either a value of a given type or `null` (or an Union between type `T` and `null`).

Similarly, `json-schema` allows union types to be defined using the `oneOf` keyword. For example, consider the following schema:

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

If we run `Bonsai.Sgen` on this schema, we will get the following signature for the `FooProperty` property:

```csharp
public object FooProperty
```

This is because while the `oneOf` keyword is supported by the `Bonsai.Sgen` tool, for statically typed languages like `C#` and `Bonsai`, we need to know the exact type of the property at compile time. As a result, we opt to "up-cast" the property to the most general type that can represent all the possible types in the union (`object`). It is up to the user to down-cast the property to the correct type at runtime.


## Tagged Unions

At this point, you might be wondering if there is a way to represent union types in a more type-safe way in json-schema. The answer is yes, and the way to do it is by using [`discriminated unions`](https://en.wikipedia.org/wiki/Tagged_union) (or `tagged union`). The syntax for discriminated unions is not supported by vanilla `json-schema`, but it is supported by the [`OpenAPI` standard](https://swagger.io/docs/specification/v3_0/data-models/inheritance-and-polymorphism/#discriminator), which is a superset of `json-schema`. The key idea behind discriminated unions is to add a `discriminator` field to the schema that specifies the property that will be used to determine the type of the object at runtime.

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
        {
          "$ref": "#/definitions/Dog"
        },
        {
          "$ref": "#/definitions/Cat"
        }
      ]
    }
```

In `C#`, `Bonsai.Sgen` will generate a root type `Pet` that will be inherited by the `Dog` and `Cat` types (since in the worst case scenario, the discriminated property must be shared). The `Pet` type will have a `pet_type` property that will be used to downcast to the proper type at runtime. At this point we can open our example in `Bonsai` and see how the `Pet` type is represented in the workflow.

As you can see below, we still get a `Pet` type. Better than `object` but still not a `Dog` or `Cat` type. Fortunately, `Bonsai.Sgen` will generate an operator that can be used to filter and downcast the `Pet` objects to the correct type at runtime. These are called `Match<T>` operators. After adding a `MatchPet` to our workflow we can select the desired target type which will allow us access to the properties of the `Dog` or `Cat` type. Conversely, we can also upcast a `Dog` or `Cat` to a `Pet` leaving the `MatchPet` operator's `Type` property empty.

:::workflow
![Discriminated Unions](~/workflows/person-pet-discriminated-union.bonsai)
:::

> [!Important]
> In is general advisable to use references in the `oneOf` syntax. Not only does this decision make your `json-schema` significantly smaller, it will also help `Bonsai.Sgen` generate the correct class hierarchy if multiple unions are present in the schema. If you use inline objects, `Bonsai.Sgen` will likely have to generate a new root class for each union, which can lead to a lot of duplicated code and a more complex object hierarchy.



## Extending generated code with `partial` classes

Since `Bonsai.Sgen` will generate proper `class` for each object in the schema, it is possible to use these types to create custom operators and methods using the `Scriping Extensions` feature of `Bonsai`. However, sometimes we may want to extend the features of the generated classes directly...

For those that inspected the general `C#` code, you will notice that all classes are marked as [`partial`](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/partial-classes-and-methods). This is a feature of `C#` that allows a class to be split. This was a deliberate design choice to allow users to extend the generated code. However, because it is usually always a bad idea to modify generated code directly (e.g. we may want to regenerate it in the future), `partial` classes allows modification to be made in a separate file.

Suppose we want to sum `Cats`, we can overload the operator with a small method in a separate file::

```csharp
namespace PersonAndDiscriminatedPets
{
    partial class Cat{
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

In `Bonsai`, we can now use the `Add` operator to sum `Cats`:


:::workflow
![Discriminated Unions](~/workflows/sum-cats.bonsai)
:::


## Other supported tags

- `x-abstract`: This tag is used to mark a class as abstract. An abstract class will not be generated as an operator in Bonsai. This may be useful for root classes of unions that may never need to be manipulated in Bonsai.
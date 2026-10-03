# Advanced usage

## Unions

In the previous examples, we have seen how to create object properties of a single type. However, in many real-world applications, data structure fields can be represented by one of several types. We have actually seen a special case of this behavior in the previous nullable example, where a field can be either a value of a given type `T` or `null` (or a "union" between type `T` and `null`).

JSON Schema allows union types using the `oneOf` keyword. For example:

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

While `oneOf` is supported, statically typed languages like C# require the exact type at compile time. Thus, the property is "up-cast" to `object`, and you must down-cast it to the correct type at runtime.

## Tagged-Unions

Union types can be made type-aware by using [`tagged unions`](https://en.wikipedia.org/wiki/Tagged_union) (or `discriminated unions`). The syntax for tagged unions is not part of the JSON Schema specification, however it is supported by the [OpenAPI standard](https://swagger.io/docs/specification/v3_0/data-models/inheritance-and-polymorphism/#discriminator), which is a superset of JSON Schema. The key idea behind tagged unions is to add a `discriminator` field to the schema that specifies the property that will be used to determine the type of the object at runtime.

For example, a `Pet` object that can be either a `Dog` or a `Cat` can be represented as follows:

[person-and-discriminated-pets.json](~/workflows/person-and-discriminated-pets.json)

```json
"Pet": {
  "discriminator": {
    "mapping": {
      "cat": "#/$defs/Cat",
      "dog": "#/$defs/Dog"
    },
    "propertyName": "pet_type"
  },
  "oneOf": [
    { "$ref": "#/$defs/Dog" },
    { "$ref": "#/$defs/Cat" }
  ]
}
```

Given this schema, `Bonsai.Sgen` will generate a root type `Pet` that will be specialised by the `Dog` and `Cat` types (since in the worst case scenario, the discriminated property must be shared). The `Pet` type will have a `pet_type` property that will be used to downcast to the proper type at runtime. At this point we can open our example in `Bonsai` and see how the `Pet` type is represented in the workflow.

As you can see below, we still get a `Pet` type. Better than `object`, but still not a `Dog` or `Cat` type. Fortunately, `Bonsai.Sgen` will generate an operator that can be used to filter and downcast the `Pet` objects to the correct type at runtime. These are called `Match<T>` operators. `MatchPet` can be used to select the desired target type which will allow us access to the properties of the `Dog` or `Cat` subtypes. Conversely, we can also upcast a `Dog` or `Cat` to a `Pet` by leaving the `MatchPet` operator's `Type` property empty.

Match operators also accept a sequence of any other type, such as `object`, and keep only the elements matching the selected type. An input type unrelated to the union, such as a type with a hand-written conversion to `Pet`, is first converted to `Pet`. Selecting a type that no input element could ever match makes the workflow fail to build.

:::workflow
![Discriminated Unions](~/workflows/person-pet-discriminated-union.bonsai)
:::

> [!IMPORTANT]
> It is strongly recommended to use references with the `oneOf` syntax. Not only does this decision make your JSON Schema significantly smaller, it will also help `Bonsai.Sgen` generate the correct class hierarchy if multiple unions are present in the schema. If you use inline objects, `Bonsai.Sgen` will likely have to generate a new root class for each union, which can lead to a lot of duplicated code and a more complex object hierarchy.


## Unions of independent types

A tagged union can include either a member type that is defined outside the generated code, such as a type generated from another schema, or a type that already derives from another type, such as a member of another union.

C# has no union types of its own, so `Bonsai.Sgen` models a tagged union as a base type that each member derives from. A type can derive from only one base, and only if it is generated in the same schema, so `Bonsai.Sgen` generates a wrapper type for each of the other members instead. The wrapper changes neither the data format nor how the union is used in a workflow.

For example, the `Dog` member below refers to the `Dog` type generated from [person-and-dog.json](~/workflows/person-and-dog.json) through the `x-sgen-typename` annotation:

[external-union-member.json](~/workflows/external-union-member.json)

```json
"Dog": {
  "title": "Dog",
  "type": "object",
  "x-sgen-typename": "PersonAndDog.Dog",
  "properties": {
    "Name": { "type": "string" },
    "Breed": { "type": "string" },
    "Age": { "type": "integer" }
  }
},
"Pet": {
  "title": "Pet",
  "discriminator": {
    "propertyName": "pet_type",
    "mapping": {
      "cat": "#/$defs/Cat",
      "dog": "#/$defs/Dog"
    }
  },
  "oneOf": [
    { "$ref": "#/$defs/Cat" },
    { "$ref": "#/$defs/Dog" }
  ]
}
```

Given this schema, `Bonsai.Sgen` generates `Cat` as a subtype of `Pet` as before. For the external `Dog` it generates a wrapper type `PetDog`, named after the union type and the discriminator tag, which derives from `Pet` and holds the `Dog` in its `Value` property. The `Pet` type also uses this wrapper type to convert implicitly from `Dog`:

```csharp
public partial class Pet
{
    public static implicit operator Pet(PersonAndDog.Dog value)
    {
        return new PetDog(value);
    }
}
```

A `Dog` is serialized with its `pet_type` discriminator tag next to its own properties, in the same flat format as a `Cat`:

```json
{ "owner": "Ana", "pet": { "pet_type": "dog", "Name": "Rex", "Breed": "Collie", "Age": 3 } }
```

In a workflow, a `MatchPet` operator with its `Type` set to `Dog` specifies a sequence that keeps only the `PetDog` wrappers of the `Pet` sequence and emits the `Dog` object held in each of them. For a sequence of `object`, it also keeps any plain `Dog` values. The implicit conversion shown above wraps a `Dog` in a `PetDog` whenever it is assigned to a property of type `Pet`.

Wrapper types are internal to the generated code, although a wrapped member still appears in visualizers under the name of its wrapper, such as `PetDog { Value = Dog { Name = Rex, Breed = Collie, Age = 3 } }`. A custom operator written in C# can read the member held by a wrapper through the generated `IUnionWrapper<T>` interface, for example by testing a `Pet` with `as IUnionWrapper<PersonAndDog.Dog>`.

> [!NOTE]
> Generation fails with an error for:
>
> - An external member that is itself a tagged union. List its members directly in one union instead.
> - A wrapper type name that clashes with an existing type. Rename the discriminator tag of that member.
> - A wrapped member with an ordinary property named like the discriminator, since its value would be lost. Declare the property as a constant or rename it.

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

## Supported annotations

- `x-abstract`: Marks a class as abstract, preventing it from being generated as an operator in Bonsai.
- `x-enumNames`: Specifies the names of the generated enum members, in the same order as the values listed in `enum`.
- `x-sgen-typename`: Specifies the fully qualified type name of a definition. A definition whose name is inside the generated namespace is generated under that name. A definition whose name is outside it refers to an existing type, which is not generated, so a type generated from one schema can be used by another.

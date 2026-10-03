# Advanced usage

## Unions

The previous examples define each property with a single type. In many applications, though, a field can hold a value of one of several types. The [nullable example](basic-usage.md#nullable-types) is a special case, where a field holds either a value of type `T` or `null`, a union of `T` and `null`.

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

A statically typed language such as C# needs a single type for the property at compile time, so the generated property has type `object`, and its value has to be downcast to the correct type at runtime.

## Tagged unions

A [tagged union](https://en.wikipedia.org/wiki/Tagged_union), also called a discriminated union, records which member type each value has. Tagged unions are not part of the JSON Schema specification, but the [OpenAPI standard](https://swagger.io/docs/specification/v3_0/data-models/inheritance-and-polymorphism/#discriminator), a superset of JSON Schema, supports them. A `discriminator` field in the schema names the property whose value selects the type of each object at runtime.

For example, the following schema declares a `Pet` that is either a `Dog` or a `Cat`:

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

From this schema, `Bonsai.Sgen` generates a base type `Pet` from which the `Dog` and `Cat` types derive. None of these types has a `pet_type` property. Instead, the generated serializers write the `pet_type` tag of each object and read it back to create an object of the matching type.

In a workflow, a property of type `Pet` is more specific than `object`, but still gives no access to the properties of `Dog` or `Cat`. `Bonsai.Sgen` therefore also generates an operator that filters and downcasts the objects at runtime. Each union gets its own match operator, such as `MatchPet` for the `Pet` union. Setting its `Type` property to `Dog` or `Cat` keeps only the objects of that type and gives access to their properties. Leaving `Type` empty instead upcasts a `Dog` or `Cat` to `Pet`.

Match operators also accept a sequence of any other type, such as `object`, and keep only the elements matching the selected type. An input type unrelated to the union, such as a type with a hand-written conversion to `Pet`, is first converted to `Pet`. Selecting a type that no input element could ever match makes the workflow fail to build.

:::workflow
![Discriminated Unions](~/workflows/person-pet-discriminated-union.bonsai)
:::

> [!IMPORTANT]
> List the members of a union in `oneOf` as references to definitions rather than as inline objects. References keep the schema smaller and let `Bonsai.Sgen` generate a single class hierarchy when the schema contains several unions. Inline objects can make it generate a separate base class for each union, which duplicates code and complicates the object hierarchy.

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
    "name": { "type": "string" },
    "breed": { "type": "string" },
    "age": { "type": "integer" }
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
{ "owner": "Ana", "pet": { "pet_type": "dog", "name": "Rex", "breed": "Collie", "age": 3 } }
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

Generated classes are declared [`partial`](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/partial-classes-and-methods), so they can be extended without changing the generated code. Place the extending `.cs` file in the [`Extensions`](https://bonsai-rx.org/docs/articles/scripting-extensions.html) folder of the project.

For example, the following file adds an operator that sums `Cat` objects:

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

In a workflow, the `Add` operator then sums `Cat` objects:

:::workflow
![Discriminated Unions](~/workflows/sum-cats.bonsai)
:::

## Supported annotations

- `x-abstract`: Marks a class as abstract, so no operator is generated for it.
- `x-enumNames`: Specifies the names of the generated enum members, in the same order as the values listed in `enum`.
- `x-sgen-typename`: Specifies the fully qualified type name of a definition. A definition whose name is inside the generated namespace is generated under that name. A definition whose name is outside it refers to an existing type, which is not generated, so a type generated from one schema can be used by another.

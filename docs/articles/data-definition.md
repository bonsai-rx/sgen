# Data definition

`Bonsai.Sgen` addresses the problem of defining custom data types in Bonsai. This article introduces the problem through a simple example.

## Introduction

Consider a new record type `Person` with the following fields:

| Field name  | Type     | Description                      |
|-------------|----------|----------------------------------|
| Age         | int      | Number of full years since birth |
| FirstName   | string   | Given name                       |
| LastName    | string   | Family name                      |
| DateOfBirth | DateTime | When the person was born         |

Bonsai has no syntax to declare object types directly, so a new record type has to be defined indirectly. The sections below describe two existing approaches and their limitations, followed by a third approach based on JSON Schema.

## Anonymous types

The [`ExpressionTransform`](xref:Bonsai.Scripting.Expressions.ExpressionTransform) operator supports [Data Object Initializers](xref:Bonsai.Scripting.Expressions.ExpressionTransform#data-object-initializers), which combine several values into a new object:

:::workflow
![Person as DynamicClass](~/workflows/person-example-dynamic-class.bonsai)
:::

**ExpressionTransform:**
```
new(
  Item1 as Age,
  Item2 as FirstName,
  Item3 as LastName,
  Item4 as DateOfBirth
)
```

The expression creates a new anonymous record type in the context of the workflow. This approach has several limitations.

First, the type has no name, so nothing identifies it as a `Person` rather than any other concept. Without a name, the type also cannot be used where a named reference to a type is required, such as when creating [Subject Sources](https://bonsai-rx.org/docs/articles/subjects.html#source-subjects). Finally, every place that creates a new object needs its own script.

## Hand-written types

A more flexible alternative is to write the type as a C# class with [Scripting Extensions](https://bonsai-rx.org/docs/articles/scripting-extensions.html):

```csharp
public class Person
{
    public int Age;
    public string FirstName;
    public string LastName;
    public DateTime DateOfBirth;
}
```

Writing the type in C# gives access to the whole language, so the class can compose any C# types, nest other records, and even define its own operators and functions. However, even a simple type needs additional code before it can be created and manipulated directly in a workflow:

```csharp
using Bonsai;
using System;
using System.Reactive.Linq;

public class CreatePerson : Source<Person>
{
    public int Age { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime DateOfBirth { get; set; }

    public override IObservable<Person> Generate()
    {
        return Observable.Return(new Person
        {
            Age = Age,
            FirstName = FirstName,
            LastName = LastName,
            DateOfBirth = DateOfBirth
        });
    }
}
```

The `CreatePerson` source operator creates a new `Person` from the values of its properties. Because it is a regular operator, it appears in the editor toolbox and can be placed and configured in the workflow as usual.

This may be enough for an occasional type in a project, but it does not scale to other common requirements of domain-specific record types, such as type hierarchies, serialization, or polymorphism. Each of these requires additional boilerplate code on top of the type itself.

As a project grows, writing this boilerplate code quickly becomes cumbersome and error prone.

## JSON Schema

The third approach describes the record type in [JSON Schema](https://json-schema.org/), a standard data definition language. `Bonsai.Sgen` then generates a C# class modeling the record, together with the operators to create and manipulate it, so none of the boilerplate code has to be written by hand.

JSON Schema gives Bonsai a way to declare types without a syntax of its own, by building on an established standard. The schema can be written by hand or exported from a model in another language, such as [Python classes defined with Pydantic](pydantic-usage.md#model-definition). The same model can then read the configuration files a workflow loads and the records it saves, so an experiment and its analysis share one definition of the data. The cost is an extra step, since the schema lives in its own file and the code has to be generated again whenever the schema changes.

### How to use

[!INCLUDE [](example-person.md)]

### Saving and loading

`Bonsai.Sgen` automatically generates [serialization and deserialization operators](basic-usage.md#serialization-and-deserialization):

:::workflow
![(de)serialization](~/workflows/simple-serialization-example.bonsai)
:::

Data objects can then serve as configuration files loaded into a workflow, or as data records saved with an experiment. JSON suits data records, written to a single file or one record per line as [JSON Lines](https://jsonlines.org/), while YAML suits configuration files written by hand.
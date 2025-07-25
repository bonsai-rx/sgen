# Data Definition

`Bonsai.Sgen` addresses the problem of defining and implementing custom data types in the Bonsai programming language. Let's explore this problem with a simple example.

## Introduction

Suppose we want to create a new record-like object type that represents a `Person`:

| Field Name | Type     | Description                  |
|------------|----------|------------------------------|
| age        | int      | The age of a person          |
| first_name | string   | The first name of the person |
| last_name  | string   | The last name of the person  |
| dob        | datetime | Date of birth                |

Since there is currently no special syntax to declare object types directly in Bonsai, we need to leverage indirect approaches to define our new record type. We start by exploring the previously available options below, along with their limitations, and finally introduce a third, more powerful, alternative.

## Data Object Initializers

One powerful feature of [`ExpressionTransform`](xref:Bonsai.Scripting.Expressions.ExpressionTransform) operators is support for writing [Data Object Initializers](xref:Bonsai.Scripting.Expressions.ExpressionTransform#data-object-initializers):

:::workflow
![Person as DynamicClass](~/workflows/person-example-dynamic-class.bonsai)
:::

**ExpressionTransform:**
```
new(
  Item1 as Age,
  Item2 as FirstName,
  Item3 as LastName,
  Item4 as DOB
)
```

A data object initializer expression will create a new anonymous record type in the current workflow context, but unfortunately this comes with several limitations.

First, the type has no name, so we do not know whether it refers to the `Person` concept, or any other concept. Furthermore, having no name means it is not possible to create any objects requiring a named reference to a type, for exampling when creating [Subject Sources](https://bonsai-rx.org/docs/articles/subjects.html#source-subjects). Finally, this approach requires the use of scripting anywhere we need to create new objects.

## Custom Scripting Extension

A more powerful alternative is to leverage C# directly to define our type class, by using custom [Scripting Extensions](https://bonsai-rx.org/docs/articles/scripting-extensions.html):

```csharp
public class Person
{
    public int Age;
    public string FirstName;
    public string LastName;
    public DateTime DOB;
}
```

This approach is much more flexible, as it allows composing our record using arbitrary C# types. It also supports nesting and even defining custom operators and functions on the new type. However, even for simple types we will need to write additional code to allow this type to be directly created and manipulated inside a Bonsai workflow:

```csharp
using Bonsai;
using System;
using System.Reactive.Linq;

public class CreatePerson : Source<Person>
{
    public int Age { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime DOB { get; set; }

    public override IObservable<Person> Generate()
    {
        return Observable.Return(new Person
        {
            Age = Age,
            FirstName = FirstName,
            LastName = LastName,
            DOB = DOB
        });
    }
}
```

Essentially we are augmenting the class with a source operator that creates a new instance of the record type with the parameters specified in the class properties. Because the record class is now a regular Bonsai operator, it will show up in the editor toolbox and can be placed and configured in the workflow as usual.

While this might be enough to work around the need for the single odd type in our project, it doesn't scale well to model other common requirements for domain-specific record types, such as support for type hierarchies, serialization, or polymorphism, all of which would require additional boilerplate code on top of our simple type.

As projects increase in complexity, writing such boilerplate code can quickly become cumbersome and error prone.

## JSON Schema

`Bonsai.Sgen` provides a new, and much more flexible, solution to this problem by leveraging [JSON Schema](https://json-schema.org/) directly as a data definition language in Bonsai.

### How to Use

[!INCLUDE [](example-person.md)]

### Advantages

Although initially this form may seem less direct and more complicated than even the C# type definition, there are a number of advantages immediately falling out from using JSON Schema as our data definition language:

1. Custom Bonsai operator code can be automatically generated from the JSON Schema.
2. Data objects backed by JSON Schemas can be used to read and write JSON files with validation guarantees.
3. Both JSON files and JSON Schemas are interoperable with any other language.

With `Bonsai.Sgen` you can focus on the specification of the data structure itself, rather than on the details of boilerplate code. Furthermore, you don't even need to write the schema by hand directly in JSON, since you can use any language supporting JSON Schemas. For example, you can easily [write a full data model in Python](python-usage.md#data-model) and use those classes directly to generate a JSON Schema for Bonsai.

### Saving and Loading

`Bonsai.Sgen` automatically generates [serialization and deserialization operators](basic-usage.md#serialization-and-deserialization):

:::workflow
![(de)serialization](~/workflows/simple-serialization-example.bonsai)
:::

This means you immediately gain the ability to use data objects as configuration files you load into your workflow, or as data records that you save with your experiment. Because all data will be backed by a schema, all these records can be immediately accessed by Python or any other language, saving you even more time setting up data processing pipelines.
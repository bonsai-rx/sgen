---
uid: why-bonsai-sgen
---

## Why should I care?

`Bonsai.Sgen` attempts to solve the problem of writing boilerplate code to create represent data structures in Bonsai. Let's try to convince you by looking at a simple example.

Let's we have a simple record-like object that represents a ´Person´:


| Field Name | Type     | Description               |
|------------|----------|---------------------------|
| age        | int      | The age of a person       |
| first_name | string   | The first name of the person |
| last_name  | string   | The last name of the person  |
| dob        | datetime | Date of birth             |


If we want to represent this object in Bonsai, we have a few alternatives:

1. Using a `DynamicClass` object:

:::workflow
![Person as DynamicClass](~/workflows/person-example-dynamic-class.bonsai)
:::

This approach is rather brittle as the representation of the record does not exist as a "first class citizen" and only at compile-time. This has a few implications one of which is the inability to create [Subject Sources](https://bonsai-rx.org/docs/articles/subjects.html#source-subjects) from the type.

2. Modeling the object as a C# class using [Scripting Extensions](https://bonsai-rx.org/docs/articles/scripting-extensions.html):

```Csharp
public class Person
{
    public int Age;
    public string FirstName;
    public string LastName;
    public DateTime DOB;
}
```

This approach is more robust than the previous one, but it requires writing additional, boilerplate code to allow the creation of the object in Bonsai:

```Csharp
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

As you can probably tell, neither of these approaches is ideal when it comes to scale large projects. This is where `Bonsai.Sgen` comes in.


## Automatic generation of Bonsai code using Bonsai.Sgen

We will expand this example later on, but for now, let's see how we can use `Bonsai.Sgen` to automatically generate the Bonsai code for the `Person` object.

First, we need to define the schema of the object in a JSON file:

```json
{
  "title": "Person",
  "type": "object",
  "properties": {
    "Age": {
      "type": "integer"
    },
    "FirstName": {
      "type": "string"
    },
    "LastName": {
      "type": "string"
    },
    "DOB": {
      "type": "string",
      "format": "date-time"
    }
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
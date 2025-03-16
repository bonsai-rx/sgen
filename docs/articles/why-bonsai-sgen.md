# Why Bonsai Sgen?

`Bonsai.Sgen` attempts to solve the problem of writing boilerplate code to model domain-specific data in Bonsai. Let's try to convince you by looking at a simple example.

Let's we have a simple record-like object that represents a ´Person´:


| Field Name | Type     | Description               |
|------------|----------|---------------------------|
| age        | int      | The age of a person       |
| first_name | string   | The first name of the person |
| last_name  | string   | The last name of the person  |
| dob        | datetime | Date of birth             |


If we want to represent this object in Bonsai, we have a few alternatives:

1. Using an [`ExpressionTransform`](xref:Bonsai.Scripting.Expressions.ExpressionTransform) with a [Data Object Initializer](https://bonsai-rx.org/docs/api/Bonsai.Scripting.Expressions.ExpressionTransform.html#data-object-initializers):

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
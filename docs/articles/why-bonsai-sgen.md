# Why use Bonsai.Sgen?

## Data schemas

`Bonsai.Sgen` addresses the challenge of writing boilerplate code to model domain-specific data in Bonsai. Let's explore this with a simple example.

Suppose we have a record-like object that represents a `Person`:

| Field Name | Type     | Description                  |
|------------|----------|------------------------------|
| age        | int      | The age of a person          |
| first_name | string   | The first name of the person |
| last_name  | string   | The last name of the person  |
| dob        | datetime | Date of birth                |

To represent this object in Bonsai, we have a few options:

1. Using an [`ExpressionTransform`](xref:Bonsai.Scripting.Expressions.ExpressionTransform) with a [Data Object Initializer](xref:Bonsai.Scripting.Expressions.ExpressionTransform#data-object-initializers):

    :::workflow
    ![Person as DynamicClass](~/workflows/person-example-dynamic-class.bonsai)
    :::

    This approach is brittle because the record representation exists only at compile-time and not as a "first-class citizen". For instance, this limitation prevents the creation of [Subject Sources](https://bonsai-rx.org/docs/articles/subjects.html#source-subjects) from the type.

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

    While more robust, this approach requires additional boilerplate code to enable object creation in Bonsai:

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

As you can see, neither approach scales well for large projects. This is where `Bonsai.Sgen` comes in.
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

public class Person
{
    public int Age;
    public string FirstName;
    public string LastName;
    public DateTime DOB;
}

# bonsai-sgen

`bonsai-sgen` provides pydantic base classes for data types shared with the [Bonsai visual reactive programming language](https://bonsai-rx.org). Models derived from these classes export JSON schemas from which [`Bonsai.Sgen`](https://www.nuget.org/packages/Bonsai.Sgen) generates matching C# types and serialization operators.

Each release of `bonsai-sgen` matches the release of `Bonsai.Sgen` with the same version number, and exports schemas following the conventions of that release.

## How to Use

1. Install the package:

    ```cmd
    pip install bonsai-sgen
    ```

2. Define the models in a module that declares the namespace of the generated types:

    ```python
    from typing import Annotated, Literal

    from pydantic import Field
    from typing_extensions import TypeAliasType

    from bonsai.sgen import SchemaModel

    SGEN_NAMESPACE = "PersonAndDiscriminatedPets"


    class PetBase(SchemaModel):
        age: int | None = None


    class Cat(PetBase):
        pet_type: Literal["cat"] = "cat"
        can_meow: bool = True


    class Dog(PetBase):
        pet_type: Literal["dog"] = "dog"
        can_bark: bool | None = True


    Pet = TypeAliasType("Pet", Annotated[Cat | Dog, Field(discriminator="pet_type")])


    class PersonAndPet(SchemaModel):
        owner: str
        pet: Pet | None = None
    ```

3. Write the schema of the models to a file:

    ```python
    from person_and_discriminated_pets import PersonAndPet

    from bonsai.sgen import write_schema

    write_schema("schemas", PersonAndPet)
    ```

4. Generate the C# types with the release of `Bonsai.Sgen` matching the installed version of `bonsai-sgen`:

    ```cmd
    dotnet bonsai.sgen schemas/PersonAndDiscriminatedPets.json -o Extensions --serializer json yaml
    ```

## Additional Documentation

For additional documentation and examples, refer to the [official Bonsai.Sgen documentation](https://bonsai-rx.org/sgen).

## Feedback & Contributing

`bonsai-sgen` is released as open source under the [MIT license](https://github.com/bonsai-rx/sgen/blob/main/LICENSE). Bug reports and contributions are welcome at [the GitHub repository](https://github.com/bonsai-rx/sgen).

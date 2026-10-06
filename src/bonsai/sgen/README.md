# bonsai-sgen

`bonsai-sgen` provides pydantic base classes for data types shared with the [Bonsai visual reactive programming language](https://bonsai-rx.org). Models derived from these classes export JSON schemas from which [`Bonsai.Sgen`](https://www.nuget.org/packages/Bonsai.Sgen) generates matching C# types and serialization operators.

Each release of `bonsai-sgen` matches the release of `Bonsai.Sgen` with the same version number, and exports schemas following the conventions of that release.

## How to Use

1. Install the package:

    ```cmd
    pip install bonsai-sgen
    ```

2. Declare the namespace of the generated types in each module, and derive models, enumerations and unions from the base classes:

    ```python
    from typing import Annotated, Literal

    from pydantic import Field

    from bonsai.sgen import SchemaEnum, SchemaModel, SchemaUnion

    SGEN_NAMESPACE = "Zoo"


    class Habitat(SchemaEnum):
        """Specifies the available habitats."""

        SAVANNA = 0
        FOREST = 1


    class Lion(SchemaModel):
        """A lion in the zoo."""

        kind: Literal["lion"] = "lion"
        name: str
        habitat: Habitat = Habitat.SAVANNA


    class Zebra(SchemaModel):
        """A zebra in the zoo."""

        kind: Literal["zebra"] = "zebra"
        name: str
        stripes: int = 0


    class Animal(SchemaUnion):
        """An animal in the zoo."""

        root: Annotated[Lion | Zebra, Field(discriminator="kind")]
    ```

3. Write the schema of each namespace to a file named after the namespace, here `schemas/Zoo.json`:

    ```python
    import zoo

    from bonsai.sgen import schema_types, write_schema

    write_schema("schemas", *schema_types(zoo))
    ```

4. Generate the C# types with the release of `Bonsai.Sgen` matching the installed version of `bonsai-sgen`:

    ```cmd
    dotnet bonsai.sgen schemas/Zoo.json -o Extensions --serializer json yaml
    ```

## Additional Documentation

For additional documentation and examples, refer to the [official Bonsai.Sgen documentation](https://bonsai-rx.org/sgen).

## Feedback & Contributing

`bonsai-sgen` is released as open source under the [MIT license](https://github.com/bonsai-rx/sgen/blob/main/LICENSE). Bug reports and contributions are welcome at [the GitHub repository](https://github.com/bonsai-rx/sgen).

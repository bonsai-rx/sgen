# Pydantic usage

JSON Schema files compatible with `Bonsai.Sgen` can be generated automatically from Python using the [Pydantic](https://docs.pydantic.dev/latest/) data validation library. This has several advantages:

- Python is more concise and more widely known than JSON Schema.
- Models are written as Python classes rather than as JSON Schema constraints.
- Pydantic models read and write JSON files directly as Python objects.

The `bonsai-sgen` package provides base classes for Pydantic models that follow the conventions of `Bonsai.Sgen`. Each release of `bonsai-sgen` matches the release of `Bonsai.Sgen` with the same version number.

## Setup instructions

[`uv`](https://docs.astral.sh/uv/) is the recommended tool for managing Python versions, environments and package dependencies. Create a self-contained virtual environment with `uv venv`, then install `bonsai-sgen` into it, which also installs Pydantic:

```powershell
uv pip install bonsai-sgen
```

## Model definition

Models derive from `SchemaModel` and enumerations from `SchemaEnum`, and a discriminated union is declared as a type alias. Each module declares the namespace of its generated types in its `SGEN_NAMESPACE` attribute, and each type is generated as the class of the same name in that namespace. For example, the following module defines the types of the [tagged unions](advanced-usage.md#tagged-unions) example:

[person_and_discriminated_pets.py](~/workflows/person_and_discriminated_pets.py)

[!code-python[](../workflows/person_and_discriminated_pets.py)]

The `Pet` union is discriminated by the constant tag that each member declares in its `pet_type` property. Since `Pet` is a type alias rather than a model, the `pet` property holds the `Cat` or `Dog` object itself. From Python 3.12, the `type` statement declares the same alias, as in `type Pet = Annotated[Cat | Dog, Field(discriminator="pet_type")]`. Since `Cat` and `Dog` both derive from `PetBase`, the generated `Pet` union derives from `PetBase` too, and `Cat` and `Dog` derive from `Pet`, so the common `age` property is available on every `Pet`.

`PetBase` lists `ABC` among its bases, so it is generated as an abstract class. Only a model that lists `ABC` itself is generated as abstract, which leaves `Cat` and `Dog` concrete.

A union declared as a type alias is generated as a local union in each namespace referring to it, over the same member types. To share a single union across namespaces instead, mark the alias with `SchemaAlias`, as in `Annotated[Cat | Dog, Field(discriminator="pet_type"), SchemaAlias()]`. A shared union can also be declared as a subclass of `SchemaUnion`, which makes it a Pydantic model whose `root` property holds the member object.

Every other type in the schema must derive from one of the base classes, so exporting a schema that refers to a plain Pydantic model or enumeration raises an error, unless the export allows [local types](#local-types). An alias of a scalar type, a list or a dictionary, nullable or not, as in `Label = TypeAliasType("Label", str)`, needs no base class.

> [!WARNING]
> Generated code cannot represent tuples, unions without a discriminator such as `int | str`, or anonymous discriminated unions declared in a field. Exporting a schema that refers to any of them raises an error.

## Schema export

The module works both as a library for manipulating model objects and as a script that writes the schema when run:

```powershell
uv run "person_and_discriminated_pets.py"
```

The `write_schema` function writes the schema to a file named after the module namespace, here `PersonAndDiscriminatedPets.json`. `Bonsai.Sgen` derives the namespace of the generated types from that file, so they share the namespace of the module:

```powershell
dotnet bonsai.sgen "PersonAndDiscriminatedPets.json" -o Extensions --serializer json
```

The [Python API reference](xref:bonsai.sgen) describes every class and function in the package, including how to refer to a type generated from another schema.

## Local types

A schema can also refer to types that do not derive from the base classes, such as plain Pydantic models, dataclasses, typed dictionaries and enumerations from another library. These types have no namespace of their own, so they would be exported as local types in the schema of each module that refers to them. In the following module, `Basket` is a local type:

```python
from dataclasses import dataclass

from bonsai.sgen import SchemaModel

SGEN_NAMESPACE = "DogAndBasket"


@dataclass
class Basket:
    size: int
    color: str = ""


class Dog(SchemaModel):
    name: str
    basket: Basket
```

Exporting such a schema would raise an error unless `allow_local_types` is set:

```python
write_schema("schemas", Dog, allow_local_types=True)
```

`Bonsai.Sgen` then generates `Basket` in the `DogAndBasket` namespace alongside `Dog`, as it does for a local union. This has some costs:

- Two namespaces that refer to the same local type each generate their own class. The two classes are distinct C# types, so an object of one cannot be assigned to a property of the other, although both read and write the same data.
- The name of the generated class matches the Python class name, unless the schema includes two Python classes with the same name from different modules. Pydantic then prefixes both with their module names to prevent a name clash, which means adding a type to the schema can change the name of a generated class.

Local types otherwise follow the conventions of the base classes. The schema of an integer enumeration specifies the names of its values, and field descriptions are kept. A parametrized generic model is also exported as a local type, and generated as a class whose name combines the model and its arguments, such as `BoxInt` for `Box[int]`.

The option affects only the type name check. A type that generated code cannot represent raises an error independently of `allow_local_types`. The models passed to `write_schema` or `export_schema` must still derive from the base classes, since the namespace of the schema is derived from them.

## Model serialization

The same model classes create and manipulate objects directly in Python, write them to a JSON file, and read a JSON file back into objects.

### Serialize to JSON

```python
from pathlib import Path
from person_and_discriminated_pets import PersonAndPet, Cat

data = PersonAndPet(owner="Avery", pet=Cat(age=2, can_meow=False))
Path("data.json").write_text(data.model_dump_json(indent=2))
```

### Deserialize from JSON

```python
from pathlib import Path
from person_and_discriminated_pets import PersonAndPet

json = Path("data.json").read_text()
data = PersonAndPet.model_validate_json(json)
```

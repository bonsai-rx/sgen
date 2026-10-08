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

Models derive from `SchemaModel`, enumerations from `SchemaEnum`, and discriminated unions from `SchemaUnion`. Each module declares the namespace of its generated types in its `SGEN_NAMESPACE` attribute, and each type is generated as the class of the same name in that namespace. For example, the following module defines the types of the [tagged unions](advanced-usage.md#tagged-unions) example:

[person_and_discriminated_pets.py](~/workflows/person_and_discriminated_pets.py)

```python
from pathlib import Path
from typing import Annotated, Literal

from pydantic import Field

from bonsai.sgen import SchemaModel, SchemaUnion, write_schema

SGEN_NAMESPACE = "PersonAndDiscriminatedPets"


class PetBase(SchemaModel):
    age: int | None = None


class Cat(PetBase):
    pet_type: Literal["cat"] = "cat"
    can_meow: bool = True


class Dog(PetBase):
    pet_type: Literal["dog"] = "dog"
    can_bark: bool | None = True


class Pet(SchemaUnion):
    root: Annotated[Cat | Dog, Field(discriminator="pet_type")]


class PersonAndPet(SchemaModel):
    owner: str
    pet: Pet | None = None


if __name__ == "__main__":
    write_schema(Path(__file__).parent, PersonAndPet)
```

The `Pet` union is discriminated by the constant tag that each member declares in its `pet_type` property. The `PetBase` class never appears in the schema, since Pydantic copies inherited properties into each derived model.

Every type in the schema must derive from one of the base classes, so exporting a schema that refers to a plain Pydantic model or enumeration raises an error.

## Schema export

The module works both as a library for manipulating model objects and as a script that writes the schema when run:

```powershell
uv run "person_and_discriminated_pets.py"
```

The `write_schema` function writes the schema to a file named after the module namespace, here `PersonAndDiscriminatedPets.json`. `Bonsai.Sgen` derives the namespace of the generated types from that file, so they share the namespace of the module:

```powershell
dotnet bonsai.sgen "PersonAndDiscriminatedPets.json" -o Extensions --serializer json
```

The [Python API reference](xref:bonsai.sgen) describes every base class and export function, including how to refer to a type generated from another schema.

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

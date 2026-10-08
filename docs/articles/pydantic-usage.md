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

The `Pet` union is discriminated by the constant tag that each member declares in its `pet_type` property. Since `Pet` is a type alias rather than a model, the `pet` property holds the `Cat` or `Dog` object itself. From Python 3.12, the `type` statement declares the same alias, as in `type Pet = Annotated[Cat | Dog, Field(discriminator="pet_type")]`. Pydantic copies inherited properties into each derived model, so the generated `Cat` and `Dog` classes do not derive from `PetBase`. The schema includes `PetBase` only if it is exported directly, as [`schema_types`](xref:bonsai.sgen.schema_types) does for every model in a module.

A union declared as a type alias is generated as a local union in each namespace referring to it, over the same member types. To share a single union across namespaces instead, mark the alias with `SchemaAlias`, as in `Annotated[Cat | Dog, Field(discriminator="pet_type"), SchemaAlias()]`. A shared union can also be declared as a subclass of `SchemaUnion`, which makes it a Pydantic model whose `root` property holds the member object.

Every other type in the schema must derive from one of the base classes, so exporting a schema that refers to a plain Pydantic model or enumeration raises an error.

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

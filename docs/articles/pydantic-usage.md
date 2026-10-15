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

Models derive from `SchemaModel` and enumerations from `SchemaEnum`, and a discriminated union is declared as a type alias. For example, the following module defines the types of the [tagged unions](advanced-usage.md#tagged-unions) example:

[person_and_discriminated_pets.py](~/workflows/person_and_discriminated_pets.py)

[!code-python[](../workflows/person_and_discriminated_pets.py)]

### Namespaces and type names

Each module declares the namespace of its generated types in the `SGEN_NAMESPACE` attribute, and each type is generated as a class of the same name in that namespace. The fully qualified name of the generated class is the type name of the model, such as `PersonAndDiscriminatedPets.Cat`.

A model describing a type generated elsewhere, for example in an external package, provides its fully qualified type name in the class definition, as in `class Dog(SchemaModel, sgen_typename="Home.Dog")`. Code generated for any other namespace then refers to the existing `Home.Dog` class instead of defining a new one. `SchemaEnum` and `SchemaUnion` accept the same keyword, and `SchemaAlias` accepts it as an argument. Type names must be unique.

Every other type in the schema must derive from one of the base classes. Exporting a schema that refers to a plain Pydantic model or enumeration raises an error, unless the export allows [local types](#local-types). An alias of a scalar type, a list or a dictionary, nullable or not, as in `Label = TypeAliasType("Label", str)`, needs no base class.

> [!WARNING]
> Generated code cannot represent tuples, unions without a discriminator such as `int | str`, or anonymous discriminated unions declared in a field. Exporting a schema that refers to any of them raises an error.

### Inheritance

A model deriving from another model is generated as a class deriving from the class of its base, so `Cat` and `Dog` share the `age` property declared once in `PetBase`. A model with more than one base model, or one that redefines a field or changes the configuration of its base, includes the fields of its bases within its own schema instead.

`PetBase` lists `ABC` among its bases, so it is generated as an abstract class. Only a model that lists `ABC` itself is generated as abstract, which leaves `Cat` and `Dog` concrete.

A base class with no type name, such as a base in a module that declares no namespace, is not generated as its own type. Instead, each model deriving from it includes the fields of the base within its own schema.

### Discriminated unions

The `Pet` union is discriminated by the constant tag that each member declares in its `pet_type` property. Since `Pet` is a type alias rather than a model, the `pet` property holds the `Cat` or `Dog` object itself. From Python 3.12, the `type` statement declares the same alias, as in `type Pet = Annotated[Cat | Dog, Field(discriminator="pet_type")]`.

Since `Cat` and `Dog` both derive from `PetBase`, the generated `Pet` union derives from `PetBase` too, and `Cat` and `Dog` derive from `Pet`. The common `age` property is therefore available on every `Pet`.

A union declared as a type alias is generated as a local union in each namespace referring to it, over the same member types. To share a single union across namespaces instead, mark the alias with `SchemaAlias`, as in `Annotated[Cat | Dog, Field(discriminator="pet_type"), SchemaAlias()]`. A shared union can also be declared as a subclass of `SchemaUnion`, which makes it a Pydantic model whose `root` property holds the member object.

### Enumerations

Enumerations derive from `SchemaEnum`. The schema of an integer enumeration specifies the names of its values, so the generated members keep the names declared in Python, converted to PascalCase when written in upper case. The names of a string enumeration are derived from its values.

> [!TIP]
> When combining `SchemaEnum` with an enumeration base that defines the behavior of its values, such as `IntEnum` or `StrEnum`, list `SchemaEnum` first, so that the other base keeps its behavior.

## Schema export

The `export` command writes one schema file for each namespace declared by the specified modules. For the example module, it writes `PersonAndDiscriminatedPets.json`:

```powershell
uv run bonsai-sgen export person_and_discriminated_pets -o schemas
```

Each module is specified by its fully qualified name, and a package exports all its submodules. `Bonsai.Sgen` derives the namespace of the generated types from the name of the schema file, so they keep the namespace declared in Python:

```powershell
dotnet bonsai.sgen "schemas/PersonAndDiscriminatedPets.json" -o Extensions --serializer json
```

Run `bonsai-sgen --help` for the options of each command. From Python, the [`export_schema`](xref:bonsai.sgen.export_schema) and [`write_schema`](xref:bonsai.sgen.write_schema) functions produce the same schemas.

## Code generation

The `generate` command exports each schema in memory and passes it to `Bonsai.Sgen` directly, so no schema file is written:

```powershell
uv run bonsai-sgen generate person_and_discriminated_pets -o Extensions --serializer json
```

The command runs `dotnet bonsai.sgen`, so `Bonsai.Sgen` must be installed as a local tool of the repository, from the same minor release as `bonsai-sgen`. To run another build of `Bonsai.Sgen`, such as a global install or a local build, set the `BONSAI_SGEN` environment variable to the command line that runs it.

## Local types

A schema can also refer to types that do not derive from the base classes, such as plain Pydantic models, dataclasses, typed dictionaries and enumerations from another library. These types have no namespace of their own, so they would be exported as local types in the schema of each namespace that refers to them. In the following module, `dog_and_basket.py`, `Basket` is a local type:

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

Exporting such a schema would raise an error unless local types are allowed:

```powershell
uv run bonsai-sgen export dog_and_basket --allow-local-types -o schemas
```

`Bonsai.Sgen` then generates `Basket` in the `DogAndBasket` namespace alongside `Dog`, as it does for a local union. This has some costs:

- Two namespaces that refer to the same local type each generate their own class. The two classes are distinct C# types, so an object of one cannot be assigned to a property of the other, although both read and write the same data.
- The name of the generated class matches the Python class name, unless the schema includes two Python classes with the same name from different modules. Pydantic then prefixes both with their module names to prevent a name clash, which means adding a type to the schema can change the name of a generated class.

Local types follow some conventions of the base classes. The schema of an integer enumeration specifies the names of its values, and field descriptions are kept. A parametrized generic model is also exported as a local type, and generated as a class whose name combines the model and its arguments, such as `BoxInt` for `Box[int]`.

The option affects only the type name check. A type that generated code cannot represent raises an error even when local types are allowed.

## Model serialization

The same model classes create and manipulate objects directly in Python, write them to a JSON file, and read a JSON file back into objects.

Schema models follow a camelCase convention for keys. The key of each field, in data and in the exported schema, is the camelCase form of its Python name, such as `canMeow` for `can_meow`. Schema models accept both forms of a key when reading data, so a file can use either the camelCase keys or the Python names. A local type such as a plain Pydantic model or dataclass follows the convention by applying [`CAMEL_CASE_CONFIG`](xref:bonsai.sgen.CAMEL_CASE_CONFIG).

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

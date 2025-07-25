# Python usage

JSON Schema files compatible with `Bonsai.Sgen` can be generated automatically from Python using the [Pydantic](https://docs.pydantic.dev/latest/) data validation library. This has several advantages:

- Python is a more concise and well-known language than JSON Schema.
- We can do object-oriented modelling directly, rather than having to tweak JSON Schema constraints.
- Pydantic models can be used to read and write JSON files directly into Python objects.

## Setup instructions

We recommend [`uv`](https://docs.astral.sh/uv/) for Python version, environment, and package dependency management. A self-contained virtual environment can be created using `uv venv`.

To install pydantic directly into the virtual environment:

```powershell
uv pip install pydantic
```

## Data model

A JSON Schema can be directly defined using [Pydantic models](https://docs.pydantic.dev/latest/concepts/models/) which fully specify all the constraints between the types in the schema. For example, the code below can be used to generate the entire schema for the [tagged unions](advanced-usage.md#tagged-unions) example:

[person_and_discriminated_pets.py](~/workflows/person_and_discriminated_pets.py).

```python
import json
from pathlib import Path
from typing import Annotated, Literal, Union
from pydantic import BaseModel, Field, RootModel


class PetBase(BaseModel):
    pet_type: str
    age: int


class Cat(PetBase):
    pet_type: Literal["cat"]
    can_meow: bool = Field(default=True)


class Dog(PetBase):
    pet_type: Literal["dog"]
    can_bark: bool = Field(default=True)


class Pet(RootModel):
    root: Annotated[Union[Cat, Dog], Field(discriminator="pet_type")]


class PersonAndPet(BaseModel):
    owner: str
    pet: Pet


if __name__ == "__main__":
    schema = PersonAndPet.model_json_schema()
    Path("person-and-discriminated-pets.json").write_text(json.dumps(schema, indent=2))
```

Note this example is designed as an executable Python module which can be used both as a library to manipulate the model objects, or to generate the schema itself by executing its main function:

```powershell
uv run "person_and_discriminated_pets.py"
```

This will generate the file `person-and-discriminated-pets.json` which can then be passed to `Bonsai.Sgen` to generate JSON serialization classes:

```powershell
dotnet bonsai.sgen "person-and-discriminated-pets.json" -o Extensions --serializer json
```
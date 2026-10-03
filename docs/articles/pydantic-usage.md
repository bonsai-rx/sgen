# Pydantic usage

JSON Schema files compatible with `Bonsai.Sgen` can be generated automatically from Python using the [Pydantic](https://docs.pydantic.dev/latest/) data validation library. This has several advantages:

- Python is more concise and more widely known than JSON Schema.
- Models are written as Python classes rather than as JSON Schema constraints.
- Pydantic models read and write JSON files directly as Python objects.

## Setup instructions

[`uv`](https://docs.astral.sh/uv/) is the recommended tool for managing Python versions, environments and package dependencies. Create a self-contained virtual environment with `uv venv`, then install Pydantic into it:

```powershell
uv pip install pydantic
```

## Model definition

[Pydantic models](https://docs.pydantic.dev/latest/concepts/models/) define a JSON Schema directly, including all the constraints between its types. For example, the following module generates the entire schema of the [tagged unions](advanced-usage.md#tagged-unions) example:

[person_and_discriminated_pets.py](~/workflows/person_and_discriminated_pets.py)

```python
import json
from pathlib import Path
from typing import Annotated, Literal, Optional, Union
from pydantic import BaseModel, Field, RootModel


class PetBase(BaseModel):
    pet_type: str
    age: Optional[int] = Field(default=None)


class Cat(PetBase):
    pet_type: Literal["cat"] = Field(default="cat")
    can_meow: bool = Field(default=True)


class Dog(PetBase):
    pet_type: Literal["dog"] = Field(default="dog")
    can_bark: Optional[bool] = Field(default=True)


class Pet(RootModel):
    root: Annotated[Union[Cat, Dog], Field(discriminator="pet_type")]


class PersonAndPet(BaseModel):
    owner: str
    pet: Optional[Pet] = Field(default=None)


if __name__ == "__main__":
    schema = PersonAndPet.model_json_schema()
    Path("person-and-discriminated-pets.json").write_text(json.dumps(schema, indent=2))
```

The module works both as a library for manipulating model objects and as a script that writes the schema when run:

```powershell
uv run "person_and_discriminated_pets.py"
```

Running it writes the file `person-and-discriminated-pets.json`, from which `Bonsai.Sgen` then generates JSON serialization classes:

```powershell
dotnet bonsai.sgen "person-and-discriminated-pets.json" -o Extensions --serializer json
```

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
import json
from pathlib import Path
from typing import Annotated, Literal, Union
from pydantic import BaseModel, Field, RootModel


class PetBase(BaseModel):
    pet_type: str
    age: int


class Cat(PetBase):
    pet_type: Literal["cat"] = "cat"
    can_meow: bool = Field(default=True)


class Dog(PetBase):
    pet_type: Literal["dog"] = "dog"
    can_bark: bool = Field(default=True)


class Pet(RootModel):
    root: Annotated[Union[Cat, Dog], Field(discriminator="pet_type")]


class PersonAndPet(BaseModel):
    owner: str
    pet: Pet


if __name__ == "__main__":
    schema = PersonAndPet.model_json_schema()
    Path("person-and-discriminated-pets.json").write_text(json.dumps(schema, indent=2))

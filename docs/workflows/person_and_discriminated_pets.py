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

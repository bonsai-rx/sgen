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

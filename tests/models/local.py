"""Types referring to a union generated locally in their namespace."""

from typing import Annotated, Literal

from pydantic import Field
from typing_extensions import TypeAliasType

from bonsai.sgen import SchemaModel

SGEN_NAMESPACE = "TestHelper.Local"


class Rabbit(SchemaModel):
    """A rabbit."""

    kind: Literal["rabbit"] = "rabbit"
    name: str


class Mole(SchemaModel):
    """A mole."""

    kind: Literal["mole"] = "mole"
    name: str


Digger = TypeAliasType("Digger", Annotated[Rabbit | Mole, Field(discriminator="kind")])
"""A discriminated union alias without the alias marker."""


class Burrow(SchemaModel):
    """A model referring to a local union."""

    digger: Digger

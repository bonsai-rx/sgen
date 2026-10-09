"""Types referring to a union generated locally in their namespace."""

from abc import ABC
from typing import Annotated, Literal

from pydantic import Field
from typing_extensions import TypeAliasType

from bonsai.sgen import SchemaModel

SGEN_NAMESPACE = "TestHelper.Local"


class Burrower(SchemaModel, ABC):
    """An abstract base type shared by every member of a union."""

    name: str
    depth: int = 0


class Rabbit(Burrower):
    """A rabbit."""

    kind: Literal["rabbit"] = "rabbit"


class Mole(Burrower):
    """A mole."""

    kind: Literal["mole"] = "mole"


Digger = TypeAliasType("Digger", Annotated[Rabbit | Mole, Field(discriminator="kind")])
"""A discriminated union alias without the alias marker."""


class Burrow(SchemaModel):
    """A model referring to a local union."""

    digger: Digger

"""A module that declares no namespace."""

from typing import Annotated

from pydantic import ConfigDict
from typing_extensions import TypeAliasType

from bonsai.sgen import DiscriminatedUnion, SchemaAlias, SchemaModel, SchemaUnion


class Mustelid(SchemaModel):
    """A base with no type name that only sets the configuration of its subclasses."""

    model_config = ConfigDict(validate_assignment=True)


class Ferret(SchemaModel):
    """A model with no type name."""

    name: str


class Mink(SchemaModel):
    """Another model with no type name."""

    name: str


class Mustelids(SchemaUnion):
    """A union with no type name."""

    root: Annotated[Ferret | Mink, DiscriminatedUnion("species")]


Burrowers = TypeAliasType(
    "Burrowers", Annotated[Ferret | Mink, DiscriminatedUnion("species"), SchemaAlias()]
)
"""A union alias with no type name."""


class Stoat(SchemaModel, sgen_typename="TestHelper.Base.Stoat"):
    """A model bound to a type defined elsewhere."""

    name: str


class Weasel(Stoat):
    """A model deriving from a type defined elsewhere, without a name of its own."""

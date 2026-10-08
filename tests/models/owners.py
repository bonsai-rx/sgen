"""Types referring to unions from another namespace."""

from typing import Annotated

from pydantic import Field

from bonsai.sgen import SchemaModel, SchemaUnion

from . import base, derived

SGEN_NAMESPACE = "TestHelper.Owners"


class Household(SchemaModel):
    """A model referring to a union defined in another namespace."""

    animal: derived.Animal


class Resident(SchemaUnion):
    """A union redefined over the members of another namespace."""

    root: Annotated[derived.Cat | base.Dog, Field(discriminator="kind")]


class Shelter(SchemaModel):
    """A model referring to a union defined in the same namespace."""

    resident: Resident

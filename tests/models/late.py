"""A module that declares its namespace after defining its types."""

from typing import Annotated, Literal

from pydantic import Field

from bonsai.sgen import SchemaModel, SchemaUnion


class Otter(SchemaModel):
    """An otter."""

    kind: Literal["otter"] = "otter"


class Beaver(SchemaModel):
    """A beaver."""

    kind: Literal["beaver"] = "beaver"


class Swimmer(SchemaUnion):
    """A swimmer."""

    root: Annotated[Otter | Beaver, Field(discriminator="kind")]


SGEN_NAMESPACE = "TestHelper.Late"

"""A module that declares no namespace."""

from bonsai.sgen import SchemaModel


class Ferret(SchemaModel):
    """A model with no type name."""

    name: str


class Stoat(SchemaModel, sgen_typename="TestHelper.Base.Stoat"):
    """A model bound to a type defined elsewhere."""

    name: str


class Weasel(Stoat):
    """A model deriving from a type defined elsewhere, without a name of its own."""

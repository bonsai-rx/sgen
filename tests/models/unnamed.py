"""A module that declares no namespace."""

from bonsai.sgen import SchemaModel


class Ferret(SchemaModel):
    """A ferret, with no type name."""

    name: str


class Stoat(SchemaModel, sgen_typename="TestHelper.Base.Stoat"):
    """A stoat, bound to a type defined elsewhere."""

    name: str


class Weasel(Stoat):
    """A weasel, deriving from a type defined elsewhere without a name of its own."""

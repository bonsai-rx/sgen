"""Types generated in their own namespace and referenced by other namespaces."""

from typing import Literal

from bonsai.sgen import SchemaModel

SGEN_NAMESPACE = "TestHelper.Base"


class Dog(SchemaModel):
    """A dog, generated outside the namespace of its unions."""

    kind: Literal["dog"] = "dog"
    name: str
    can_bark: bool = True

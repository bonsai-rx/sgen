"""Types without type names, exported as local types."""

from dataclasses import dataclass
from enum import IntEnum, StrEnum
from typing import Generic, TypeVar

from pydantic import BaseModel
from typing_extensions import TypedDict

from bonsai.sgen import SchemaModel

SGEN_NAMESPACE = "TestHelper.Plain"

T = TypeVar("T")


class Collar(BaseModel):
    """A plain model."""

    size: int
    color: str = ""


@dataclass
class Basket:
    """A dataclass."""

    width: float
    lined: bool = False


class Bowl(TypedDict):
    """A typed dictionary."""

    food: str
    grams: int


class Coat(IntEnum):
    """A plain integer enumeration."""

    SHORT = 1
    LONG_HAIR = 2


class Temper(StrEnum):
    """A plain string enumeration."""

    CALM = "calm"
    VERY_PLAYFUL = "very playful"


class Crate(BaseModel, Generic[T]):
    """A plain generic model."""

    content: T


class Box(SchemaModel, Generic[T]):
    """A generic schema model."""

    content: T


class Puppy(SchemaModel):
    """A model referring to local types."""

    name: str
    collar: Collar
    basket: Basket | None = None
    bowls: list[Bowl] = []
    coat: Coat | None = None
    temper: Temper | None = None
    crate: Crate[int] | None = None
    box: Box[Collar] | None = None

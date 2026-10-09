"""Tests that data written by pydantic round-trips through generated C# types."""

import json
from datetime import UTC, datetime, timedelta
from typing import Any

import pytest
import yaml
from conftest import Runner
from models import base, derived, local, owners
from pydantic import BaseModel

from bonsai.sgen import get_typename

CAT = derived.Cat(name="Tom", lives=3)
DOG = base.Dog(name="Rex", can_bark=False)

CASES: dict[str, BaseModel] = {
    "scalars": derived.Owner(
        name="Avery", age=41, weight=72.5, nicknames=["Av", "Ave"], animal=derived.Animal(CAT)
    ),
    "nulls": derived.Owner(name="Avery", age=None, animal=derived.Animal(CAT)),
    "integer-enum": derived.Palette(
        color=derived.Color.DARK_BLUE, favorite=derived.Color.LightGreen
    ),
    "null-enum": derived.Palette(favorite=None),
    "string-enum": derived.Palette(mood=derived.Mood.VERY_SAD),
    "dictionary": derived.Owner(name="Avery", friends={"tom": CAT}, animal=derived.Animal(CAT)),
    "local-member": derived.Owner(name="Avery", animal=derived.Animal(CAT)),
    "external-member": derived.Owner(name="Avery", animal=derived.Animal(DOG)),
    "shared-member": derived.Owner(
        name="Avery",
        animal=derived.Animal(CAT),
        pets=[derived.Pet(CAT), derived.Pet(derived.Hamster(name="Hammy"))],
    ),
    "union-tags": derived.Owner(
        name="Avery",
        animal=derived.Animal(CAT),
        creature=derived.Creature(derived.Parrot(name="Polly", words=12)),
    ),
    "datetime": derived.Visit(start=datetime(2026, 10, 4, 12, 30, tzinfo=UTC)),
    "duration": derived.Visit(
        start=datetime(2026, 10, 4, 12, 30, tzinfo=UTC), duration=timedelta(seconds=90)
    ),
    "alias-union": derived.Home(
        companion=CAT,
        companions=[derived.Hamster(name="Hammy")],
        flock=derived.Parrot(name="Polly", words=12),
    ),
    "local-union": local.Burrow(digger=local.Mole(name="Digger", depth=2)),
    "union-from-other-namespace": owners.Household(animal=derived.Animal(DOG)),
    "redefined-union": owners.Shelter(resident=owners.Resident(DOG)),
}
"""Values covering each schema pattern, one pattern per value so failures stay separate."""

YAML_ENUM_FAILURE = "YAML writes enum member names on .NET Framework, #115"
"""Reason for the failure of enumerations in YAML on .NET Framework."""

YAML_UNION_FAILURE = "YAML cannot read or write a union defined in another package"
"""Reason for the failure of a union referred to from another namespace in YAML."""

KNOWN_FAILURES: dict[tuple[str, str | None, str | None, str | None], str] = {
    ("integer-enum", "Yaml", "net472", "test_roundtrip_preserves_value"): YAML_ENUM_FAILURE,
    ("null-enum", "Yaml", "net472", "test_roundtrip_preserves_value"): YAML_ENUM_FAILURE,
    ("string-enum", "Yaml", "net472", None): YAML_ENUM_FAILURE,
    ("datetime", "Yaml", None, None): "YAML reads date-time only in the round-trip format of .NET",
    ("duration", None, None, None): "durations are not written in ISO 8601, #85",
    ("union-from-other-namespace", "Yaml", None, None): YAML_UNION_FAILURE,
}
"""Reasons for known failures by case, serializer, framework and test, where None matches any."""


def _dump(value: BaseModel, serializer: str) -> str:
    if serializer == "Json":
        return value.model_dump_json()
    return yaml.safe_dump(value.model_dump(mode="json"), sort_keys=False)


def _load(text: str, serializer: str) -> Any:
    return json.loads(text) if serializer == "Json" else yaml.safe_load(text)


def _member_paths(document: Any, path: str = "") -> set[str]:
    if isinstance(document, dict):
        return {f"{path}.{key}" for key in document} | {
            item
            for key, value in document.items()
            for item in _member_paths(value, f"{path}.{key}")
        }
    if isinstance(document, list):
        return {item for value in document for item in _member_paths(value, f"{path}[]")}
    return set()


@pytest.fixture(params=["Json", "Yaml"])
def serializer(request: pytest.FixtureRequest) -> str:
    """The serializer generated for the types."""
    return request.param


@pytest.fixture(params=list(CASES))
def case(request: pytest.FixtureRequest, serializer: str, framework: str) -> str:
    """The name of a value in `CASES`, marked if its round trip is known to fail."""
    test = request.node.originalname
    for (name, known_serializer, known_framework, known_test), reason in KNOWN_FAILURES.items():
        if (
            name == request.param
            and known_serializer in (None, serializer)
            and known_framework in (None, framework)
            and known_test in (None, test)
        ):
            request.applymarker(pytest.mark.xfail(reason=reason, strict=True))
    return request.param


def test_roundtrip_preserves_value(runner: Runner, framework: str, serializer: str, case: str):
    """Data written by pydantic and rewritten by the generated code validates to the same value."""
    value = CASES[case]
    text = runner.roundtrip(
        framework, serializer, get_typename(type(value)), _dump(value, serializer)
    )
    assert type(value).model_validate(_load(text, serializer)) == value


def test_roundtrip_adds_no_members(runner: Runner, framework: str, serializer: str, case: str):
    """The generated code writes no member that pydantic does not write, so extra="forbid" holds."""
    value = CASES[case]
    text = runner.roundtrip(
        framework, serializer, get_typename(type(value)), _dump(value, serializer)
    )
    expected = _member_paths(_load(_dump(value, serializer), serializer))
    assert _member_paths(_load(text, serializer)) - expected == set()

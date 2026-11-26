"""Tests that data written by pydantic round-trips through generated C# types."""

import json
import warnings
from datetime import UTC, datetime, timedelta
from typing import Any

import pytest
import yaml
from conftest import Runner
from jsonschema import Draft202012Validator
from models import base, derived, local, owners, plain
from pydantic import BaseModel

from bonsai.sgen import TYPENAME_KEY, SgenWarning, export_schema, get_typename

CAT = derived.Cat(name="Tom", lives=3)
DOG = base.Dog(name="Rex", can_bark=False)

CASES: dict[str, BaseModel] = {
    "scalars": derived.Owner(
        name="Avery", age=41, weight=72.5, nicknames=["Av", "Ave"], animal=derived.Animal(CAT)
    ),
    "nulls": derived.Owner(name="Avery", age=None, animal=derived.Animal(CAT)),
    "scalar-like-strings": derived.Owner(
        name="1", nicknames=["true", "null", "1.5", "~"], animal=derived.Animal(CAT)
    ),
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
    "member-subclass-declared-tag": derived.Nursery(kitten=derived.Kitten(name="Tom")),
    "member-subclass-assigned-tag": derived.Nursery(parakeet=derived.Parakeet(name="Polly")),
    "member-derived-from-member": derived.Aviary(
        songbird=derived.Canary(name="Tweety", song="trill")
    ),
    "single-member-union": derived.Roost(
        nightbird=derived.Nightbird(derived.Owl(name="Hedwig")),
        flightless=derived.Kiwi(name="Kiri"),
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
    "scalar-alias": derived.Tag(label="indoor", nickname="Tom"),
    "collection-alias": derived.Tag(label="indoor", aliases=["Tom", "Kit"], counts={"meals": 2}),
    "local-union": local.Burrow(digger=local.Mole(name="Digger", depth=2)),
    "union-from-other-namespace": owners.Household(animal=derived.Animal(DOG)),
    "redefined-union": owners.Shelter(resident=owners.Resident(DOG)),
    "local-model": plain.Puppy(name="Rex", collar=plain.Collar(size=3, color="red")),
    "local-dataclass": plain.Puppy(
        name="Rex", collar=plain.Collar(size=3), basket=plain.Basket(width=1.5)
    ),
    "local-typed-dict": plain.Puppy(
        name="Rex", collar=plain.Collar(size=3), bowls=[{"food": "kibble", "grams": 200}]
    ),
    "local-integer-enum": plain.Puppy(
        name="Rex", collar=plain.Collar(size=3), coat=plain.Coat.LONG_HAIR
    ),
    "local-string-enum": plain.Puppy(
        name="Rex", collar=plain.Collar(size=3), temper=plain.Temper.VERY_PLAYFUL
    ),
    "local-generic": plain.Puppy(
        name="Rex", collar=plain.Collar(size=3), crate=plain.Crate[int](content=2)
    ),
    "local-generic-schema-model": plain.Puppy(
        name="Rex",
        collar=plain.Collar(size=3),
        box=plain.Box[plain.Collar](content=plain.Collar(size=1)),
    ),
}
"""Values covering each schema pattern, one pattern per value so failures stay separate."""

YAML_ENUM_FAILURE = "YAML writes enum member names on .NET Framework, #115"
"""Reason for the failure of enumerations in YAML on .NET Framework."""

YAML_UNION_FAILURE = "YAML cannot read or write a union defined in another package"
"""Reason for the failure of a union referred to from another namespace in YAML."""

ROUNDTRIP_KNOWN_FAILURES: dict[tuple[str, str | None, str | None, str | None], str] = {
    ("integer-enum", "Yaml", "net472", "test_roundtrip_preserves_value"): YAML_ENUM_FAILURE,
    ("null-enum", "Yaml", "net472", "test_roundtrip_preserves_value"): YAML_ENUM_FAILURE,
    ("string-enum", "Yaml", "net472", None): YAML_ENUM_FAILURE,
    ("local-integer-enum", "Yaml", "net472", "test_roundtrip_preserves_value"): YAML_ENUM_FAILURE,
    ("local-string-enum", "Yaml", "net472", None): YAML_ENUM_FAILURE,
    ("datetime", "Yaml", None, None): "YAML reads date-time only in the round-trip format of .NET",
    ("duration", None, None, None): "durations are not written in ISO 8601, #85",
    ("union-from-other-namespace", "Yaml", None, None): YAML_UNION_FAILURE,
    ("member-subclass-assigned-tag", None, None, "test_roundtrip_adds_no_members"): (
        "generated code writes the union tag on a property typed as a subclass of a member"
    ),
}
"""Reasons for known failures by case, serializer, framework and test, where None matches any."""

SCHEMA_KNOWN_FAILURES: dict[str, str] = {}
"""Reasons for known failures of documents under their exported schema, by case."""


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
def roundtrip_case(request: pytest.FixtureRequest, serializer: str, framework: str) -> str:
    """The name of a value in `CASES`, marked if its round trip is known to fail."""
    test = request.node.originalname
    for key, reason in ROUNDTRIP_KNOWN_FAILURES.items():
        name, known_serializer, known_framework, known_test = key
        if (
            name == request.param
            and known_serializer in (None, serializer)
            and known_framework in (None, framework)
            and known_test in (None, test)
        ):
            request.applymarker(pytest.mark.xfail(reason=reason, strict=True))
    return request.param


@pytest.fixture(params=list(CASES))
def schema_case(request: pytest.FixtureRequest) -> str:
    """The name of a value in `CASES`, marked if its document is known to fail its schema."""
    reason = SCHEMA_KNOWN_FAILURES.get(request.param)
    if reason is not None:
        request.applymarker(pytest.mark.xfail(reason=reason, strict=True))
    return request.param


def test_roundtrip_preserves_value(
    runner: Runner, framework: str, serializer: str, roundtrip_case: str
):
    """Data written by pydantic and rewritten by the generated code validates to the same value."""
    value = CASES[roundtrip_case]
    text = runner.roundtrip(
        framework, serializer, get_typename(type(value)), _dump(value, serializer)
    )
    assert type(value).model_validate(_load(text, serializer)) == value


def test_roundtrip_adds_no_members(
    runner: Runner, framework: str, serializer: str, roundtrip_case: str
):
    """The generated code writes no member that pydantic does not write, so extra="forbid" holds."""
    value = CASES[roundtrip_case]
    text = runner.roundtrip(
        framework, serializer, get_typename(type(value)), _dump(value, serializer)
    )
    expected = _member_paths(_load(_dump(value, serializer), serializer))
    assert _member_paths(_load(text, serializer)) - expected == set()


def test_roundtrip_reads_omitted_defaults(runner: Runner, framework: str, serializer: str):
    """A document omitting fields defaulted to models is read with the defaults of pydantic."""
    text = runner.roundtrip(framework, serializer, get_typename(derived.Lodge), "{}")
    assert derived.Lodge.model_validate(_load(text, serializer)) == derived.Lodge()


def test_roundtrip_reads_omitted_collection_defaults(
    runner: Runner, framework: str, serializer: str
):
    """A document omitting fields defaulted to collections is read with the defaults of pydantic."""
    text = runner.roundtrip(framework, serializer, get_typename(derived.Kennel), "{}")
    assert derived.Kennel.model_validate(_load(text, serializer)) == derived.Kennel()


def test_roundtrip_replaces_collection_defaults(runner: Runner, framework: str, serializer: str):
    """A document setting fields defaulted to collections replaces the defaults."""
    document = '{"sizes": [3], "names": [], "weights": {"max": 1.0}, "beds": []}'
    text = runner.roundtrip(framework, serializer, get_typename(derived.Kennel), document)
    expected = derived.Kennel.model_validate_json(document)
    assert derived.Kennel.model_validate(_load(text, serializer)) == expected


def test_roundtrip_reads_partial_defaults(runner: Runner, framework: str, serializer: str):
    """A document setting some fields of a model with a default reads the rest from its type."""
    document = '{"spareBed": {"isHeated": true}}'
    text = runner.roundtrip(framework, serializer, get_typename(derived.Lodge), document)
    expected = derived.Lodge.model_validate_json(document)
    assert derived.Lodge.model_validate(_load(text, serializer)) == expected


def _schema(model: type[BaseModel]) -> dict[str, Any]:
    with warnings.catch_warnings():
        warnings.simplefilter("ignore", SgenWarning)
        schema = export_schema(model, allow_local_types=True)
    typename = get_typename(model)
    key = next(k for k, v in schema["$defs"].items() if v.get(TYPENAME_KEY) == typename)
    return {"$schema": Draft202012Validator.META_SCHEMA["$id"], **schema, "$ref": f"#/$defs/{key}"}


def test_schema_accepts_case(serializer: str, schema_case: str):
    """A document written by pydantic validates."""
    value = CASES[schema_case]
    validator = Draft202012Validator(_schema(type(value)))
    document = _load(_dump(value, serializer), serializer)
    assert [error.message for error in validator.iter_errors(document)] == []

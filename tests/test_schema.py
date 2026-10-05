"""Tests for schemas exported from the base classes."""

import json
import warnings
from enum import Enum
from typing import Annotated, Generic, Literal, TypeVar

import pytest
from models import base, derived, late, owners, unnamed
from pydantic import BaseModel, ConfigDict, Field, ValidationError

from bonsai.sgen import (
    DiscriminatedUnion,
    SchemaEnum,
    SchemaModel,
    SchemaUnion,
    SgenWarning,
    export_schema,
    schema_types,
    write_schema,
)

SGEN_NAMESPACE = "TestHelper.Schema"

T = TypeVar("T")


def _definitions(*models: type[BaseModel]) -> dict:
    with warnings.catch_warnings():
        warnings.simplefilter("ignore", SgenWarning)
        return export_schema(*models)["$defs"]


def test_typename_from_module_namespace():
    """The type name of a model is derived from the namespace declared by its module."""
    assert _definitions(derived.Cat)["Cat"]["x-sgen-typename"] == "TestHelper.Derived.Cat"


def test_subclass_named_for_own_class():
    """A subclass has a type name of its own, and leaves the type name of its parent unchanged."""

    class Kitten(derived.Cat):
        """A kitten."""

    definitions = _definitions(Kitten, derived.Cat)
    assert definitions["Kitten"]["x-sgen-typename"] == "TestHelper.Schema.Kitten"
    assert definitions["Cat"]["x-sgen-typename"] == "TestHelper.Derived.Cat"


def test_inherited_schema_extra_does_not_rename_subclass():
    """A type name written by hand into the configuration of a parent is not inherited."""

    class Lion(SchemaModel):
        """A lion."""

        model_config = ConfigDict(json_schema_extra={"x-sgen-typename": "Savanna.Lion"})

    class Cub(Lion):
        """A cub."""

    assert _definitions(Cub)["Cub"]["x-sgen-typename"] == "TestHelper.Schema.Cub"


def test_namespace_declared_after_types():
    """A module can declare its namespace after its types, since names bind on export."""
    definitions = _definitions(late.Swimmer)
    assert {key: value["x-sgen-typename"] for key, value in definitions.items()} == {
        "Beaver": "TestHelper.Late.Beaver",
        "Otter": "TestHelper.Late.Otter",
        "Swimmer": "TestHelper.Late.Swimmer",
    }


def test_missing_namespace_raises_on_export():
    """A model whose module declares no namespace cannot be exported."""
    with pytest.raises(TypeError, match="SGEN_NAMESPACE"):
        export_schema(unnamed.Ferret)


def test_plain_model_raises_on_export():
    """A plain pydantic model has no type name, which would lead to duplicate types."""

    class Collar(BaseModel):
        """A collar."""

        size: int

    class Puppy(SchemaModel):
        """A puppy."""

        collar: Collar

    with pytest.raises(TypeError, match="Missing type names for Collar"):
        export_schema(Puppy)


def test_plain_enum_raises_on_export():
    """A plain enumeration has no type name, which would lead to duplicate types."""

    class Coat(Enum):
        """Specifies the available coats."""

        SHORT = 1
        LONG = 2

    class Puppy(SchemaModel):
        """A puppy."""

        coat: Coat

    with pytest.raises(TypeError, match="Missing type names for Coat"):
        export_schema(Puppy)


def test_explicit_typename_not_inherited():
    """A type defined elsewhere keeps its name, which a subclass does not inherit."""
    assert _definitions(unnamed.Stoat)["Stoat"]["x-sgen-typename"] == "TestHelper.Base.Stoat"
    with pytest.raises(TypeError, match="Weasel has no type name"):
        export_schema(unnamed.Weasel)


def test_generic_model_raises_on_export():
    """A parametrized generic model cannot be exported, but a concrete subclass can."""

    class Box(SchemaModel, Generic[T]):
        """A box."""

        content: T

    class IntBox(Box[int]):
        """A box of integers."""

    class Crate(SchemaModel):
        """A crate."""

        box: Box[int]

    with pytest.raises(TypeError, match=r"Box\[int\] is a generic model"):
        export_schema(Crate)
    assert _definitions(IntBox)["IntBox"]["properties"]["content"]["type"] == "integer"


def test_description_of_type_kept():
    """A field described exactly like its type keeps its description, which pydantic drops."""

    class Painting(SchemaModel):
        """A painting."""

        base: derived.Color = Field(description="Specifies the available colors.")
        accent: derived.Color = Field(description="The color used for highlights.")

    properties = _definitions(Painting)["Painting"]["properties"]
    assert properties["base"]["description"] == "Specifies the available colors."
    assert properties["accent"]["description"] == "The color used for highlights."


def test_union_from_other_namespace_warns():
    """A field referring to a union of another namespace warns, while a redefinition does not."""
    with pytest.warns(SgenWarning, match="Household.animal") as record:
        export_schema(owners.Household, owners.Shelter)
    assert len(record) == 1


def test_integer_enum_names_members():
    """An integer enumeration specifies the names of its values, converting only upper case."""
    assert _definitions(derived.Palette)["Color"]["x-enumNames"] == [
        "Red",
        "DarkBlue",
        "LightGreen",
    ]


def test_integer_enum_names_aliases():
    """Names stay aligned with aliased values, since the schema keeps a value for each alias."""

    class Size(SchemaEnum):
        """Specifies the available sizes."""

        SMALL = 1
        LITTLE = 1
        LARGE = 2

    class Shirt(SchemaModel):
        """A shirt."""

        size: Size

    definition = _definitions(Shirt)["Size"]
    assert list(zip(definition["x-enumNames"], definition["enum"], strict=True)) == [
        ("Small", 1),
        ("Little", 1),
        ("Large", 2),
    ]


def test_string_enum_names_no_members():
    """The names of a string enumeration are derived directly from its values."""
    definition = _definitions(derived.Palette)["Mood"]
    assert "x-enumNames" not in definition
    assert definition["x-sgen-typename"] == "TestHelper.Derived.Mood"


def test_union_definition_named_and_mapped():
    """A union is defined once under its type name, mapping each tag to a member."""
    definition = _definitions(derived.Owner)["Animal"]
    assert definition["x-sgen-typename"] == "TestHelper.Derived.Animal"
    assert definition["discriminator"] == {
        "propertyName": "kind",
        "mapping": {"cat": "#/$defs/Cat", "dog": "#/$defs/Dog"},
    }


def test_union_tags_mapped():
    """Tags assigned by a union are mapped like tags declared by its members."""
    definition = _definitions(derived.Owner)["Creature"]
    assert definition["discriminator"] == {
        "propertyName": "species",
        "mapping": {"Parrot": "#/$defs/Parrot", "Goldfish": "#/$defs/Goldfish"},
    }


def test_union_tags_written_and_read():
    """A union writes the tag of a member, and reads a member by its tag."""
    value = derived.Creature(derived.Goldfish(name="Bubbles"))
    data = value.model_dump()
    assert data == {"species": "Goldfish", "name": "Bubbles"}
    assert derived.Creature.model_validate(data) == value


def test_union_tags_qualified():
    """Qualified tags are the type names of the members."""

    class Fish(SchemaUnion):
        """A fish."""

        root: Annotated[derived.Goldfish | base.Dog, DiscriminatedUnion("species", qualified=True)]

    assert Fish(base.Dog(name="Rex")).model_dump()["species"] == "TestHelper.Base.Dog"


def test_union_rejects_member_of_same_name():
    """An instance of another class with the name of a member is not accepted as the member."""

    class Goldfish(SchemaModel):
        """A goldfish of another namespace."""

        name: str

    with pytest.raises(ValidationError):
        derived.Creature.model_validate(Goldfish(name="Bubbles"))


def test_union_shared_tag_raises():
    """Two members of a union cannot share a tag."""

    class Goldfish(SchemaModel):
        """A goldfish of another namespace."""

        name: str

    with pytest.raises(TypeError, match="share tag 'Goldfish'"):

        class School(SchemaUnion):
            """A school of fish."""

            root: Annotated[Goldfish | derived.Goldfish, DiscriminatedUnion("species")]


def test_union_without_discriminator_raises():
    """A union whose member types are not discriminated by a tag raises on definition."""
    with pytest.raises(TypeError, match="not discriminated"):

        class Mixed(SchemaUnion):
            """A mixture."""

            root: derived.Cat | derived.Hamster


def test_union_single_member_raises():
    """A union with a single member raises on definition."""
    with pytest.raises(TypeError, match="single member"):

        class Solo(SchemaUnion):
            """A single parrot."""

            root: Annotated[derived.Parrot, DiscriminatedUnion("species")]


def test_union_plain_member_raises():
    """A union member must be a schema model."""

    class Turtle(BaseModel):
        """A turtle."""

        kind: Literal["turtle"] = "turtle"

    with pytest.raises(TypeError, match="not a SchemaModel"):

        class Reptile(SchemaUnion):
            """A reptile."""

            root: Annotated[derived.Cat | Turtle, Field(discriminator="kind")]


def test_inline_union_raises():
    """A discriminated union declared inline in a field raises on definition."""
    with pytest.raises(TypeError, match="Zoo.animals declares a discriminated union inline"):

        class Zoo(SchemaModel):
            """A zoo."""

            animals: list[Annotated[derived.Cat | base.Dog, Field(discriminator="kind")]]


def test_schema_types_in_declaration_order():
    """The schema types of a module are those defined in it, in declaration order."""
    assert schema_types(owners) == [owners.Household, owners.Resident, owners.Shelter]


def test_write_schema_named_after_namespace(tmp_path):
    """A schema is written to a file named after its namespace, identically on each export."""
    first = write_schema(tmp_path, *schema_types(derived)).read_bytes()
    path = write_schema(tmp_path, *schema_types(derived))
    assert path.name == "TestHelper.Derived.json"
    assert path.read_bytes() == first
    assert set(json.loads(first)) == {"$defs"}


def test_write_schema_mixed_namespaces_raises(tmp_path):
    """Models from different namespaces cannot be written to one schema."""
    with pytest.raises(ValueError, match="2 namespaces"):
        write_schema(tmp_path, derived.Cat, base.Dog)

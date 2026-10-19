"""Tests for schemas exported from the base classes."""

import dataclasses
import json
import sys
import warnings
from enum import Enum, StrEnum, auto
from typing import Annotated, Any, Generic, Literal, TypeVar

import pytest
from models import base, derived, late, local, owners, plain, unnamed
from pydantic import (
    AfterValidator,
    AliasChoices,
    AliasGenerator,
    BaseModel,
    ConfigDict,
    Field,
    HttpUrl,
    RootModel,
    SerializeAsAny,
    TypeAdapter,
    ValidationError,
    create_model,
    with_config,
)
from pydantic.alias_generators import to_camel, to_pascal
from pydantic.json_schema import SkipJsonSchema
from typing_extensions import TypeAliasType

from bonsai.sgen import (
    CAMEL_CASE_CONFIG,
    DiscriminatedUnion,
    SchemaAlias,
    SchemaEnum,
    SchemaModel,
    SchemaType,
    SchemaUnion,
    SgenWarning,
    export_schema,
    get_typename,
    schema_types,
    write_schema,
)

SGEN_NAMESPACE = "TestHelper.Schema"

T = TypeVar("T")


class Turtle(BaseModel):
    """A plain pydantic model."""

    kind: Literal["turtle"] = "turtle"


Pack = TypeAliasType(
    "Pack",
    Annotated[
        derived.Cat | base.Dog,
        Field(discriminator="kind"),
        SchemaAlias(sgen_typename="Kennel.Pack"),
    ],
)
"""A union alias declaring its type name explicitly."""

Mixed = TypeAliasType("Mixed", Annotated[derived.Cat | derived.Parrot, SchemaAlias()])
"""A union alias whose members are not discriminated."""

Flight = TypeAliasType("Flight", Annotated[list[derived.Parrot], SchemaAlias()])
"""A list alias marked as a union."""

Reptile = TypeAliasType(
    "Reptile", Annotated[derived.Cat | Turtle, Field(discriminator="kind"), SchemaAlias()]
)
"""A union alias with a member that is not a schema model."""

Plain = TypeAliasType("Plain", derived.Cat | derived.Hamster)
"""A union alias not marked for generation."""

Brood = TypeAliasType(
    "Brood", Annotated[derived.Cat | derived.Hamster, Field(discriminator="kind")]
)
"""A discriminated union alias not marked for generation."""

Link = TypeAliasType("Link", HttpUrl)
"""An alias of a library type with a scalar schema."""

Speed = TypeAliasType("Speed", Literal["fast"])
"""An alias of a single literal value."""

Speeds = TypeAliasType("Speeds", Literal["fast", "slow"])
"""An alias of several literal values, generated as an enumeration."""

MaybeLabel = TypeAliasType("MaybeLabel", str | None)
"""An alias of a nullable scalar type."""

MaybeLink = TypeAliasType("MaybeLink", Link | None)
"""An alias of a nullable alias of a scalar type."""

Roster = TypeAliasType("Roster", list[str])
"""An alias of a list."""

Herd = TypeAliasType("Herd", list[derived.Cat])
"""An alias of a list of models."""

Scores = TypeAliasType("Scores", dict[str, int])
"""An alias of a dictionary."""

MaybeRoster = TypeAliasType("MaybeRoster", Roster | None)
"""An alias of a nullable alias of a list."""

Coordinates = TypeAliasType("Coordinates", tuple[int, int])
"""An alias of a tuple."""


class Count(RootModel[int]):
    """A plain root model of a scalar type."""


def _definitions(*models: SchemaType) -> dict:
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


@pytest.mark.parametrize(
    "schema_type", [unnamed.Ferret, unnamed.Mustelids, unnamed.Burrowers], ids=str
)
def test_unnamed_type_schema_omits_typename(schema_type: SchemaType):
    """A type with no type name has a schema without one outside an export."""
    schema = TypeAdapter(schema_type).json_schema()
    assert "x-sgen-typename" not in json.dumps(schema)


@pytest.mark.parametrize(
    "schema_type", [unnamed.Ferret, unnamed.Mustelids, unnamed.Burrowers], ids=str
)
def test_unnamed_type_reference_raises_only_on_export(schema_type: SchemaType):
    """A model referring to a type with no type name raises on export, but not before or after."""
    Keeper = create_model("Keeper", __base__=SchemaModel, animal=(schema_type, ...))
    Keeper.model_json_schema()
    with pytest.raises(TypeError, match="SGEN_NAMESPACE"):
        export_schema(Keeper)
    Keeper.model_json_schema()


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


def test_unmarked_union_alias_exports_as_local_union():
    """A discriminated union alias without the marker is exported as a local union."""

    class Nest(SchemaModel):
        """A nest."""

        brood: Brood

    definition = export_schema(Nest)["$defs"]["Brood"]
    assert "discriminator" in definition
    assert "x-sgen-typename" not in definition


def test_subclass_refers_to_base():
    """A model deriving from another schema model refers to it instead of repeating its fields."""
    definitions = _definitions(local.Burrow)
    assert definitions["Mole"]["allOf"] == [{"$ref": "#/$defs/Burrower"}]
    assert set(definitions["Mole"]["properties"]) == {"kind"}
    assert definitions["Burrower"]["x-sgen-typename"] == "TestHelper.Local.Burrower"


def test_subclass_repeats_unnamed_base_fields():
    """A model deriving from a base with no type name repeats the fields of the base."""

    class Polecat(unnamed.Ferret):
        """A model deriving from a base with no type name."""

        age: int

    definitions = _definitions(Polecat)
    assert "allOf" not in definitions["Polecat"]
    assert set(definitions["Polecat"]["properties"]) == {"name", "age"}
    assert "Ferret" not in definitions


def test_unnamed_configuration_base_not_exported():
    """A base with no type name that only sets configuration is left out of the schema."""

    class Otter(unnamed.Mustelid):
        """A model deriving from a configuration base."""

        name: str

    definitions = _definitions(Otter)
    assert "allOf" not in definitions["Otter"]
    assert set(definitions) == {"Otter"}


def test_abstract_base_marked_abstract():
    """A model listing ABC among its bases is abstract, while its subclasses are not."""
    definitions = _definitions(local.Burrow)
    assert definitions["Burrower"]["x-abstract"] is True
    assert "x-abstract" not in definitions["Mole"]


def test_subclass_of_derived_base_declares_only_own_fields():
    """A model deriving from a derived model declares only its own fields."""

    class Kit(local.Mole):
        """A model deriving from a model that derives from another."""

        whiskers: int = 0

    definition = _definitions(Kit)["Kit"]
    assert definition["allOf"] == [{"$ref": "#/$defs/Mole"}]
    assert set(definition["properties"]) == {"whiskers"}


def test_subclass_changing_base_field_default_repeats_fields():
    """A model changing the default of a field of its base repeats every field of the base."""

    class Kit(local.Burrower):
        """A model changing the default of a field of its base."""

        depth: int = 1

    definition = _definitions(Kit)["Kit"]
    assert "allOf" not in definition
    assert set(definition["properties"]) == {"name", "depth"}


def test_subclass_changing_base_field_keys_repeats_fields():
    """A model whose configuration changes the keys of its base fields repeats every field."""

    class Kit(local.Burrower):
        """A model changing the configuration of its base."""

        model_config = ConfigDict(alias_generator=str.upper)

    definition = _definitions(Kit)["Kit"]
    assert "allOf" not in definition
    assert set(definition["properties"]) == {"NAME", "DEPTH"}


@pytest.mark.parametrize(
    "config",
    [
        ConfigDict(frozen=True),
        ConfigDict(validate_assignment=True),
        ConfigDict(extra="forbid"),
        ConfigDict(title="Pup"),
    ],
    ids=["frozen", "validate_assignment", "extra", "title"],
)
def test_subclass_config_keeping_base_fields_refers_to_base(config: ConfigDict):
    """A model whose configuration leaves the schema of its base fields unchanged refers to it."""

    class Kit(local.Burrower):
        """A model changing the configuration of its base."""

        model_config = config

    definition = _definitions(Kit)["Kit"]
    assert definition["allOf"] == [{"$ref": "#/$defs/Burrower"}]
    assert definition.get("properties", {}) == {}


def test_subclass_redefining_base_field_schema_refers_to_base():
    """A model redefining a field of its base with the same schema refers to the base."""

    class Kit(local.Burrower):
        """A model adding a validator to a field of its base."""

        depth: Annotated[int, AfterValidator(abs)] = 0

    definition = _definitions(Kit)["Kit"]
    assert definition["allOf"] == [{"$ref": "#/$defs/Burrower"}]
    assert definition.get("properties", {}) == {}


def test_subclass_making_base_field_optional_repeats_fields():
    """A model making a required field of its base optional repeats every field of the base."""

    class Kit(local.Burrower):
        """A model with a default for a required field of its base."""

        name: str = Field(default_factory=str)

    definition = _definitions(Kit)["Kit"]
    assert "allOf" not in definition
    assert "name" not in definition.get("required", [])


def test_subclass_of_base_with_type_description_refers_to_base():
    """A field described like its type is compared with the base after restoring its description."""

    class Collar(SchemaModel):
        """A collar."""

        size: int

    class Pup(SchemaModel):
        """A model with a field described like its type."""

        collar: Collar = Field(description="A collar.")

    class Kit(Pup):
        """A model adding no fields."""

    assert Kit.model_json_schema()["allOf"] == [{"$ref": "#/$defs/Pup"}]


def test_subclass_refers_to_base_reusing_field_type():
    """A model deriving from a base with two fields of the same type refers to the base."""

    class Litter(SchemaModel):
        """A model with two fields of the same type."""

        first: derived.Cat
        second: derived.Cat

    class Kittens(Litter):
        """A model deriving from a base with two fields of the same type."""

        size: int

    definitions = _definitions(Kittens)
    assert Kittens.model_json_schema()["allOf"] == [{"$ref": "#/$defs/Litter"}]
    assert definitions["Kittens"]["allOf"] == [{"$ref": "#/$defs/Litter"}]
    assert set(definitions["Kittens"]["properties"]) == {"size"}
    assert set(definitions) == {"Cat", "Kittens", "Litter"}


def test_subclass_refers_to_recursive_base():
    """A model deriving from a base with a field of its own type refers to the base."""

    class Lineage(SchemaModel):
        """A model with a field of its own type."""

        parent: "Lineage | None" = None

    class Pedigree(Lineage):
        """A model deriving from a base with a field of its own type."""

        name: str

    definitions = _definitions(Pedigree)
    assert Pedigree.model_json_schema()["allOf"] == [{"$ref": "#/$defs/Lineage"}]
    assert definitions["Pedigree"]["allOf"] == [{"$ref": "#/$defs/Lineage"}]
    assert set(definitions["Pedigree"]["properties"]) == {"name"}
    assert set(definitions) == {"Lineage", "Pedigree"}


def test_subclass_narrowing_recursive_base_field_repeats_fields():
    """A model narrowing a field of its base to its own type repeats every field of the base."""

    class Lineage(SchemaModel):
        """A model with a field of its own type."""

        parent: "Lineage | None" = None

    class Pedigree(Lineage):
        """A model narrowing a field of its base to its own type."""

        parent: "Pedigree | None" = None  # pyright: ignore[reportIncompatibleVariableOverride]
        name: str

    definitions = _definitions(Pedigree)
    assert "allOf" not in Pedigree.model_json_schema()
    assert "allOf" not in definitions["Pedigree"]
    assert set(definitions["Pedigree"]["properties"]) == {"parent", "name"}
    assert set(definitions) == {"Pedigree"}


def test_subclass_refers_to_serialize_as_any_base():
    """A model deriving from a base with a field serialized as any subclass refers to the base."""

    class Carrier(SchemaModel):
        """A model with a field serialized as any subclass of its type."""

        pet: SerializeAsAny[derived.Cat] | None = None

    class Crate(Carrier):
        """A model deriving from a base with a field serialized as any subclass."""

        size: int

    definitions = _definitions(Crate)
    assert Crate.model_json_schema()["allOf"] == [{"$ref": "#/$defs/Carrier"}]
    assert definitions["Crate"]["allOf"] == [{"$ref": "#/$defs/Carrier"}]
    assert set(definitions["Crate"]["properties"]) == {"size"}
    assert set(definitions) == {"Carrier", "Cat", "Crate"}


def test_subclass_repeating_base_fields_omits_base():
    """A model repeating the fields of its base exports no definition of the base."""

    class Kit(local.Burrower):
        """A model changing the configuration of its base."""

        model_config = ConfigDict(alias_generator=str.upper)

    assert set(_definitions(Kit)) == {"Kit"}


def test_skipped_field_type_omitted_on_export():
    """A type referred to only by a field left out of the schema is left out of the export."""

    class Pond(SchemaModel):
        """A model with a field left out of the schema."""

        name: str
        turtle: SkipJsonSchema[Turtle]

    assert set(_definitions(Pond)) == {"Pond"}


def test_subclass_sharing_base_typename_raises_on_export():
    """A model with the same type name as its base would define the same type twice."""

    class Kitten(derived.Cat, sgen_typename=get_typename(derived.Cat)):
        """A model with the same type name as its base."""

    with pytest.raises(TypeError, match="Cat and Kitten share type name TestHelper.Derived.Cat"):
        export_schema(Kitten)


def test_subclass_sharing_indirect_base_typename_raises_on_export():
    """A model with the same type name as an indirect base would define the same type twice."""

    class Tabby(derived.Cat):
        """A model with a type name of its own."""

    class Kitten(Tabby, sgen_typename=get_typename(derived.Cat)):
        """A model with the same type name as the base of its base."""

    with pytest.raises(TypeError, match="Cat and Kitten share type name TestHelper.Derived.Cat"):
        export_schema(Kitten)


def test_models_sharing_name_in_namespace_raise_on_export():
    """Two models of the same name in one namespace would define the same type twice."""

    class Bowl(SchemaModel):
        """A model in the namespace of this module."""

        size: int

    Other = create_model("Bowl", __base__=SchemaModel, color=(str, ...))
    Home = create_model("Home", __base__=SchemaModel, bowl=(Bowl, ...), other=(Other, ...))
    with pytest.raises(TypeError, match="share type name TestHelper.Schema.Bowl"):
        export_schema(Home)


def test_enum_sharing_model_typename_raises_on_export():
    """An enumeration with the same type name as a model would define the same type twice."""

    class Coat(SchemaEnum, sgen_typename=get_typename(derived.Cat)):
        """Specifies the available coats."""

        SHORT = 1

    class Puppy(SchemaModel):
        """A model referring to both types."""

        cat: derived.Cat
        coat: Coat

    with pytest.raises(TypeError, match="Cat and Coat share type name TestHelper.Derived.Cat"):
        export_schema(Puppy)


def test_union_sharing_member_typename_raises_on_export():
    """A union with the same type name as one of its members would define the same type twice."""

    class Litter(SchemaUnion, sgen_typename=get_typename(derived.Cat)):
        """A union with the type name of a member."""

        root: Annotated[derived.Cat | derived.Hamster, Field(discriminator="kind")]

    with pytest.raises(TypeError, match="Cat and Litter share type name TestHelper.Derived.Cat"):
        export_schema(Litter)


def test_identical_models_of_external_type_defined_once():
    """Identical models describing one external type define it once."""

    class Bowl(SchemaModel, sgen_typename="Home.Bowl"):
        """A bowl defined elsewhere."""

        size: int

    Other = create_model(
        "Bowl",
        __base__=SchemaModel,
        __doc__=Bowl.__doc__,
        __cls_kwargs__={"sgen_typename": "Home.Bowl"},
        size=(int, ...),
    )
    Home = create_model("Home", __base__=SchemaModel, bowl=(Bowl, ...), other=(Other, ...))
    definitions = _definitions(Home)
    assert [d.get("x-sgen-typename") for d in definitions.values()].count("Home.Bowl") == 1


def test_dump_uses_schema_property_names():
    """A model with field aliases is serialized under the property names of its schema."""

    class Kit(local.Burrower):
        """A model with field aliases."""

        model_config = ConfigDict(alias_generator=str.upper)

    properties = _definitions(Kit)["Kit"]["properties"]
    data = json.loads(Kit.model_validate({"NAME": "Ruby"}).model_dump_json())
    assert set(data) == set(properties) == {"NAME", "DEPTH"}


def test_dump_uses_aliases_of_nested_plain_models():
    """A dump of a schema model writes the aliases of nested plain models, unless disabled."""

    class Collar(BaseModel):
        """A plain model with field aliases."""

        model_config = ConfigDict(alias_generator=to_camel)

        collar_size: int = 1

    class Dog(SchemaModel):
        """A model nesting plain models."""

        collar: Collar = Collar()
        spare_collars: list[Collar] = [Collar()]

    dog = Dog()
    expected = {"collar": {"collarSize": 1}, "spareCollars": [{"collarSize": 1}]}
    assert dog.model_dump() == expected
    assert json.loads(dog.model_dump_json()) == expected
    assert dog.model_dump(by_alias=False)["collar"] == {"collar_size": 1}


SPLIT_ALIASES = ConfigDict(
    alias_generator=AliasGenerator(validation_alias=to_pascal, serialization_alias=to_camel)
)


def test_split_field_aliases_raise_on_export():
    """A field read and written under different keys cannot be exported."""

    class Dog(SchemaModel):
        """A model with a field read and written under different keys."""

        toy_count: int = Field(0, validation_alias="toyTotal", serialization_alias="toys")

    with pytest.raises(TypeError, match="read under key 'toyTotal' is written under key 'toys'"):
        export_schema(Dog)


def test_split_alias_generator_raises_on_export():
    """A model whose alias generator derives different read and write keys cannot be exported."""

    class Dog(SchemaModel):
        """A model with a split alias generator."""

        model_config = SPLIT_ALIASES

        toy_count: int = 0

    with pytest.raises(TypeError, match="read under key 'ToyCount' is written under key"):
        export_schema(Dog)


def test_local_type_split_aliases_raise_on_export():
    """A local type read and written under different keys cannot be exported either."""

    @with_config(SPLIT_ALIASES)
    @dataclasses.dataclass
    class Basket:
        """A dataclass with a split alias generator."""

        basket_color: str = "red"

    class Dog(SchemaModel):
        """A model nesting the dataclass."""

        basket: Basket = dataclasses.field(default_factory=Basket)

    with pytest.raises(TypeError, match="read under key 'BasketColor'"):
        export_schema(Dog, allow_local_types=True)


def test_alias_choices_led_by_written_key_exported():
    """A field also reading other keys is exported when its first key is the written one."""

    class Dog(SchemaModel):
        """A model reading a field under several keys."""

        toy_count: int = Field(0, validation_alias=AliasChoices("toyCount", "toys"))

    assert list(_definitions(Dog)["Dog"]["properties"]) == ["toyCount"]


def test_alias_choices_led_by_other_key_raise_on_export():
    """A field whose first read key is not the written key cannot be exported."""

    class Dog(SchemaModel):
        """A model reading a field first under a key it does not write."""

        toy_count: int = Field(0, validation_alias=AliasChoices("toys", "toyCount"))

    with pytest.raises(TypeError, match="read under key 'toys' is written under key 'toyCount'"):
        export_schema(Dog)


def test_subclass_of_base_with_split_alias_refers_to_base():
    """A model deriving from a base with a split alias does not repeat the field of the base."""

    class Canine(SchemaModel):
        """A base with a field read and written under different keys."""

        toy_count: int = Field(0, validation_alias="toyTotal", serialization_alias="toys")

    class Dog(Canine):
        """A model deriving from the base."""

        can_bark: bool = True

    schema = Dog.model_json_schema()
    assert schema["allOf"] == [{"$ref": "#/$defs/Canine"}]
    assert list(schema["properties"]) == ["canBark"]


def test_union_dump_uses_aliases_of_nested_plain_models():
    """A dump of a schema union writes the aliases of plain models nested in its members."""

    class Collar(BaseModel):
        """A plain model with field aliases."""

        model_config = ConfigDict(alias_generator=to_camel)

        collar_size: int = 1

    class Cat(SchemaModel):
        """A union member nesting a plain model."""

        kind: Literal["cat"] = "cat"
        collar: Collar = Collar()

    class Animal(SchemaUnion):
        """A union whose members nest plain models."""

        root: Annotated[Cat | derived.Hamster, Field(discriminator="kind")]

    animal = Animal(Cat())
    expected = {"kind": "cat", "collar": {"collarSize": 1}}
    assert animal.model_dump() == expected
    assert json.loads(animal.model_dump_json()) == expected
    assert animal.model_dump(by_alias=False)["collar"] == {"collar_size": 1}


def test_camel_case_keys_written():
    """A schema model writes and exports camelCase keys, including in nested models."""
    owner = derived.Owner(name="Avery", animal=derived.Animal(base.Dog(name="Rex", can_bark=False)))
    properties = _definitions(base.Dog)["Dog"]["properties"]
    data = json.loads(owner.model_dump_json())
    assert set(properties) == set(data["animal"]) == {"kind", "name", "canBark"}
    assert data["animal"]["canBark"] is False


@pytest.mark.parametrize("key", ["canBark", "can_bark"])
def test_camel_and_snake_keys_read(key: str):
    """A schema model reads keys in either form, so files written before the convention load."""
    data = {"name": "Avery", "animal": {"kind": "dog", "name": "Rex", key: False}}
    animal = derived.Owner.model_validate(data).animal.root
    assert isinstance(animal, base.Dog)
    assert animal.can_bark is False


def test_camel_key_read_over_snake_key():
    """Data with both forms of one key reads the camelCase form."""
    dog = base.Dog.model_validate({"name": "Rex", "can_bark": True, "canBark": False})
    assert dog.can_bark is False


def test_constructor_accepts_field_names():
    """A schema model is constructed from the Python names of its fields."""
    assert base.Dog(name="Rex", can_bark=False).can_bark is False


def test_local_type_follows_convention_only_with_config():
    """A plain local type writes camelCase keys only when it applies the convention."""

    class Collar(BaseModel):
        """A plain model without configuration."""

        collar_size: int = 1

    @with_config(CAMEL_CASE_CONFIG)
    @dataclasses.dataclass
    class Basket:
        """A dataclass applying the convention."""

        basket_color: str = "red"

    class Dog(SchemaModel):
        """A model nesting local types."""

        collar: Collar = Collar()
        basket: Basket = dataclasses.field(default_factory=Basket)

    definitions = export_schema(Dog, allow_local_types=True)["$defs"]
    assert list(definitions["Collar"]["properties"]) == ["collar_size"]
    assert list(definitions["Basket"]["properties"]) == ["basketColor"]


def test_base_without_alias_generator_writes_field_names():
    """A project base clearing the alias generator writes and exports the Python names."""

    class SnakeCaseModel(SchemaModel):
        """A base opting out of the camelCase convention."""

        model_config = ConfigDict(alias_generator=None)

    class Cat(SnakeCaseModel):
        """A model deriving from the base."""

        can_meow: bool = True

    assert list(_definitions(Cat)["Cat"]["properties"]) == ["can_meow"]
    assert json.loads(Cat().model_dump_json()) == {"can_meow": True}


def test_field_titles_match_property_names():
    """The title of each field is the name of the generated property, unless set explicitly."""

    class Dog(SchemaModel):
        """A model with explicit aliases and an explicit title."""

        favorite_toy: str = "ball"
        toy_count: int = Field(0, alias="toy-total")
        paw_count: int = Field(0, alias="paw2d")
        owner_id: str = Field("", alias="_id")
        nickname: str = Field("", title="Pet name")

    class Cat(SchemaModel):
        """A model with field names of different forms."""

        x: float = 0
        paw_1: bool = False
        canMeow: bool = True

    definitions = _definitions(Dog, Cat)
    titles = {
        key: prop["title"]
        for name in ("Dog", "Cat")
        for key, prop in definitions[name]["properties"].items()
    }
    assert titles == {
        "favoriteToy": "FavoriteToy",
        "toy-total": "ToyTotal",
        "paw2d": "Paw2d",
        "_id": "Id",
        "nickname": "Pet name",
        "x": "X",
        "paw1": "Paw1",
        "canMeow": "CanMeow",
    }


def test_undiscriminated_alias_raises_on_export():
    """A union alias without a discriminator raises on export, since its members are lost."""

    class Litter(SchemaModel):
        """A litter."""

        plain: Plain

    with pytest.raises(TypeError, match="Plain is a union without a discriminator"):
        export_schema(Litter)


def test_undiscriminated_union_raises_on_export():
    """A union of models without a discriminator raises on export, since its members are lost."""

    class Litter(SchemaModel):
        """A litter."""

        runt: derived.Cat | derived.Hamster

    with pytest.raises(TypeError, match="Litter.runt is a union without a discriminator"):
        export_schema(Litter)


def test_scalar_union_raises_on_export():
    """A union of scalar types raises on export, since generated code cannot represent it."""

    class Collar(SchemaModel):
        """A collar."""

        size: int | str | None = None

    with pytest.raises(TypeError, match="Collar.size is a union without a discriminator"):
        export_schema(Collar)


def test_scalar_alias_exports_without_typename():
    """An alias of a scalar type needs no type name, since generated code inlines it."""

    class Gauge(SchemaModel):
        """A gauge."""

        label: derived.Label
        link: Link
        speed: Speed
        count: Count
        maybe_label: MaybeLabel
        maybe_link: MaybeLink

    definitions = _definitions(Gauge)
    untagged = {key for key, value in definitions.items() if "x-sgen-typename" not in value}
    assert untagged == {"Label", "Link", "Speed", "Count", "MaybeLabel", "MaybeLink"}


def test_collection_alias_exports_without_typename():
    """An alias of a list or dictionary needs no type name, since generated code inlines it."""

    class Kennel(SchemaModel):
        """A kennel."""

        roster: Roster
        herd: Herd
        scores: Scores
        maybe_roster: MaybeRoster

    definitions = _definitions(Kennel)
    untagged = {key for key, value in definitions.items() if "x-sgen-typename" not in value}
    assert untagged == {"Roster", "Herd", "Scores", "MaybeRoster"}


def test_tuple_alias_raises_on_export():
    """An alias of a tuple raises, since generated code loses the types of its items."""

    class Map(SchemaModel):
        """A map."""

        coordinates: Coordinates

    with pytest.raises(TypeError, match="Coordinates is a tuple"):
        export_schema(Map)


def test_tuple_raises_on_export():
    """A tuple raises on export, since generated code loses the types of its items."""

    class Map(SchemaModel):
        """A map."""

        bounds: list[tuple[int, int]]

    with pytest.raises(TypeError, match="Map.bounds is a tuple"):
        export_schema(Map)


def test_unsupported_types_all_reported():
    """Every type that generated code cannot represent is reported in a single error."""

    class Map(SchemaModel):
        """A map."""

        origin: tuple[int, int]
        scale: int | float

    with pytest.raises(TypeError, match=r"Map.origin is a tuple(.|\n)*Map.scale is a union"):
        export_schema(Map)


def test_enumerated_literal_alias_raises_on_export():
    """An alias of several literal values is generated as an enumeration needing a type name."""

    class Gearbox(SchemaModel):
        """A gearbox."""

        speeds: Speeds

    with pytest.raises(TypeError, match="Missing type names for Speeds"):
        export_schema(Gearbox)


def test_export_plain_type_raises():
    """Only schema types can be exported, since a plain type has no type name."""

    @dataclasses.dataclass
    class Point:
        """A point."""

        x: int

    with pytest.raises(TypeError, match="str has no type name"):
        export_schema(str)  # pyright: ignore[reportArgumentType]
    with pytest.raises(TypeError, match="Point has no type name"):
        export_schema(Point)  # pyright: ignore[reportArgumentType]


def test_plain_class_typename_raises():
    """A class that is not a schema type has no type name, since its schema carries none."""

    @dataclasses.dataclass
    class Point:
        """A point."""

        x: int

    with pytest.raises(TypeError, match="not a subclass of SchemaModel, SchemaEnum or SchemaUnion"):
        get_typename(Point)


def test_explicit_typename_not_inherited():
    """A type defined elsewhere keeps its name, which a subclass does not inherit."""
    assert _definitions(unnamed.Stoat)["Stoat"]["x-sgen-typename"] == "TestHelper.Base.Stoat"
    with pytest.raises(TypeError, match="Weasel has no type name"):
        export_schema(unnamed.Weasel)


def test_generic_model_raises_on_export():
    """A parametrized generic model has no type name, but a concrete subclass has one."""

    class Box(SchemaModel, Generic[T]):
        """A box."""

        content: T

    class IntBox(Box[int]):
        """A box of integers."""

    class Crate(SchemaModel):
        """A crate."""

        box: Box[int]

    with pytest.raises(TypeError, match="Missing type names for Box_int_"):
        export_schema(Crate)
    assert _definitions(IntBox)["IntBox"]["properties"]["content"]["type"] == "integer"


def test_generic_model_typename_raises():
    """A generic model has no type name, so it cannot be exported directly."""
    with pytest.raises(TypeError, match=r"Box\[int\] has no type name"):
        get_typename(plain.Box[int])
    with pytest.raises(TypeError, match="Box has no type name"):
        export_schema(plain.Box)


def test_local_types_export_without_typenames():
    """Types without type names are exported as local types when local types are allowed."""
    definitions = export_schema(plain.Puppy, allow_local_types=True)["$defs"]
    untagged = {key for key, value in definitions.items() if "x-sgen-typename" not in value}
    assert untagged == {"Collar", "Basket", "Bowl", "Coat", "Temper", "Crate_int_", "Box_Collar_"}


def test_generic_model_from_other_namespace_exports_as_local_type():
    """A generic model parametrized in another namespace has no type name of its module."""

    class Crate(SchemaModel):
        """A crate."""

        box: plain.Box[int]

    definitions = export_schema(Crate, allow_local_types=True)["$defs"]
    assert "x-sgen-typename" not in definitions["Box_int_"]
    assert definitions["Crate"]["x-sgen-typename"] == "TestHelper.Schema.Crate"


def test_local_integer_enum_specifies_value_names():
    """A local integer enumeration specifies the names of its values like a SchemaEnum."""
    definitions = export_schema(plain.Puppy, allow_local_types=True)["$defs"]
    assert definitions["Coat"]["x-enumNames"] == ["Short", "LongHair"]
    assert "x-enumNames" not in definitions["Temper"]


def test_local_type_description_kept():
    """A field described exactly like its local type keeps its description, which pydantic drops."""

    @dataclasses.dataclass
    class Basket:
        """A basket."""

        width: float

    class Home(BaseModel):
        """A home."""

        basket: Basket = Field(description="A basket.")

    class Puppy(SchemaModel):
        """A puppy."""

        home: Home = Field(description="A home.")

    definitions = export_schema(Puppy, allow_local_types=True)["$defs"]
    assert definitions["Puppy"]["properties"]["home"]["description"] == "A home."
    assert definitions["Home"]["properties"]["basket"]["description"] == "A basket."


def test_local_type_union_from_other_namespace_warns():
    """A local type referring to a union of another namespace warns of the YAML gap."""

    class Basket(BaseModel):
        """A basket."""

        animal: derived.Animal

    class Home(SchemaModel):
        """A home."""

        basket: Basket

    with pytest.warns(SgenWarning, match="Basket.animal refers to union TestHelper.Derived.Animal"):
        export_schema(Home, allow_local_types=True)


def test_local_types_unsupported_type_raises_on_export():
    """A type that generated code cannot represent raises even when local types are allowed."""

    class Map(SchemaModel):
        """A map."""

        origin: tuple[int, int]

    with pytest.raises(TypeError, match="Map.origin is a tuple"):
        export_schema(Map, allow_local_types=True)


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


def test_integer_enum_specifies_value_names():
    """An integer enumeration specifies the names of its values, converting only upper case."""
    assert _definitions(derived.Palette)["Color"]["x-enumNames"] == [
        "Red",
        "DarkBlue",
        "LightGreen",
    ]


@pytest.mark.parametrize(
    ("name", "key", "title"),
    [
        ("can_meow", "canMeow", "CanMeow"),
        ("canMeow", "canMeow", "CanMeow"),
        ("CanMeow", "canMeow", "CanMeow"),
        ("paw_2d", "paw2d", "Paw2d"),
    ],
    ids=["snake_case", "camelCase", "PascalCase", "digit"],
)
def test_field_key_and_title_keep_capitals(name: str, key: str, title: str):
    """A field key and title keep the capitals of a camelCase or PascalCase name."""
    fields: dict[str, Any] = {name: (bool, False)}
    Cat = create_model("Cat", __base__=SchemaModel, **fields)
    properties = _definitions(Cat)["Cat"]["properties"]
    assert set(properties) == {key}
    assert properties[key]["title"] == title


def test_integer_enum_value_names_keep_underscore():
    """Integer enumeration value names keep an underscore before a leading digit."""

    class Shape(SchemaEnum):
        """Specifies the available shapes."""

        RGB_2D = 0
        _2D = 1

    class Canvas(SchemaModel):
        """A canvas with a shape."""

        shape: Shape

    assert _definitions(Canvas)["Shape"]["x-enumNames"] == ["Rgb2d", "_2d"]


def test_integer_enum_value_names_follow_aliases():
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


def test_enum_base_before_schema_enum_raises():
    """Another enumeration base listed before SchemaEnum raises, since auto() would use it."""
    with pytest.raises(TypeError, match="lists StrEnum before SchemaEnum"):

        class Mood(StrEnum, SchemaEnum):
            """Specifies the available moods."""

            VERY_HAPPY = auto()


def test_str_enum_values_from_member_names():
    """A string enumeration listing SchemaEnum first keeps the values generated by StrEnum."""

    class Mood(SchemaEnum, StrEnum):
        """Specifies the available moods."""

        VERY_HAPPY = auto()

    class Diary(SchemaModel):
        """A diary."""

        mood: Mood

    assert _definitions(Diary)["Mood"]["enum"] == ["very_happy"]


def test_string_enum_specifies_no_value_names():
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


class Pet(SchemaUnion):
    """A union tagging its members under a multi-word property name."""

    root: Annotated[derived.Parrot | derived.Goldfish, DiscriminatedUnion("pet_kind")]


def test_union_tag_key_follows_member_convention():
    """The key of the tag property is the camelCase form of its name, as for fields."""
    assert Pet(derived.Parrot(name="Polly")).model_dump()["petKind"] == "Parrot"
    assert _definitions(Pet)["Pet"]["discriminator"]["propertyName"] == "petKind"


@pytest.mark.parametrize("key", ["petKind", "pet_kind"])
def test_union_tag_key_read_in_both_forms(key: str):
    """A union reads the tag under either form of its key."""
    value = Pet.model_validate({key: "Goldfish", "name": "Bubbles"})
    assert isinstance(value.root, derived.Goldfish)


def test_union_tag_camel_key_read_over_snake_key():
    """Data with both forms of the tag key reads the camelCase form."""
    value = Pet.model_validate({"pet_kind": "Parrot", "petKind": "Goldfish", "name": "Bubbles"})
    assert isinstance(value.root, derived.Goldfish)


def test_union_tag_key_kept_without_alias_generator():
    """The tag key is the specified name when the members clear the alias generator."""

    class SnakeCaseModel(SchemaModel):
        """A base opting out of the camelCase convention."""

        model_config = ConfigDict(alias_generator=None)

    class Cat(SnakeCaseModel):
        """A cat."""

        can_meow: bool = True

    class Dog(SnakeCaseModel):
        """A dog."""

        can_bark: bool = True

    class Animal(SchemaUnion):
        """A union of members without an alias generator."""

        root: Annotated[Cat | Dog, DiscriminatedUnion("pet_kind")]

    assert Animal(Cat()).model_dump() == {"pet_kind": "Cat", "can_meow": True}


def test_union_members_deriving_different_tag_keys_raise():
    """A union raises when its members derive different keys for the tag property."""

    class Cat(SchemaModel):
        """A cat opting out of the convention."""

        model_config = ConfigDict(alias_generator=None)

    with pytest.raises(TypeError, match=r"different keys .* \(got petKind, pet_kind\)"):

        class Animal(SchemaUnion):
            """A union of members deriving different tag keys."""

            root: Annotated[Cat | derived.Parrot, DiscriminatedUnion("pet_kind")]


@pytest.mark.parametrize(
    ("generator", "property_name"),
    [(lambda name: to_camel(name), "pet_kind"), (None, "species")],
    ids=["equivalent-generator", "single-word"],
)
def test_union_members_deriving_same_tag_key_accepted(generator, property_name: str):
    """A union accepts members whose alias generators derive the same tag key."""

    class Cat(SchemaModel):
        """A cat with its own alias generator."""

        model_config = ConfigDict(alias_generator=generator)

    class Animal(SchemaUnion):
        """A union of members deriving the same tag key."""

        root: Annotated[Cat | derived.Parrot, DiscriminatedUnion(property_name)]

    key = to_camel(property_name)
    assert Animal(Cat()).model_dump() == {key: "Cat"}


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


@pytest.mark.parametrize(
    ("union", "member", "tag"),
    [("Nightbird", "Owl", "owl"), ("Flightless", "Kiwi", "kiwi")],
    ids=["class", "alias"],
)
def test_union_single_member_exports_union(union: str, member: str, tag: str):
    """A union with a single member exports as a discriminated union of that member."""
    definition = _definitions(derived.Roost)[union]
    assert definition["oneOf"] == [{"$ref": f"#/$defs/{member}"}]
    assert definition["discriminator"] == {
        "mapping": {tag: f"#/$defs/{member}"},
        "propertyName": "kind",
    }


def test_union_plain_member_raises():
    """A union member must be a schema model."""

    class Turtle(BaseModel):
        """A turtle."""

        kind: Literal["turtle"] = "turtle"

    with pytest.raises(TypeError, match="not a SchemaModel"):

        class Reptile(SchemaUnion):
            """A reptile."""

            root: Annotated[derived.Cat | Turtle, Field(discriminator="kind")]


def test_alias_union_named_and_mapped():
    """A union declared as a type alias is defined once under the name of the alias."""
    definition = _definitions(derived.Home)["Companion"]
    assert definition["x-sgen-typename"] == "TestHelper.Derived.Companion"
    assert definition["discriminator"] == {
        "propertyName": "kind",
        "mapping": {"cat": "#/$defs/Cat", "hamster": "#/$defs/Hamster"},
    }


def test_alias_union_tags_mapped():
    """Tags assigned by a union declared as a type alias are mapped to its members."""
    definition = _definitions(derived.Home)["Flock"]
    assert definition["x-sgen-typename"] == "TestHelper.Derived.Flock"
    assert definition["discriminator"] == {
        "propertyName": "species",
        "mapping": {"Parrot": "#/$defs/Parrot", "Goldfish": "#/$defs/Goldfish"},
    }


def test_alias_union_holds_member():
    """A field annotated with a union alias holds the member itself."""
    home = derived.Home.model_validate({"companion": {"kind": "hamster", "name": "Hammy"}})
    assert type(home.companion) is derived.Hamster


def test_alias_union_explicit_typename():
    """A union alias can refer to a union defined elsewhere."""

    class Sled(SchemaModel):
        """A vehicle on runners for traveling over snow."""

        pack: Pack

    assert _definitions(Sled)["Pack"]["x-sgen-typename"] == "Kennel.Pack"


def test_alias_union_from_other_namespace_warns():
    """Referring to a union alias from another namespace warns of the YAML gap."""

    class Visitor(SchemaModel):
        """A visitor."""

        companion: derived.Companion

    with pytest.warns(SgenWarning, match="TestHelper.Derived.Companion from another namespace"):
        export_schema(Visitor)


def test_alias_union_without_discriminator_raises():
    """A union alias whose members are not discriminated by a tag raises on export."""

    class Menagerie(SchemaModel):
        """A collection of animals of different kinds."""

        mixed: Mixed

    with pytest.raises(TypeError, match="Union Mixed is not discriminated"):
        export_schema(Menagerie)


def test_alias_non_union_raises():
    """An alias marked for generation must be a union, raising on definition of a model."""
    with pytest.raises(TypeError, match="supports only aliases of discriminated unions"):

        class Perch(SchemaModel):
            """A perch."""

            flight: Flight


def test_alias_union_plain_member_raises():
    """A member of a union alias must be a schema model."""
    with pytest.raises(TypeError, match="not a SchemaModel"):

        class Terrarium(SchemaModel):
            """A terrarium."""

            reptile: Reptile


def test_alias_marker_outside_alias_raises():
    """The alias marker raises on export when it annotates anything but a type alias."""

    class Basket(SchemaModel):
        """A basket."""

        animal: Annotated[derived.Cat | derived.Hamster, SchemaAlias()]

    with pytest.raises(TypeError, match="must annotate the value of a type alias"):
        export_schema(Basket)


@pytest.mark.skipif(sys.version_info < (3, 12), reason="the type statement requires Python 3.12")
def test_alias_union_type_statement():
    """A union declared with the type statement is defined under the name of the alias."""
    namespace = {
        "__name__": __name__,
        "Annotated": Annotated,
        "Field": Field,
        "SchemaAlias": SchemaAlias,
        "SchemaModel": SchemaModel,
        "derived": derived,
    }
    exec(
        "type Litter = Annotated[derived.Cat | derived.Hamster, "
        "Field(discriminator='kind'), SchemaAlias()]\n"
        "class Den(SchemaModel):\n"
        "    litter: Litter\n",
        namespace,
    )
    definitions = _definitions(namespace["Den"])
    assert definitions["Litter"]["x-sgen-typename"] == "TestHelper.Schema.Litter"
    assert get_typename(namespace["Litter"]) == "TestHelper.Schema.Litter"


def test_anonymous_union_raises_on_export():
    """An anonymous discriminated union declared in a field raises on export."""

    class Zoo(SchemaModel):
        """A zoo."""

        animals: list[Annotated[derived.Cat | base.Dog, Field(discriminator="kind")]]

    with pytest.raises(TypeError, match="Zoo.animals declares an anonymous discriminated union"):
        export_schema(Zoo)


def test_anonymous_union_in_plain_model_raises_on_export():
    """An anonymous discriminated union declared in a plain model raises on export."""

    class Basket(BaseModel):
        """A basket."""

        animal: Annotated[derived.Cat | derived.Hamster, Field(discriminator="kind")]

    class Home(SchemaModel):
        """A home."""

        basket: Basket

    with pytest.raises(TypeError, match="Basket.animal declares an anonymous discriminated union"):
        export_schema(Home)


def test_schema_types_in_declaration_order():
    """The schema types of a module are those defined in it, in declaration order."""
    assert schema_types(owners) == [owners.Household, owners.Resident, owners.Shelter]


def test_schema_types_exclude_generic_models():
    """The schema types of a module leave out generic models, which have no type name."""
    assert schema_types(plain) == [plain.Puppy]


def test_schema_types_include_marked_aliases():
    """The schema types of a module include the aliases marked for generation."""
    aliases = [value for value in schema_types(derived) if not isinstance(value, type)]
    assert aliases == [derived.Companion, derived.Flock, derived.Flightless]
    assert Plain not in schema_types(sys.modules[__name__])


def test_union_alias_typename():
    """The type name of a union alias is derived from the namespace of its module."""
    assert get_typename(derived.Companion) == "TestHelper.Derived.Companion"
    assert get_typename(Pack) == "Kennel.Pack"


def test_unmarked_alias_typename_raises():
    """A type alias without the alias marker has no type name."""
    with pytest.raises(TypeError, match="Plain is not annotated with SchemaAlias"):
        get_typename(Plain)


def test_export_union_alias():
    """A union alias can be exported without a model referring to it."""
    definitions = _definitions(derived.Flock)
    assert set(definitions) == {"Flock", "Parrot", "Goldfish"}
    assert definitions["Flock"]["x-sgen-typename"] == "TestHelper.Derived.Flock"


def test_write_schema_union_alias(tmp_path):
    """A schema of a union alias is written to a file named after its namespace."""
    assert write_schema(tmp_path, derived.Flock).name == "TestHelper.Derived.json"


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

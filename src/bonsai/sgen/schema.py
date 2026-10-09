"""Base classes for pydantic models used to generate code with Bonsai.Sgen."""

import inspect
import sys
import types
import typing
import warnings
from abc import ABC
from collections.abc import Iterator
from enum import Enum
from typing import Annotated, Any, TypeAlias, Union, get_args, get_origin

import typing_extensions
from pydantic import (
    BaseModel,
    Discriminator,
    GetCoreSchemaHandler,
    GetJsonSchemaHandler,
    RootModel,
    SerializerFunctionWrapHandler,
)
from pydantic.alias_generators import to_pascal
from pydantic.fields import FieldInfo
from pydantic.json_schema import JsonSchemaValue
from pydantic_core import CoreSchema, core_schema

TYPENAME_KEY = "x-sgen-typename"
"""Annotation binding a schema definition to the fully qualified name of a type."""

ENUM_NAMES_KEY = "x-enumNames"
"""Annotation specifying the names of the values in an enumeration type."""

ABSTRACT_KEY = "x-abstract"
"""Annotation marking a type as abstract."""

NAMESPACE_ATTRIBUTE = "SGEN_NAMESPACE"
"""Module attribute declaring the generated namespace of the schema types in the module."""

_TYPENAME_ATTRIBUTE = "__sgen_typename__"
"""Class attribute holding the type name specified explicitly for a type defined elsewhere."""

if sys.version_info >= (3, 12):
    SchemaType: TypeAlias = type[BaseModel] | typing_extensions.TypeAliasType | typing.TypeAliasType
    """A model, union or union alias from which a schema can be exported."""

    _ALIAS_TYPES = (typing_extensions.TypeAliasType, typing.TypeAliasType)
else:
    SchemaType: TypeAlias = type[BaseModel] | typing_extensions.TypeAliasType
    _ALIAS_TYPES = (typing_extensions.TypeAliasType,)


class SgenWarning(UserWarning):
    """Warns that Bonsai.Sgen may not handle the exported schema correctly."""


def get_typename(schema_type: type | SchemaType) -> str:
    """Returns the fully qualified name of the type generated for a schema type.

    The name is the one specified explicitly for the class or union alias, or else its name
    in the namespace declared by the `SGEN_NAMESPACE` attribute of its module. Only the class
    itself is consulted, so a subclass can never inherit the type name of its parent.

    Raises:
        TypeError: If the type is not a subclass of `SchemaModel`, `SchemaEnum` or
            `SchemaUnion`, is a type alias not annotated with `SchemaAlias`, or has no
            explicit name and its module declares no namespace.
    """
    if isinstance(schema_type, type):
        if not issubclass(schema_type, SchemaModel | SchemaEnum | SchemaUnion):
            raise TypeError(
                f"{schema_type.__qualname__} has no type name, since it is not a subclass "
                "of SchemaModel, SchemaEnum or SchemaUnion."
            )
        typename = vars(schema_type).get(_TYPENAME_ATTRIBUTE)
        if typename is not None:
            return typename
        name = schema_type.__name__
        return _module_typename(schema_type.__qualname__, name, schema_type.__module__)
    marker = _alias_marker(schema_type)
    if marker is None:
        raise TypeError(f"Type alias {schema_type.__name__} is not annotated with SchemaAlias.")
    if marker.sgen_typename is not None:
        return marker.sgen_typename
    name = schema_type.__name__
    return _module_typename(name, name, schema_type.__module__ or "")


def _module_typename(qualname: str, name: str, module_name: str) -> str:
    namespace = getattr(sys.modules.get(module_name), NAMESPACE_ATTRIBUTE, None)
    if namespace is None:
        raise TypeError(
            f"{qualname} has no type name, since module {module_name} does not "
            f"declare {NAMESPACE_ATTRIBUTE}. Declare it in the module, or pass "
            "sgen_typename for a type defined elsewhere."
        )
    return f"{namespace}.{name}"


def _namespace(typename: str) -> str:
    return typename.rpartition(".")[0]


def _set_typename(cls: type, typename: str | None) -> None:
    if typename is not None:
        setattr(cls, _TYPENAME_ATTRIBUTE, typename)


def _bind_typename(cls: type, json_schema: JsonSchemaValue, handler: GetJsonSchemaHandler) -> str:
    typename = get_typename(cls)
    handler.resolve_ref_schema(json_schema)[TYPENAME_KEY] = typename
    return typename


def _alias_marker(alias: Any) -> "SchemaAlias | None":
    if not isinstance(alias, _ALIAS_TYPES):
        return None
    value = alias.__value__
    if get_origin(value) is not Annotated:
        return None
    return next((item for item in get_args(value)[1:] if isinstance(item, SchemaAlias)), None)


def _referenced_unions(annotation: Any) -> Iterator[str]:
    if isinstance(annotation, _ALIAS_TYPES):
        if _alias_marker(annotation) is not None:
            yield get_typename(annotation)
        return
    origin = get_origin(annotation)
    if origin is None:
        if isinstance(annotation, type) and issubclass(annotation, SchemaUnion):
            yield get_typename(annotation)
        return
    for argument in get_args(annotation):
        yield from _referenced_unions(argument)


def _is_discriminator(metadata: Any) -> bool:
    if isinstance(metadata, DiscriminatedUnion | Discriminator):
        return True
    return isinstance(metadata, FieldInfo) and metadata.discriminator is not None


def _has_inline_union(annotation: Any) -> bool:
    if get_origin(annotation) is Annotated:
        target, *metadata = get_args(annotation)
        return any(_is_discriminator(item) for item in metadata) or _has_inline_union(target)
    return any(_has_inline_union(argument) for argument in get_args(annotation))


def _union_members(annotation: Any) -> tuple[Any, ...]:
    if get_origin(annotation) in (Union, types.UnionType):
        return get_args(annotation)
    return (annotation,)


def _field_key(name: str, field: FieldInfo) -> str:
    return field.serialization_alias or field.alias or name


def _schema_base(cls: type["SchemaModel"]) -> type["SchemaModel"] | None:
    bases = [
        base for base in cls.__bases__ if issubclass(base, SchemaModel) and base is not SchemaModel
    ]
    if len(bases) != 1:
        return None
    base = bases[0]
    metadata = base.__pydantic_generic_metadata__
    if metadata["origin"] is not None or metadata["parameters"]:
        return None
    annotations = inspect.get_annotations(cls)
    if base.model_config != cls.model_config or any(
        name in annotations for name in base.model_fields
    ):
        return None
    return base


def _inherit_base(
    definition: JsonSchemaValue, base: type["SchemaModel"], handler: GetJsonSchemaHandler
) -> None:
    base_schema = base.__pydantic_core_schema__
    ref = base_schema.get("ref")
    if ref is None:
        raise TypeError(f"{base.__qualname__} has no core schema reference.")
    base_ref = handler(
        core_schema.definitions_schema(core_schema.definition_reference_schema(ref), [base_schema])
    )
    properties = definition.get("properties", {})
    for name, field in base.model_fields.items():
        properties.pop(_field_key(name, field), None)
    definition["allOf"] = [base_ref]


def _check_union_members(name: str, annotation: Any) -> None:
    members = _union_members(annotation)
    if len(members) < 2:
        raise TypeError(
            f"Union {name} has a single member, which generated code "
            "represents as the member itself rather than as a union."
        )
    for member in members:
        if not (isinstance(member, type) and issubclass(member, SchemaModel)):
            raise TypeError(f"Union {name} member {member!r} is not a SchemaModel.")


class SchemaModel(BaseModel):
    """The base class for models generated as classes by Bonsai.Sgen.

    A model is generated as the class of the same name in the namespace declared by the
    `SGEN_NAMESPACE` attribute of its module. Pass `sgen_typename` to describe a type defined
    elsewhere, as in `class Dog(SchemaModel, sgen_typename="Kennel.Dog")`, so that a schema
    refers to the existing type rather than defining a new one.

    A model listing `ABC` among its direct bases, as in `class PetBase(SchemaModel, ABC)`, is
    generated as an abstract class. Its subclasses are generated as concrete classes unless they
    also list `ABC`.

    A discriminated union is declared as a type alias or as a `SchemaUnion`, so that it is
    generated under its own name. A generic model cannot be generated, so only concrete
    subclasses of a generic model may appear in a schema.
    """

    def __init_subclass__(cls, sgen_typename: str | None = None, **kwargs: Any) -> None:
        """Records the type name specified for a type defined elsewhere, which `object` rejects."""
        super().__init_subclass__(**kwargs)
        _set_typename(cls, sgen_typename)

    @classmethod
    def __pydantic_init_subclass__(cls, **kwargs: Any) -> None:
        """Rejects a discriminated union declared inline in a field."""
        super().__pydantic_init_subclass__(**kwargs)
        for name, field in cls.model_fields.items():
            if any(_is_discriminator(item) for item in field.metadata) or _has_inline_union(
                field.annotation
            ):
                raise TypeError(
                    f"{cls.__qualname__}.{name} declares a discriminated union inline. Declare "
                    "it as a type alias or as a SchemaUnion, so it is generated under its own "
                    "name."
                )

    @classmethod
    def __get_pydantic_json_schema__(
        cls, core_schema: CoreSchema, handler: GetJsonSchemaHandler
    ) -> JsonSchemaValue:
        """Binds the definition to its type name and completes the field descriptions.

        Pydantic leaves out a field description identical to that of the field type, which
        would leave the generated property without one, so the description is restored.

        Raises:
            TypeError: If the model is generic or has no type name.
        """
        metadata = cls.__pydantic_generic_metadata__
        if metadata["origin"] is not None or metadata["parameters"]:
            raise TypeError(
                f"{cls.__name__} is a generic model, which generated code cannot represent. "
                "Define a concrete subclass instead."
            )
        json_schema = handler(core_schema)
        typename = _bind_typename(cls, json_schema, handler)
        definition = handler.resolve_ref_schema(json_schema)
        if ABC in cls.__bases__:
            definition[ABSTRACT_KEY] = True
        properties = definition.get("properties", {})
        base = _schema_base(cls)
        if base is not None:
            _inherit_base(definition, base, handler)
        for name, field in cls.model_fields.items():
            prop = properties.get(_field_key(name, field))
            if field.description and prop is not None and "description" not in prop:
                prop["description"] = field.description
            for union_typename in _referenced_unions(field.annotation):
                if _namespace(union_typename) != _namespace(typename):
                    warnings.warn(
                        f"{cls.__qualname__}.{name} refers to union {union_typename} from "
                        "another namespace, which the generated YAML serializer cannot "
                        f"read or write. Redefine the union in {_namespace(typename)} over "
                        "the same members.",
                        SgenWarning,
                        stacklevel=2,
                    )
        return json_schema


class SchemaEnum(Enum):
    """The base class for enumerations generated by Bonsai.Sgen.

    Type names follow the same rules as `SchemaModel`, including `sgen_typename` for a type
    defined elsewhere. The schema of an integer enumeration specifies the names of its values,
    with names in the upper case of the Python convention converted to Pascal case. For a
    string enumeration the names are derived directly from its values.
    """

    def __init_subclass__(cls, sgen_typename: str | None = None, **kwargs: Any) -> None:
        """Records the type name specified for a type defined elsewhere."""
        super().__init_subclass__(**kwargs)
        _set_typename(cls, sgen_typename)

    @classmethod
    def __get_pydantic_json_schema__(
        cls, core_schema: CoreSchema, handler: GetJsonSchemaHandler
    ) -> JsonSchemaValue:
        """Binds the definition to its type name, and specifies the names of integer enum values.

        The names come from `__members__` rather than from iterating the class, since the
        schema values keep aliases and iteration drops them.
        """
        json_schema = handler(core_schema)
        definition = handler.resolve_ref_schema(json_schema)
        if definition.get("type") == "integer":
            definition[ENUM_NAMES_KEY] = [
                to_pascal(name) if name.isupper() else name for name in cls.__members__
            ]
        _bind_typename(cls, json_schema, handler)
        return json_schema


class DiscriminatedUnion:
    """Tags assigned by a union to each of its members, carried in the specified property.

    Annotate the root of a `SchemaUnion` with an instance, as in
    `root: Annotated[Parrot | Goldfish, DiscriminatedUnion("species")]`. The tag of each
    member is its class name, or its fully qualified type name when `qualified` is set, so
    the members do not declare the tag themselves. With qualified tags, the module of each
    member must declare its namespace before the union is defined. A member instance is
    resolved by its exact class, so an instance of another class with the same name is
    rejected.
    """

    def __init__(self, property_name: str, *, qualified: bool = False):
        """Declares the property carrying the tag, and whether tags are qualified.

        Args:
            property_name: The property carrying the tag of each union member.
            qualified: True to tag each union member with its fully qualified type name;
                otherwise with its class name.
        """
        self.property_name = property_name
        self.qualified = qualified
        self._tags: dict[str, type] | None = None

    def __get_pydantic_core_schema__(
        self, source_type: Any, handler: GetCoreSchemaHandler
    ) -> core_schema.CoreSchema:
        """Builds a union validated and serialized through the tag of each member.

        Raises:
            TypeError: If two members share a tag, or the marker already tags other members.
        """
        tags: dict[str, type] = {}
        for member in _union_members(source_type):
            tag = get_typename(member) if self.qualified else member.__name__
            if tag in tags:
                raise TypeError(f"Union members {tags[tag]!r} and {member!r} share tag {tag!r}.")
            tags[tag] = member
        if self._tags is not None and self._tags != tags:
            raise TypeError(f"DiscriminatedUnion({self.property_name!r}) tags other members.")
        self._tags = tags
        tag_of = {member: tag for tag, member in tags.items()}
        property_name = self.property_name

        def select(value: Any) -> str | None:
            if isinstance(value, dict):
                return value.get(property_name)
            return tag_of.get(type(value))

        def serialize(value: Any, serializer: SerializerFunctionWrapHandler) -> Any:
            return {property_name: tag_of[type(value)], **serializer(value)}

        return core_schema.tagged_union_schema(
            {tag: handler.generate_schema(member) for tag, member in tags.items()},
            discriminator=select,
            serialization=core_schema.wrap_serializer_function_ser_schema(serialize),
        )

    def __get_pydantic_json_schema__(
        self, core_schema: CoreSchema, handler: GetJsonSchemaHandler
    ) -> JsonSchemaValue:
        """Declares the tag mapping, which pydantic omits for tags assigned by the union."""
        json_schema = handler(core_schema)
        definition = handler.resolve_ref_schema(json_schema)
        references = [member["$ref"] for member in definition["oneOf"]]
        definition["discriminator"] = {
            "propertyName": self.property_name,
            "mapping": dict(zip(self._tags or {}, references, strict=True)),
        }
        return json_schema


class SchemaUnion(RootModel[Any]):
    """The base class for discriminated unions generated by Bonsai.Sgen.

    A union can be defined by subclassing `SchemaUnion` and annotating its `root` field with
    the member types, discriminated either by a constant tag declared by each member,
    `root: Annotated[Cat | Dog, Field(discriminator="kind")]`, or by tags assigned by the
    union, `root: Annotated[Parrot | Goldfish, DiscriminatedUnion("kind")]`.
    The type name of the union follows the same rules as `SchemaModel`, and every member type
    must be a `SchemaModel`. The generated code wraps any member type that is generated
    elsewhere or belongs to another union.
    """

    def __init_subclass__(cls, sgen_typename: str | None = None, **kwargs: Any) -> None:
        """Records the type name specified for a type defined elsewhere."""
        super().__init_subclass__(**kwargs)
        _set_typename(cls, sgen_typename)

    @classmethod
    def __pydantic_init_subclass__(cls, **kwargs: Any) -> None:
        """Ensures the union can be generated.

        Raises:
            TypeError: If the union is not discriminated, has a single member, or has a
                member that is not a `SchemaModel`.
        """
        super().__pydantic_init_subclass__(**kwargs)
        field = cls.model_fields["root"]
        tagged = any(isinstance(item, DiscriminatedUnion) for item in field.metadata)
        if not tagged and not isinstance(field.discriminator, str):
            raise TypeError(
                f"Union {cls.__qualname__} is not discriminated. Annotate its root with "
                "Field(discriminator=...) referring to a constant tag, or with DiscriminatedUnion."
            )
        _check_union_members(cls.__qualname__, field.annotation)

    @classmethod
    def __get_pydantic_json_schema__(
        cls, core_schema: CoreSchema, handler: GetJsonSchemaHandler
    ) -> JsonSchemaValue:
        """Binds the definition of the union to its type name."""
        json_schema = handler(core_schema)
        _bind_typename(cls, json_schema, handler)
        return json_schema


class SchemaAlias:
    """Marks a type alias of a discriminated union shared by every namespace referring to it.

    Annotating the value of a union alias with an instance, as in
    `type Animal = Annotated[Cat | Dog, Field(discriminator="kind"), SchemaAlias()]`,
    generates the union under the alias name in the namespace declared by the module of the
    alias. Before Python 3.12, `TypeAliasType` declares the same alias. Without the marker,
    the union is generated as a local union in each namespace referring to it.
    """

    def __init__(self, *, sgen_typename: str | None = None):
        """Declares the type name of the union, if it is defined elsewhere.

        Args:
            sgen_typename: The fully qualified type name of a union defined elsewhere.
        """
        self.sgen_typename = sgen_typename

    def __get_pydantic_core_schema__(
        self, source_type: Any, handler: GetCoreSchemaHandler
    ) -> core_schema.CoreSchema:
        """Ensures the alias is a union that can be generated.

        Raises:
            TypeError: If the alias is not a union, or has a member that is not a
                `SchemaModel`.
        """
        if get_origin(source_type) not in (Union, types.UnionType):
            raise TypeError(
                f"SchemaAlias supports only aliases of discriminated unions, not {source_type!r}, "
                "since Bonsai.Sgen cannot represent any other alias as a type of its own. "
                "Refer to the aliased type directly instead."
            )
        _check_union_members(repr(source_type), source_type)
        return handler(source_type)

    def __get_pydantic_json_schema__(
        self, core_schema: CoreSchema, handler: GetJsonSchemaHandler
    ) -> JsonSchemaValue:
        """Binds the definition of the union to its type name.

        Raises:
            TypeError: If the marker does not annotate the value of a type alias, or the
                union is not discriminated.
        """
        json_schema = handler(core_schema)
        definition = handler.resolve_ref_schema(json_schema)
        module_name, _, name = str(core_schema.get("ref", "")).partition(":")[0].rpartition(".")
        if not module_name:
            raise TypeError("SchemaAlias must annotate the value of a type alias.")
        if "discriminator" not in definition:
            raise TypeError(
                f"Union {name} is not discriminated. Annotate its members with "
                "Field(discriminator=...) referring to a constant tag, or with DiscriminatedUnion."
            )
        definition[TYPENAME_KEY] = self.sgen_typename or _module_typename(name, name, module_name)
        return json_schema

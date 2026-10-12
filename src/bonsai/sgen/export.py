"""Exports the JSON schema documents from which Bonsai.Sgen generates code."""

import json
import warnings
from collections.abc import Iterator
from os import PathLike
from pathlib import Path
from types import ModuleType

from pydantic import TypeAdapter
from pydantic.json_schema import GenerateJsonSchema, JsonSchemaValue
from pydantic_core import core_schema

from .schema import (
    TYPENAME_KEY,
    SchemaModel,
    SchemaType,
    SchemaUnion,
    SgenWarning,
    _add_enum_names,
    _alias_marker,
    _is_generic,
    _namespace,
    get_typename,
)

_SCALAR_TYPES = frozenset({"boolean", "integer", "number", "string"})
"""JSON types of scalar definitions."""


_DEFINITION_PREFIX = "#/$defs/"
"""Prefix of a reference to a definition in the same schema document."""


class _SchemaGenerator(GenerateJsonSchema):
    """Generates the schema of a local type with the annotations of a schema type."""

    def enum_schema(self, schema: core_schema.EnumSchema) -> JsonSchemaValue:
        """Specifies the names of the values of an integer enumeration."""
        json_schema = super().enum_schema(schema)
        _add_enum_names(schema["cls"], json_schema)
        return json_schema

    def handle_ref_overrides(self, json_schema: JsonSchemaValue) -> JsonSchemaValue:
        """Keeps a field description identical to that of the field type."""
        result = super().handle_ref_overrides(json_schema)
        if "description" in json_schema:
            result["description"] = json_schema["description"]
        return result


def _is_inlined_value(definition: JsonSchemaValue) -> bool:
    kind = definition.get("type")
    if kind in _SCALAR_TYPES:
        return "enum" not in definition
    if kind == "array":
        return True
    if kind == "object":
        return "additionalProperties" in definition and "properties" not in definition
    return False


def _is_inlined(definition: JsonSchemaValue, definitions: dict[str, JsonSchemaValue]) -> bool:
    if _is_inlined_value(definition):
        return True
    members = definition.get("anyOf") or definition.get("oneOf") or []
    values = [member for member in members if member.get("type") != "null"]
    if len(members) != 2 or len(values) != 1:
        return False
    value = values[0]
    reference = value.get("$ref", "")
    if reference.startswith(_DEFINITION_PREFIX):
        value = definitions.get(reference.removeprefix(_DEFINITION_PREFIX), {})
    return _is_inlined_value(value)


def _unsupported_types(schema: JsonSchemaValue, path: str, definition: bool) -> Iterator[str]:
    if "discriminator" in schema:
        if not definition:
            yield (
                f"{path} declares an anonymous discriminated union. Declare it as a type alias "
                "or as a SchemaUnion, so it is generated under its own name."
            )
        return
    if "prefixItems" in schema or isinstance(schema.get("items"), list):
        yield (
            f"{path} is a tuple, whose item types generated code cannot represent. Declare a "
            "model with a field for each item instead."
        )
        return
    members = [*schema.get("anyOf", []), *schema.get("oneOf", [])]
    if sum(member.get("type") != "null" for member in members) > 1:
        yield (
            f"{path} is a union without a discriminator, which generated code cannot "
            "represent. Declare a discriminated union of models instead, or use a single type."
        )
        return
    for name, value in schema.get("properties", {}).items():
        yield from _unsupported_types(value, f"{path}.{name}", False)
    for keyword in ("items", "additionalProperties"):
        value = schema.get(keyword)
        if isinstance(value, dict):
            yield from _unsupported_types(value, path, False)
    for member in [*members, *schema.get("allOf", [])]:
        yield from _unsupported_types(member, path, False)


def _references(schema: JsonSchemaValue, path: str) -> Iterator[tuple[str, str]]:
    reference = schema.get("$ref", "")
    if reference.startswith(_DEFINITION_PREFIX):
        yield path, reference.removeprefix(_DEFINITION_PREFIX)
    for name, value in schema.get("properties", {}).items():
        yield from _references(value, f"{path}.{name}")
    for keyword in ("items", "additionalProperties"):
        value = schema.get(keyword)
        if isinstance(value, dict):
            yield from _references(value, path)
    for keyword in ("anyOf", "oneOf", "allOf"):
        for member in schema.get(keyword, []):
            yield from _references(member, path)


def _warn_unions_from_other_namespaces(
    definitions: dict[str, JsonSchemaValue], local_namespace: str | None
) -> None:
    for key, definition in definitions.items():
        typename = definition.get(TYPENAME_KEY)
        namespace = _namespace(typename) if typename is not None else local_namespace
        if namespace is None:
            continue
        for path, target in _references(definition, key):
            union = definitions.get(target, {})
            union_typename = union.get(TYPENAME_KEY)
            if (
                "discriminator" in union
                and union_typename is not None
                and _namespace(union_typename) != namespace
            ):
                warnings.warn(
                    f"{path} refers to union {union_typename} from another namespace, which "
                    "the generated YAML serializer cannot read or write. Redefine the union in "
                    f"{namespace} over the same members.",
                    SgenWarning,
                    stacklevel=3,
                )


def schema_types(module: ModuleType) -> list[SchemaType]:
    """Returns the models, unions and union aliases defined in a module, in declaration order.

    Generic models are left out, since they have no type name.
    """
    return [
        value
        for value in vars(module).values()
        if (
            isinstance(value, type)
            and issubclass(value, SchemaModel | SchemaUnion)
            and not _is_generic(value)
            or _alias_marker(value) is not None
        )
        and value.__module__ == module.__name__
    ]


def export_schema(*models: SchemaType, allow_local_types: bool = False) -> JsonSchemaValue:
    """Returns a schema document defining the specified models and every type they refer to.

    The document has no root type, only definitions, including those of types from other
    namespaces. The code generated by Bonsai.Sgen defines only the types in the namespace
    being generated, and refers to any other types by their type names. A discriminated
    union without a type name, such as a type alias not marked with `SchemaAlias`, is
    generated as a local union in every namespace referring to it.

    Union aliases annotated with `SchemaAlias` can be specified alongside models, so that a
    union is defined even if no model in its namespace refers to it.

    Args:
        models: The models and union aliases to define.
        allow_local_types: True to allow types with no type name, such as plain pydantic
            models, dataclasses and enumerations, as local types; otherwise such types raise
            an error. Each namespace that refers to a local type defines its own copy of it.

    Raises:
        TypeError: If a specified model is not a schema type, or the schema refers to a type
            with no type name, such as a plain pydantic model or enumeration, and
            `allow_local_types` is not set. The error is also raised when the schema refers
            to a type that generated code cannot represent, such as a tuple, a union without
            a discriminator, or an anonymous discriminated union.

    Warns:
        SgenWarning: If a type refers to a union from another namespace, which the generated
            YAML serializer cannot read or write.
    """
    if not models:
        raise ValueError("No models to export.")
    namespaces = {_namespace(get_typename(model)) for model in models}
    _, schema = TypeAdapter.json_schemas(
        [(index, "validation", TypeAdapter(model)) for index, model in enumerate(models)],
        schema_generator=_SchemaGenerator,
    )
    definitions = schema["$defs"]
    unsupported = [
        error
        for key, definition in definitions.items()
        for error in _unsupported_types(definition, key, True)
    ]
    if unsupported:
        raise TypeError("\n".join(unsupported))
    unnamed = [
        key
        for key, definition in definitions.items()
        if TYPENAME_KEY not in definition
        and "discriminator" not in definition
        and not _is_inlined(definition, definitions)
    ]
    if unnamed and not allow_local_types:
        raise TypeError(
            f"Missing type names for {', '.join(sorted(unnamed))}. Declare these types as "
            "subclasses of SchemaModel, SchemaEnum or SchemaUnion, or allow them as local "
            "types with allow_local_types=True."
        )
    local_namespace = next(iter(namespaces)) if len(namespaces) == 1 else None
    _warn_unions_from_other_namespaces(definitions, local_namespace)
    return schema


def write_schema(
    directory: str | PathLike[str], *models: SchemaType, allow_local_types: bool = False
) -> Path:
    """Writes the schema of models in a single namespace to a file.

    The schema file is named after the namespace. Bonsai.Sgen derives both the namespace and
    the name of the generated file from the name of the schema file.

    Args:
        directory: The directory of the schema file.
        models: The models and union aliases to define.
        allow_local_types: True to allow types with no type name as local types, as in
            `export_schema`.

    Returns:
        The path of the schema file.

    Raises:
        ValueError: If the models belong to more than one namespace.
    """
    namespaces = sorted({get_typename(model).rpartition(".")[0] for model in models})
    if len(namespaces) != 1:
        raise ValueError(f"Models belong to {len(namespaces)} namespaces: {namespaces}.")
    schema = export_schema(*models, allow_local_types=allow_local_types)
    return _write_schema_file(directory, namespaces[0], schema)


def _write_schema_file(
    directory: str | PathLike[str], namespace: str, schema: JsonSchemaValue
) -> Path:
    path = Path(directory) / f"{namespace}.json"
    path.write_text(json.dumps(schema, indent=2) + "\n", encoding="utf-8")
    return path

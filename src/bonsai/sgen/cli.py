"""Exports schemas of Python models, or generates their C# types with Bonsai.Sgen.

Each module or package is specified by its fully qualified name and imported
from the current environment. Each module declares the namespace of its types
in its SGEN_NAMESPACE attribute. Modules that declare the same namespace are
exported together.

Arguments not specified on the command line are read from a [tool.bonsai-sgen]
table. The nearest pyproject.toml file with this table is used, in the current
folder or above, and its paths are relative to that file.
"""

import argparse
import importlib
import importlib.metadata
import json
import os
import re
import shlex
import subprocess
import sys
import tomllib
import warnings
from collections.abc import Sequence
from pathlib import Path
from types import ModuleType
from typing import Any

from pydantic.json_schema import JsonSchemaValue

from .export import _write_schema_file, export_schema, schema_types
from .schema import NAMESPACE_ATTRIBUTE, SchemaType, _namespace, get_typename

_COMMAND_VARIABLE = "BONSAI_SGEN"
"""Environment variable overriding the command line running Bonsai.Sgen."""

_DEFAULT_COMMAND: tuple[str, ...] = ("dotnet", "bonsai.sgen")
"""Command line running Bonsai.Sgen from the local tool manifest of a repository."""

_NAMESPACE_PLACEHOLDER = "{namespace}"
"""Placeholder in the output location replaced by the namespace of each output."""

_SETTINGS_TABLE = "bonsai-sgen"
"""Name of the table in pyproject.toml specifying the arguments of the command."""

_SETTING_TYPES: dict[str, type] = {
    "modules": list,
    "output": str,
    "schemas": str,
    "serializer": list,
    "allow-local-types": bool,
}
"""Type of each setting in the table."""

_TYPE_DESCRIPTIONS: dict[type, str] = {
    list: "a list of strings",
    str: "a string",
    bool: "a boolean",
}
"""Description of each setting type in error messages."""

_SERIALIZERS = ("json", "yaml")
"""Serializers whose data annotations can be included in the generated classes."""


class _CommandError(Exception):
    """Reports a failure that the command describes without a traceback."""


def _import_module(module_name: str) -> ModuleType:
    try:
        return importlib.import_module(module_name)
    except ModuleNotFoundError as error:
        raise _CommandError(f"Cannot import {module_name}: {error}.") from error


def _submodule_names(package: ModuleType) -> list[str]:
    names: set[str] = set()
    for directory in package.__path__:
        root = Path(directory)
        for path in root.rglob("*.py"):
            parts = path.relative_to(root).with_suffix("").parts
            if parts[-1] == "__init__":
                parts = parts[:-1]
            if parts and parts[-1] != "__main__" and all(part.isidentifier() for part in parts):
                names.add(".".join([package.__name__, *parts]))
    return sorted(names)


def _module_types(module: ModuleType) -> tuple[str | None, list[SchemaType]]:
    namespace = getattr(module, NAMESPACE_ATTRIBUTE, None)
    if namespace is None:
        return None, []
    types = [
        schema_type
        for schema_type in schema_types(module)
        if _namespace(get_typename(schema_type)) == namespace
    ]
    return namespace, types


def _namespace_types(module_names: Sequence[str]) -> dict[str, list[SchemaType]]:
    namespaces: dict[str, list[SchemaType]] = {}
    exported: set[str] = set()
    for module_name in module_names:
        module = _import_module(module_name)
        if hasattr(module, "__path__"):
            modules = [module, *map(_import_module, _submodule_names(module))]
            found = [_module_types(submodule) for submodule in modules]
            if not any(types for _, types in found):
                raise _CommandError(f"Package {module_name} defines no schema types.")
        else:
            modules = [module]
            namespace, types = _module_types(module)
            if namespace is None:
                raise _CommandError(f"Module {module_name} does not declare {NAMESPACE_ATTRIBUTE}.")
            if not types:
                raise _CommandError(f"Module {module_name} defines no schema types.")
            found = [(namespace, types)]
        for submodule, (namespace, types) in zip(modules, found, strict=True):
            if namespace is not None and types and submodule.__name__ not in exported:
                exported.add(submodule.__name__)
                namespaces.setdefault(namespace, []).extend(types)
    return namespaces


def _export_schemas(
    module_names: Sequence[str], allow_local_types: bool
) -> dict[str, JsonSchemaValue]:
    try:
        return {
            namespace: export_schema(*types, allow_local_types=allow_local_types)
            for namespace, types in _namespace_types(module_names).items()
        }
    except (TypeError, ValueError) as error:
        raise _CommandError(str(error)) from error


def _split_command(command: str) -> list[str]:
    if os.name != "nt":
        return shlex.split(command)
    return [
        token[1:-1] if len(token) > 1 and token[0] == token[-1] == '"' else token
        for token in shlex.split(command, posix=False)
    ]


def _minor_release(version: str) -> tuple[str, str] | None:
    match = re.match(r"(\d+)\.(\d+)", version)
    return (match[1], match[2]) if match else None


def _sgen_command() -> list[str]:
    override = os.environ.get(_COMMAND_VARIABLE)
    if override:
        return _split_command(override)
    command = list(_DEFAULT_COMMAND)
    try:
        result = subprocess.run([*command, "--version"], capture_output=True, text=True)
    except FileNotFoundError as error:
        raise _CommandError("The .NET SDK is not installed, so Bonsai.Sgen cannot run.") from error
    if result.returncode != 0:
        raise _CommandError(
            "Bonsai.Sgen is not installed. Install it as a local tool with "
            "'dotnet tool install Bonsai.Sgen', or restore the local tools with "
            "'dotnet tool restore'."
        )
    sgen_version = result.stdout.strip()
    package_version = importlib.metadata.version("bonsai-sgen")
    if _minor_release(sgen_version) is None or (
        _minor_release(sgen_version) != _minor_release(package_version)
    ):
        raise _CommandError(
            f"Bonsai.Sgen {sgen_version} does not match bonsai-sgen {package_version}. "
            "Install the same minor release of both."
        )
    return command


def _check_settings(path: Path, settings: dict[str, Any]) -> None:
    for key, value in settings.items():
        expected = _SETTING_TYPES.get(key)
        if expected is None:
            raise _CommandError(f"Unknown setting {key} in {path}.")
        if not isinstance(value, expected) or (
            expected is list and not all(isinstance(item, str) for item in value)
        ):
            raise _CommandError(f"Setting {key} in {path} must be {_TYPE_DESCRIPTIONS[expected]}.")
    invalid = [name for name in settings.get("serializer", []) if name not in _SERIALIZERS]
    if invalid:
        raise _CommandError(f"Unknown serializer {', '.join(invalid)} in {path}.")


def _read_settings() -> tuple[Path, dict[str, Any]]:
    current_folder = Path.cwd()
    for folder in (current_folder, *current_folder.parents):
        path = folder / "pyproject.toml"
        if not path.is_file():
            continue
        try:
            document = tomllib.loads(path.read_text(encoding="utf-8"))
        except tomllib.TOMLDecodeError as error:
            raise _CommandError(f"Cannot read {path}: {error}.") from error
        settings = document.get("tool", {}).get(_SETTINGS_TABLE)
        if settings is not None:
            _check_settings(path, settings)
            return Path(os.path.relpath(folder)), settings
    return Path(), {}


def _apply_settings(arguments: argparse.Namespace) -> None:
    root, settings = _read_settings()

    def location(argument: str | None, key: str) -> str | None:
        if argument is not None:
            return argument
        return str(root / settings[key]) if key in settings else None

    arguments.modules = arguments.modules or settings.get("modules")
    if not arguments.modules:
        raise _CommandError(
            f"No modules specified on the command line or in [tool.{_SETTINGS_TABLE}]."
        )
    arguments.allow_local_types = arguments.allow_local_types or settings.get(
        "allow-local-types", False
    )
    if arguments.command == "export":
        arguments.output = location(arguments.output, "schemas") or "."
    else:
        arguments.output = location(arguments.output, "output") or "."
        arguments.schemas = location(arguments.schemas, "schemas")
        arguments.serializer = arguments.serializer or settings.get("serializer")


def _output_folder(output: str, namespace: str) -> str:
    return output.replace(_NAMESPACE_PLACEHOLDER, namespace)


def _write_schemas(location: str, schemas: dict[str, JsonSchemaValue]) -> None:
    for namespace, schema in schemas.items():
        output = Path(_output_folder(location, namespace))
        output.mkdir(parents=True, exist_ok=True)
        path = _write_schema_file(output, namespace, schema)
        print(f"Writing schema to {path}...")


def _export(arguments: argparse.Namespace) -> int:
    schemas = _export_schemas(arguments.modules, arguments.allow_local_types)
    _write_schemas(arguments.output, schemas)
    return 0


def _generate(arguments: argparse.Namespace) -> int:
    command = _sgen_command()
    schemas = _export_schemas(arguments.modules, arguments.allow_local_types)
    if arguments.schemas is not None:
        _write_schemas(arguments.schemas, schemas)
    for namespace, schema in schemas.items():
        output = _output_folder(arguments.output, namespace)
        options = ["--namespace", namespace, "--output", output]
        if arguments.serializer:
            options += ["--serializer", *arguments.serializer]
        result = subprocess.run([*command, *options], input=json.dumps(schema).encode())
        if result.returncode != 0:
            return result.returncode
    return 0


def _parser() -> argparse.ArgumentParser:
    shared = argparse.ArgumentParser(add_help=False)
    shared.add_argument(
        "modules",
        nargs="*",
        metavar="module",
        help="The name of a module or package to export, such as pkg.schema.",
    )
    shared.add_argument(
        "-o",
        "--output",
        help="Location to place the generated output. The default is the current directory. "
        f"A {_NAMESPACE_PLACEHOLDER} placeholder is replaced by the namespace of each output.",
    )
    shared.add_argument(
        "--allow-local-types",
        action="store_true",
        help="Allows types with no type name as local types, generated in each namespace that "
        "refers to them.",
    )

    parser = argparse.ArgumentParser(
        prog="bonsai-sgen",
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser(
        "export",
        parents=[shared],
        help="Writes the schema of each namespace to a file.",
        description="Writes the schema of each namespace to a file.",
    )
    generate = commands.add_parser(
        "generate",
        parents=[shared],
        help="Generates the C# types of each namespace with Bonsai.Sgen.",
        description=(
            "Generates the C# types of each namespace with Bonsai.Sgen. The "
            f"{_COMMAND_VARIABLE} environment variable overrides the command line."
        ),
    )
    generate.add_argument(
        "--serializer",
        nargs="+",
        choices=_SERIALIZERS,
        help="Specifies the serializer data annotations to include in the generated classes.",
    )
    generate.add_argument(
        "--schemas",
        help="Location to write the schema of each namespace. "
        f"A {_NAMESPACE_PLACEHOLDER} placeholder is replaced by the namespace of each schema.",
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    """Runs the command line of the package and returns its exit code.

    Args:
        argv: The command-line arguments, or None to read them from `sys.argv`.

    Returns:
        Zero if the command succeeded; otherwise a nonzero exit code.
    """
    parser = _parser()
    arguments = parser.parse_args(argv)
    current_folder = os.getcwd()
    add_current_folder = "" not in sys.path and current_folder not in sys.path
    if add_current_folder:
        sys.path.insert(0, current_folder)

    def show_warning(message: Warning | str, *_: object, **__: object) -> None:
        print(f"{parser.prog}: warning: {message}", file=sys.stderr)

    try:
        with warnings.catch_warnings():
            warnings.showwarning = show_warning
            _apply_settings(arguments)
            if arguments.command == "export":
                return _export(arguments)
            return _generate(arguments)
    except _CommandError as error:
        print(f"{parser.prog}: error: {error}", file=sys.stderr)
        return 1
    finally:
        if add_current_folder and current_folder in sys.path:
            sys.path.remove(current_folder)

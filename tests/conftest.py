"""Fixtures that generate C# code from the test models and run it."""

import json
import shlex
import shutil
import subprocess
import warnings
from functools import cache
from pathlib import Path

import pytest
from models import base, derived, local, owners

from bonsai.sgen import SgenWarning, schema_types, write_schema

GENERATOR = Path(__file__).parents[1] / "src" / "Bonsai.Sgen" / "Bonsai.Sgen.csproj"
"""Project of the generator under test."""

INTEROP = Path(__file__).parent / "Bonsai.Sgen.Interop.Tests" / "Bonsai.Sgen.Interop.Tests.csproj"
"""Project running documents through the code generated from the test models."""


def _run(*command: str | Path) -> str:
    result = subprocess.run(command, capture_output=True, text=True)
    if result.returncode != 0:
        raise RuntimeError(f"{' '.join(map(str, command))} failed:\n{result.stdout}{result.stderr}")
    return result.stdout


def _properties(project: Path, names: list[str], **properties: str) -> dict[str, str]:
    options = [f"-p:{key}={value}" for key, value in properties.items()]
    output = _run("dotnet", "msbuild", project, *options, *(f"-getProperty:{n}" for n in names))
    if len(names) == 1:
        return {names[0]: output.strip()}
    return json.loads(output)["Properties"]


def _run_command(project: Path, **properties: str) -> list[str]:
    names = ["RunCommand", "RunArguments"]
    run = _properties(project, names, Configuration="Release", **properties)
    return [run["RunCommand"], *shlex.split(run["RunArguments"])]


@cache
def _frameworks() -> list[str]:
    return _properties(INTEROP, ["TargetFrameworks"])["TargetFrameworks"].split(";")


def pytest_generate_tests(metafunc: pytest.Metafunc) -> None:
    """Runs each test using the generated code once for every target framework."""
    if "framework" in metafunc.fixturenames:
        if shutil.which("dotnet") is None:
            skip = pytest.mark.skip(reason="The .NET SDK is not installed.")
            metafunc.parametrize("framework", [pytest.param(None, marks=skip)])
        else:
            metafunc.parametrize("framework", _frameworks())


class Runner:
    """Runs documents through the generated deserializer and serializer operators."""

    def __init__(self, commands: dict[str, list[str]]):
        """Uses the command running the interop test project on each target framework."""
        self.commands = commands

    def roundtrip(self, framework: str, serializer: str, typename: str, text: str) -> str:
        """Returns a document after a round trip through the generated serializer operators."""
        command = [*self.commands[framework], serializer, typename]
        result = subprocess.run(command, input=text.encode(), capture_output=True)
        if result.returncode != 0:
            raise RuntimeError(result.stderr.decode(errors="replace").strip().splitlines()[0])
        return result.stdout.decode()


@pytest.fixture(scope="session")
def runner(tmp_path_factory: pytest.TempPathFactory) -> Runner:
    """Generates code for every test namespace with the local generator and compiles it."""
    root = tmp_path_factory.mktemp("interop")
    with warnings.catch_warnings():
        warnings.simplefilter("ignore", SgenWarning)
        paths = [
            write_schema(root, *schema_types(module)) for module in (base, derived, local, owners)
        ]

    _run("dotnet", "build", GENERATOR, "--configuration", "Release")
    generator = _run_command(GENERATOR)
    generated = root / "Generated"
    for path in paths:
        _run(*generator, path, "--output", generated, "--serializer", "json", "yaml")

    directory = f"-p:GeneratedCodeDirectory={generated}"
    _run("dotnet", "build", INTEROP, "--configuration", "Release", directory)
    frameworks = _frameworks()
    return Runner({name: _run_command(INTEROP, TargetFramework=name) for name in frameworks})

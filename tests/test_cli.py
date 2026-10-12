"""Tests for the command line that exports schemas and generates code."""

import json
import os
import shlex
import shutil
import subprocess
import sys
import types
from importlib.metadata import version
from pathlib import Path

import pytest
from models import derived

from bonsai.sgen import cli, schema_types, write_schema

requires_dotnet = pytest.mark.skipif(
    shutil.which("dotnet") is None, reason="The .NET SDK is not installed."
)
"""Marks a test that runs the local generator."""


def _command_line(command: list[str]) -> str:
    return subprocess.list2cmdline(command) if os.name == "nt" else shlex.join(command)


def _fake_generator(directory: Path, reported_version: str) -> list[str]:
    script = directory / "fake generator.py"
    script.write_text(
        "import sys\n"
        f"print({reported_version!r}) if '--version' in sys.argv else sys.stdin.read()\n"
    )
    return [sys.executable, str(script)]


def test_module_entry_point_runs():
    """The package runs as a module with python -m."""
    command = [sys.executable, "-m", "bonsai.sgen", "export", "--help"]
    result = subprocess.run(command, capture_output=True, text=True, check=True)
    assert "--allow-local-types" in result.stdout


def test_console_script_imports_from_current_folder(tmp_path: Path):
    """The console script imports modules from the current folder."""
    script = shutil.which("bonsai-sgen", path=str(Path(sys.executable).parent))
    assert script is not None
    command = [script, "export", "models.base", "-o", str(tmp_path)]
    subprocess.run(command, cwd=Path(__file__).parent, check=True)
    assert (tmp_path / "TestHelper.Base.json").exists()


def test_main_restores_module_search_path(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    """Running the command in-process leaves the module search path as it was."""
    monkeypatch.chdir(tmp_path)
    search_path = list(sys.path)
    assert cli.main(["export", "models.base", "-o", str(tmp_path)]) == 0
    assert sys.path == search_path


def test_export_writes_schema_per_namespace(tmp_path: Path):
    """Each namespace is written to a file named after it, as write_schema writes it."""
    output = tmp_path / "schemas"
    assert cli.main(["export", "models.derived", "models.base", "-o", str(output)]) == 0
    expected = write_schema(tmp_path, *schema_types(derived))
    assert (output / expected.name).read_bytes() == expected.read_bytes()
    assert sorted(path.name for path in output.iterdir()) == [
        "TestHelper.Base.json",
        "TestHelper.Derived.json",
    ]


def test_export_modules_sharing_namespace_write_one_file(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    """Modules declaring the same namespace are exported together to a single file."""
    sources = {
        "home_dogs": "class Dog(SchemaModel):\n    name: str\n",
        "home_cats": "class Cat(SchemaModel):\n    lives: int = 9\n",
    }
    for name, source in sources.items():
        module = types.ModuleType(name)
        module.SGEN_NAMESPACE = "TestHelper.Home"  # pyright: ignore[reportAttributeAccessIssue]
        monkeypatch.setitem(sys.modules, name, module)
        exec("from bonsai.sgen import SchemaModel\n" + source, module.__dict__)

    assert cli.main(["export", *sources, "-o", str(tmp_path)]) == 0
    assert [path.name for path in tmp_path.iterdir()] == ["TestHelper.Home.json"]
    definitions = json.loads((tmp_path / "TestHelper.Home.json").read_text())["$defs"]
    assert set(definitions) == {"Dog", "Cat"}


def test_export_package_includes_submodules(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    """A package exports every submodule with schema types, including namespace packages."""
    header = "from bonsai.sgen import SchemaModel\n"
    files = {
        "home/__init__.py": "",
        "home/__main__.py": "raise RuntimeError('The entry point was imported.')\n",
        "home/helpers.py": "CAPACITY = 2\n",
        "home/dogs.py": f"{header}SGEN_NAMESPACE = 'TestHelper.Home'\n"
        "class Dog(SchemaModel):\n    name: str\n",
        "home/cats.py": f"{header}SGEN_NAMESPACE = 'TestHelper.Home'\n"
        "class Cat(SchemaModel):\n    lives: int = 9\n",
        "home/aviary/birds.py": f"{header}SGEN_NAMESPACE = 'TestHelper.Aviary'\n"
        "class Bird(SchemaModel):\n    wings: int = 2\n",
    }
    for name, source in files.items():
        path = tmp_path / "src" / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(source)
    monkeypatch.syspath_prepend(str(tmp_path / "src"))

    output = tmp_path / "schemas"
    assert cli.main(["export", "home", "-o", str(output)]) == 0
    assert sorted(path.name for path in output.iterdir()) == [
        "TestHelper.Aviary.json",
        "TestHelper.Home.json",
    ]
    definitions = json.loads((output / "TestHelper.Home.json").read_text())["$defs"]
    assert set(definitions) == {"Dog", "Cat"}


def test_export_package_without_schema_types_raises(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
):
    """A package whose submodules define no schema types raises an error."""
    package = tmp_path / "src" / "empty_home"
    package.mkdir(parents=True)
    (package / "helpers.py").write_text("CAPACITY = 2\n")
    monkeypatch.syspath_prepend(str(tmp_path / "src"))
    assert cli.main(["export", "empty_home", "-o", str(tmp_path / "schemas")]) == 1
    assert "Package empty_home defines no schema types" in capsys.readouterr().err


def test_export_warning_shown_as_message(tmp_path: Path, capsys: pytest.CaptureFixture[str]):
    """A warning is shown as a message of the command, without the source that raised it."""
    assert cli.main(["export", "models.owners", "-o", str(tmp_path)]) == 0
    message = capsys.readouterr().err
    assert "bonsai-sgen: warning: Household.animal refers to union" in message
    assert ".py:" not in message


def test_export_local_types_raise_without_option(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
):
    """Local types raise an error unless allowed, and no schema file is written."""
    output = tmp_path / "schemas"
    assert cli.main(["export", "models.plain", "-o", str(output)]) == 1
    assert "Missing type names" in capsys.readouterr().err
    assert not output.exists()


def test_export_allows_local_types(tmp_path: Path):
    """Local types are exported when the option allows them."""
    assert cli.main(["export", "models.plain", "--allow-local-types", "-o", str(tmp_path)]) == 0
    definitions = json.loads((tmp_path / "TestHelper.Plain.json").read_text())["$defs"]
    assert "x-sgen-typename" not in definitions["Basket"]


def test_export_module_without_namespace_raises(tmp_path: Path, capsys: pytest.CaptureFixture[str]):
    """A module that declares no namespace raises an error."""
    assert cli.main(["export", "models.unnamed", "-o", str(tmp_path)]) == 1
    assert "does not declare SGEN_NAMESPACE" in capsys.readouterr().err


def test_export_missing_module_raises(tmp_path: Path, capsys: pytest.CaptureFixture[str]):
    """A module that cannot be imported raises an error."""
    assert cli.main(["export", "models.missing", "-o", str(tmp_path)]) == 1
    assert "No module named 'models.missing'" in capsys.readouterr().err


def test_generate_version_mismatch_raises(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
):
    """A generator from another minor release raises an error naming both versions."""
    monkeypatch.delenv("BONSAI_SGEN", raising=False)
    monkeypatch.setattr(cli, "_DEFAULT_COMMAND", tuple(_fake_generator(tmp_path, "99.0.0")))
    assert cli.main(["generate", "models.derived", "-o", str(tmp_path)]) == 1
    message = capsys.readouterr().err
    assert "Bonsai.Sgen 99.0.0 does not match" in message
    assert version("bonsai-sgen") in message


def test_generate_matching_minor_release_runs(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    """A generator from the same minor release runs, whatever its patch version."""
    monkeypatch.delenv("BONSAI_SGEN", raising=False)
    major, minor = version("bonsai-sgen").split(".")[:2]
    command = _fake_generator(tmp_path, f"{major}.{minor}.7")
    monkeypatch.setattr(cli, "_DEFAULT_COMMAND", tuple(command))
    assert cli.main(["generate", "models.derived", "-o", str(tmp_path)]) == 0


def test_generate_missing_generator_raises(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
):
    """A generator that does not run raises an error explaining how to install it."""
    monkeypatch.delenv("BONSAI_SGEN", raising=False)
    monkeypatch.setattr(cli, "_DEFAULT_COMMAND", (sys.executable, "-c", "raise SystemExit(1)"))
    assert cli.main(["generate", "models.derived", "-o", str(tmp_path)]) == 1
    assert "Bonsai.Sgen is not installed" in capsys.readouterr().err


def test_generate_override_skips_version_check(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    """The environment variable overrides the generator command and skips the version check."""
    directory = tmp_path / "with space"
    directory.mkdir()
    monkeypatch.setenv("BONSAI_SGEN", _command_line(_fake_generator(directory, "99.0.0")))
    assert cli.main(["generate", "models.derived", "-o", str(tmp_path)]) == 0


@requires_dotnet
def test_generate_matches_code_from_schema_file(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, generator: list[str]
):
    """Code generated from a schema read on standard input is identical to code from its file."""
    schema = write_schema(tmp_path, *schema_types(derived))
    file_output, stdin_output = tmp_path / "file", tmp_path / "stdin"
    subprocess.run([*generator, schema, "-o", file_output, "--serializer", "json"], check=True)
    monkeypatch.setenv("BONSAI_SGEN", _command_line(generator))
    arguments = ["generate", "models.derived", "-o", str(stdin_output), "--serializer", "json"]
    assert cli.main(arguments) == 0
    name = "TestHelper.Derived.Generated.cs"
    assert (stdin_output / name).read_bytes() == (file_output / name).read_bytes()

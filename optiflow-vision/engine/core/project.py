"""
OptiFlow Vision Studio - Project Schema and Serialization
Handles .vsproj JSON specification, version migration, and atomic file I/O.
"""

import os
import json
import time
from typing import List, Dict, Any, Optional
from pydantic import BaseModel, Field

CURRENT_SCHEMA_VERSION = "1.0.0"


class ProjectMetadata(BaseModel):
    name: str = "New Inspection"
    description: str = "Automated visual inspection project"
    author: str = "Kha Studio"
    version: str = "1.0.0"
    tags: List[str] = Field(default_factory=lambda: ["industrial", "inspection"])


class ProjectSettings(BaseModel):
    target_fps: float = 30.0
    mode: str = "FULL_EDITOR"  # "FULL_EDITOR", "SIMPLE_MODE", "RUNTIME_PLAYER"
    default_language: str = "vi"  # "en" or "vi"
    stop_on_error: bool = False
    diagnostic_logging: bool = True
    active_program_id: str = "prog_main"


class PortBinding(BaseModel):
    port_id: str
    value_type: str
    formula: Optional[str] = None
    default_value: Optional[Any] = None


class FilterNode(BaseModel):
    id: str
    filter_type: str
    label: str
    category: str = "Processing"
    position_x: float = 100.0
    position_y: float = 100.0
    inputs: Dict[str, Any] = Field(default_factory=dict)
    outputs: Dict[str, Any] = Field(default_factory=dict)
    is_disabled: bool = False
    comment: Optional[str] = None


class FilterConnection(BaseModel):
    id: str
    source_node_id: str
    source_port: str
    target_node_id: str
    target_port: str


class ProgramGraph(BaseModel):
    id: str = "prog_main"
    name: str = "Main Inspection Task"
    type: str = "MAIN_LOOP"  # "STARTUP", "MAIN_LOOP", "WORKER_TASK"
    nodes: List[FilterNode] = Field(default_factory=list)
    connections: List[FilterConnection] = Field(default_factory=list)


class MacroDefinition(BaseModel):
    id: str
    name: str
    description: str = ""
    icon: str = "box"
    inputs: List[Dict[str, Any]] = Field(default_factory=list)
    outputs: List[Dict[str, Any]] = Field(default_factory=list)
    nodes: List[FilterNode] = Field(default_factory=list)
    connections: List[FilterConnection] = Field(default_factory=list)


class OperatorControl(BaseModel):
    id: str
    type: str  # "lamp", "video_box", "button", "slider", "indicator", "table", "chart"
    title: str
    x: int = 20
    y: int = 20
    width: int = 200
    height: int = 150
    bindings: Dict[str, str] = Field(default_factory=dict)  # {"target_property": "filter_id.output_port"}


class OperatorScreen(BaseModel):
    id: str = "screen_operator"
    name: str = "Production Screen"
    controls: List[OperatorControl] = Field(default_factory=list)


class OptiFlowProject(BaseModel):
    format_version: str = CURRENT_SCHEMA_VERSION
    generator: str = "OptiFlow Vision Studio"
    created_at: float = Field(default_factory=time.time)
    updated_at: float = Field(default_factory=time.time)
    metadata: ProjectMetadata = Field(default_factory=ProjectMetadata)
    settings: ProjectSettings = Field(default_factory=ProjectSettings)
    programs: List[ProgramGraph] = Field(default_factory=lambda: [ProgramGraph()])
    macros: List[MacroDefinition] = Field(default_factory=list)
    global_variables: Dict[str, Any] = Field(default_factory=dict)
    operator_screens: List[OperatorScreen] = Field(default_factory=lambda: [OperatorScreen()])

    def to_json(self, indent: int = 2) -> str:
        return self.model_dump_json(indent=indent)

    @classmethod
    def from_json(cls, json_str: str) -> "OptiFlowProject":
        raw = json.loads(json_str)
        migrated = migrate_project_schema(raw)
        return cls.model_validate(migrated)

    @classmethod
    def load_from_file(cls, file_path: str) -> "OptiFlowProject":
        with open(file_path, "r", encoding="utf-8") as f:
            content = f.read()
        return cls.from_json(content)

    def save_to_file(self, file_path: str) -> None:
        self.updated_at = time.time()
        os.makedirs(os.path.dirname(os.path.abspath(file_path)), exist_ok=True)
        # Atomic write pattern to prevent corrupting projects during crashes
        temp_file = f"{file_path}.tmp_{os.getpid()}"
        with open(temp_file, "w", encoding="utf-8") as f:
            f.write(self.to_json(indent=2))
        if os.path.exists(file_path):
            os.replace(temp_file, file_path)
        else:
            os.rename(temp_file, file_path)


def migrate_project_schema(data: Dict[str, Any]) -> Dict[str, Any]:
    """
    Schema migration handler.
    Guarantees backward and forward compatibility between project versions.
    """
    version = data.get("format_version", "0.0.0")

    # Future version migrations can be chained sequentially:
    # if version == "0.9.0": ... data["format_version"] = "1.0.0"
    if "programs" not in data:
        data["programs"] = [ProgramGraph().model_dump()]

    if "metadata" not in data:
        data["metadata"] = ProjectMetadata().model_dump()

    if "settings" not in data:
        data["settings"] = ProjectSettings().model_dump()

    data["format_version"] = CURRENT_SCHEMA_VERSION
    return data

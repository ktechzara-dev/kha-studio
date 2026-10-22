"""
OptiFlow Vision Studio - Geometric & Inspection Primitives
Defines standard typed domain objects used across graph filters, overlays, and serialization.
"""

from typing import List, Optional, Dict, Any, Union
from pydantic import BaseModel, Field


class Point2D(BaseModel):
    x: float
    y: float

    def to_tuple(self) -> tuple[float, float]:
        return (self.x, self.y)


class Segment2D(BaseModel):
    p1: Point2D
    p2: Point2D

    @property
    def length(self) -> float:
        import math
        return math.hypot(self.p2.x - self.p1.x, self.p2.y - self.p1.y)


class Rectangle2D(BaseModel):
    x: float
    y: float
    width: float
    height: float
    angle_deg: float = 0.0

    @property
    def center(self) -> Point2D:
        return Point2D(x=self.x + self.width / 2.0, y=self.y + self.height / 2.0)

    @property
    def area(self) -> float:
        return self.width * self.height


class Circle2D(BaseModel):
    cx: float
    cy: float
    radius: float

    @property
    def diameter(self) -> float:
        return self.radius * 2.0

    @property
    def area(self) -> float:
        import math
        return math.pi * (self.radius ** 2)


class ImageMeta(BaseModel):
    id: str
    width: int
    height: int
    channels: int
    color_space: str = "GRAY"  # GRAY, BGR, RGB
    dtype: str = "uint8"
    file_path: Optional[str] = None
    timestamp: float = 0.0
    memory_size_bytes: int = 0


class PixelInspectionResult(BaseModel):
    x: int
    y: int
    in_bounds: bool
    channels: List[int] = Field(default_factory=list)
    intensity_text: str = ""
    neighborhood_7x7: List[List[int]] = Field(default_factory=list)


class HistogramData(BaseModel):
    image_id: str
    bins: List[int]
    min_val: float
    max_val: float
    mean_val: float
    std_val: float
    percentile_25: float
    percentile_50: float
    percentile_75: float


class OverlayItem(BaseModel):
    type: str  # "rect", "circle", "line", "point", "crosshair", "text", "polygon"
    color: str = "#00ffff"  # Hex or CSS color
    thickness: int = 2
    filled: bool = False
    fill_color: Optional[str] = None
    label: Optional[str] = None
    coordinates: Dict[str, Any] = Field(default_factory=dict)

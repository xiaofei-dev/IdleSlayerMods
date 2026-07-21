"""Inspect Idle Slayer's serialized bonus-map assets without modifying the game."""

from __future__ import annotations

import argparse
import collections
import json
import math
import struct
import sys
from pathlib import Path
from typing import Any

import UnityPy


def simplify(value: Any) -> Any:
    if isinstance(value, dict):
        return {str(k): simplify(v) for k, v in value.items()}
    if isinstance(value, (list, tuple)):
        return [simplify(v) for v in value]
    if hasattr(value, "file_id") and hasattr(value, "path_id"):
        return {"file_id": value.file_id, "path_id": value.path_id}
    if isinstance(value, (str, int, float, bool)) or value is None:
        return value
    return repr(value)


def pptr_id(value: Any) -> int:
    if not isinstance(value, dict):
        return 0
    return int(value.get("m_PathID", value.get("path_id", 0)))


def object_name(obj: Any) -> str:
    try:
        data = obj.read_typetree()
        return str(data.get("m_Name", ""))
    except Exception:
        return ""


def compose_2d(parent: tuple[float, float, float, float, float], data: dict[str, Any]):
    px, py, prot, psx, psy = parent
    pos = data["m_LocalPosition"]
    scale = data["m_LocalScale"]
    rotation = data["m_LocalRotation"]
    local_angle = math.atan2(
        2 * (rotation["w"] * rotation["z"] + rotation["x"] * rotation["y"]),
        1 - 2 * (rotation["y"] ** 2 + rotation["z"] ** 2),
    )
    lx, ly = pos["x"] * psx, pos["y"] * psy
    cosine, sine = math.cos(prot), math.sin(prot)
    return (
        px + cosine * lx - sine * ly,
        py + sine * lx + cosine * ly,
        prot + local_angle,
        psx * scale["x"],
        psy * scale["y"],
    )


def extract_prefab(root_id: int, objects: dict[int, Any]) -> dict[str, Any]:
    root = objects[root_id].read_typetree()
    transform_id = pptr_id(root["m_Component"][0]["component"])
    nodes: list[dict[str, Any]] = []

    def visit(current_id: int, parent_world: tuple[float, float, float, float, float], path: str):
        transform = objects[current_id].read_typetree()
        world = compose_2d(parent_world, transform)
        game_object_id = pptr_id(transform["m_GameObject"])
        game_object = objects[game_object_id].read_typetree()
        current_path = f"{path}/{game_object['m_Name']}" if path else game_object["m_Name"]
        components = []
        for item in game_object.get("m_Component", []):
            component_id = pptr_id(item["component"])
            component = objects.get(component_id)
            if component is None or component.type.name == "Transform":
                continue
            record: dict[str, Any] = {"path_id": component_id, "type": component.type.name}
            try:
                tree = component.read_typetree()
                for key in (
                    "m_Enabled", "m_IsTrigger", "m_Offset", "m_Size", "m_Radius",
                    "m_Points", "m_UsedByEffector", "m_CompositeOperation", "m_Origin",
                ):
                    if key in tree:
                        record[key] = simplify(tree[key])
                if component.type.name == "Tilemap":
                    record["occupied_cells"] = [simplify(tile[0]) for tile in tree.get("m_Tiles", [])]
            except Exception:
                record["serialized_bytes"] = len(component.get_raw_data())
            components.append(record)
        nodes.append(
            {
                "path": current_path,
                "game_object_id": game_object_id,
                "active": game_object.get("m_IsActive", True),
                "local_position": simplify(transform["m_LocalPosition"]),
                "world_2d": {"x": world[0], "y": world[1], "rotation_radians": world[2], "scale_x": world[3], "scale_y": world[4]},
                "components": components,
            }
        )
        for child in transform.get("m_Children", []):
            visit(pptr_id(child), world, current_path)

    visit(transform_id, (0.0, 0.0, 0.0, 1.0, 1.0), "")
    return {"root_id": root_id, "name": root["m_Name"], "nodes": nodes}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("assets", type=Path)
    parser.add_argument("--output", type=Path, default=Path("artifacts/bonus_asset_index.json"))
    args = parser.parse_args()

    env = UnityPy.load(str(args.assets))
    counts = collections.Counter(obj.type.name for obj in env.objects)
    matches: list[dict[str, Any]] = []
    errors = collections.Counter()

    for obj in env.objects:
        if obj.type.name not in {"MonoBehaviour", "GameObject"}:
            continue
        try:
            data = obj.read_typetree()
        except Exception as exc:  # stripped custom types are expected in IL2CPP builds
            errors[type(exc).__name__] += 1
            continue
        text = json.dumps(simplify(data), ensure_ascii=False)
        if "bonus" not in text.lower():
            continue
        matches.append(
            {
                "path_id": obj.path_id,
                "type": obj.type.name,
                "data": simplify(data),
            }
        )

    report = {
        "source": str(args.assets),
        "object_counts": counts,
        "read_errors": errors,
        "matches": matches,
    }

    objects = {obj.path_id: obj for obj in env.objects}
    # Bonus Stage 3's stripped MonoBehaviour still contains ordinary PPtrs.
    # Resolve every in-file GameObject reference whose target is a ground prefab.
    map_candidates = []
    for obj in env.objects:
        if obj.type.name != "MonoBehaviour" or b"Bonus Stage 3" not in obj.get_raw_data():
            continue
        references = []
        raw = obj.get_raw_data()
        for offset in range(0, len(raw) - 12, 4):
            file_id = struct.unpack_from("<i", raw, offset)[0]
            path_id = struct.unpack_from("<q", raw, offset + 4)[0]
            target = objects.get(path_id)
            if file_id != 0 or target is None or target.type.name != "GameObject":
                continue
            name = object_name(target)
            if name.startswith("Ground") or name.startswith("Reward Zone"):
                references.append({"offset": offset, "path_id": path_id, "name": name})
        map_candidates.append({"path_id": obj.path_id, "size": len(raw), "references": references})
    ground_ids = sorted({r["path_id"] for m in map_candidates for r in m["references"] if r["name"].startswith("Ground")})
    report["bonus_stage_3_candidates"] = map_candidates
    report["ground_prefabs"] = [extract_prefab(path_id, objects) for path_id in ground_ids]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"objects={sum(counts.values())} matches={len(matches)} errors={sum(errors.values())}")
    print(f"wrote {args.output}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

import argparse
import csv
import math
import random
from dataclasses import dataclass
from datetime import datetime, timedelta
from pathlib import Path


WORKSPACE_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE_CSV = WORKSPACE_ROOT / "2.csv"
DEFAULT_OUTPUT_CSV = WORKSPACE_ROOT / "glider_underwater_synthetic.csv"
FRAME_INTERVAL_SECONDS = 10


@dataclass(frozen=True)
class SegmentConfig:
    segment_id: int
    start_time: datetime
    start_lon: float
    start_lat: float
    target_depth: float
    current_scale: float
    mean_speed: float
    initial_heading: float


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate a synthetic underwater glider CSV compatible with the Unity twin.")
    parser.add_argument("--source", default=str(DEFAULT_SOURCE_CSV), help="CSV file to copy the header from.")
    parser.add_argument("--output", default=str(DEFAULT_OUTPUT_CSV), help="Output CSV path.")
    parser.add_argument("--segments", type=int, default=20)
    parser.add_argument("--frames-per-segment", type=int, default=2520)
    parser.add_argument("--seed", type=int, default=20260716)
    return parser.parse_args()


def format_timestamp(dt: datetime) -> str:
    mission_delta = dt - datetime(dt.year, 1, 1)
    hours, remainder = divmod(mission_delta.seconds, 3600)
    minutes, seconds = divmod(remainder, 60)
    return f"{mission_delta.days}d {hours:02d}h {minutes:02d}m {seconds:02d}s"


def wrap_angle(angle: float) -> float:
    return angle % 360.0


def shortest_angle_delta(target: float, value: float) -> float:
    return ((target - value + 180.0) % 360.0) - 180.0


def clamp(value: float, low: float, high: float) -> float:
    return max(low, min(high, value))


def build_heading_schedule(base_heading: float) -> list[tuple[int, int, float]]:
    return [
        (0, 420, wrap_angle(base_heading)),
        (420, 780, wrap_angle(base_heading + 18)),
        (780, 1140, wrap_angle(base_heading + 96)),
        (1140, 1500, wrap_angle(355)),
        (1500, 1860, wrap_angle(22)),
        (1860, 2160, wrap_angle(base_heading + 210)),
        (2160, 2520, wrap_angle(base_heading + 260)),
    ]


def build_depth_schedule(target_depth: float) -> list[tuple[int, int, float, float]]:
    cruise = target_depth
    deeper = min(165.0, target_depth + 35.0)
    shallower = max(25.0, target_depth - 28.0)
    return [
        (0, 300, 8.0, 0.28),
        (300, 900, cruise, 0.18),
        (900, 1260, deeper, 0.20),
        (1260, 1620, shallower, 0.23),
        (1620, 2040, cruise + 12.0, 0.19),
        (2040, 2310, 18.0, 0.26),
        (2310, 2520, cruise, 0.21),
    ]


def schedule_value(frame_idx: int, schedule: list[tuple[int, int, float, float]]) -> tuple[float, float]:
    for start, end, value, rate in schedule:
        if start <= frame_idx < end:
            return value, rate
    last = schedule[-1]
    return last[2], last[3]


def schedule_heading(frame_idx: int, schedule: list[tuple[int, int, float]]) -> float:
    for start, end, value in schedule:
        if start <= frame_idx < end:
            return value
    return schedule[-1][2]


def load_header(source_csv: Path) -> list[str]:
    for encoding in ("utf-8-sig", "gb18030", "utf-8", "cp936"):
        try:
            with source_csv.open("r", encoding=encoding, newline="") as handle:
                header = next(csv.reader(handle))
                if len(header) < 51:
                    raise ValueError(f"Expected at least 51 columns in {source_csv}, got {len(header)}")
                return header
        except UnicodeDecodeError:
            continue
    raise UnicodeDecodeError("unknown", b"", 0, 1, f"Unable to decode {source_csv}")


def generate_segment(config: SegmentConfig, frames_per_segment: int) -> list[list[object]]:
    rows: list[list[object]] = []
    depth = 6.0
    heading = config.initial_heading
    roll = 0.0
    pitch = -8.0
    lon = config.start_lon
    lat = config.start_lat
    consumed_ah = 0.0
    battery = 96.0 - config.segment_id * 0.6
    progress = 0
    heading_schedule = build_heading_schedule(config.initial_heading)
    depth_schedule = build_depth_schedule(config.target_depth)

    for frame_idx in range(frames_per_segment):
        timestamp = config.start_time + timedelta(seconds=frame_idx * FRAME_INTERVAL_SECONDS)
        elapsed_min = int((frame_idx * FRAME_INTERVAL_SECONDS) // 60)
        planned_range = int(frame_idx * config.mean_speed * FRAME_INTERVAL_SECONDS)

        target_heading = schedule_heading(frame_idx, heading_schedule)
        heading_error = shortest_angle_delta(target_heading, heading)
        turn_bias = 0.55 if abs(heading_error) > 50 else 0.35
        heading = wrap_angle(heading + heading_error * turn_bias + random.uniform(-1.0, 1.0))
        turn_angle = clamp(heading_error * 0.9 + random.uniform(-4.0, 4.0), -55.0, 55.0)

        target_depth, vertical_rate = schedule_value(frame_idx, depth_schedule)
        depth_error = target_depth - depth
        depth = clamp(depth + depth_error * vertical_rate + random.uniform(-0.45, 0.45), 2.0, 180.0)
        altitude = max(2.0, 180.0 - depth + random.uniform(-0.8, 0.8))
        ctd_depth = max(0.0, depth + random.uniform(-0.8, 0.8))

        pitch_target = clamp(-14.0 if depth_error > 4 else (12.0 if depth_error < -4 else random.uniform(-4.0, 4.0)), -18.0, 18.0)
        pitch = clamp(pitch + (pitch_target - pitch) * 0.3 + random.uniform(-0.5, 0.5), -18.0, 18.0)
        roll_target = clamp(turn_angle * 0.18, -12.0, 12.0)
        roll = clamp(roll + (roll_target - roll) * 0.25 + random.uniform(-0.7, 0.7), -15.0, 15.0)

        base_speed = config.mean_speed + 0.07 * math.sin(frame_idx / 70.0) + random.uniform(-0.03, 0.03)
        current_u = config.current_scale * math.sin((frame_idx / 140.0) + config.segment_id * 0.9)
        current_v = config.current_scale * 0.8 * math.cos((frame_idx / 180.0) + config.segment_id * 0.6)
        vx = base_speed * math.cos(math.radians(heading)) + current_u
        vy = base_speed * math.sin(math.radians(heading)) + current_v
        meters_per_deg_lat = 111320.0
        meters_per_deg_lon = meters_per_deg_lat * math.cos(math.radians(lat))
        lon += (vx * FRAME_INTERVAL_SECONDS) / meters_per_deg_lon
        lat += (vy * FRAME_INTERVAL_SECONDS) / meters_per_deg_lat

        voltage = clamp(28.8 - frame_idx / 12000.0 + random.uniform(-0.08, 0.05), 26.9, 29.1)
        current = clamp(0.45 + abs(turn_angle) / 120.0 + abs(depth_error) / 260.0 + random.uniform(-0.05, 0.06), 0.22, 1.3)
        consumed_ah += current * (FRAME_INTERVAL_SECONDS / 3600.0)
        battery = clamp(battery - current / 2600.0, 46.0, 98.0)
        piston = int(clamp((target_depth - depth) * 2.2 + random.uniform(-8.0, 8.0), -48, 48))
        progress = (progress + random.choice([0, 0, 1])) % 101

        rows.append([
            format_timestamp(timestamp),
            config.segment_id,
            f"{voltage:.1f}",
            f"{current:.1f}",
            f"{consumed_ah:.2f}",
            random.choice([32, 48, 64]),
            random.choice([10, 14, 22, 30, 38, 46, 54]),
            0,
            random.choice([3, 7]),
            random.choice([24, 88, 90]),
            random.choice([3, 11]),
            0,
            0,
            "underwater_mode",
            "descending" if depth_error > 3 else ("ascending" if depth_error < -3 else "cruising"),
            planned_range,
            elapsed_min,
            f"{turn_angle:.0f}",
            piston,
            random.choice([0, 0, 0, 8]),
            random.choice([0, 16, 32, 48]),
            f"{lon:.8f}",
            f"{lat:.8f}",
            f"{depth:.1f}",
            f"{altitude:.1f}",
            f"{heading:.1f}",
            f"{pitch:.0f}",
            f"{roll:.0f}",
            int(clamp(260 + base_speed * 160 + abs(turn_angle) * 4, 180, 820)),
            config.segment_id,
            f"{target_heading:.1f}",
            f"{target_depth:.1f}",
            f"{altitude + random.uniform(-3.0, 3.0):.1f}",
            f"{clamp(17.5 + depth / 40.0 + random.uniform(-1.2, 1.0), 15.0, 29.5):.1f}",
            f"{clamp(71.0 + depth * 0.06 + random.uniform(-1.8, 1.8), 68.0, 86.0):.1f}",
            f"{clamp(2.88 + depth * 0.0019 + random.uniform(-0.05, 0.05), 2.76, 3.35):.4f}",
            f"{clamp(26.7 - depth * 0.024 + random.uniform(-0.25, 0.25), 4.8, 27.2):.4f}",
            f"{ctd_depth:.2f}",
            progress,
            random.randint(10, 64),
            int(clamp(9000 + abs(depth_error) * 320 + abs(turn_angle) * 45 + random.uniform(-600, 600), 7500, 22000)),
            f"{battery:.0f}",
            random.choice([30, 31, 89]),
            int(clamp(1800 + 900 * math.sin(frame_idx / 60.0 + config.segment_id), 0, 26000)),
            int(clamp(1200 + 850 * math.cos(frame_idx / 72.0 + config.segment_id * 0.2), 0, 26000)),
            int(clamp(1600 + 980 * math.sin(frame_idx / 91.0 + config.segment_id * 0.4), 0, 26000)),
            int(clamp(2500 + depth * 70 + random.uniform(-450, 450), 0, 34000)),
            int(clamp(2200 + depth * 62 + random.uniform(-420, 420), 0, 34000)),
            int(clamp(2000 + depth * 58 + random.uniform(-420, 420), 0, 34000)),
            int(clamp(500 + depth * 12 + random.uniform(-220, 220), 0, 32000)),
            f"{clamp(1550 + max(0.0, 125 - depth) * 18 + random.uniform(-120, 120), 900, 3800):.0f}",
        ])

    return rows


def build_configs(segment_count: int) -> list[SegmentConfig]:
    configs: list[SegmentConfig] = []
    base_time = datetime(2026, 5, 1, 0, 0, 0)
    for segment_id in range(1, segment_count + 1):
        configs.append(
            SegmentConfig(
                segment_id=segment_id,
                start_time=base_time + timedelta(hours=(segment_id - 1) * 9),
                start_lon=119.6 + segment_id * 0.18 + random.uniform(-0.03, 0.03),
                start_lat=24.2 + segment_id * 0.11 + random.uniform(-0.03, 0.03),
                target_depth=55.0 + (segment_id % 6) * 14.0 + random.uniform(-6.0, 6.0),
                current_scale=0.05 + (segment_id % 5) * 0.025,
                mean_speed=0.42 + (segment_id % 4) * 0.04,
                initial_heading=wrap_angle(35.0 + segment_id * 27.0 + random.uniform(-12.0, 12.0)),
            )
        )
    return configs


def main() -> None:
    args = parse_args()
    source_csv = Path(args.source).resolve()
    output_csv = Path(args.output).resolve()
    random.seed(args.seed)

    header = load_header(source_csv)
    configs = build_configs(args.segments)
    output_csv.parent.mkdir(parents=True, exist_ok=True)
    with output_csv.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(header)
        for config in configs:
            writer.writerows(generate_segment(config, args.frames_per_segment))

    print(f"Wrote {output_csv}")
    print(f"Segments: {args.segments}")
    print(f"Frames: {args.segments * args.frames_per_segment}")


if __name__ == "__main__":
    main()

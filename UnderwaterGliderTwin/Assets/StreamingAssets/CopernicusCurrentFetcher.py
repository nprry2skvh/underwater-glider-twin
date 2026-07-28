#!/usr/bin/env python3
import argparse
import json
import math
import os
import sys
import tempfile
from datetime import datetime, timedelta, timezone
from pathlib import Path


def parse_args():
    parser = argparse.ArgumentParser(description="Fetch layered ocean currents from Copernicus Marine.")
    parser.add_argument("--request", help="Path to the Unity-generated request JSON.")
    parser.add_argument("--output", required=True, help="Path for the current-profile JSON result.")
    parser.add_argument("--convert", help="Convert a local NetCDF4/HDF5 file to the Unity current JSON schema.")
    return parser.parse_args()


def ensure_credentials_available():
    if os.environ.get("COPERNICUSMARINE_SERVICE_USERNAME") and os.environ.get("COPERNICUSMARINE_SERVICE_PASSWORD"):
        return
    credential_files = [
        Path.home() / ".copernicusmarine" / ".copernicusmarine-credentials",
        Path.home() / ".netrc",
        Path.home() / "_netrc",
    ]
    if not any(path.is_file() for path in credential_files):
        raise RuntimeError("Copernicus credentials are not configured. Run: copernicusmarine login")


def finite_mean(values):
    finite_values = [float(value) for value in values.reshape(-1) if math.isfinite(float(value))]
    return sum(finite_values) / len(finite_values) if finite_values else None


def coordinate_name(dataset, preferred_name):
    if preferred_name in dataset.coords:
        return preferred_name
    raise ValueError(f"Copernicus response has no '{preferred_name}' coordinate.")


def build_layers(dataset):
    import numpy as np

    longitude_name = coordinate_name(dataset, "longitude")
    latitude_name = coordinate_name(dataset, "latitude")
    selected = dataset.sel(
        {
            longitude_name: dataset.attrs["requested_longitude"],
            latitude_name: dataset.attrs["requested_latitude"],
        },
        method="nearest",
    )
    if "time" in selected.dims:
        selected = selected.isel(time=-1)

    if "depth" not in selected.coords:
        raise ValueError("Copernicus response has no depth coordinate.")

    depths = np.asarray(selected["depth"].values, dtype=float).reshape(-1)
    samples = []
    for index, depth in enumerate(depths):
        u_value = finite_mean(np.asarray(selected["uo"].isel(depth=index).values, dtype=float))
        v_value = finite_mean(np.asarray(selected["vo"].isel(depth=index).values, dtype=float))
        if u_value is not None and v_value is not None:
            samples.append((max(0.0, float(depth)), u_value, v_value))

    if not samples:
        raise ValueError("Copernicus returned no usable uo/vo values for this location.")

    layers = []
    for index, (depth, eastward, northward) in enumerate(samples):
        lower_bound = 0.0 if index == 0 else (samples[index - 1][0] + depth) / 2.0
        if index + 1 < len(samples):
            upper_bound = (depth + samples[index + 1][0]) / 2.0
        elif index > 0:
            upper_bound = depth + (depth - samples[index - 1][0]) / 2.0
        else:
            upper_bound = depth + 1.0
        layers.append(
            {
                "minDepthM": lower_bound,
                "maxDepthM": upper_bound,
                "eastwardMps": eastward,
                "northwardMps": northward,
            }
        )
    return layers


def build_field_samples(dataset, reference_time):
    import numpy as np

    longitude_name = coordinate_name(dataset, "longitude")
    latitude_name = coordinate_name(dataset, "latitude")
    if "depth" not in dataset.coords:
        raise ValueError("Copernicus response has no depth coordinate.")

    table = dataset[["uo", "vo"]].to_dataframe().reset_index()
    samples = []
    reference = np.datetime64(reference_time.replace(tzinfo=None))
    for _, row in table.iterrows():
        eastward = row["uo"]
        northward = row["vo"]
        if not math.isfinite(float(eastward)) or not math.isfinite(float(northward)):
            continue

        elapsed_seconds = 0.0
        if "time" in row:
            elapsed_seconds = float((np.datetime64(row["time"]) - reference) / np.timedelta64(1, "s"))
        samples.append(
            {
                "longitudeDeg": float(row[longitude_name]),
                "latitudeDeg": float(row[latitude_name]),
                "depthM": max(0.0, float(row["depth"])),
                "elapsedSeconds": elapsed_seconds,
                "eastwardMps": float(eastward),
                "northwardMps": float(northward),
            }
        )

    if not samples:
        raise ValueError("Copernicus returned no usable spatial current samples for this region.")
    return samples


def convert_local_netcdf(input_path, output_path):
    """Fallback invoked by Unity only when its bounded classic-NetCDF reader rejects HDF5."""
    import xarray as xr

    aliases = {
        "lon": "longitude", "x": "longitude", "lat": "latitude", "y": "latitude",
        "lev": "depth", "z": "depth", "u": "uo", "eastward_sea_water_velocity": "uo",
        "v": "vo", "northward_sea_water_velocity": "vo",
    }
    with xr.open_dataset(input_path) as source:
        rename = {name: aliases[name] for name in source.variables if name in aliases and aliases[name] not in source.variables}
        dataset = source.rename(rename)
        for required in ("longitude", "latitude", "depth", "uo", "vo"):
            if required not in dataset:
                raise ValueError(f"Local NetCDF has no unambiguous '{required}' variable.")
        dataset.attrs["requested_longitude"] = float(dataset["longitude"].values.reshape(-1)[0])
        dataset.attrs["requested_latitude"] = float(dataset["latitude"].values.reshape(-1)[0])
        now = datetime.now(timezone.utc)
        response = {
            "source": "Local NetCDF conversion",
            "datasetId": Path(input_path).name,
            "retrievedAtUtc": now.isoformat(),
            "layers": build_layers(dataset),
            "fieldSamples": build_field_samples(dataset, now),
        }
    Path(output_path).write_text(json.dumps(response, separators=(",", ":")), encoding="utf-8")


def main():
    args = parse_args()
    if args.convert:
        convert_local_netcdf(args.convert, args.output)
        return
    if not args.request:
        raise ValueError("--request is required unless --convert is used.")
    request = json.loads(Path(args.request).read_text(encoding="utf-8"))
    try:
        import copernicusmarine
        import xarray as xr
    except ImportError as error:
        raise RuntimeError(
            "Copernicus Marine toolbox is not installed. Run: python -m pip install copernicusmarine, then copernicusmarine login."
        ) from error

    ensure_credentials_available()

    now = datetime.now(timezone.utc)
    forecast_hours = max(0.0, float(request.get("forecastHours", 0.0)))
    minimum_longitude = float(request.get("minimumLongitudeDeg", request["longitudeDeg"]))
    maximum_longitude = float(request.get("maximumLongitudeDeg", request["longitudeDeg"]))
    minimum_latitude = float(request.get("minimumLatitudeDeg", request["latitudeDeg"]))
    maximum_latitude = float(request.get("maximumLatitudeDeg", request["latitudeDeg"]))
    with tempfile.TemporaryDirectory(prefix="copernicus-current-") as temporary_directory:
        output_name = "current.nc"
        copernicusmarine.subset(
            dataset_id=request.get("datasetId") or "cmems_mod_glo_phy-cur_anfc_0.083deg_P1D-m",
            variables=["uo", "vo"],
            minimum_longitude=minimum_longitude,
            maximum_longitude=maximum_longitude,
            minimum_latitude=minimum_latitude,
            maximum_latitude=maximum_latitude,
            minimum_depth=float(request["minDepthM"]),
            maximum_depth=float(request["maxDepthM"]),
            start_datetime=(now - timedelta(hours=1)).isoformat(),
            end_datetime=(now + timedelta(hours=forecast_hours)).isoformat(),
            coordinates_selection_method="nearest",
            output_directory=temporary_directory,
            output_filename=output_name,
            overwrite=True,
            disable_progress_bar=True,
        )
        netcdf_path = Path(temporary_directory) / output_name
        if not netcdf_path.exists():
            netcdf_files = list(Path(temporary_directory).glob("*.nc"))
            if not netcdf_files:
                raise RuntimeError("Copernicus toolbox did not create a NetCDF response.")
            netcdf_path = netcdf_files[0]

        with xr.open_dataset(netcdf_path) as dataset:
            dataset.attrs["requested_longitude"] = float(request["longitudeDeg"])
            dataset.attrs["requested_latitude"] = float(request["latitudeDeg"])
            layers = build_layers(dataset)
            field_samples = build_field_samples(dataset, now)

    response = {
        "source": "Copernicus Marine",
        "datasetId": request.get("datasetId") or "cmems_mod_glo_phy-cur_anfc_0.083deg_P1D-m",
        "retrievedAtUtc": datetime.now(timezone.utc).isoformat(),
        "layers": layers,
        "fieldSamples": field_samples,
    }
    Path(args.output).write_text(json.dumps(response, separators=(",", ":")), encoding="utf-8")


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace UnderwaterGliderTwin.Telemetry
{
    /// <summary>Small, dependency-free reader for the classic NetCDF files accepted by the local-file workflow.</summary>
    public static class NetCdfClassicCurrentReader
    {
        private const int NcDimension = 10;
        private const int NcVariable = 11;
        private const int NcAttribute = 12;

        public static bool TryRead(string path, DateTime referenceTimeUtc, out CopernicusCurrentResult result, out string error)
        {
            result = null;
            error = string.Empty;
            try
            {
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream))
                {
                    var model = ReadModel(reader);
                    CurrentModel = model;
                    var u = FindVelocity(model, true);
                    var v = FindVelocity(model, false);
                    if (u == null || v == null) throw new InvalidDataException("NetCDF requires unambiguous eastward (uo/u) and northward (vo/v) variables.");
                    if (!SameDimensions(u, v)) throw new InvalidDataException("NetCDF u and v variables must use the same dimensions.");

                    var axes = ResolveAxes(model, u);
                    ValidateCoordinateUnits(axes);
                    var timeIndex = axes.Time == null ? 0 : NearestTimeIndex(ReadValues(reader, axes.Time), axes.Time, referenceTimeUtc);
                    var uValues = ReadValues(reader, u);
                    var vValues = ReadValues(reader, v);
                    var w = FindOptionalVerticalVelocity(model, u);
                    var wValues = w == null ? null : ReadValues(reader, w);
                    var longitude = ReadValues(reader, axes.Longitude);
                    var latitude = ReadValues(reader, axes.Latitude);
                    var depth = ReadValues(reader, axes.Depth);
                    var longitudeOrder = OrderedIndices(longitude, true);
                    var latitudeOrder = OrderedIndices(latitude, false);
                    var depthOrder = OrderedIndices(depth, false);
                    var samples = new List<OceanCurrentFieldSample>();
                    var totals = new float[depth.Length];
                    var eastSums = new float[depth.Length];
                    var northSums = new float[depth.Length];
                    for (var depthOrderIndex = 0; depthOrderIndex < depthOrder.Length; depthOrderIndex++)
                    for (var latitudeOrderIndex = 0; latitudeOrderIndex < latitudeOrder.Length; latitudeOrderIndex++)
                    for (var longitudeOrderIndex = 0; longitudeOrderIndex < longitudeOrder.Length; longitudeOrderIndex++)
                    {
                        var depthIndex = depthOrder[depthOrderIndex];
                        var latitudeIndex = latitudeOrder[latitudeOrderIndex];
                        var longitudeIndex = longitudeOrder[longitudeOrderIndex];
                        var index = IndexFor(u, axes, timeIndex, depthIndex, latitudeIndex, longitudeIndex);
                        var eastward = ConvertVelocity(uValues[index], u);
                        var northward = ConvertVelocity(vValues[index], v);
                        if (double.IsNaN(eastward) || double.IsNaN(northward) || double.IsInfinity(eastward) || double.IsInfinity(northward)) continue;
                        var vertical = wValues == null ? 0d : ConvertVelocity(wValues[index], w);
                        if (double.IsNaN(vertical) || double.IsInfinity(vertical)) vertical = 0d;
                        samples.Add(new OceanCurrentFieldSample(NormalizeLongitude(longitude[longitudeIndex]), latitude[latitudeIndex], (float)Math.Max(0d, depth[depthIndex]), 0f, (float)eastward, (float)northward, (float)vertical));
                        eastSums[depthIndex] += (float)eastward;
                        northSums[depthIndex] += (float)northward;
                        totals[depthIndex]++;
                    }
                    if (samples.Count == 0) throw new InvalidDataException("NetCDF has no finite u/v current samples.");
                    var layers = new List<OceanCurrentLayer>();
                    for (var index = 0; index < depthOrder.Length; index++)
                    {
                        var sourceIndex = depthOrder[index];
                        if (totals[sourceIndex] <= 0f) continue;
                        var currentDepth = (float)depth[sourceIndex];
                        var lower = index == 0 ? 0f : ((float)depth[depthOrder[index - 1]] + currentDepth) * 0.5f;
                        var upper = index + 1 < depthOrder.Length ? (currentDepth + (float)depth[depthOrder[index + 1]]) * 0.5f : currentDepth + (index == 0 ? 1f : (currentDepth - (float)depth[depthOrder[index - 1]]) * .5f);
                        layers.Add(new OceanCurrentLayer(Math.Max(0f, lower), Math.Max(lower, upper), eastSums[sourceIndex] / totals[sourceIndex], northSums[sourceIndex] / totals[sourceIndex]));
                    }
                    var normalized = new CopernicusCurrentResult("Local NetCDF", Path.GetFileName(path), referenceTimeUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture), new OceanCurrentProfile(layers), new OceanCurrentField(samples));
                    // NetCDF crosses the same validated schema boundary as JSON and Python-converted files.
                    result = CopernicusCurrentResponseParser.Parse(CopernicusCurrentResponseParser.Serialize(normalized));
                    return true;
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static bool IsUnsupportedFormat(string error)
        {
            return !string.IsNullOrEmpty(error) && error.StartsWith("Unsupported NetCDF", StringComparison.Ordinal);
        }

        private static int[] OrderedIndices(double[] values, bool normalizeLongitude)
        {
            return Enumerable.Range(0, values.Length).OrderBy(index => normalizeLongitude ? NormalizeLongitude(values[index]) : values[index]).ToArray();
        }

        private static double NormalizeLongitude(double longitude)
        {
            var normalized = longitude % 360d;
            return normalized > 180d ? normalized - 360d : normalized <= -180d ? normalized + 360d : normalized;
        }

        private static Model ReadModel(BinaryReader reader)
        {
            var magic = new string(reader.ReadChars(3));
            if (magic != "CDF")
            {
                if (magic == "\x89HD") throw new InvalidDataException("Unsupported NetCDF4/HDF5 format.");
                throw new InvalidDataException("Unsupported NetCDF format.");
            }
            var version = reader.ReadByte();
            if (version != 1 && version != 2) throw new InvalidDataException("Unsupported NetCDF format version.");
            ReadUInt32(reader); // numrecs; unlimited dimensions are deliberately not accepted for a bounded reader.
            var model = new Model { Is64BitOffset = version == 2 };
            ReadDimensions(reader, model);
            SkipAttributes(reader);
            ReadVariables(reader, model);
            return model;
        }

        private static void ReadDimensions(BinaryReader reader, Model model)
        {
            var tag = ReadInt32(reader);
            if (tag == 0) return;
            if (tag != NcDimension) throw new InvalidDataException("Malformed NetCDF dimension list.");
            var count = CheckedCount(ReadInt32(reader));
            for (var index = 0; index < count; index++) model.Dimensions.Add(new Dimension { Name = ReadName(reader), Length = CheckedCount(ReadInt32(reader)) });
        }

        private static void ReadVariables(BinaryReader reader, Model model)
        {
            var tag = ReadInt32(reader);
            if (tag == 0) return;
            if (tag != NcVariable) throw new InvalidDataException("Malformed NetCDF variable list.");
            var count = CheckedCount(ReadInt32(reader));
            for (var index = 0; index < count; index++)
            {
                var variable = new Variable { Name = ReadName(reader) };
                var dimensionCount = CheckedCount(ReadInt32(reader));
                for (var dimension = 0; dimension < dimensionCount; dimension++) variable.DimensionIds.Add(CheckedCount(ReadInt32(reader)));
                variable.Attributes = ReadAttributes(reader);
                variable.Type = ReadInt32(reader);
                variable.Size = ReadUInt32(reader);
                variable.Begin = model.Is64BitOffset ? (long)ReadUInt64(reader) : ReadUInt32(reader);
                model.Variables.Add(variable);
            }
        }

        private static void SkipAttributes(BinaryReader reader) { ReadAttributes(reader); }
        private static Dictionary<string, string> ReadAttributes(BinaryReader reader)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var tag = ReadInt32(reader);
            if (tag == 0) return values;
            if (tag != NcAttribute) throw new InvalidDataException("Malformed NetCDF attributes.");
            var count = CheckedCount(ReadInt32(reader));
            for (var index = 0; index < count; index++)
            {
                var name = ReadName(reader);
                var type = ReadInt32(reader);
                var length = CheckedCount(ReadInt32(reader));
                var bytes = CheckedByteLength(type, length);
                var raw = reader.ReadBytes(bytes);
                if (raw.Length != bytes) throw new EndOfStreamException();
                SkipPadding(reader, bytes);
                if (type == 2)
                {
                    values[name] = System.Text.Encoding.ASCII.GetString(raw).TrimEnd('\0');
                }
                else
                {
                    using (var numericReader = new BinaryReader(new MemoryStream(raw, false)))
                    {
                        var numeric = ReadNumeric(numericReader, type, length);
                        if (numeric.Length > 0)
                        {
                            values[name] = numeric[0].ToString("R", CultureInfo.InvariantCulture);
                        }
                    }
                }
            }
            return values;
        }

        private static Axes ResolveAxes(Model model, Variable velocity)
        {
            var axes = new Axes();
            foreach (var variable in model.Variables.Where(value => value.DimensionIds.Count == 1))
            {
                var dimension = variable.DimensionIds[0];
                if (Matches(variable, "longitude", "lon", "x")) axes.Longitude = PickAxis(axes.Longitude, variable, "longitude");
                if (Matches(variable, "latitude", "lat", "y")) axes.Latitude = PickAxis(axes.Latitude, variable, "latitude");
                if (Matches(variable, "depth", "lev", "z")) axes.Depth = PickAxis(axes.Depth, variable, "depth");
                if (Matches(variable, "time")) axes.Time = PickAxis(axes.Time, variable, "time");
            }
            if (axes.Longitude == null || axes.Latitude == null || axes.Depth == null) throw new InvalidDataException("NetCDF requires named longitude, latitude, and depth coordinate variables.");
            var requiredDimensions = new List<int> { axes.Longitude.DimensionIds[0], axes.Latitude.DimensionIds[0], axes.Depth.DimensionIds[0] };
            if (axes.Time != null) requiredDimensions.Add(axes.Time.DimensionIds[0]);
            if (velocity.DimensionIds.Count != requiredDimensions.Count || requiredDimensions.Any(id => !velocity.DimensionIds.Contains(id))) throw new InvalidDataException("NetCDF current dimensions must be [depth,lat,lon] or [time,depth,lat,lon].");
            return axes;
        }

        private static Variable PickAxis(Variable prior, Variable candidate, string axis)
        {
            if (prior != null && prior != candidate) throw new InvalidDataException("NetCDF contains ambiguous " + axis + " coordinates.");
            return candidate;
        }

        private static bool Matches(Variable variable, params string[] names)
        {
            var standardName = Attribute(variable, "standard_name");
            return names.Any(name => string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(standardName, name, StringComparison.OrdinalIgnoreCase));
        }

        private static Variable FindVelocity(Model model, bool eastward)
        {
            var aliases = eastward ? new[] { "uo", "u", "eastward_sea_water_velocity" } : new[] { "vo", "v", "northward_sea_water_velocity" };
            var candidates = model.Variables.Where(value => aliases.Any(alias => string.Equals(value.Name, alias, StringComparison.OrdinalIgnoreCase) || string.Equals(Attribute(value, "standard_name"), alias, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (candidates.Length > 1) throw new InvalidDataException("NetCDF contains ambiguous " + (eastward ? "eastward" : "northward") + " velocity variables.");
            return candidates.Length == 1 ? candidates[0] : null;
        }

        private static Variable FindOptionalVerticalVelocity(Model model, Variable u)
        {
            var candidates = model.Variables.Where(value => string.Equals(value.Name, "wo", StringComparison.OrdinalIgnoreCase) || string.Equals(value.Name, "w", StringComparison.OrdinalIgnoreCase) || string.Equals(Attribute(value, "standard_name"), "upward_sea_water_velocity", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (candidates.Length > 1) throw new InvalidDataException("NetCDF contains ambiguous vertical velocity variables.");
            if (candidates.Length == 1 && !SameDimensions(candidates[0], u)) throw new InvalidDataException("NetCDF vertical velocity dimensions differ from u/v.");
            return candidates.Length == 1 ? candidates[0] : null;
        }

        private static void ValidateCoordinateUnits(Axes axes)
        {
            ValidateUnits(axes.Longitude, new[] { "degrees_east", "degree_east", "degrees_e", "degree_e" }, "longitude");
            ValidateUnits(axes.Latitude, new[] { "degrees_north", "degree_north", "degrees_n", "degree_n" }, "latitude");
            ValidateUnits(axes.Depth, new[] { "m", "meter", "meters", "metre", "metres" }, "depth");
            if (axes.Time != null && !Attribute(axes.Time, "units").StartsWith("seconds since", StringComparison.OrdinalIgnoreCase) && !Attribute(axes.Time, "units").StartsWith("hours since", StringComparison.OrdinalIgnoreCase) && !Attribute(axes.Time, "units").StartsWith("days since", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("NetCDF time uses unknown units.");
        }

        private static void ValidateUnits(Variable variable, IEnumerable<string> expected, string name)
        {
            var units = Attribute(variable, "units");
            if (!string.IsNullOrEmpty(units) && !expected.Contains(units.Trim().ToLowerInvariant())) throw new InvalidDataException("NetCDF " + name + " uses unknown units '" + units + "'.");
        }

        private static int NearestTimeIndex(double[] values, Variable time, DateTime referenceTimeUtc)
        {
            var units = Attribute(time, "units");
            var index = units.IndexOf(" since ", StringComparison.OrdinalIgnoreCase);
            var unit = units.Substring(0, index).Trim().ToLowerInvariant();
            DateTime origin;
            if (!DateTime.TryParse(units.Substring(index + 7), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out origin)) throw new InvalidDataException("NetCDF time origin is invalid.");
            var best = 0;
            var smallest = double.PositiveInfinity;
            for (var valueIndex = 0; valueIndex < values.Length; valueIndex++)
            {
                var timeValue = unit == "seconds" ? origin.AddSeconds(values[valueIndex]) : unit == "hours" ? origin.AddHours(values[valueIndex]) : origin.AddDays(values[valueIndex]);
                var distance = Math.Abs((timeValue - referenceTimeUtc.ToUniversalTime()).TotalSeconds);
                if (distance < smallest) { best = valueIndex; smallest = distance; }
            }
            return best;
        }

        private static double[] ReadValues(BinaryReader reader, Variable variable)
        {
            reader.BaseStream.Seek(variable.Begin, SeekOrigin.Begin);
            var elementCount = variable.DimensionIds.Aggregate(1, (total, id) => checked(total * CurrentModel.Dimensions[id].Length));
            return ReadNumeric(reader, variable.Type, elementCount);
        }
        private static Model CurrentModel;
        private static int IndexFor(Variable variable, Axes axes, int time, int depth, int latitude, int longitude)
        {
            var values = new Dictionary<int, int> { { axes.Longitude.DimensionIds[0], longitude }, { axes.Latitude.DimensionIds[0], latitude }, { axes.Depth.DimensionIds[0], depth } };
            if (axes.Time != null) values[axes.Time.DimensionIds[0]] = time;
            var index = 0;
            foreach (var dimension in variable.DimensionIds) index = index * CurrentModel.Dimensions[dimension].Length + values[dimension];
            return index;
        }

        private static double[] ReadNumeric(BinaryReader reader, int type, int count)
        {
            var result = new double[count];
            for (var index = 0; index < count; index++) result[index] = type == 5 ? ReadSingle(reader) : type == 6 ? ReadDouble(reader) : type == 4 ? ReadInt32(reader) : type == 3 ? ReadInt16(reader) : type == 1 ? reader.ReadSByte() : throw new InvalidDataException("NetCDF variable type is unsupported.");
            return result;
        }
        private static double ConvertVelocity(double value, Variable variable)
        {
            if (IsFillValue(value, variable)) return double.NaN;
            value = value * NumericAttribute(variable, "scale_factor", 1d) + NumericAttribute(variable, "add_offset", 0d);
            var units = Attribute(variable, "units").Trim().ToLowerInvariant();
            if (units.Length == 0 || units == "m/s" || units == "m s-1" || units == "m s^-1") return value;
            if (units == "cm/s" || units == "cm s-1" || units == "cm s^-1") return value / 100d;
            if (units == "knot" || units == "knots" || units == "kt") return value * 0.514444d;
            throw new InvalidDataException("NetCDF velocity uses unknown units '" + units + "'.");
        }
        private static bool IsFillValue(double value, Variable variable)
        {
            return NumericAttribute(variable, "_FillValue", double.NaN) == value
                || NumericAttribute(variable, "missing_value", double.NaN) == value;
        }
        private static double NumericAttribute(Variable variable, string name, double fallback)
        {
            var raw = Attribute(variable, name);
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        }
        private static string Attribute(Variable variable, string name) { string value; return variable.Attributes.TryGetValue(name, out value) ? value ?? string.Empty : string.Empty; }
        private static bool SameDimensions(Variable first, Variable second) { return first.DimensionIds.SequenceEqual(second.DimensionIds); }
        private static string ReadName(BinaryReader reader) { var length = CheckedCount(ReadInt32(reader)); var bytes = reader.ReadBytes(length); if (bytes.Length != length) throw new EndOfStreamException(); SkipPadding(reader, length); return System.Text.Encoding.ASCII.GetString(bytes); }
        private static void SkipPadding(BinaryReader reader, int bytes) { var padding = (4 - bytes % 4) % 4; if (padding > 0) reader.ReadBytes(padding); }
        private static int CheckedByteLength(int type, int count) { return checked(ElementSize(type) * count); }
        private static int ElementSize(int type) { return type == 1 || type == 2 ? 1 : type == 3 ? 2 : type == 4 || type == 5 ? 4 : type == 6 ? 8 : throw new InvalidDataException("NetCDF type is unsupported."); }
        private static int CheckedCount(int value) { if (value < 0) throw new InvalidDataException("NetCDF count is invalid."); return value; }
        private static ushort ReadUInt16(BinaryReader reader) { var bytes = reader.ReadBytes(2); if (bytes.Length != 2) throw new EndOfStreamException(); return (ushort)((bytes[0] << 8) | bytes[1]); }
        private static short ReadInt16(BinaryReader reader) { return unchecked((short)ReadUInt16(reader)); }
        private static uint ReadUInt32(BinaryReader reader) { var bytes = reader.ReadBytes(4); if (bytes.Length != 4) throw new EndOfStreamException(); return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3]; }
        private static int ReadInt32(BinaryReader reader) { return unchecked((int)ReadUInt32(reader)); }
        private static ulong ReadUInt64(BinaryReader reader) { return ((ulong)ReadUInt32(reader) << 32) | ReadUInt32(reader); }
        private static float ReadSingle(BinaryReader reader) { return BitConverter.Int32BitsToSingle(ReadInt32(reader)); }
        private static double ReadDouble(BinaryReader reader) { return BitConverter.Int64BitsToDouble(unchecked((long)ReadUInt64(reader))); }

        private sealed class Model { public bool Is64BitOffset; public readonly List<Dimension> Dimensions = new List<Dimension>(); public readonly List<Variable> Variables = new List<Variable>(); }
        private sealed class Dimension { public string Name; public int Length; }
        private sealed class Variable { public string Name; public readonly List<int> DimensionIds = new List<int>(); public Dictionary<string, string> Attributes; public int Type; public uint Size; public long Begin; }
        private sealed class Axes { public Variable Longitude; public Variable Latitude; public Variable Depth; public Variable Time; }
    }
}

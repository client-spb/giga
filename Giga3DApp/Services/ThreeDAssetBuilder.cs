using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Giga3DApp.Models;

namespace Giga3DApp.Services;

public static class ThreeDAssetBuilder
{
    public static async Task<string> BuildFromPromptAsync(string promptText, string modelId, GigaChatClient client, string outputDirectory)
    {
        var responseJson = await client.GenerateAssetSpecAsync(modelId, promptText);
        return BuildFromGeneratedSpec(responseJson, outputDirectory);
    }

    public static string BuildFromGeneratedSpec(string responseJson, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var spec = JsonSerializer.Deserialize<GeneratedAssetSpec>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new GeneratedAssetSpec();

        var assetName = SanitizeFileName(string.IsNullOrWhiteSpace(spec.AssetName) ? "GeneratedAsset" : spec.AssetName);
        var objectName = string.IsNullOrWhiteSpace(spec.BaseShape) ? "box" : spec.BaseShape.ToLowerInvariant();

        var texturePath = Path.Combine(outputDirectory, $"{assetName}_texture.png");
        GenerateTexture(texturePath, spec.Colors);

        var objPath = Path.Combine(outputDirectory, $"{assetName}.obj");
        var mtlPath = Path.Combine(outputDirectory, $"{assetName}.mtl");

        var objContent = BuildObjContent(spec, texturePath);

        File.WriteAllText(objPath, objContent, Encoding.UTF8);
        File.WriteAllText(mtlPath, BuildMtl(texturePath), Encoding.UTF8);
        ExportGltf(responseJson, outputDirectory, assetName);

        return objPath;
    }

    public static ModelVisual3D CreatePreviewModel(string responseJson)
    {
        var spec = JsonSerializer.Deserialize<GeneratedAssetSpec>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new GeneratedAssetSpec();

        var mesh = BuildMesh(spec);
        var material = new DiffuseMaterial(new SolidColorBrush(ParseColor(spec.Colors.FirstOrDefault() ?? "#5B8DEF")));
        var model = new GeometryModel3D(mesh, material)
        {
            BackMaterial = material
        };

        return new ModelVisual3D { Content = model };
    }

    public static void ExportGltf(string responseJson, string outputDirectory, string assetName)
    {
        var spec = JsonSerializer.Deserialize<GeneratedAssetSpec>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new GeneratedAssetSpec();

        var mesh = BuildMesh(spec);
        var positions = new List<float>();
        var indices = new List<uint>();
        var normals = new List<float>();

        for (var i = 0; i < mesh.Positions.Count; i++)
        {
            var p = mesh.Positions[i];
            positions.Add((float)p.X);
            positions.Add((float)p.Y);
            positions.Add((float)p.Z);
            normals.Add(0f);
            normals.Add(1f);
            normals.Add(0f);
        }

        for (var i = 0; i < mesh.TriangleIndices.Count; i++)
            indices.Add((uint)mesh.TriangleIndices[i]);

        var buffer = new List<byte>();
        foreach (var value in positions)
            buffer.AddRange(BitConverter.GetBytes(value));
        foreach (var value in normals)
            buffer.AddRange(BitConverter.GetBytes(value));
        foreach (var value in indices)
            buffer.AddRange(BitConverter.GetBytes(value));

        var gltfFile = Path.Combine(outputDirectory, $"{assetName}.gltf");
        var binFile = Path.Combine(outputDirectory, $"{assetName}.bin");

        File.WriteAllBytes(binFile, buffer.ToArray());

        var gltf = new
        {
            asset = new { version = "2.0", generator = "GigaChat 3D Asset Generator Pro" },
            scene = 0,
            scenes = new[] { new { name = assetName, nodes = new[] { 0 } } },
            nodes = new[] { new { mesh = 0 } },
            meshes = new[]
            {
                new
                {
                    primitives = new[]
                    {
                        new
                        {
                            attributes = new { POSITION = 0, NORMAL = 1 },
                            indices = 2,
                            material = 0
                        }
                    }
                }
            },
            materials = new[]
            {
                new
                {
                    pbrMetallicRoughness = new
                    {
                        baseColorFactor = new[] { 1.0, 1.0, 1.0, 1.0 },
                        metallicFactor = 0.0,
                        roughnessFactor = 0.8
                    },
                    doubleSided = true
                }
            },
            buffers = new[] { new { uri = $"{assetName}.bin", byteLength = buffer.Count } },
            bufferViews = new[]
            {
                new { buffer = 0, byteOffset = 0, byteLength = positions.Count * sizeof(float), target = 34962 },
                new { buffer = 0, byteOffset = positions.Count * sizeof(float), byteLength = normals.Count * sizeof(float), target = 34962 },
                new { buffer = 0, byteOffset = (positions.Count + normals.Count) * sizeof(float), byteLength = indices.Count * sizeof(uint), target = 34963 }
            },
            accessors = new[]
            {
                new { bufferView = 0, componentType = 5126, count = positions.Count / 3, type = "VEC3" },
                new { bufferView = 1, componentType = 5126, count = normals.Count / 3, type = "VEC3" },
                new { bufferView = 2, componentType = 5125, count = indices.Count, type = "SCALAR" }
            }
        };

        File.WriteAllText(gltfFile, JsonSerializer.Serialize(gltf, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string BuildObjContent(GeneratedAssetSpec spec, string texturePath)
    {
        var objectName = string.IsNullOrWhiteSpace(spec.BaseShape) ? "box" : spec.BaseShape.ToLowerInvariant();

        return objectName switch
        {
            "sphere" => BuildSphereObj(spec, texturePath),
            "cylinder" => BuildCylinderObj(spec, texturePath),
            "pyramid" => BuildPyramidObj(spec, texturePath),
            "chair" => BuildChairObj(spec, texturePath),
            "rocket" => BuildRocketObj(spec, texturePath),
            _ => BuildBoxObj(spec, texturePath)
        };
    }

    private static MeshGeometry3D BuildMesh(GeneratedAssetSpec spec)
    {
        var objectName = string.IsNullOrWhiteSpace(spec.BaseShape) ? "box" : spec.BaseShape.ToLowerInvariant();

        return objectName switch
        {
            "sphere" => BuildSphereMesh(spec),
            "cylinder" => BuildCylinderMesh(spec),
            "pyramid" => BuildPyramidMesh(spec),
            "chair" => BuildChairMesh(spec),
            "rocket" => BuildRocketMesh(spec),
            _ => BuildBoxMesh(spec)
        };
    }

    private static string BuildBoxObj(GeneratedAssetSpec spec, string texturePath)
    {
        var sizeX = spec.Size.X;
        var sizeY = spec.Size.Y;
        var sizeZ = spec.Size.Z;

        var vertices = new[]
        {
            (-sizeX / 2, -sizeY / 2, -sizeZ / 2),
            ( sizeX / 2, -sizeY / 2, -sizeZ / 2),
            ( sizeX / 2,  sizeY / 2, -sizeZ / 2),
            (-sizeX / 2,  sizeY / 2, -sizeZ / 2),
            (-sizeX / 2, -sizeY / 2,  sizeZ / 2),
            ( sizeX / 2, -sizeY / 2,  sizeZ / 2),
            ( sizeX / 2,  sizeY / 2,  sizeZ / 2),
            (-sizeX / 2,  sizeY / 2,  sizeZ / 2)
        };

        var faces = new[]
        {
            (1, 2, 3, 4),
            (5, 8, 7, 6),
            (1, 5, 6, 2),
            (2, 6, 7, 3),
            (3, 7, 8, 4),
            (5, 1, 4, 8)
        };

        var sb = new StringBuilder();
        sb.AppendLine("# Generated by GigaChat 3D Asset Builder Pro");
        sb.AppendLine("mtllib " + Path.GetFileNameWithoutExtension(texturePath) + ".mtl");
        sb.AppendLine("o Box");

        foreach (var v in vertices)
            sb.AppendLine($"v {v.Item1:F6} {v.Item2:F6} {v.Item3:F6}");

        sb.AppendLine("usemtl Material");
        foreach (var (a, b, c, d) in faces)
            sb.AppendLine($"f {a} {b} {c} {d}");

        return sb.ToString();
    }

    private static string BuildSphereObj(GeneratedAssetSpec spec, string texturePath)
    {
        var radius = Math.Max(spec.Size.X, Math.Max(spec.Size.Y, spec.Size.Z));
        var sb = new StringBuilder();
        sb.AppendLine("# Generated by GigaChat 3D Asset Builder Pro");
        sb.AppendLine("mtllib " + Path.GetFileNameWithoutExtension(texturePath) + ".mtl");
        sb.AppendLine("o Sphere");

        const int segments = 18;
        const int rings = 12;

        for (var ring = 0; ring <= rings; ring++)
        {
            var v = ring / (double)rings;
            var phi = v * Math.PI;
            var y = Math.Cos(phi) * radius;
            var r = Math.Sin(phi) * radius;

            for (var seg = 0; seg <= segments; seg++)
            {
                var u = seg / (double)segments;
                var theta = u * Math.PI * 2;
                var x = Math.Cos(theta) * r;
                var z = Math.Sin(theta) * r;
                sb.AppendLine($"v {x:F6} {y:F6} {z:F6}");
            }
        }

        sb.AppendLine("usemtl Material");
        for (var ring = 0; ring < rings; ring++)
        {
            for (var seg = 0; seg < segments; seg++)
            {
                var a = ring * (segments + 1) + seg + 1;
                var b = ring * (segments + 1) + seg + 2;
                var c = (ring + 1) * (segments + 1) + seg + 2;
                var d = (ring + 1) * (segments + 1) + seg + 1;

                sb.AppendLine($"f {a} {b} {c}");
                sb.AppendLine($"f {a} {c} {d}");
            }
        }

        return sb.ToString();
    }

    private static string BuildCylinderObj(GeneratedAssetSpec spec, string texturePath)
    {
        var radius = Math.Max(0.2, spec.Size.X / 2.0);
        var height = Math.Max(0.5, spec.Size.Y);
        var segments = 20;
        var sb = new StringBuilder();
        sb.AppendLine("# Generated by GigaChat 3D Asset Builder Pro");
        sb.AppendLine("mtllib " + Path.GetFileNameWithoutExtension(texturePath) + ".mtl");
        sb.AppendLine("o Cylinder");

        for (var i = 0; i < segments; i++)
        {
            var angle = i * 2 * Math.PI / segments;
            var x = Math.Cos(angle) * radius;
            var z = Math.Sin(angle) * radius;
            sb.AppendLine($"v {x:F6} {-height / 2:F6} {z:F6}");
            sb.AppendLine($"v {x:F6} {height / 2:F6} {z:F6}");
        }

        sb.AppendLine("usemtl Material");
        for (var i = 0; i < segments; i++)
        {
            var a = 2 * i + 1;
            var b = 2 * i + 2;
            var c = 2 * ((i + 1) % segments) + 2;
            var d = 2 * ((i + 1) % segments) + 1;

            sb.AppendLine($"f {a} {b} {c}");
            sb.AppendLine($"f {a} {c} {d}");
        }

        return sb.ToString();
    }

    private static string BuildPyramidObj(GeneratedAssetSpec spec, string texturePath)
    {
        var sizeX = spec.Size.X;
        var sizeY = spec.Size.Y;
        var sizeZ = spec.Size.Z;

        var vertices = new[]
        {
            (-sizeX / 2, -sizeY / 2, -sizeZ / 2),
            ( sizeX / 2, -sizeY / 2, -sizeZ / 2),
            ( sizeX / 2, -sizeY / 2,  sizeZ / 2),
            (-sizeX / 2, -sizeY / 2,  sizeZ / 2),
            (0, sizeY / 2, 0)
        };

        var sb = new StringBuilder();
        sb.AppendLine("# Generated by GigaChat 3D Asset Builder Pro");
        sb.AppendLine("mtllib " + Path.GetFileNameWithoutExtension(texturePath) + ".mtl");
        sb.AppendLine("o Pyramid");

        foreach (var v in vertices)
            sb.AppendLine($"v {v.Item1:F6} {v.Item2:F6} {v.Item3:F6}");

        sb.AppendLine("usemtl Material");
        sb.AppendLine("f 1 2 5");
        sb.AppendLine("f 2 3 5");
        sb.AppendLine("f 3 4 5");
        sb.AppendLine("f 4 1 5");
        sb.AppendLine("f 1 4 3 2");

        return sb.ToString();
    }

    private static string BuildChairObj(GeneratedAssetSpec spec, string texturePath)
    {
        var x = spec.Size.X;
        var y = spec.Size.Y;
        var z = spec.Size.Z;

        var sb = new StringBuilder();
        sb.AppendLine("# Generated by GigaChat 3D Asset Builder Pro");
        sb.AppendLine("mtllib " + Path.GetFileNameWithoutExtension(texturePath) + ".mtl");
        sb.AppendLine("o Chair");

        var vertices = new (double X, double Y, double Z)[]
        {
            (-x / 2, 0, -z / 2), (x / 2, 0, -z / 2), (x / 2, 0, z / 2), (-x / 2, 0, z / 2),
            (-x / 2, y * 0.7, -z / 2), (x / 2, y * 0.7, -z / 2), (x / 2, y * 0.7, z / 2), (-x / 2, y * 0.7, z / 2),
            (-x / 2, y, 0), (x / 2, y, 0)
        };

        foreach (var v in vertices)
            sb.AppendLine($"v {v.X:F6} {v.Y:F6} {v.Z:F6}");

        sb.AppendLine("usemtl Material");
        sb.AppendLine("f 1 2 3 4");
        sb.AppendLine("f 5 6 7 8");
        sb.AppendLine("f 1 5 8 4");
        sb.AppendLine("f 2 6 7 3");
        sb.AppendLine("f 5 9 10 6");

        return sb.ToString();
    }

    private static string BuildRocketObj(GeneratedAssetSpec spec, string texturePath)
    {
        var x = spec.Size.X;
        var y = spec.Size.Y;
        var z = spec.Size.Z;
        var sb = new StringBuilder();

        sb.AppendLine("# Generated by GigaChat 3D Asset Builder Pro");
        sb.AppendLine("mtllib " + Path.GetFileNameWithoutExtension(texturePath) + ".mtl");
        sb.AppendLine("o Rocket");

        var vertices = new (double X, double Y, double Z)[]
        {
            (0, y / 2, 0),
            (-x / 2, 0, -z / 2),
            (x / 2, 0, -z / 2),
            (x / 2, 0, z / 2),
            (-x / 2, 0, z / 2),
            (0, -y / 2, 0)
        };

        foreach (var v in vertices)
            sb.AppendLine($"v {v.X:F6} {v.Y:F6} {v.Z:F6}");

        sb.AppendLine("usemtl Material");
        sb.AppendLine("f 1 2 3");
        sb.AppendLine("f 1 3 4");
        sb.AppendLine("f 1 4 5");
        sb.AppendLine("f 1 5 2");
        sb.AppendLine("f 2 5 6");
        sb.AppendLine("f 5 4 6");
        sb.AppendLine("f 4 3 6");
        sb.AppendLine("f 3 2 6");

        return sb.ToString();
    }

    private static MeshGeometry3D BuildBoxMesh(GeneratedAssetSpec spec)
    {
        var mesh = new MeshGeometry3D();
        var x = spec.Size.X / 2;
        var y = spec.Size.Y / 2;
        var z = spec.Size.Z / 2;

        var points = new[]
        {
            new Point3D(-x, -y, -z), new Point3D(x, -y, -z), new Point3D(x, y, -z), new Point3D(-x, y, -z),
            new Point3D(-x, -y, z), new Point3D(x, -y, z), new Point3D(x, y, z), new Point3D(-x, y, z)
        };

        var triangles = new[]
        {
            0, 1, 2, 0, 2, 3,
            4, 6, 5, 4, 7, 6,
            0, 3, 7, 0, 7, 4,
            3, 2, 6, 3, 6, 7,
            2, 1, 5, 2, 5, 6,
            0, 4, 5, 0, 5, 1
        };

        foreach (var p in points)
            mesh.Positions.Add(p);

        foreach (var idx in triangles)
            mesh.TriangleIndices.Add(idx);

        return mesh;
    }

    private static MeshGeometry3D BuildSphereMesh(GeneratedAssetSpec spec)
    {
        var mesh = new MeshGeometry3D();
        var radius = Math.Max(spec.Size.X, Math.Max(spec.Size.Y, spec.Size.Z)) / 2;
        var latCount = 18;
        var lonCount = 18;
        var points = new List<Point3D>();

        for (var lat = 0; lat <= latCount; lat++)
        {
            var v = lat / (double)latCount;
            var theta = v * Math.PI;
            var y = Math.Cos(theta) * radius;
            var r = Math.Sin(theta) * radius;

            for (var lon = 0; lon <= lonCount; lon++)
            {
                var u = lon / (double)lonCount;
                var phi = u * 2 * Math.PI;
                var x = Math.Cos(phi) * r;
                var z = Math.Sin(phi) * r;
                points.Add(new Point3D(x, y, z));
            }
        }

        for (var lat = 0; lat < latCount; lat++)
        {
            for (var lon = 0; lon < lonCount; lon++)
            {
                var a = lat * (lonCount + 1) + lon;
                var b = a + lonCount + 1;
                var c = a + 1;
                var d = b + 1;

                mesh.Positions.Add(points[a]);
                mesh.Positions.Add(points[b]);
                mesh.Positions.Add(points[c]);
                mesh.Positions.Add(points[d]);

                mesh.TriangleIndices.Add(mesh.Positions.Count - 4);
                mesh.TriangleIndices.Add(mesh.Positions.Count - 3);
                mesh.TriangleIndices.Add(mesh.Positions.Count - 2);

                mesh.TriangleIndices.Add(mesh.Positions.Count - 4);
                mesh.TriangleIndices.Add(mesh.Positions.Count - 2);
                mesh.TriangleIndices.Add(mesh.Positions.Count - 1);
            }
        }

        return mesh;
    }

    private static MeshGeometry3D BuildCylinderMesh(GeneratedAssetSpec spec)
    {
        var mesh = new MeshGeometry3D();
        var radius = Math.Max(0.2, spec.Size.X / 2.0);
        var height = Math.Max(0.5, spec.Size.Y);
        var segments = 24;

        for (var i = 0; i < segments; i++)
        {
            var angle = i * 2 * Math.PI / segments;
            var x = Math.Cos(angle) * radius;
            var z = Math.Sin(angle) * radius;
            mesh.Positions.Add(new Point3D(x, -height / 2, z));
            mesh.Positions.Add(new Point3D(x, height / 2, z));
        }

        for (var i = 0; i < segments; i++)
        {
            var a = 2 * i;
            var b = 2 * i + 1;
            var c = 2 * ((i + 1) % segments);
            var d = 2 * ((i + 1) % segments) + 1;

            mesh.TriangleIndices.Add(a); mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(c);
            mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(d); mesh.TriangleIndices.Add(c);
        }

        return mesh;
    }

    private static MeshGeometry3D BuildPyramidMesh(GeneratedAssetSpec spec)
    {
        var mesh = new MeshGeometry3D();
        var points = new[]
        {
            new Point3D(-spec.Size.X / 2, -spec.Size.Y / 2, -spec.Size.Z / 2),
            new Point3D(spec.Size.X / 2, -spec.Size.Y / 2, -spec.Size.Z / 2),
            new Point3D(spec.Size.X / 2, -spec.Size.Y / 2, spec.Size.Z / 2),
            new Point3D(-spec.Size.X / 2, -spec.Size.Y / 2, spec.Size.Z / 2),
            new Point3D(0, spec.Size.Y / 2, 0)
        };

        foreach (var p in points)
            mesh.Positions.Add(p);

        mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(4);
        mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(4);
        mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(4);
        mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(4);
        mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(2);
        mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(1);

        return mesh;
    }

    private static MeshGeometry3D BuildChairMesh(GeneratedAssetSpec spec)
    {
        var mesh = BuildBoxMesh(new GeneratedAssetSpec { Size = new AssetSize { X = spec.Size.X, Y = spec.Size.Y * 0.7, Z = spec.Size.Z } });
        return mesh;
    }

    private static MeshGeometry3D BuildRocketMesh(GeneratedAssetSpec spec)
    {
        var mesh = BuildPyramidMesh(new GeneratedAssetSpec { Size = new AssetSize { X = spec.Size.X, Y = spec.Size.Y, Z = spec.Size.Z } });
        return mesh;
    }

    private static void GenerateTexture(string texturePath, List<string> colors)
    {
        var palette = colors.Count > 0 ? colors : new List<string> { "#1F6FEB", "#F4C95D", "#1A2433" };

        var width = 512;
        var height = 512;
        var bytes = new byte[width * height * 4];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var colorIndex = ((x / 32) + (y / 32)) % palette.Count;
                var color = ParseColor(palette[colorIndex]);
                var offset = (y * width + x) * 4;
                bytes[offset] = color.B;
                bytes[offset + 1] = color.G;
                bytes[offset + 2] = color.R;
                bytes[offset + 3] = color.A;
            }
        }

        for (var x = 0; x < width; x += 32)
        {
            for (var y = 0; y < height; y++)
            {
                var offset = (y * width + x) * 4;
                var color = ParseColor(palette[^1]);
                bytes[offset] = color.B;
                bytes[offset + 1] = color.G;
                bytes[offset + 2] = color.R;
                bytes[offset + 3] = color.A;
            }
        }

        var bitmap = new System.Windows.Media.Imaging.WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), bytes, width * 4, 0);
        using var stream = File.Create(texturePath);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }

    private static string BuildMtl(string textureFileName)
    {
        var fileName = Path.GetFileName(textureFileName);
        return $"""
newmtl Material
Ka 0.1 0.1 0.1
Kd 1.0 1.0 1.0
Ks 0.2 0.2 0.2
Ns 50.0
map_Kd {fileName}
""";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var result = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            result.Append(invalidChars.Contains(ch) ? '_' : ch);
        }

        var final = result.ToString().Trim();
        return string.IsNullOrWhiteSpace(final) ? "GeneratedAsset" : final;
    }

    private static Color ParseColor(string hex)
    {
        var normalized = hex.Trim();
        if (normalized.StartsWith("#"))
            normalized = normalized[1..];

        if (normalized.Length == 3)
            normalized = string.Concat(normalized.Select(ch => new string(ch, 2)));

        if (normalized.Length != 6)
            return Colors.DeepSkyBlue;

        return (Color)ColorConverter.ConvertFromString($"#{normalized}");
    }
}

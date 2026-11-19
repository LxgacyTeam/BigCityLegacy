using System.Collections.Generic;
using UnityEngine;

internal static class LegacyGazelCanopy
{
    private const string FabricName = "Brezent_6";
    private static Mesh mesh;
    private static Material material;
    private static int fabricLookupFrame = -1;

    internal static void Attach(CarControl car)
    {
        if (NetManager.isServer || !car.tr_view || car.tr_view.Find("GazelCanopy"))
            return;

        PrepareMaterial();
        if (!mesh) mesh = CreateMesh();
        var canopy = new GameObject("GazelCanopy") { layer = car.tr_view.gameObject.layer };
        canopy.transform.SetParent(car.tr_view, false);
        canopy.AddComponent<MeshFilter>().sharedMesh = mesh;
        canopy.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    internal static void PrepareMaterial()
    {
        if (NetManager.isServer || (material && material.mainTexture) || fabricLookupFrame == Time.frameCount)
            return;

        fabricLookupFrame = Time.frameCount;
        if (!material)
        {
            material = new Material(Shader.Find("Standard"))
            {
                name = "Gazel canopy fabric",
                color = new Color(0.4165f, 0.6642f, 0.7868f)
            };
            material.SetFloat("_Glossiness", 0.12f);
        }

        // The stock garage cover uses a scene asset, not a Resources path.
        foreach (var source in Resources.FindObjectsOfTypeAll<Material>())
        {
            if (source.name != FabricName || !source.mainTexture)
                continue;

            material.mainTexture = source.mainTexture;
            if (source.HasProperty("_Color"))
                material.color = source.color;

            if (source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap") is Texture normal && normal)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            return;
        }
    }

    private static Mesh CreateMesh()
    {
        const float rear = -2.72f;
        const float front = 0.56f;
        const float tileSize = front - rear;
        var profile = new[]
        {
            new Vector2(-1.06f, 0.70f), new Vector2(-1.04f, 1.68f),
            new Vector2(-0.76f, 1.94f), new Vector2(0.75f, 1.94f),
            new Vector2(1.03f, 1.68f), new Vector2(1.05f, 0.70f)
        };
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uv = new List<Vector2>();
        float profileDistance = 0f;

        for (int i = 0; i < profile.Length - 1; i++)
        {
            var a = profile[i];
            var b = profile[i + 1];
            int start = vertices.Count;
            vertices.Add(new Vector3(a.x, a.y, rear));
            vertices.Add(new Vector3(a.x, a.y, front));
            vertices.Add(new Vector3(b.x, b.y, front));
            vertices.Add(new Vector3(b.x, b.y, rear));
            float startV = profileDistance / tileSize;
            profileDistance += Vector2.Distance(a, b);
            float endV = profileDistance / tileSize;
            uv.Add(new Vector2(0f, startV));
            uv.Add(new Vector2(1f, startV));
            uv.Add(new Vector2(1f, endV));
            uv.Add(new Vector2(0f, endV));
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        for (int end = 0; end < 2; end++)
        {
            int start = vertices.Count;
            foreach (var point in profile)
            {
                vertices.Add(new Vector3(point.x, point.y, end == 0 ? rear : front));
                float u = (end == 0 ? -point.x : point.x) / tileSize + 0.5f;
                uv.Add(new Vector2(u, (point.y - profile[0].y) / tileSize));
            }

            for (int i = 1; i < profile.Length - 1; i++)
            {
                triangles.Add(start);
                triangles.Add(start + i + end);
                triangles.Add(start + i + 1 - end);
            }
        }

        var canopy = new Mesh { name = "Gazel canopy" };
        canopy.SetVertices(vertices);
        canopy.SetTriangles(triangles, 0);
        canopy.SetUVs(0, uv);
        canopy.RecalculateNormals();
        canopy.RecalculateTangents();
        canopy.RecalculateBounds();
        return canopy;
    }
}

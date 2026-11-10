using System;
using UnityEngine;

internal static class LegacyRaceCheckpointVisual
{
    internal static void Apply(RaceDirection point)
    {
        bool finish = !point.next;
        if (point.rotByDirection) point.rotByDirection.gameObject.SetActive(!finish);
        point.lookAt_Test = null;
        var filter = point.GetComponent<MeshFilter>();
        var renderer = point.GetComponent<MeshRenderer>();
        if (!filter || !renderer) return;
        var materials = renderer.sharedMaterials;
        if (materials.Length == 0 || !materials[0]) return;
        var beam = materials[0];
        Material icon = materials.Length > 1 ? materials[1] : null;
        if (!icon && point.rotByDirection)
        {
            var arrow = point.rotByDirection.GetComponent<MeshRenderer>();
            if (arrow) icon = arrow.sharedMaterial;
        }
        string name = finish ? "RaceStrelka_Finish" : "RaceStrelka";
        foreach (var mesh in Resources.FindObjectsOfTypeAll<Mesh>())
            if (string.Equals(mesh.name, name, StringComparison.OrdinalIgnoreCase))
            {
                if (finish && !icon) return;
                filter.sharedMesh = mesh;
                renderer.sharedMaterials = finish ? new[] { beam, icon } : new[] { beam };
                return;
            }
    }
}

using UnityEngine;
using UnityEngine.UI;

internal sealed class LegacyRaceBulletPickup : MonoBehaviour
{
    private static readonly Color BulletColor = new Color(1f, 0.08f, 0.8f);
    private SpriteRenderer icon;
    private MeshRenderer shell;
    private Material shellMaterial;
    private Material emptyMaterial;

    internal static void Restore(Bonus pickup)
    {
        var view = pickup.gameObject.AddComponent<LegacyRaceBulletPickup>();
        pickup.type = Bonus.BonusType.BonusBullet;
        view.shell = pickup.GetComponent<MeshRenderer>();
        var sprite = FindBulletIcon();
        if (view.shell)
        {
            view.shellMaterial = view.shell.material;
            var material = view.shellMaterial;
            if (material.HasProperty("_Color")) material.SetColor("_Color", BulletColor);
            if (material.HasProperty("_Color0")) material.SetColor("_Color0", BulletColor);
            if (material.HasProperty("_Color1")) material.SetColor("_Color1", BulletColor);
            var materials = view.shell.sharedMaterials;
            var mesh = pickup.GetComponent<MeshFilter>();
            if (sprite && mesh && mesh.sharedMesh && mesh.sharedMesh.subMeshCount == 2 && materials.Length == 2)
            {
                var iconShader = Shader.Find("Sprites/Default");
                if (iconShader)
                {
                    view.emptyMaterial = new Material(iconShader) { color = Color.clear };
                    materials[1] = view.emptyMaterial;
                    view.shell.sharedMaterials = materials;
                }
            }
        }

        var light = pickup.GetComponent<Light>();
        if (light) light.color = BulletColor;
        pickup.color = BulletColor;

        var particles = RaceSettings.me ? RaceSettings.me.bonuses?.bullet : null;
        if (particles && particles.Particle) pickup.TakeParticles = particles.Particle;

        if (!sprite) return;
        var marker = new GameObject("BulletSymbol");
        marker.layer = pickup.gameObject.layer;
        marker.transform.SetParent(pickup.transform, false);
        view.icon = marker.AddComponent<SpriteRenderer>();
        view.icon.sprite = sprite;
        marker.transform.localScale = Vector3.one * (1.15f / sprite.bounds.size.x);
    }

    private static Sprite FindBulletIcon()
    {
        var menu = GameUI.me ? GameUI.me.weaponeSelect : null;
        if (!menu) return null;
        foreach (var item in menu.GetComponentsInChildren<WeaponeSelectItem>(true))
        {
            if (item.SubTypeStr != nameof(WeaponeSelectItem.SubTypes.Car_Weapone_Bullets))
                continue;

            var image = item.GetComponentInChildren<Image>(true);
            if (image && image.sprite) return image.sprite;
        }
        return null;
    }

    private void LateUpdate()
    {
        if (!icon) return;
        icon.enabled = shell && shell.enabled;
        if (icon.enabled && Cam.me && Cam.me.unityCam)
            icon.transform.rotation = Cam.me.unityCam.transform.rotation;
    }

    private void OnDestroy()
    {
        if (emptyMaterial) Destroy(emptyMaterial);
        if (shellMaterial) Destroy(shellMaterial);
    }
}

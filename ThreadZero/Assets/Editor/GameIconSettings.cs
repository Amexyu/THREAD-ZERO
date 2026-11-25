using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ThreadZero.Editor
{
    /// 11-24 22:08 游戏图标：用户看了三轮例图，选了 K「霓虹 0 + 主角」。图在 Assets/Icon/icon_<尺寸>.png，由 Tools/icon/make_icon.py 生成。
    /// 点一次菜单就存进 ProjectSettings：默认图标用 1024；Windows / Mac / Linux 按尺寸各放各的（16 是只留霓虹「0」的简化版，主角缩到那么小看不清）。
    /// 导入设置顺手改成不压缩、不要 mipmap，图标不会被压糊。
    public static class GameIconSettings
    {
        private const string Folder = "Assets/Icon";

        [MenuItem("Thread Zero/检查/设置游戏图标（Assets/Icon）")]
        public static void Apply()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
            {
                var imp = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (imp == null) continue;
                bool dirty = false;
                if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
                if (imp.mipmapEnabled) { imp.mipmapEnabled = false; dirty = true; }
                if (!imp.alphaIsTransparency) { imp.alphaIsTransparency = true; dirty = true; }
                if (imp.npotScale != TextureImporterNPOTScale.None) { imp.npotScale = TextureImporterNPOTScale.None; dirty = true; }
                if (imp.maxTextureSize < 1024) { imp.maxTextureSize = 1024; dirty = true; }
                if (dirty) imp.SaveAndReimport();
            }
            Texture2D big = Load(1024);
            if (big == null) { Debug.LogError("THREAD ZERO：找不到 " + Folder + "/icon_1024.png，先跑 Tools/icon/make_icon.py"); return; }
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { big }, IconKind.Any);   // 默认图标
            int[] sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any);
            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < sizes.Length; i++) icons[i] = Load(sizes[i]) ?? big;
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Any);
            AssetDatabase.SaveAssets();
            Debug.Log("THREAD ZERO：游戏图标设好了（默认 1024；Standalone " + string.Join(" / ", System.Array.ConvertAll(sizes, s => s.ToString())) + "）");
        }

        private static Texture2D Load(int size) { return AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/icon_" + size + ".png"); }
    }
}

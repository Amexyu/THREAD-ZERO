using UnityEditor;
using UnityEngine;

namespace ThreadZero.Editor
{
    public sealed class ArtImport : AssetPostprocessor
    {
        // v0.20 电脑版贴图压缩（BC7 高质量，每像素 1 字节；不压是 3～4 字节）。逐张画质见「Thread Zero/检查/压缩画质（逐张和原 PNG 比）」。
        //   怪（512×512，2 的幂）：带 mipmap 一起压。
        //   远景 / 中景：按 0.6～1.5 倍画，用不着 mipmap；不是 2 的幂的图开着 mipmap 时 Unity 不给压（实测），所以关掉 mipmap 再压。
        //   地面带缩得很小（约 0.25～0.6 倍），要 mipmap 防闪烁，又不是 2 的幂，保持不压缩。角色部件、界面图小，不压。
        // 想全部改回不压缩：把 Compress 改成 false，Unity 会自动把这些图重新导入成原来的设置。
        internal const bool Compress = true;
        public override uint GetVersion() { return Compress ? 2u : 1u; }

        internal static bool IsEnemy(string path) { return path.StartsWith("Assets/Resources/Art/V16/Enemies/"); }
        internal static bool IsBackdrop(string path) { return path.StartsWith("Assets/Resources/Art/V16/Stage") && (path.EndsWith("_far.png") || path.EndsWith("_mid.png")); }

        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Resources/Art/")) return;
            var texture=(TextureImporter)assetImporter;
            texture.textureType=TextureImporterType.Default;
            texture.alphaSource=TextureImporterAlphaSource.FromInput;
            texture.alphaIsTransparency=true;
            texture.mipmapEnabled=false;
            texture.maxTextureSize=2048;
            texture.npotScale=TextureImporterNPOTScale.None;
            texture.textureCompression=TextureImporterCompression.Uncompressed;
            texture.filterMode=FilterMode.Bilinear;
            texture.wrapMode=TextureWrapMode.Clamp;
            if(assetPath.StartsWith("Assets/Resources/Art/V16/Stage")) texture.wrapMode=TextureWrapMode.Repeat;   // 背景横向平铺
            // V13 layered parts are drawn ~2.5x smaller than authored; mipmaps keep the line art from shimmering.
            if(assetPath.StartsWith("Assets/Resources/Art/V13/Starter/") || assetPath.StartsWith("Assets/Resources/Art/V13/Parts/") || assetPath.StartsWith("Assets/Resources/Art/V16/") || assetPath.StartsWith("Assets/Resources/Art/V15/Hud/"))
            { texture.mipmapEnabled=true; texture.filterMode=FilterMode.Trilinear; }

            bool backdrop=Compress && IsBackdrop(assetPath), compress=backdrop || (Compress && IsEnemy(assetPath));
            if(backdrop) { texture.mipmapEnabled=false; texture.filterMode=FilterMode.Bilinear; }
            var ps=texture.GetPlatformTextureSettings("Standalone");
            if(compress) { ps.overridden=true; ps.format=TextureImporterFormat.BC7; ps.maxTextureSize=2048; texture.SetPlatformTextureSettings(ps); }
            else if(ps.overridden) { ps.overridden=false; texture.SetPlatformTextureSettings(ps); }
        }
    }
}

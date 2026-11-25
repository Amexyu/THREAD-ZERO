using UnityEditor;
using UnityEngine;

namespace ThreadZero.Editor
{
    /// 11-24 v0.29 界面皮肤贴图（Resources/UI/Skin，Tools/ui_skin/make_skin.py 生成）：不压缩、开 mipmap（按 0.4～1 倍画，缩小时不闪），
    /// 名字以 tile_ 开头的是平铺底纹，要 Repeat。
    public sealed class UiSkinImport : AssetPostprocessor
    {
        public override uint GetVersion() { return 1u; }
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/UI/Skin/")) return;
            var texture = (TextureImporter)assetImporter;
            texture.textureType = TextureImporterType.Default;
            texture.alphaSource = TextureImporterAlphaSource.FromInput;
            texture.alphaIsTransparency = true;
            texture.npotScale = TextureImporterNPOTScale.None;
            texture.textureCompression = TextureImporterCompression.Uncompressed;
            texture.maxTextureSize = 2048;
            texture.isReadable = false;
            bool tile = System.IO.Path.GetFileName(assetPath).StartsWith("tile_");
            texture.mipmapEnabled = !tile;
            texture.filterMode = tile ? FilterMode.Bilinear : FilterMode.Trilinear;
            texture.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        }
    }
}

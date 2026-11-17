using UnityEditor;
using UnityEngine;

namespace ThreadZero.Editor
{
    public sealed class ArtImport : AssetPostprocessor
    {
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
            // V13 layered parts are drawn ~2.5x smaller than authored; mipmaps keep the line art from shimmering.
            if(assetPath.StartsWith("Assets/Resources/Art/V13/Starter/") || assetPath.StartsWith("Assets/Resources/Art/V13/Parts/") || assetPath.StartsWith("Assets/Resources/Art/V15/Hud/"))
            { texture.mipmapEnabled=true; texture.filterMode=FilterMode.Trilinear; }
        }
    }
}

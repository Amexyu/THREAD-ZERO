using UnityEditor;
using UnityEngine;

namespace ThreadZero.EditorTools
{
    /// v0.21 音频导入设置：Resources/Audio/Music 下的曲子用流式加载（不整首解压进内存）、Vorbis 压缩；Sfx 下的短音效解压进内存、加载快。
    public sealed class AudioImport : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            var importer = (AudioImporter)assetImporter;
            string p = assetPath.Replace('\\', '/');
            if (p.Contains("/Resources/Audio/Music/"))
            {
                AudioImporterSampleSettings s = importer.defaultSampleSettings;
                s.loadType = AudioClipLoadType.Streaming; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = .55f;
                importer.defaultSampleSettings = s; importer.loadInBackground = true;
            }
            else if (p.Contains("/Resources/Audio/Sfx/"))
            {
                AudioImporterSampleSettings s = importer.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = .7f;
                importer.defaultSampleSettings = s;
            }
        }
    }
}

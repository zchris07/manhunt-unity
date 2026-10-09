using UnityEditor;
using UnityEngine;

namespace Vision.EditorTools
{
    /// <summary>
    /// Import settings for the original's media under Assets/Vision/Resources: pictures keep their exact size (they fill
    /// the screen, so no power-of-two rescale or mipmaps), and long tracks (the Soundcloud Burst, Sexton's reel) stream.
    /// </summary>
    public sealed class MediaImport : AssetPostprocessor
    {
        const string Images = "Assets/Vision/Resources/Images/";
        const string Audio = "Assets/Vision/Resources/Audio/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Images)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.alphaSource = TextureImporterAlphaSource.None;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Audio)) return;
            var ai = (AudioImporter)assetImporter;
            ai.loadInBackground = true;
            AudioImporterSampleSettings s = ai.defaultSampleSettings;
            bool longTrack = assetPath.EndsWith("gmajor.mp3") || assetPath.EndsWith("reel.mp3");
            s.loadType = longTrack ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            ai.defaultSampleSettings = s;
        }
    }
}

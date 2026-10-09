using UnityEditor;
using UnityEngine;

namespace Vision.EditorTools
{
    /// <summary>
    /// Import settings for the original's media under Assets/Vision/Resources: pictures keep their exact size (they fill
    /// the screen, so no power-of-two rescale or mipmaps), the HUD icons (drawn from the original's item art) stay
    /// uncompressed with their transparency, and long tracks (the Soundcloud Burst, Sexton's reel) stream.
    /// </summary>
    public sealed class MediaImport : AssetPostprocessor
    {
        const string Images = "Assets/Vision/Resources/Images/";
        const string Audio = "Assets/Vision/Resources/Audio/";
        const string Icons = "Assets/Vision/Resources/Icons/";

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(Icons))
            {
                // The HUD's item and ability icons: crisp, with their transparency, at their drawn size.
                var ii = (TextureImporter)assetImporter;
                ii.textureType = TextureImporterType.Default;
                ii.npotScale = TextureImporterNPOTScale.None;
                ii.mipmapEnabled = false;
                ii.wrapMode = TextureWrapMode.Clamp;
                ii.alphaSource = TextureImporterAlphaSource.FromInput;
                ii.alphaIsTransparency = true;
                ii.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }
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

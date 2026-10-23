using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    /// <summary>Imports .lrc lyric files as TextAssets so a SongAsset can reference them.</summary>
    [ScriptedImporter(1, "lrc")]
    public class LrcImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var text = new TextAsset(File.ReadAllText(ctx.assetPath));
            ctx.AddObjectToAsset("lyrics", text);
            ctx.SetMainObject(text);
        }
    }
}

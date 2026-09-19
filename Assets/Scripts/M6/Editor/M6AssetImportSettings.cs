using UnityEditor;
using UnityEngine;

namespace BeMyArms.M6.EditorTools
{
    /// <summary>
    /// Applies the production import conventions to every model under Assets/Art. Geometry is
    /// authored in metres and exported Y-up from Blender, so Unity must import it at 1:1.
    /// </summary>
    public static class M6AssetImportSettings
    {
        public static int ApplyAll()
        {
            int changed = 0;
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { M6AssetConventions.ArtRoot });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer) continue;

                bool dirty = false;
                if (!Mathf.Approximately(importer.globalScale, 1f)) { importer.globalScale = 1f; dirty = true; }
                if (!importer.useFileScale) { importer.useFileScale = true; dirty = true; }
                if (importer.bakeAxisConversion) { importer.bakeAxisConversion = false; dirty = true; }
                if (importer.importAnimation) { importer.importAnimation = false; dirty = true; }
                if (importer.importCameras) { importer.importCameras = false; dirty = true; }
                if (importer.importLights) { importer.importLights = false; dirty = true; }
                if (importer.importBlendShapes) { importer.importBlendShapes = false; dirty = true; }
                if (importer.isReadable) { importer.isReadable = false; dirty = true; }
                if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
                {
                    importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                    dirty = true;
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            return changed;
        }
    }
}

// Distinguish coastal rock from poured decks after the continuity preview. Saved-scene edits
// through the live Editor only; supplemental crest silhouettes are unreachable backdrop.
if (UnityEditor.EditorApplication.isPlaying || UnityEditor.Lightmapping.isRunning || UnityEditor.EditorApplication.isCompiling ||
    UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new System.Exception("Finish/review open work first.");
string dir="Assets/Art/Maps/BoatyardLookSample";
if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir+"/Textures/Rock058_2K-JPG_Color.jpg") == null) throw new System.Exception("Import the recorded Rock058 maps first.");
var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Boatyard.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
var roots=scene.GetRootGameObjects(); var backdrop=roots.Single(g=>g.name=="Backdrop").transform;
if(backdrop.Find("CoastalRockCrest_0")!=null) throw new System.Exception("Already authored; do not rerun.");
var arena=roots.Single(g=>g.name=="Arena").transform;
var map=roots.SelectMany(g=>g.GetComponentsInChildren<BeMyArms.Match.MapSpawns>()).Single(); var before=map.BuildCollision();
foreach(string kind in new[]{"Color","NormalGL","Roughness"}){
 var importer=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(dir+"/Textures/Rock058_2K-JPG_"+kind+".jpg");
 importer.textureType=kind=="NormalGL"?UnityEditor.TextureImporterType.NormalMap:UnityEditor.TextureImporterType.Default;
 importer.sRGBTexture=kind=="Color"; importer.isReadable=kind=="Roughness";importer.maxTextureSize=2048;
 importer.textureCompression=UnityEditor.TextureImporterCompression.CompressedHQ; importer.mipmapEnabled=true;importer.anisoLevel=8;
 importer.filterMode=UnityEngine.FilterMode.Trilinear;importer.wrapMode=UnityEngine.TextureWrapMode.Repeat;importer.SaveAndReimport();
}
var rough=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir+"/Textures/Rock058_2K-JPG_Roughness.jpg");
var pixels=rough.GetPixels32();for(int i=0;i<pixels.Length;i++)pixels[i]=new UnityEngine.Color32(0,0,0,(byte)(255-pixels[i].r));
var texture=new UnityEngine.Texture2D(rough.width,rough.height,UnityEngine.TextureFormat.RGBA32,false,true);texture.SetPixels32(pixels);texture.Apply();
string packedPath=dir+"/Textures/Rock058_MetalSmooth.png";System.IO.File.WriteAllBytes(packedPath,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
UnityEditor.AssetDatabase.ImportAsset(packedPath);var packedImporter=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(packedPath);
packedImporter.sRGBTexture=false;packedImporter.maxTextureSize=2048;packedImporter.textureCompression=UnityEditor.TextureImporterCompression.CompressedHQ;
packedImporter.anisoLevel=8;packedImporter.filterMode=UnityEngine.FilterMode.Trilinear;packedImporter.SaveAndReimport();
var roughImporter=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(dir+"/Textures/Rock058_2K-JPG_Roughness.jpg");roughImporter.isReadable=false;roughImporter.SaveAndReimport();
var material=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(dir+"/Continuity/CoastalRock.mat");
material.SetTexture("_BaseMap",UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir+"/Textures/Rock058_2K-JPG_Color.jpg"));
material.SetTexture("_BumpMap",UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir+"/Textures/Rock058_2K-JPG_NormalGL.jpg"));
material.SetTexture("_MetallicGlossMap",UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(packedPath));
material.SetColor("_BaseColor",new UnityEngine.Color(.85f,.87f,.85f));material.SetFloat("_BumpScale",.9f);material.SetFloat("_Smoothness",.5f);
UnityEditor.EditorUtility.SetDirty(material);
foreach(UnityEngine.Transform t in arena){
 if(!t.name.Contains("Cliff")&&!t.name.Contains("CoastSpur"))continue;
 var mesh=t.GetComponent<UnityEngine.MeshFilter>().sharedMesh;
 // Existing world projection was 12 m for the concrete variant; actual rock source covers 6 m.
 mesh.uv=mesh.uv.Select(v=>v*2).ToArray();mesh.RecalculateTangents();UnityEditor.EditorUtility.SetDirty(mesh);
}
var centres=new[]{new UnityEngine.Vector3(-18,9,-9),new UnityEngine.Vector3(-18,12,3),new UnityEngine.Vector3(-18,16,14),new UnityEngine.Vector3(3,9,-7)};
var sizes=new[]{new UnityEngine.Vector3(10,6,7),new UnityEngine.Vector3(9,7,14),new UnityEngine.Vector3(10,9,12),new UnityEngine.Vector3(8,4,3)};
for(int i=0;i<centres.Length;i++){
 var go=UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Sphere);go.name="CoastalRockCrest_"+i;go.transform.SetParent(backdrop,false);
 go.transform.position=centres[i];go.transform.localScale=sizes[i];UnityEngine.Object.DestroyImmediate(go.GetComponent<UnityEngine.Collider>());
 var mesh=UnityEngine.Object.Instantiate(go.GetComponent<UnityEngine.MeshFilter>().sharedMesh);var vertices=mesh.vertices;
 for(int j=0;j<vertices.Length;j++){var p=vertices[j];float ridge=1+.13f*UnityEngine.Mathf.Sin(p.x*19+p.z*11)+.07f*UnityEngine.Mathf.Cos(p.y*23-p.z*14);vertices[j]=p*ridge;}
 mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
 mesh.uv=vertices.Select(v=>new UnityEngine.Vector2(v.x*sizes[i].x,v.z*sizes[i].z)/6).ToArray();mesh.RecalculateTangents();UnityEditor.Unwrapping.GenerateSecondaryUVSet(mesh);
 mesh.name=go.name;UnityEditor.AssetDatabase.CreateAsset(mesh,dir+"/Continuity/"+mesh.name+".asset");go.GetComponent<UnityEngine.MeshFilter>().sharedMesh=mesh;
 go.GetComponent<UnityEngine.Renderer>().sharedMaterial=material;UnityEditor.GameObjectUtility.SetStaticEditorFlags(go,UnityEditor.StaticEditorFlags.ContributeGI);
}
var after=map.BuildCollision();if(!before.Solids.SequenceEqual(after.Solids)||!before.Surfaces.SequenceEqual(after.Surfaces))throw new System.Exception("Rock finish changed collision.");
if(backdrop.GetComponentsInChildren<UnityEngine.Collider>().Length!=0)throw new System.Exception("Backdrop remains collider-free.");
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);UnityEditor.AssetDatabase.SaveAssets();
return new{RockSource="Rock058 CC0",BackgroundCrests=centres.Length,CollisionUnchanged=true,NeedsBake=true};

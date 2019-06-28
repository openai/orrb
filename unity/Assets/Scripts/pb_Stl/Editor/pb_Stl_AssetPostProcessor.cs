using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections;
using System.IO;
using System.Collections.Generic;

namespace Parabox.STL
{
	public class pb_Stl_AssetPostProcessor : AssetPostprocessor
	{
		private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromPath)
		{
			foreach(string path in importedAssets.Where(x => x.ToLowerInvariant().EndsWith(".stl")))
			{
				string dir = Path.GetDirectoryName(path).Replace("\\", "/");
				string name = Path.GetFileNameWithoutExtension(path);

				IList<Mesh> meshes = pb_Stl_Importer.Import(path);

				if(meshes == null)
					continue;

				GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
				Material defaultDiffuse = cube.GetComponent<MeshRenderer>().sharedMaterial;
				GameObject.DestroyImmediate(cube);

				string prefab_path = string.Format("{0}/{1}.prefab", dir, name);

#if UNITY_4_7
				GameObject prefab_source = (GameObject) AssetDatabase.LoadAssetAtPath(prefab_path, typeof(GameObject));
#else
				GameObject prefab_source = AssetDatabase.LoadAssetAtPath<GameObject>(prefab_path);
#endif
				GameObject prefab = new GameObject();
				prefab.name = name;
				if(prefab_source == null)
					prefab_source = PrefabUtility.CreatePrefab(prefab_path, prefab);
				GameObject.DestroyImmediate(prefab);

				Object[] children = AssetDatabase.LoadAllAssetsAtPath(prefab_path);

				for(int i = 0; i < children.Length; i++)
				{
					if(AssetDatabase.IsSubAsset(children[i]))
						GameObject.DestroyImmediate(children[i], true);
				}

				for(int i = 0; i < meshes.Count; i++)
					AssetDatabase.AddObjectToAsset(meshes[i], prefab_source);

				children = AssetDatabase.LoadAllAssetsAtPath(prefab_path);
				GameObject render = new GameObject();

				for(int i = 0; i < children.Length; i++)
				{
					Mesh m = children[i] as Mesh;
					if(m == null) continue;
					GameObject child = new GameObject();
					child.name = string.Format("{0} ({1})", name, i);
					m.name = child.name;
					child.AddComponent<MeshFilter>().sharedMesh = m;
					child.AddComponent<MeshRenderer>().sharedMaterial = defaultDiffuse;
					child.transform.SetParent(render.transform, false);
				}

				PrefabUtility.ReplacePrefab(render, prefab_source, ReplacePrefabOptions.ReplaceNameBased);

				GameObject.DestroyImmediate(render);
			}
		}

		public static void CreateMeshAssetWithPath(string path)
		{
			string dir = Path.GetDirectoryName(path).Replace("\\", "/");
			string name = Path.GetFileNameWithoutExtension(path);

			IList<Mesh> meshes = pb_Stl_Importer.Import(path);

			if(meshes == null)
				return;

			for(int i = 0; i < meshes.Count; i++)
				AssetDatabase.CreateAsset(meshes[i], string.Format("{0}/{1}{2}.asset", dir, name, i));
		}

		[MenuItem("Tools/Force Import &d")]
		static void ditos()
		{
			foreach(Object o in Selection.objects)
			{
				CreateMeshAssetWithPath(AssetDatabase.GetAssetPath(o));
			}
		}
	}
}

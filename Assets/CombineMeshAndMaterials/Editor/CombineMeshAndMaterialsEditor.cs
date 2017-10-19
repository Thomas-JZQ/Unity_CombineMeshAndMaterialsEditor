using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;



public class CombineMeshAndMaterialsEditor
{

    private const string DefFolder = "/_Combines/";
    private static string DefDir = Application.dataPath + DefFolder;

    [MenuItem("Tools/CombineMeshAndMaterials")]
    static void CombineMeshAndMaterials()
    {
        if (Selection.activeTransform == null)
            return;

        Transform trs = Selection.activeTransform;

        MeshFilter[] mfChildren = trs.GetComponentsInChildren<MeshFilter>();
        if (mfChildren.Length == 0)
            return;
        CombineInstance[] combine = new CombineInstance[mfChildren.Length];

        MeshRenderer[] mrChildren = trs.GetComponentsInChildren<MeshRenderer>();
        if (mrChildren.Length == 0)
            return;
        Material[] materials = new Material[mrChildren.Length];

        GameObject go = new GameObject("_Combine_" + trs.name);
        go.transform.position = trs.position;
        go.transform.rotation = trs.rotation;
        MeshRenderer mrSelf = go.AddComponent<MeshRenderer>();
        MeshFilter mfSelf = go.AddComponent<MeshFilter>();

        Texture2D[] textures = new Texture2D[mrChildren.Length];
        for (int i = 0; i < mrChildren.Length; i++)
        {
            materials[i] = mrChildren[i].sharedMaterial;
            Texture2D tx = materials[i].GetTexture("_MainTex") as Texture2D;

            Texture2D tx2D = new Texture2D(tx.width, tx.height, TextureFormat.ARGB32, false);
            tx2D.SetPixels(tx.GetPixels(0, 0, tx.width, tx.height));
            tx2D.Apply();
            textures[i] = tx2D;
        }

        Material materialNew = new Material(materials[0].shader);
        materialNew.CopyPropertiesFromMaterial(materials[0]);
        mrSelf.sharedMaterial = materialNew;

        Texture2D texture = new Texture2D(1024, 1024);
        materialNew.SetTexture("_MainTex", texture);
        Rect[] rects = texture.PackTextures(textures, 10, 1024);

        for (int i = 0; i < mfChildren.Length; i++)
        {
            if (mfChildren[i].transform == trs)
            {
                continue;
            }
            Rect rect = rects[i];

            Mesh meshCombine = new Mesh(); //这里复制一份mesh，不直接修改原mesh
            meshCombine.vertices = mfChildren[i].sharedMesh.vertices;
            meshCombine.uv = mfChildren[i].sharedMesh.uv;
            meshCombine.normals = mfChildren[i].sharedMesh.normals;
            meshCombine.triangles = mfChildren[i].sharedMesh.triangles;
            meshCombine.RecalculateNormals();

            Vector2[] uvs = new Vector2[meshCombine.uv.Length];
            for (int j = 0; j < uvs.Length; j++)
            {
                uvs[j].x = rect.x + meshCombine.uv[j].x * rect.width;
                uvs[j].y = rect.y + meshCombine.uv[j].y * rect.height;
            }
            meshCombine.uv = uvs;
            combine[i].mesh = meshCombine;
            combine[i].transform = Matrix4x4.TRS(mfChildren[i].transform.position - trs.position, mfChildren[i].transform.rotation, mfChildren[i].transform.lossyScale);

            mfChildren[i].gameObject.SetActive(false);
        }

        Mesh newMesh = new Mesh();
        newMesh.CombineMeshes(combine, true, true);//合并网格
        mfSelf.mesh = newMesh;

        //确定存在指定目录
        if (!Directory.Exists(DefDir+trs.name))
            Directory.CreateDirectory(DefDir + trs.name);

        //导出png贴图
        byte[] bytes = texture.EncodeToPNG();
        string pngPath = DefDir + trs.name+ "/" + go.name + ".png";
        if (bytes != null && bytes.Length > 0)
            File.WriteAllBytes(pngPath, bytes);

        //导出材质球
        string matPath = "Assets" + DefFolder + trs.name + "/" + go.name + ".mat";
        AssetDatabase.CreateAsset(mrSelf.sharedMaterial, matPath);

        //先保存并刷新一次，不然后面加载不到png
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        //给导出的材质球贴上到处的贴图
        Texture s = AssetDatabase.LoadAssetAtPath<Texture>("Assets" + DefFolder + trs.name + "/" + go.name + ".png");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        m.mainTexture = s;

        //导出合并后的网格
        AssetDatabase.CreateAsset(newMesh, "Assets" + DefFolder + trs.name + "/" + go.name+".asset");
        
        //导出预制物
        Object tempPrefab = PrefabUtility.CreateEmptyPrefab("Assets" + DefFolder + trs.name + "/" + go.name + ".prefab");
        PrefabUtility.ReplacePrefab(go, tempPrefab);

        //再保存刷新一次
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        //将选中的对象定位到新生成的网格合并对象上
        Selection.activeObject = go;
    }

}

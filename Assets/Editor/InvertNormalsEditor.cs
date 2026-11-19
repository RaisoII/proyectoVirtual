using UnityEngine;
using UnityEditor;

public class InvertNormalsEditor : EditorWindow
{
    private GameObject targetObject;
    private bool includeChildren = true;

    [MenuItem("Tools/Invertir Normales")]
    public static void ShowWindow()
    {
        GetWindow<InvertNormalsEditor>("Invertir Normales");
    }

    private void OnGUI()
    {
        GUILayout.Label("Inversor de Normales de Malla", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetObject = (GameObject)EditorGUILayout.ObjectField(
            "GameObject Objetivo", 
            targetObject, 
            typeof(GameObject), 
            true
        );

        includeChildren = EditorGUILayout.Toggle("Incluir Hijos", includeChildren);

        EditorGUILayout.Space();

        GUI.enabled = targetObject != null;
        if (GUILayout.Button("Invertir Normales", GUILayout.Height(30)))
        {
            InvertGameObjectNormals();
        }
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Nota: Este proceso crea una instancia de la malla en la escena para no sobreescribir el archivo original (FBX/OBJ) de tu proyecto.", 
            MessageType.Info
        );
    }

    private void InvertGameObjectNormals()
    {
        MeshFilter[] meshFilters;

        if (includeChildren)
            meshFilters = targetObject.GetComponentsInChildren<MeshFilter>(true);
        else
            meshFilters = targetObject.GetComponents<MeshFilter>();

        if (meshFilters.Length == 0)
        {
            Debug.LogWarning("El GameObject seleccionado no tiene componentes MeshFilter.");
            return;
        }

        int meshesModified = 0;

        foreach (MeshFilter mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;

            // Instanciamos la malla para no dañar el asset original en el proyecto
            Mesh mesh = Instantiate(mf.sharedMesh);
            mesh.name = mf.sharedMesh.name + "_Inverted";

            // 1. Invertir los vectores de las normales
            Vector3[] normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = -normals[i];
            }
            mesh.normals = normals;

            // 2. Invertir el orden de los vértices de cada triángulo (winding order)
            // Esto es obligatorio para que el Backface Culling renderice el lado correcto
            for (int m = 0; m < mesh.subMeshCount; m++)
            {
                int[] triangles = mesh.GetTriangles(m);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int temp = triangles[i];
                    triangles[i] = triangles[i + 1];
                    triangles[i + 1] = temp;
                }
                mesh.SetTriangles(triangles, m);
            }

            // Recalcular tangentes para que los Normal Maps sigan funcionando bien
            mesh.RecalculateTangents();

            // Registrar para poder hacer Ctrl+Z (Undo)
            Undo.RecordObject(mf, "Invertir Normales");
            mf.sharedMesh = mesh;
            meshesModified++;
        }

        Debug.Log($"Se invirtieron las normales de {meshesModified} malla(s) en '{targetObject.name}'.");
    }
}
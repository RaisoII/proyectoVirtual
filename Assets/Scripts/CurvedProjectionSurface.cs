using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class CurvedProjectionSurface : MonoBehaviour
{
    [Header("══ Distancia Asimétrica en X (Ancho / Posición Lateral) ══")]
    [Tooltip("Distancia lateral hacia la izquierda desde el centro del usuario (metros, valor positivo)")]
    public float leftDistanceX = 1.0f;

    [Tooltip("Distancia lateral hacia la derecha desde el centro del usuario (metros, valor positivo)")]
    public float rightDistanceX = 2.0f;

    [Header("══ Distancia en Z (Profundidad del Frente) ══")]
    [Tooltip("Distancia frontal hacia el parabrisas (metros)")]
    public float distanceZ = 2.0f;

    [Header("══ Ventanas Laterales (Rectas ‖ Z hacia atrás) ══")]
    [Tooltip("Largo de la ventana izquierda hacia atrás (metros)")]
    public float leftWindowLength = 1.5f;

    [Tooltip("Largo de la ventana derecha hacia atrás (metros)")]
    public float rightWindowLength = 1.5f;

    [Header("══ Curvatura de Esquinas ══")]
    [Tooltip("Radio de curvatura esquina izquierda (metros)")]
    [Range(0.01f, 1.5f)]
    public float leftCornerRadius = 0.35f;

    [Tooltip("Radio de curvatura esquina derecha (metros)")]
    [Range(0.01f, 1.5f)]
    public float rightCornerRadius = 0.35f;

    [Tooltip("Suavidad de la curva (subdivisiones)")]
    [Range(2, 32)]
    public int cornerSegments = 16;

    [Header("══ Geometría General ══")]
    [Tooltip("Altura total de la superficie (metros)")]
    public float surfaceHeight = 1.4f;

    [Tooltip("Subdivisiones de los tramos planos")]
    [Range(1, 16)]
    public int flatSegments = 4;

    private void OnEnable() => Build();
    private void OnValidate() => Build();

    private void Build()
    {
        // 1. Limpiar objetos hijos si quedaron de la versión anterior
        CleanOldChildren();

        // 2. Coordenadas de las paredes laterales en X
        float leftX = -Mathf.Abs(leftDistanceX);
        float rightX = Mathf.Abs(rightDistanceX);
        float totalWidth = rightX - leftX;

        // Proteger radios de curvatura
        float rL = Mathf.Min(leftCornerRadius, totalWidth * 0.45f, Mathf.Max(0.05f, leftWindowLength) * 0.95f);
        float rR = Mathf.Min(rightCornerRadius, totalWidth * 0.45f, Mathf.Max(0.05f, rightWindowLength) * 0.95f);

        // Centros de los cuartos de círculo
        float lcx = leftX + rL;
        float lcz = distanceZ - rL;
        float rcx = rightX - rR;
        float rcz = distanceZ - rR;

        // 3. Generar el perfil 2D continuo de izquierda a derecha en un solo trazo
        var pts = new List<Vector2>();

        // ── Tramo 1: Ventana lateral izquierda (recta ‖ Z) ──
        float leftSideStart = lcz - leftWindowLength;
        for (int i = 0; i <= flatSegments; i++)
        {
            float t = (float)i / flatSegments;
            pts.Add(new Vector2(leftX, Mathf.Lerp(leftSideStart, lcz, t)));
        }

        // ── Tramo 2: Esquina izquierda (cuarto de círculo: π → π/2) ──
        for (int i = 1; i <= cornerSegments; i++)
        {
            float t = (float)i / cornerSegments;
            float angle = Mathf.Lerp(Mathf.PI, Mathf.PI * 0.5f, t);
            pts.Add(new Vector2(lcx + rL * Mathf.Cos(angle), lcz + rL * Mathf.Sin(angle)));
        }

        // ── Tramo 3: Frente 100% plano (z = distanceZ constante) ──
        for (int i = 1; i <= flatSegments; i++)
        {
            float t = (float)i / flatSegments;
            float x = Mathf.Lerp(lcx, rcx, t);
            pts.Add(new Vector2(x, distanceZ));
        }

        // ── Tramo 4: Esquina derecha (cuarto de círculo: π/2 → 0) ──
        for (int i = 1; i <= cornerSegments; i++)
        {
            float t = (float)i / cornerSegments;
            float angle = Mathf.Lerp(Mathf.PI * 0.5f, 0f, t);
            pts.Add(new Vector2(rcx + rR * Mathf.Cos(angle), rcz + rR * Mathf.Sin(angle)));
        }

        // ── Tramo 5: Ventana lateral derecha (recta ‖ Z) ──
        float rightSideEnd = rcz - rightWindowLength;
        for (int i = 1; i <= flatSegments; i++)
        {
            float t = (float)i / flatSegments;
            pts.Add(new Vector2(rightX, Mathf.Lerp(rcz, rightSideEnd, t)));
        }

        // 4. Construir la malla única en este mismo GameObject
        BuildSingleMesh(pts);
    }

    private void BuildSingleMesh(List<Vector2> profile)
    {
        var mf = GetComponent<MeshFilter>();
        if (mf == null) return;

        int cols = profile.Count;
        float[] arcLen = new float[cols];
        arcLen[0] = 0f;
        for (int i = 1; i < cols; i++)
        {
            arcLen[i] = arcLen[i - 1] + Vector2.Distance(profile[i], profile[i - 1]);
        }
        float totalArc = arcLen[cols - 1];

        float halfH = surfaceHeight * 0.5f;
        var verts = new Vector3[cols * 2];
        var uvs = new Vector2[cols * 2];

        // UVs continuas de 0 a 1 a lo largo de toda la superficie
        for (int i = 0; i < cols; i++)
        {
            float u = totalArc > 0f ? arcLen[i] / totalArc : 0f;
            verts[i * 2]     = new Vector3(profile[i].x, -halfH, profile[i].y);
            verts[i * 2 + 1] = new Vector3(profile[i].x,  halfH, profile[i].y);
            uvs[i * 2]       = new Vector2(u, 0f);
            uvs[i * 2 + 1]   = new Vector2(u, 1f);
        }

        int segCount = cols - 1;
        var tris = new int[segCount * 6];
        for (int i = 0; i < segCount; i++)
        {
            int v = i * 2, k = i * 6;
            tris[k]     = v;
            tris[k + 1] = v + 1;
            tris[k + 2] = v + 2;
            tris[k + 3] = v + 2;
            tris[k + 4] = v + 1;
            tris[k + 5] = v + 3;
        }

        var mesh = new Mesh { name = "AsymmetricCurvedSurface" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.sharedMesh = mesh;
    }

    private void CleanOldChildren()
    {
        string[] names = { "Left_Window", "Center_Windshield", "Right_Window" };
        foreach (var n in names)
        {
            Transform t = transform.Find(n);
            if (t != null)
            {
                if (Application.isPlaying)
                    Destroy(t.gameObject);
                else
                    DestroyImmediate(t.gameObject);
            }
        }
    }
}

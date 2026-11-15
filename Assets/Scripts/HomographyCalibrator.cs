using UnityEngine;

[ExecuteAlways]
public class HomographyCalibrator : MonoBehaviour
{
    public Material targetMaterial;

    [Header("Coordenadas UV en la imagen de la cámara (0 a 1)")]
    [Tooltip("Abajo-Izquierda de la imagen original que irá a la esquina Abajo-Izquierda del Quad")]
    public Vector2 srcBottomLeft = new Vector2(0f, 0f);
    [Tooltip("Abajo-Derecha")]
    public Vector2 srcBottomRight = new Vector2(1f, 0f);
    [Tooltip("Arriba-Derecha")]
    public Vector2 srcTopRight = new Vector2(1f, 1f);
    [Tooltip("Arriba-Izquierda")]
    public Vector2 srcTopLeft = new Vector2(0f, 1f);

    void Update()
    {
        if (targetMaterial == null)
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null) targetMaterial = renderer.sharedMaterial;
        }

        if (targetMaterial == null) return;

        // Las esquinas destino son las UV estándar del Quad: (0,0), (1,0), (1,1), (0,1)
        Vector2[] dst = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };

        // Las esquinas origen son qué parte de la textura de la cámara mapeamos a cada esquina del Quad
        Vector2[] src = new Vector2[]
        {
            srcBottomLeft,
            srcBottomRight,
            srcTopRight,
            srcTopLeft
        };

        float[,] H = CalculateHomography(dst, src);

        targetMaterial.SetVector("_Row0", new Vector4(H[0, 0], H[0, 1], H[0, 2], 0f));
        targetMaterial.SetVector("_Row1", new Vector4(H[1, 0], H[1, 1], H[1, 2], 0f));
        targetMaterial.SetVector("_Row2", new Vector4(H[2, 0], H[2, 1], H[2, 2], 0f));
    }

    // Resuelve H tal que src ~ H * dst
    private float[,] CalculateHomography(Vector2[] dst, Vector2[] src)
    {
        float[,] A = new float[8, 8];
        float[] B = new float[8];

        for (int i = 0; i < 4; i++)
        {
            float x = dst[i].x;
            float y = dst[i].y;
            float u = src[i].x;
            float v = src[i].y;

            A[2 * i, 0] = x;
            A[2 * i, 1] = y;
            A[2 * i, 2] = 1;
            A[2 * i, 3] = 0;
            A[2 * i, 4] = 0;
            A[2 * i, 5] = 0;
            A[2 * i, 6] = -u * x;
            A[2 * i, 7] = -u * y;
            B[2 * i] = u;

            A[2 * i + 1, 0] = 0;
            A[2 * i + 1, 1] = 0;
            A[2 * i + 1, 2] = 0;
            A[2 * i + 1, 3] = x;
            A[2 * i + 1, 4] = y;
            A[2 * i + 1, 5] = 1;
            A[2 * i + 1, 6] = -v * x;
            A[2 * i + 1, 7] = -v * y;
            B[2 * i + 1] = v;
        }

        float[] h = SolveGaussian(A, B);

        return new float[,]
        {
            { h[0], h[1], h[2] },
            { h[3], h[4], h[5] },
            { h[6], h[7], 1f   }
        };
    }

    private float[] SolveGaussian(float[,] A, float[] B)
    {
        int n = 8;
        for (int i = 0; i < n; i++)
        {
            int max = i;
            for (int k = i + 1; k < n; k++)
                if (Mathf.Abs(A[k, i]) > Mathf.Abs(A[max, i])) max = k;

            for (int k = i; k < n; k++)
            {
                float tmp = A[max, k];
                A[max, k] = A[i, k];
                A[i, k] = tmp;
            }
            float t = B[max];
            B[max] = B[i];
            B[i] = t;

            for (int k = i + 1; k < n; k++)
            {
                float factor = A[k, i] / A[i, i];
                B[k] -= factor * B[i];
                for (int j = i; j < n; j++)
                    A[k, j] -= factor * A[i, j];
            }
        }

        float[] x = new float[n];
        for (int i = n - 1; i >= 0; i--)
        {
            float sum = 0f;
            for (int j = i + 1; j < n; j++)
                sum += A[i, j] * x[j];
            x[i] = (B[i] - sum) / A[i, i];
        }
        return x;
    }
}
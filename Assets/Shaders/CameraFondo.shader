Shader "Custom/CameraFondo"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FOVHorizontal ("FOV Horizontal (Grados)", Range(10, 170)) = 90.0
        _AspectRatio ("Relacion de Aspecto (Ancho/Alto)", Float) = 1.777
        _FondoColor ("Color Fuera de Camara", Color) = (0.05, 0.05, 0.05, 1)
        [Toggle] _PreviewMargenes ("Vista previa de margenes", Float) = 1.0
        _RotacionY ("Ajuste Horizontal (Grados)", Range(-180, 180)) = 0.0
        _RotacionX ("Ajuste Vertical (Grados)", Range(-90, 90)) = 0.0
    }
    SubShader
    {
        Tags { "Queue"="Background+1" "RenderType"="Opaque" }
        Pass
        {
            // Cull Front permite ver la esfera desde adentro sin invertir normales en Blender
            Cull Front
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _FOVHorizontal;
            float _AspectRatio;
            float _PreviewMargenes;
            fixed4 _FondoColor;
            float _RotacionY;
            float _RotacionX;

            struct appdata {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float3 localDir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Función auxiliar para rotar el vector de dirección
            float3 RotateVector(float3 v, float degX, float degY) {
                float radX = radians(degX);
                float radY = radians(degY);
                
                // Rotación en X (Inclinación vertical)
                float sX = sin(radX), cX = cos(radX);
                v = float3(v.x, v.y * cX - v.z * sX, v.y * sX + v.z * cX);
                
                // Rotación en Y (Giro horizontal)
                float sY = sin(radY), cY = cos(radY);
                v = float3(v.x * cY + v.z * sY, v.y, -v.x * sY + v.z * cY);
                
                return v;
            }

            v2f vert (appdata v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                // Guardamos la dirección desde el centro de la esfera hacia el vértice
                o.localDir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Normalizamos la dirección y aplicamos ajustes de alineación
                float3 dir = RotateVector(normalize(i.localDir), _RotacionX, _RotacionY);

                // Proyección perspectiva (pinhole camera) sobre el plano Z = 1
                float halfFovRad = radians(_FOVHorizontal * 0.5);
                float scaleX = tan(halfFovRad);
                float scaleY = scaleX / _AspectRatio;

                float2 uv;
                if (dir.z > 0.0) {
                    uv.x = (dir.x / dir.z) / (2.0 * scaleX) + 0.5;
                    uv.y = (dir.y / dir.z) / (2.0 * scaleY) + 0.5;
                } else {
                    // En la parte trasera se extiende el borde más cercano hasta cubrir el domo.
                    float2 edgeDir = float2(dir.x / scaleX, dir.y / scaleY);
                    float edgeScale = max(abs(edgeDir.x), abs(edgeDir.y));
                    edgeDir = edgeScale > 0.00001 ? edgeDir / edgeScale : float2(0.0, 1.0);
                    uv = edgeDir * 0.5 + 0.5;
                }

                if (_PreviewMargenes > 0.5) {
                    if (dir.z <= 0.0 || uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) {
                        return fixed4(0.12, 0.20, 0.23, 1.0);
                    }

                    float2 gridPosition = frac(uv * 8.0);
                    float2 gridDistance = abs(gridPosition - 0.5);
                    float gridLine = step(0.47, max(gridDistance.x, gridDistance.y));
                    float edgeDistance = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                    float border = 1.0 - step(0.004, edgeDistance);
                    fixed3 previewColor = lerp(fixed3(0.28, 0.33, 0.34), fixed3(0.42, 0.47, 0.46), gridLine);
                    previewColor = lerp(previewColor, fixed3(1.0, 0.55, 0.16), border);
                    return fixed4(previewColor, 1.0);
                }

                // Muestrea dentro del primer/último texel, aunque la textura use wrap Repeat.
                float2 halfTexel = _MainTex_TexelSize.xy * 0.5;
                uv = clamp(uv, halfTexel, 1.0 - halfTexel);
                return tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
}
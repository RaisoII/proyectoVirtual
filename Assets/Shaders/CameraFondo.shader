Shader "Custom/CameraFondo"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FOVHorizontal ("FOV Horizontal (Grados)", Range(10, 170)) = 90.0
        _AspectRatio ("Relacion de Aspecto (Ancho/Alto)", Float) = 1.777
        _FondoColor ("Color Fuera de Camara", Color) = (0.05, 0.05, 0.05, 1)
        _RotacionY ("Ajuste Horizontal (Grados)", Range(-180, 180)) = 0.0
        _RotacionX ("Ajuste Vertical (Grados)", Range(-90, 90)) = 0.0
        [Toggle] _InvertirY ("Invertir Vertical (Arriba/Abajo)", Float) = 1.0
        [Toggle] _InvertirX ("Invertir Horizontal (Espejo)", Float) = 0.0
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
            float _FOVHorizontal;
            float _AspectRatio;
            fixed4 _FondoColor;
            float _RotacionY;
            float _RotacionX;
            float _InvertirY;
            float _InvertirX;

            struct appdata {
                float4 vertex : POSITION;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float3 localDir : TEXCOORD0;
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
                o.vertex = UnityObjectToClipPos(v.vertex);
                // Guardamos la dirección desde el centro de la esfera hacia el vértice
                o.localDir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // Normalizamos la dirección y aplicamos ajustes de alineación
                float3 dir = RotateVector(normalize(i.localDir), _RotacionX, _RotacionY);

                // Asumimos que el frente del auto es +Z. Si la proyección sale atrás, cambia > a < y el signo de dir.z
                if (dir.z <= 0.0) {
                    return _FondoColor;
                }

                // Proyección perspectiva (pinhole camera) sobre el plano Z = 1
                float halfFovRad = radians(_FOVHorizontal * 0.5);
                float scaleX = tan(halfFovRad);
                float scaleY = scaleX / _AspectRatio;

                float2 uv;
                uv.x = (dir.x / dir.z) / (2.0 * scaleX) + 0.5;
                uv.y = (dir.y / dir.z) / (2.0 * scaleY) + 0.5;

                // Si el píxel cae fuera del rectángulo de la cámara, pintamos el color de fondo
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) {
                    return _FondoColor;
                }

                if (_InvertirX > 0.5) {
                    uv.x = 1.0 - uv.x;
                }
                if (_InvertirY > 0.5) {
                    uv.y = 1.0 - uv.y;
                }

                return tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
}
// The Heartbeat orb's heart: a soft blob with a few rounded lobes that turn around it, swelling with its own
// frequency band. It glows additively and draws both sides, so its folds never sort wrongly or drop out.
Shader "MusicInYourHands/Orb"
{
    Properties
    {
        _Tint ("Layer colour", Color) = (1, 0.37, 0.68, 1)
        _Lobes ("Lobes", Float) = 3
        _Spin ("Spin (rad/s, sign is direction)", Float) = 1
        _Amp ("Lobe depth", Float) = 0.2
        _Band ("Band 0-1 (set by HeartbeatOrb)", Float) = 0.3
        _Kick ("Beat 0-1 (set by HeartbeatOrb)", Float) = 0
        _Opacity ("Base opacity", Float) = 0.2
        _Alpha ("Fade (set by HeartbeatOrb)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+2" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Lobes;
                float _Spin;
                float _Amp;
                float _Band;
                float _Kick;
                float _Opacity;
                float _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Gielis' superformula: one closed "supershape" curve. m sets the symmetry (petals), n1 how pinched the
            // petals are (small n1 = spiky star, large = soft and round), n2 and n3 the petal outline.
            float Superformula(float angle, float m, float n1, float n2, float n3)
            {
                float a = pow(abs(cos(m * angle * 0.25)), n2);
                float b = pow(abs(sin(m * angle * 0.25)), n3);
                return pow(max(a + b, 1e-4), -1.0 / n1);
            }

            // The layer's surface in object space for a direction from the centre: a 3D supershape (two supershape
            // curves, around and up-down) that morphs with the music, blended with a sphere so it stays organic.
            float3 Blob(float3 dir)
            {
                dir = normalize(dir);
                float t = _Time.y;
                float lon = atan2(dir.z, dir.x) + t * _Spin * 0.35;
                float lat = asin(clamp(dir.y, -1, 1));
                float energy = saturate(_Band * 0.9 + _Kick * 0.5);
                float n1 = lerp(2.4, 0.75, energy) + 0.35 * sin(t * 0.7 + _Lobes);
                float n2 = 1.7 + 0.7 * sin(t * 0.45 + _Lobes * 1.3);
                float r1 = Superformula(lon, _Lobes, n1, n2, n2);
                float r2 = Superformula(lat, _Lobes - 1, n1 + 1, 1.6, 1.6);
                float3 shape = float3(r1 * cos(lon) * r2 * cos(lat), r2 * sin(lat) * 0.9, r1 * sin(lon) * r2 * cos(lat));
                float len = length(shape);
                shape *= min(1.0, 1.35 / max(len, 1e-3)) / 1.15;
                float3 sphere = dir;
                return lerp(sphere, shape, saturate(_Amp * 5)) * 0.5;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 dir = normalize(input.positionOS.xyz);
                float3 p = Blob(dir);
                // Normal from two nearby points on the bent surface, so rims follow the blob's real shape.
                float3 helper = abs(dir.y) > 0.95 ? float3(1, 0, 0) : float3(0, 1, 0);
                float3 t1 = normalize(cross(dir, helper));
                float3 t2 = cross(dir, t1);
                const float e = 0.02;
                float3 n = normalize(cross(Blob(dir + t1 * e) - p, Blob(dir + t2 * e) - p));
                if (dot(n, dir) < 0) n = -n;

                output.positionOS = p;
                output.positionWS = TransformObjectToWorld(p);
                output.normalWS = TransformObjectToWorldNormal(n);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                float ndv = abs(dot(n, v));
                float fresnel = pow(1 - ndv, 2.4);

                // A soft shimmer that drifts across the surface.
                float shimmer = sin(dot(input.positionOS, float3(7, 11, 5)) * 2 + _Time.y * 2.2) * 0.5 + 0.5;
                // Hot in the middle (white-gold where the heart faces you), its own colour towards the edges.
                float3 hot = float3(1.0, 0.9, 0.72);
                float centre = pow(ndv, 2.5) * (0.75 + _Kick * 0.25);
                float3 color = lerp(_Tint.rgb * (0.8 + 0.25 * shimmer), hot, centre);
                color += _Tint.rgb * fresnel * 0.5;

                float alpha = _Opacity * (0.45 + centre * 1.1) + fresnel * 0.3 * (1 + _Kick * 0.6);
                return half4(color, saturate(alpha) * _Alpha);
            }
            ENDHLSL
        }
    }
}

// Sound pool's handful of water: the little body of water you scoop up in your cupped hands. The mesh is two discs
// (SoundPool.HandfulMesh): uv.y 1 is the water's surface, nearly level and rounding down at the rim, with slow rings
// running out over it (_Ripple: how much, from the beat and the hands' movement); uv.y 0 is the bowl under it, resting
// in the palms. Shaped here, in object space, so it can be scaled to the hands. It looks like the pool's water (Water
// shader): see-through teal, brighter at grazing angles, with a soft highlight. It draws after the hands, so it shows
// on top of the palms it sits in.
Shader "MusicInYourHands/SoundPoolHandful"
{
    Properties
    {
        _Deep ("Deep colour", Color) = (0.05, 0.22, 0.32, 0.42)
        _Shallow ("Light colour", Color) = (0.37, 0.83, 1, 1)
        _Ripple ("Ripple 0-1 (set by SoundPool)", Float) = 0.3
        _Clock ("Time (set by SoundPool)", Float) = 0
        _Alpha ("Fade (set by SoundPool)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Deep;
                float4 _Shallow;
                float _Ripple;
                float _Clock;
                float _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 info : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float2 xz = input.positionOS.xz;
                // 0 in the middle, 1 at the rim.
                float q = saturate(length(xz) * 2);
                float2 dir = q > 1e-4 ? normalize(xz) : float2(1, 0);
                float top = input.uv.y;
                float y, slope;
                if (top > 0.5)
                {
                    // Nearly level in the middle, rounding gently down to the rim, with slow rings running outwards.
                    float q2 = q * q;
                    y = 1 - q2 * (0.7 + 0.3 * q2);
                    slope = -(1.4 * q + 1.2 * q2 * q);
                    float k = 14, phase = k * q - _Clock * 3.2, a = _Ripple * 0.1;
                    y += a * (1 - q) * sin(phase);
                    slope += a * (k * (1 - q) * cos(phase) - sin(phase));
                }
                else
                {
                    // The bowl, about half as deep as the water stands above the rim.
                    y = -0.6 * (1 - q * q);
                    slope = 1.2 * q;
                }
                // The slope across x and z (q changes twice as fast as the distance from the middle).
                float2 grad = slope * 2 * dir;
                float3 normal = top > 0.5 ? float3(-grad.x, 1, -grad.y) : float3(grad.x, -1, grad.y);
                output.positionWS = TransformObjectToWorld(float3(xz.x, y, xz.y));
                output.normalWS = TransformObjectToWorldNormal(normal);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.info = float2(q, top);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                if (dot(n, v) < 0) n = -n;
                float fresnel = pow(1 - saturate(dot(n, v)), 3);
                // The same soft light as the pool's surface, from above and behind the viewer.
                float3 light = normalize(float3(0.25, 1, -0.35));
                float spec = pow(saturate(dot(reflect(-light, n), v)), 40) * 0.9;
                float top = input.info.y;
                float3 color = lerp(_Deep.rgb, _Shallow.rgb, saturate(0.45 + fresnel * 0.5 + top * 0.15));
                // A thin brighter line where the water's surface meets the hands.
                float rim = smoothstep(0.7, 1, input.info.x) * top * 0.4;
                color += spec + _Shallow.rgb * rim;
                float alpha = _Deep.a * 0.8 + fresnel * 0.35 + spec + rim;
                // Mostly the water's surface shows; the bowl under it, in the hands, only faintly.
                alpha *= lerp(0.35, 1, top);
                return half4(color, saturate(alpha) * _Alpha);
            }
            ENDHLSL
        }
    }
}

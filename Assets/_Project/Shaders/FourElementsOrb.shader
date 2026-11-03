// Four elements: the wobbling ball of water held between the palms. The vertex stage bends a sphere with three slow
// waves that grow with the bass (_Bass) and a ripple that runs from top to bottom after each kick (_Wave: seconds since
// it); the fragment stage shades it as clear water: a deep blue body, a bright rim, glints of light from above and a
// web of caustics drifting inside it. Drawn see-through, after the hands' fill, without writing depth.
Shader "MusicInYourHands/FourElementsOrb"
{
    Properties
    {
        _Deep ("Body colour", Color) = (0.12, 0.42, 0.78, 0.2)
        _Light ("Rim and caustics", Color) = (0.55, 0.92, 1, 1)
        _Bass ("Bass 0-1 (set by FourElements)", Float) = 0.3
        _Wave ("Seconds since the kick (set by FourElements)", Float) = 9
        _Kick ("Beat 0-1 (set by FourElements)", Float) = 0
        _Clock ("Time (set by FourElements)", Float) = 0
        _Alpha ("Fade (set by FourElements)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+3" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
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
                float4 _Light;
                float _Bass;
                float _Wave;
                float _Kick;
                float _Clock;
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

            // The water's surface for a direction from its centre (object space, radius about 0.5).
            float3 Surface(float3 dir)
            {
                float t = _Clock;
                float wobble = sin(dot(dir, float3(0.8, 0.5, 0.3)) * 3.1 + t * 2.3) * 0.05
                             + sin(dot(dir, float3(-0.4, 0.9, 0.2)) * 4.3 - t * 1.7) * 0.035
                             + sin(dot(dir, float3(0.3, -0.2, 0.95)) * 5.9 + t * 3.1) * 0.025;
                wobble *= 0.55 + _Bass * 1.3;
                // A ripple running down from the top after each kick, fading as it goes.
                float down = acos(clamp(dir.y, -1, 1));
                float front = _Wave * 9;
                float ripple = sin((down * 3 - front) * 3.1) * exp(-_Wave * 2.2) * smoothstep(0.0, 1.2, front - down * 3 + 1.2);
                return dir * 0.5 * (1 + wobble + ripple * 0.07);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 dir = normalize(input.positionOS.xyz);
                float3 p = Surface(dir);
                // The normal from two nearby points on the bent surface, so the rim follows its real shape.
                float3 helper = abs(dir.y) > 0.95 ? float3(1, 0, 0) : float3(0, 1, 0);
                float3 t1 = normalize(cross(dir, helper));
                float3 t2 = cross(dir, t1);
                const float e = 0.03;
                float3 n = normalize(cross(Surface(normalize(dir + t1 * e)) - p, Surface(normalize(dir + t2 * e)) - p));
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
                float ndv = saturate(dot(n, v));
                float fresnel = pow(1 - ndv, 2.6);
                float3 q = input.positionOS * 13;
                float t = _Clock;
                // Light through moving water: a web of thin bright lines where three warped waves cross zero, drifting.
                float wave = sin(q.x + sin(q.y * 1.3 + t * 1.1) * 1.4) + sin(q.y * 1.1 + sin(q.z * 1.7 - t * 0.9) * 1.4) + sin(q.z * 0.9 + sin(q.x * 1.2 + t * 1.3) * 1.4);
                float caustic = pow(saturate(1 - abs(wave) * 0.45), 7) * (1 + _Kick * 0.8);
                float3 light = normalize(float3(0.3, 1, -0.45));
                float spec = pow(saturate(dot(reflect(-light, n), v)), 60) * 1.6;
                float spec2 = pow(saturate(dot(reflect(-normalize(float3(-0.7, 0.35, -0.5)), n), v)), 22) * 0.35;
                // Light gathers towards the bottom of the water, as in a glass of it.
                float depth = saturate(0.5 - input.positionOS.y);
                float3 color = lerp(_Deep.rgb, _Light.rgb, saturate(fresnel * 1.2 + caustic * 0.6 + depth * 0.15));
                color += spec + spec2 + _Light.rgb * caustic * 0.35;
                float alpha = _Deep.a + fresnel * 0.6 + caustic * 0.3 + spec + spec2 * 0.5;
                return half4(color, saturate(alpha) * _Alpha);
            }
            ENDHLSL
        }
    }
}

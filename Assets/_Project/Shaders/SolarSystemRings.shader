// Saturn's rings, on a quad lying in its equator (the quad's edge is 2.3 planet radii out): a faint inner ring, the
// bright broad ring, the dark gap after it and the outer ring with its own thin gap, all made of fine bands of
// different brightness. The planet's shadow falls across the rings on the side away from the sun (_SunPos, _PlanetPos,
// _PlanetRadius), and they brighten with the music (_Glow). Drawn after the planet, so the part behind it is hidden.
Shader "MusicInYourHands/SolarSystemRings"
{
    Properties
    {
        _ColorA ("Bright colour", Color) = (0.95, 0.87, 0.68, 1)
        _ColorB ("Dim colour", Color) = (0.7, 0.6, 0.46, 1)
        _SunPos ("Sun position (set by SolarSystem)", Vector) = (0, 0, 0, 0)
        _PlanetPos ("Planet position (set by SolarSystem)", Vector) = (0, 0, 0, 0)
        _PlanetRadius ("Planet radius, m (set by SolarSystem)", Float) = 0.025
        _SunPower ("Sunlight (set by SolarSystem)", Float) = 1
        _Glow ("Music glow 0-1 (set by SolarSystem)", Float) = 0
        _Alpha ("Fade (set by SolarSystem)", Float) = 1
        _EnvironmentDepthBias ("Real-room depth bias (share of the distance)", Float) = 0.03
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-2" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RoomOcclusion.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorA;
                float4 _ColorB;
                float4 _SunPos;
                float4 _PlanetPos;
                float _PlanetRadius;
                float _SunPower;
                float _Glow;
                float _Alpha;
                float _EnvironmentDepthBias;
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
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }

            float Hash(float n)
            {
                n = frac(n * 0.1031);
                n *= n + 33.33;
                n *= n + n;
                return frac(n);
            }

            // Smooth 1D noise along the radius, for the rings' fine bands.
            float Bands(float x)
            {
                float i = floor(x);
                float f = frac(x);
                return lerp(Hash(i), Hash(i + 1), f * f * (3 - 2 * f));
            }

            float Band(float r, float from, float to, float soft)
            {
                return smoothstep(from - soft, from + soft, r) * smoothstep(to + soft, to - soft, r);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // r is 1 at the quad's edge, 2.3 planet radii out.
                float r = length(input.uv * 2 - 1);
                float density = Band(r, 0.54, 0.665, 0.008) * 0.22
                              + Band(r, 0.665, 0.848, 0.006) * (0.75 + 0.25 * smoothstep(0.7, 0.8, r))
                              + Band(r, 0.883, 0.987, 0.005) * 0.6 * (1 - Band(r, 0.955, 0.962, 0.002) * 0.8);
                density *= 0.72 + Bands(r * 150) * 0.2 + Bands(r * 47 + 9) * 0.16;

                // The planet's shadow: is the planet between this point and the sun?
                float3 toSun = normalize(_SunPos.xyz - input.positionWS);
                float3 toPlanet = _PlanetPos.xyz - input.positionWS;
                float along = dot(toPlanet, toSun);
                float miss = length(toPlanet - toSun * along);
                float shadow = step(0, along) * smoothstep(_PlanetRadius * 1.02, _PlanetRadius * 0.88, miss);

                float tone = Bands(r * 31 + 3);
                float3 color = lerp(_ColorB.rgb, _ColorA.rgb, saturate(tone * 0.6 + density * 0.5));
                color *= (0.3 + 0.75 * (1 - shadow * 0.85)) * _SunPower * (1 + _Glow * 0.45);
                float visible = META_DEPTH_GET_OCCLUSION_VALUE_WORLDPOS(input.positionWS, _EnvironmentDepthBias);
                return half4(color, saturate(density * 0.9 * _Alpha) * visible);
            }
            ENDHLSL
        }
    }
}

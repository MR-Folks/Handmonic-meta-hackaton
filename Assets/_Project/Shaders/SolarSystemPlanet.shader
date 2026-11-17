// The Solar system's sun, planets and moons: a sphere whose surface is drawn in code. _Kind picks the look (0 the sun,
// 1 rocky, 2 cloudy, 3 Earth, 4 Mars, 5 banded giant, 6 ice giant) from three colours and a seed; the pattern is fixed
// to the sphere, so it turns as the planet spins. Planets are lit by the sun (_SunPos) with a soft line between day
// and night and a thin glowing atmosphere at the rim; Earth's oceans catch the sun and its cities glow at night. On top
// comes the music: _Glow (the planet's band) brightens its day side and its air, _Twinkle flashes it for a melody
// note, and _Highlight rims it in white while a pinch is close enough to pick it up. The sun lights itself: hot white
// in the middle, deep orange at the edge, its surface slowly boiling, brighter with _SunPower.
// It writes depth, so orbits, rings and comets behind a planet are hidden by it. Real things hide it behind them while
// the real room's depth is on (RoomOcclusion.hlsl).
Shader "MusicInYourHands/SolarSystemPlanet"
{
    Properties
    {
        _Kind ("Look (0 sun, 1 rocky, 2 cloudy, 3 Earth, 4 Mars, 5 banded, 6 ice)", Float) = 1
        _ColorA ("Main colour", Color) = (0.6, 0.58, 0.55, 1)
        _ColorB ("Second colour", Color) = (0.4, 0.38, 0.36, 1)
        _ColorC ("Accent colour", Color) = (0.8, 0.4, 0.25, 1)
        _Air ("Atmosphere colour (alpha: strength)", Color) = (0.6, 0.8, 1, 0.5)
        _Seed ("Pattern seed", Float) = 0
        _Spot ("Storm spot", Float) = 0
        _SunPos ("Sun position (set by SolarSystem)", Vector) = (0, 0, 0, 0)
        _SunPower ("Sunlight (set by SolarSystem)", Float) = 1
        _Glow ("Music glow 0-1 (set by SolarSystem)", Float) = 0
        _Twinkle ("Note twinkle 0-1 (set by SolarSystem)", Float) = 0
        _Highlight ("Within reach 0-1 (set by SolarSystem)", Float) = 0
        _Clock ("Time (set by SolarSystem)", Float) = 0
        _Alpha ("Fade (set by SolarSystem)", Float) = 1
        _EnvironmentDepthBias ("Real-room depth bias (share of the distance)", Float) = 0.03
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-3" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
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
                float _Kind;
                float4 _ColorA;
                float4 _ColorB;
                float4 _ColorC;
                float4 _Air;
                float _Seed;
                float _Spot;
                float4 _SunPos;
                float _SunPower;
                float _Glow;
                float _Twinkle;
                float _Highlight;
                float _Clock;
                float _Alpha;
                float _EnvironmentDepthBias;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 direction : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.direction = input.positionOS.xyz;
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            // Smooth value noise, 0-1.
            float Noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            // Four octaves of noise, about 0-1.
            float Fbm(float3 p)
            {
                float sum = 0, amp = 0.5;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    sum += amp * Noise(p);
                    p = p * 2.03 + 3.1;
                    amp *= 0.5;
                }
                return sum / 0.9375;
            }

            // The sun's surface: limb-darkened from white-hot to deep orange, with slowly boiling granules.
            float3 SunSurface(float3 d, float mu)
            {
                float t = _Clock;
                float boil = Fbm(d * 5.5 + float3(t * 0.06, -t * 0.05, t * 0.04));
                float grains = Noise(d * 26 + float3(0, t * 0.35, 0));
                float limb = saturate(mu);
                float3 deep = float3(0.9, 0.17, 0.02);
                float3 warm = float3(1.0, 0.5, 0.06);
                float3 hot = float3(1.0, 0.85, 0.42);
                float3 white = float3(1.0, 0.97, 0.86);
                float3 c = lerp(deep, warm, smoothstep(0.0, 0.45, limb));
                c = lerp(c, hot, smoothstep(0.3, 0.85, limb) * saturate(0.55 + boil * 0.7));
                c = lerp(c, white, smoothstep(0.7, 1.0, limb) * boil * 0.6);
                c *= 0.7 + boil * 0.5 + grains * 0.16;
                return c * _SunPower;
            }

            // A planet's colour at direction d from its centre (in its own turning frame). Also says where Earth's
            // cities are (glowing at night) and where water catches the sun.
            float3 Surface(float3 d, out float cities, out float water)
            {
                cities = 0;
                water = 0;
                float3 s = d + _Seed * float3(1.7, 2.3, 1.1);
                float3 result;
                if (_Kind < 1.5)
                {
                    // Rocky: dusty plains with darker lowlands, pocked with small craters.
                    float n = Fbm(s * 2.6);
                    float3 c = lerp(_ColorB.rgb, _ColorA.rgb, smoothstep(0.3, 0.7, n));
                    float pits = Noise(s * 13);
                    c *= 0.9 + smoothstep(0.66, 0.74, pits) * 0.25 - smoothstep(0.74, 0.84, pits) * 0.3;
                    result = c;
                }
                else if (_Kind < 2.5)
                {
                    // Cloudy: soft bands of cloud swirled by slow eddies.
                    float swirl = Fbm(s * 2.2 + float3(0, _Clock * 0.02, 0));
                    float bands = sin(d.y * 6.5 + swirl * 5.0) * 0.5 + 0.5;
                    result = lerp(_ColorB.rgb, _ColorA.rgb, smoothstep(0.1, 0.9, bands * 0.55 + swirl * 0.55));
                }
                else if (_Kind < 3.5)
                {
                    // Earth: deep blue oceans, green and sandy continents, ice at the poles, drifting clouds.
                    float shape = Fbm(s * 1.7);
                    float land = smoothstep(0.5, 0.54, shape);
                    float3 ground = lerp(_ColorB.rgb, _ColorC.rgb, smoothstep(0.42, 0.62, Noise(s * 5.5)));
                    float3 c = lerp(_ColorA.rgb * (0.85 + shape * 0.3), ground, land);
                    float ice = smoothstep(0.8, 0.86, abs(d.y) + (Noise(s * 7) - 0.5) * 0.08);
                    c = lerp(c, float3(0.93, 0.96, 1.0), ice);
                    float cloud = smoothstep(0.5, 0.74, Fbm(s * 2.8 + float3(_Clock * 0.025, 0, 0)));
                    c = lerp(c, float3(1, 1, 1), cloud * 0.9);
                    cities = land * (1 - ice) * (1 - cloud) * smoothstep(0.55, 0.8, Noise(s * 18));
                    water = (1 - land) * (1 - ice) * (1 - cloud);
                    result = c;
                }
                else if (_Kind < 4.5)
                {
                    // Mars: rusty plains, dark markings and small white polar caps.
                    float n = Fbm(s * 2.4);
                    float3 c = lerp(_ColorA.rgb, _ColorB.rgb, smoothstep(0.45, 0.65, n));
                    c *= 0.92 + Noise(s * 12) * 0.14;
                    result = lerp(c, float3(0.97, 0.95, 0.92), smoothstep(0.87, 0.91, abs(d.y) + n * 0.05));
                }
                else if (_Kind < 5.5)
                {
                    // Banded giant: light zones and darker belts with turbulent edges, and (with _Spot) a great storm.
                    float turbulence = Fbm(float3(d.x * 2.5, d.y * 9, d.z * 2.5) + s);
                    float latitude = d.y + (turbulence - 0.5) * 0.09;
                    // Belts of uneven width, the bright zone round the equator widest.
                    float bands = sin(latitude * 15 + sin(latitude * 5.3 + 1.1) * 1.8) * 0.5 + 0.5;
                    bands *= smoothstep(0.05, 0.22, abs(latitude + 0.03)) * 0.6 + 0.4;
                    float fine = sin(latitude * 63 + turbulence * 4) * 0.5 + 0.5;
                    float3 c = lerp(_ColorA.rgb, _ColorB.rgb, smoothstep(0.3, 0.8, bands) * 0.9);
                    c = lerp(c, _ColorC.rgb, smoothstep(0.75, 1.0, fine) * 0.25);
                    c *= 0.93 + fine * 0.1;
                    float2 storm = float2(atan2(d.z, d.x) - 0.7, (d.y + 0.36) * 3.0);
                    c = lerp(c, _ColorC.rgb, saturate(exp(-dot(storm, storm) * 16) * 1.4 * _Spot));
                    result = c;
                }
                else
                {
                    // Ice giant: a calm haze with faint bands; with _Spot a dark storm and bright streaks of high cloud.
                    float haze = Fbm(s * 1.8);
                    float stripes = sin(d.y * 8 + haze * 2) * 0.5 + 0.5;
                    float3 ice = lerp(_ColorA.rgb, _ColorB.rgb, stripes * 0.35 + smoothstep(0.55, 1.0, abs(d.y)) * 0.45);
                    float2 dark = float2(atan2(d.z, d.x) + 1.2, (d.y + 0.3) * 3.2);
                    ice = lerp(ice, _ColorC.rgb, saturate(exp(-dot(dark, dark) * 20) * _Spot));
                    ice += smoothstep(0.72, 0.9, Noise(float3(d.x * 2.5, d.y * 14, d.z * 2.5) + s)) * 0.2 * _Spot;
                    result = ice;
                }
                return result;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                float mu = saturate(dot(n, v));
                float3 d = normalize(input.direction);
                float visible = META_DEPTH_GET_OCCLUSION_VALUE_WORLDPOS(input.positionWS, _EnvironmentDepthBias);
                if (_Kind < 0.5) return half4(SunSurface(d, mu), _Alpha * visible);

                float3 l = normalize(_SunPos.xyz - input.positionWS);
                float ndl = dot(n, l);
                float day = smoothstep(-0.16, 0.3, ndl);
                float diffuse = saturate(ndl * 0.7 + 0.3);
                float cities, water;
                float3 albedo = Surface(d, cities, water);
                // The night side stays faintly visible, so a planet never reads as a hole in the room.
                float3 color = albedo * (0.18 + mu * 0.1 + day * diffuse * _SunPower);
                // The sun glints off the oceans.
                float3 h = normalize(l + v);
                color += water * pow(saturate(dot(n, h)), 48) * 0.7 * day * _SunPower;
                // Cities glow on the night side.
                color += float3(1.0, 0.72, 0.38) * cities * (1 - day) * 0.9;
                // A thin atmosphere at the rim, brightest on the day side, and lit up all round when the planet is
                // seen against the sun.
                float rim = pow(1 - mu, 2.6);
                float backlit = pow(saturate(dot(-v, l)), 3);
                color += _Air.rgb * rim * (_Air.a * (0.3 + day * 0.7) + backlit * 1.2) * (0.9 + _Glow * 1.3);
                // The music: its band brightens the planet, a melody note flashes it.
                color += albedo * day * _Glow * 0.45;
                color += lerp(_Air.rgb, float3(1, 1, 1), 0.5) * _Twinkle * (0.3 + rim * 1.4);
                // Within reach of a pinch: a white rim.
                color += float3(1.0, 0.96, 0.88) * _Highlight * (rim * 1.6 + 0.1);
                return half4(color, _Alpha * visible);
            }
            ENDHLSL
        }
    }
}

// A veil over the whole view that darkens the room (passthrough shows through by 1 - alpha), drawn on a small sphere
// around the head before everything else the world draws. Optionally one wall is lit: a pool of warm light from a lamp
// behind you, with shadows cut out of it and coloured light added from _Mask (r: shadow, g/b/a: three coloured light
// layers tinted by _Light0-2), which is laid over the wall's rectangle. _Flash washes everything towards _FlashColor,
// for lightning.
Shader "MusicInYourHands/Veil"
{
    Properties
    {
        _Dark ("Darkness 0-1", Float) = 0.85
        _DarkColor ("Colour of the dark", Color) = (0.01, 0.01, 0.02, 1)
        _Flash ("Flash 0-1", Float) = 0
        _FlashColor ("Flash colour", Color) = (0.75, 0.85, 1, 1)
        _WallOn ("Light a wall 0/1", Float) = 0
        _WallCenter ("Wall centre", Vector) = (0, 1.5, 2.5, 0)
        _WallRight ("Wall right", Vector) = (1, 0, 0, 0)
        _WallUp ("Wall up", Vector) = (0, 1, 0, 0)
        _WallNormal ("Wall normal (into the room)", Vector) = (0, 0, -1, 0)
        _WallSize ("Wall size (m)", Vector) = (4, 2.6, 0, 0)
        _Pool ("Light pool: centre xy (m on the wall), radius, strength", Vector) = (0, 0, 1.2, 1)
        _PoolColor ("Light colour", Color) = (1, 0.78, 0.52, 1)
        _Mask ("Shadow mask over the wall", 2D) = "black" {}
        _Light0 ("Coloured light 0", Color) = (1, 0.4, 0.7, 1)
        _Light1 ("Coloured light 1", Color) = (0.4, 0.8, 1, 1)
        _Light2 ("Coloured light 2", Color) = (0.6, 1, 0.6, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest Always
            // Premultiplied: what the room keeps is 1 - alpha.
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);

            CBUFFER_START(UnityPerMaterial)
                float _Dark;
                float4 _DarkColor;
                float _Flash;
                float4 _FlashColor;
                float _WallOn;
                float4 _WallCenter;
                float4 _WallRight;
                float4 _WallUp;
                float4 _WallNormal;
                float4 _WallSize;
                float4 _Pool;
                float4 _PoolColor;
                float4 _Mask_ST;
                float4 _Light0;
                float4 _Light1;
                float4 _Light2;
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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float alpha = _Dark;
                float3 rgb = _DarkColor.rgb * _Dark;

                if (_WallOn > 0.5)
                {
                    float3 eye = GetCameraPositionWS();
                    float3 dir = normalize(input.positionWS - eye);
                    float facing = dot(dir, _WallNormal.xyz);
                    if (facing < -1e-3)
                    {
                        float t = dot(_WallCenter.xyz - eye, _WallNormal.xyz) / facing;
                        float3 hit = eye + dir * t;
                        float2 local = float2(dot(hit - _WallCenter.xyz, _WallRight.xyz), dot(hit - _WallCenter.xyz, _WallUp.xyz));
                        float2 edge = _WallSize.xy * 0.5 - abs(local);
                        float onWall = saturate(min(edge.x, edge.y) / 0.05) * step(0, t);
                        if (onWall > 0)
                        {
                            float4 mask = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, local / _WallSize.xy + 0.5);
                            float d = length(local - _Pool.xy) / max(_Pool.z, 1e-3);
                            // A lamp's pool: bright middle, soft falloff, a gentle edge.
                            float lit = _Pool.w * exp(-d * d * 1.6) * smoothstep(1.25, 0.8, d);
                            lit *= 1 - mask.r * 0.95;
                            // The coloured light stays behind the shadow: the shadow is always dark.
                            float echo = 1 - mask.r;
                            float3 colored = (_Light0.rgb * mask.g + _Light1.rgb * mask.b + _Light2.rgb * mask.a) * echo;
                            float glow = saturate(lit + dot(mask.gba, 1) * 0.45 * echo);
                            float wallAlpha = lerp(_Dark, 0.12, glow);
                            float3 wallRgb = _DarkColor.rgb * wallAlpha + _PoolColor.rgb * lit * 0.32 + colored * 0.55;
                            alpha = lerp(alpha, wallAlpha, onWall);
                            rgb = lerp(rgb, wallRgb, onWall);
                        }
                    }
                }

                alpha = lerp(alpha, 0.55, _Flash);
                rgb = lerp(rgb, _FlashColor.rgb * 0.55, _Flash);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}

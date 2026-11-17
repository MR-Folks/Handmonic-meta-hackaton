// Hidden by the real room: real furniture, people and your real hands hide what a world draws behind them, with Meta's
// Depth API (com.meta.xr.sdk.core). RoomDepthFeed runs Meta's EnvironmentDepthManager only while a world that is hidden
// by the real room is on (World.HiddenByRealRoom); once the headset's first depth map arrives the manager switches on
// the global keyword HARD_OCCLUSION or SOFT_OCCLUSION, and switches it off again for every other world. Without either
// keyword (the editor, every other world) Meta's macros are empty and a shader draws exactly as before.
//
// A shader that takes part:
//   #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION
//   #include "RoomOcclusion.hlsl"                               after URP's Core.hlsl
//   float _EnvironmentDepthBias;  in UnityPerMaterial, with a Properties default: the share of its distance a thing is
//                                 pulled towards you before it is tested against the room, so things on or flush with a
//                                 real surface (the floor, the ceiling) don't flicker against it
//   META_DEPTH_VERTEX_OUTPUT(n)   in Varyings (the next free TEXCOORD), unless it already carries a world position
//   META_DEPTH_INITIALIZE_VERTEX_OUTPUT(output, input.positionOS.xyz)   in the vertex shader for geometry it doesn't
//                                 move, or ROOM_OCCLUSION_WORLD(output, positionWS) for geometry it moves itself
//   and in the fragment shader, after UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX (the depth map is per eye):
//     straight alpha (Blend SrcAlpha ...):   alpha *= the visibility from META_DEPTH_GET_OCCLUSION_VALUE(input, bias)
//                                            (or META_DEPTH_GET_OCCLUSION_VALUE_WORLDPOS with the shader's own position)
//     opaque or premultiplied output:        META_DEPTH_OCCLUDE_OUTPUT_PREMULTIPLY(_WORLDPOS)(..., color, bias)
// The shared shaders (Glow, Ring, Line, Lettering, Bubble) also have _RoomOcclusion (0-1, default 0) and lerp the
// visibility by it: only a world's own copies of their materials (World.InRoom, its sung words) set it, so the menu,
// effects on the hands and other worlds' things stay in front even while the keyword is on.
#ifndef MUSIC_IN_YOUR_HANDS_ROOM_OCCLUSION_INCLUDED
#define MUSIC_IN_YOUR_HANDS_ROOM_OCCLUSION_INCLUDED

#include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/URP/EnvironmentOcclusionURP.hlsl"

#if defined(HARD_OCCLUSION) || defined(SOFT_OCCLUSION)
    // Fills META_DEPTH_VERTEX_OUTPUT's posWorld with a world position the vertex shader worked out itself.
    #define ROOM_OCCLUSION_WORLD(output, positionWS) output.posWorld = (positionWS)
#else
    #define ROOM_OCCLUSION_WORLD(output, positionWS)
#endif

#endif

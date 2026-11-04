// The Solar system's room and your head, shared by its orbit lines and asteroid belt. SolarSystem sets them as globals:
// _SolarSystemReach holds, for 64 directions round you (from +x towards +z), how far the scanned walls are (100 m where
// there is no wall); _SolarSystemRoom.xy is the point they are measured from (x, z), and .zw how far inside a wall
// something starts to fade and how far beyond it it is gone. _SolarSystemHead is where your eyes are;
// _SolarSystemClear.x is how far below them things near your head keep, .y the closest they come to your eyes, and .zw
// the level distances within which that dip under your face is full and beyond which it is gone. Until SolarSystem has
// set them, nothing fades or moves.
#ifndef SOLAR_SYSTEM_ROOM_INCLUDED
#define SOLAR_SYSTEM_ROOM_INCLUDED

float4 _SolarSystemReach[16];
float4 _SolarSystemRoom;
float4 _SolarSystemHead;
float4 _SolarSystemClear;

// How far the wall is in direction i (0-63).
float SolarReachAt(float i)
{
    float4 v = _SolarSystemReach[(int)(i * 0.25)];
    float c = i - floor(i * 0.25) * 4;
    return c < 0.5 ? v.x : (c < 1.5 ? v.y : (c < 2.5 ? v.z : v.w));
}

// 1 inside the room, fading to 0 as the point goes into a scanned wall.
float SolarInRoom(float3 p)
{
    if (_SolarSystemRoom.z <= 0) return 1;
    float2 d = p.xz - _SolarSystemRoom.xy;
    float f = frac(atan2(d.y, d.x) * 0.15915494) * 64;
    float i = min(floor(f), 63);
    float limit = lerp(SolarReachAt(i), SolarReachAt(fmod(i + 1, 64)), f - i);
    return smoothstep(limit + _SolarSystemRoom.w, limit - _SolarSystemRoom.z, length(d));
}

// The smaller of a and b, rounded off over k so things bend away smoothly instead of with a kink.
float SolarSmoothMin(float a, float b, float k)
{
    float h = max(k - abs(a - b), 0) / k;
    return min(a, b) - h * h * k * 0.25;
}

// Moves a point near your head out of the way, as SolarSystem.Clear does for the planets: within about a metre (level
// distance) it keeps below your eyes, dipping under your face as you lean in, and it never comes too close to them.
float3 SolarClear(float3 p)
{
    if (_SolarSystemClear.y <= 0) return p;
    float3 eye = _SolarSystemHead.xyz;
    float level = length(p.xz - eye.xz);
    float under = _SolarSystemClear.x * (1 - smoothstep(_SolarSystemClear.z, _SolarSystemClear.w, level));
    p.y = SolarSmoothMin(p.y, eye.y - under, 0.1);
    float3 away = p - eye;
    float d = max(length(away), 1e-4);
    return eye + away / d * -SolarSmoothMin(-d, -_SolarSystemClear.y, 0.1);
}

// The beat's wave of light running out from the sun: how bright it is at this distance (m) from the sun.
// wave: x seconds since the beat, y its strength, z its speed (m/s), w its width (m).
float SolarWave(float distance, float4 wave)
{
    float x = (wave.x * wave.z - distance) / max(wave.w, 1e-3);
    return exp(-x * x) * exp(-wave.x * 1.2) * wave.y * 0.8;
}

#endif

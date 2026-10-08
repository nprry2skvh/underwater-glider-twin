Shader "UnderwaterGliderTwin/DeterministicOceanSurface"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.05, 0.38, 0.50, 1)
        _DeepColor ("Deep Color", Color) = (0.01, 0.12, 0.20, 1)
        _UnderwaterColor ("Underwater Color", Color) = (0.00, 0.27, 0.23, 1)
        _InteractionFoamColor ("Interaction Foam Color", Color) = (0.62, 0.92, 1.00, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.92
        _FresnelStrength ("Fresnel Strength", Range(0, 1)) = 0.82
        _Alpha ("Alpha", Range(0, 1)) = 0.72
        _RefractionDistortion ("Refraction Distortion", Range(0, 0.2)) = 0.018
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 250
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float interaction : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            float _SimulationTime;
            float _SurfaceHeight;
            float _TimeMultiplier;
            float4 _LongWave;
            float4 _CrossWave;
            float4 _MediumWave;
            float4 _DetailWave;
            float4 _FineWave;
            float4 _WavePhases;
            float4 _WavePhasesB;
            fixed4 _ShallowColor;
            fixed4 _DeepColor;
            fixed4 _UnderwaterColor;
            fixed4 _InteractionFoamColor;
            float _Smoothness;
            float _FresnelStrength;
            float _Alpha;
            float _RefractionDistortion;
            float4 _InteractorPosition;
            float4 _InteractorVelocity;
            float4 _InteractionParams;
            sampler2D _WakeField;
            float4 _WakeFieldOriginExtent;
            float4 _WakeField_TexelSize;
            float _WakeFieldEnabled;

            static const float TWO_PI = 6.28318530718;
            static const float GRAVITY = 9.81;

            float4 SampleWakeField(float2 worldXZ)
            {
                if (_WakeFieldEnabled < 0.5)
                {
                    return 0.0;
                }

                float2 uv = (worldXZ - _WakeFieldOriginExtent.xy)
                    * _WakeFieldOriginExtent.w + 0.5;
                if (any(uv < 0.0) || any(uv > 1.0))
                {
                    return 0.0;
                }

                return tex2Dlod(_WakeField, float4(uv, 0.0, 0.0));
            }

            void AccumulateWave(float4 wave, float phaseOffset, float2 worldXZ, inout float height, inout float2 slope)
            {
                float wavelength = max(0.25, wave.w);
                float waveNumber = TWO_PI / wavelength;
                float angularFrequency = sqrt(GRAVITY * waveNumber);
                float phase = waveNumber * dot(normalize(wave.xy), worldXZ)
                    - angularFrequency * (_SimulationTime * _TimeMultiplier)
                    + phaseOffset;
                float sineValue = sin(phase);
                float slopeValue = wave.z * waveNumber * cos(phase);
                height += wave.z * sineValue;
                slope += slopeValue * normalize(wave.xy);
            }

            float EvaluateInteractionSurface(float2 worldXZ, out float foamPotential, out float interactionMask)
            {
                float depth = abs(_SurfaceHeight - _InteractorPosition.y);
                float depthFade = exp(-depth / max(0.0001, _InteractionParams.z)) * _InteractorPosition.w;
                float2 delta = worldXZ - _InteractorPosition.xz;
                float2 travelDirection = _InteractorVelocity.xz;
                float speed = length(travelDirection);
                float speedFactor = saturate(_InteractorVelocity.w);
                if (_InteractorPosition.w < 0.5 || speed < 0.0001 || speedFactor < 0.001)
                {
                    foamPotential = 0.0;
                    interactionMask = 0.0;
                    return 0.0;
                }

                travelDirection /= speed;
                float2 sideDirection = float2(-travelDirection.y, travelDirection.x);
                float forward = dot(delta, travelDirection);
                float signedLateral = dot(delta, sideDirection);
                float lateral = abs(signedLateral);
                float strength = _InteractionParams.x * depthFade;

                // The pressure signature is elongated along the hull and has a
                // smaller stern depression. It replaces the old radial pulse.
                float pressureLength = max(1.8, _InteractionParams.y * 0.16);
                float pressureBeam = max(0.65, pressureLength * 0.36);
                float bowDistance = (forward - pressureLength * 0.28) / pressureLength;
                float sternDistance = (forward + pressureLength * 0.62) / (pressureLength * 1.18);
                float lateralPressure = signedLateral / pressureBeam;
                float bowPressure = exp(-(bowDistance * bowDistance * 2.8 + lateralPressure * lateralPressure * 2.2));
                float sternPressure = exp(-(sternDistance * sternDistance * 2.4 + lateralPressure * lateralPressure * 1.9));
                float nearField = (bowPressure - sternPressure * 0.58)
                    * strength * speedFactor * 0.8;
                float bowCrest = pow(saturate(bowPressure - sternPressure * 0.45), 3.0);
                foamPotential = saturate(speedFactor * depthFade * bowCrest * 0.24);
                interactionMask = saturate(max(bowPressure, sternPressure * 0.58) * depthFade);

                return nearField;
            }

            float AccumulateInteraction(float2 worldXZ, inout float height, inout float2 slope)
            {
                float foamPotential;
                float interactionMask;
                float interactionHeight = EvaluateInteractionSurface(worldXZ, foamPotential, interactionMask);
                float ignoredFoam;
                float ignoredMask;
                const float normalSampleDistance = 0.16;
                float heightX = EvaluateInteractionSurface(
                    worldXZ + float2(normalSampleDistance, 0.0), ignoredFoam, ignoredMask);
                float heightZ = EvaluateInteractionSurface(
                    worldXZ + float2(0.0, normalSampleDistance), ignoredFoam, ignoredMask);
                height += interactionHeight;
                slope += float2(
                    (heightX - interactionHeight) / normalSampleDistance,
                    (heightZ - interactionHeight) / normalSampleDistance);
                return foamPotential;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
                float height = 0.0;
                float2 slope = 0.0;
                AccumulateWave(_LongWave, _WavePhases.x, worldPosition.xz, height, slope);
                AccumulateWave(_CrossWave, _WavePhases.y, worldPosition.xz, height, slope);
                AccumulateWave(_MediumWave, _WavePhases.z, worldPosition.xz, height, slope);
                AccumulateWave(_DetailWave, _WavePhases.w, worldPosition.xz, height, slope);
                AccumulateWave(_FineWave, _WavePhasesB.x, worldPosition.xz, height, slope);
                float interaction = AccumulateInteraction(worldPosition.xz, height, slope);
                float4 wake = SampleWakeField(worldPosition.xz);
                float texelWorldSize = max(0.01, _WakeFieldOriginExtent.z * _WakeField_TexelSize.x);
                float wakeHeightX = SampleWakeField(worldPosition.xz + float2(texelWorldSize, 0.0)).r;
                float wakeHeightZ = SampleWakeField(worldPosition.xz + float2(0.0, texelWorldSize)).r;
                height += wake.r;
                slope += float2(
                    (wakeHeightX - wake.r) / texelWorldSize,
                    (wakeHeightZ - wake.r) / texelWorldSize);
                worldPosition.y += height;

                o.worldPos = worldPosition;
                o.worldNormal = normalize(float3(-slope.x, 1.0, -slope.y));
                o.interaction = interaction;
                o.pos = UnityWorldToClipPos(worldPosition);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float3 normalDirection = normalize(i.worldNormal);
                if (dot(normalDirection, viewDirection) < 0.0)
                {
                    normalDirection = -normalDirection;
                }

                float foam;
                float interactionMask;
                float ignoredInteractionHeight = EvaluateInteractionSurface(i.worldPos.xz, foam, interactionMask);
                float4 wake = SampleWakeField(i.worldPos.xz);
                float wakeMask = saturate(wake.g);
                interactionMask = max(interactionMask, wakeMask);
                foam = max(foam, saturate(wake.b * 0.42));
                float scaledTime = _SimulationTime * _TimeMultiplier;
                float2 microSlope;
                microSlope.x = sin(i.worldPos.x * 2.7 + i.worldPos.z * 1.3 - scaledTime * 1.1)
                    + 0.52 * sin(i.worldPos.x * 5.1 - i.worldPos.z * 2.4 + scaledTime * 1.7);
                microSlope.y = cos(i.worldPos.z * 3.1 - i.worldPos.x * 0.9 + scaledTime * 0.8)
                    + 0.47 * cos(i.worldPos.z * 4.7 + i.worldPos.x * 2.2 - scaledTime * 1.45);
                microSlope *= 1.0 - interactionMask * 0.96;
                normalDirection = normalize(normalDirection + float3(microSlope.x, 0.0, microSlope.y) * (_RefractionDistortion * 2.35));

                float ndv = saturate(dot(normalDirection, viewDirection));
                float fresnel = pow(1.0 - ndv, 5.0) * _FresnelStrength;
                float crest = saturate((i.worldPos.y - _SurfaceHeight) * 2.5 + 0.5);
                float3 waterColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(ndv * 0.45 + crest * 0.3));

                float underwater = step(_WorldSpaceCameraPos.y, _SurfaceHeight);
                waterColor = lerp(waterColor, lerp(_UnderwaterColor.rgb, waterColor, 0.35), underwater);

                float3 lightDirection = normalize(UnityWorldSpaceLightDir(i.worldPos));
                float3 halfDirection = normalize(lightDirection + viewDirection);
                float diffuse = saturate(dot(normalDirection, lightDirection));
                float specularPower = lerp(24.0, 180.0, _Smoothness)
                    * lerp(1.0, 0.62, wakeMask);
                float specular = pow(saturate(dot(normalDirection, halfDirection)), specularPower);
                specular *= 0.30 * (1.0 + wakeMask * 1.6);
                float3 reflectionTint = lerp(float3(0.20, 0.48, 0.58), float3(0.62, 0.90, 1.0), saturate(lightDirection.y * 0.5 + 0.5));
                float3 finalColor = waterColor * (0.56 + diffuse * 0.34)
                    + _LightColor0.rgb * specular * (0.35 + _Smoothness)
                    + reflectionTint * fresnel;
                finalColor += reflectionTint * clamp(wake.r * 1.5, -0.12, 0.12);
                finalColor += reflectionTint * smoothstep(0.20, 0.35, wake.b) * 0.10;

                finalColor = lerp(finalColor, _InteractionFoamColor.rgb, foam * 0.62);
                finalColor += _InteractionFoamColor.rgb * foam * 0.16;

                fixed4 color = fixed4(finalColor, saturate(_Alpha + fresnel * 0.18 + foam * 0.12 - underwater * 0.08));
                UNITY_APPLY_FOG(i.fogCoord, color);
                return color;
            }
            ENDCG
        }
    }

    Fallback Off
}

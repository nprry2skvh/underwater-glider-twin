Shader "Hidden/UnderwaterGliderTwin/WaterWakeField"
{
    Properties
    {
        _PathSamples ("Path Samples", 2D) = "black" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _PathSamples;
            float _SampleCount;
            float4 _FieldOriginExtent;
            float _WakeLength;
            float _WakeAgeDuration;
            float _DepthFadeDistance;
            float _DepthScale;
            float _WakeStrength;
            float _FieldTexelWidth;

            static const float TWO_PI = 6.28318530718;

            float4 ReadPathSample(int index)
            {
                float u = (index + 0.5) / 64.0;
                return tex2Dlod(_PathSamples, float4(u, 0.25, 0.0, 0.0));
            }

            float ReadPathDistance(int index)
            {
                float u = (index + 0.5) / 64.0;
                return tex2Dlod(_PathSamples, float4(u, 0.75, 0.0, 0.0)).x;
            }

            float4 frag(v2f_img input) : SV_Target
            {
                int count = min(64, (int)_SampleCount);
                if (count < 2)
                {
                    return 0;
                }

                float2 worldXZ = (input.uv - 0.5) * _FieldOriginExtent.z;
                float waveNumber = TWO_PI / max(2.5, lerp(4.0, 8.0, saturate(_WakeStrength)));
                float height = 0.0;
                float interactionMask = 0.0;
                float crest = 0.0;
                float travelledDistance = 0.0;
                float accumulatedWeight = 0.0;
                float packetSpacing = max(2.0, _WakeLength * 0.055);
                float4 newer = ReadPathSample(0);
                float newerDistance = ReadPathDistance(0);
                [loop]
                for (int index = 1; index < count; index++)
                {
                    float4 older = ReadPathSample(index);
                    float olderDistance = ReadPathDistance(index);
                    float2 segment = newer.xy - older.xy;
                    float segmentLength = length(segment);
                    if (segmentLength < 0.0001)
                    {
                        newer = older;
                        newerDistance = olderDistance;
                        continue;
                    }

                    float2 direction = segment / segmentLength;
                    float2 side = float2(-direction.y, direction.x);
                    float2 source = (newer.xy + older.xy) * 0.5;
                    float2 delta = worldXZ - source;
                    float forward = dot(delta, direction);
                    float aft = max(0.0, -forward);
                    float lateral = abs(dot(delta, side));
                    float segmentProgress = saturate(dot(worldXZ - older.xy, direction) / segmentLength);
                    float2 nearestPathPoint = older.xy + direction * (segmentProgress * segmentLength);
                    float centerDistance = length(worldXZ - nearestPathPoint);
                    float centerWidth = max(0.35, _FieldTexelWidth * 1.2);
                    float centerEnvelope = exp(-pow(centerDistance / centerWidth, 2.0));
                    float sampleDepth = 0.5 * (newer.z + older.z) * _DepthScale;
                    float depthFade = exp(-sampleDepth / max(0.0001, _DepthFadeDistance));
                    float sampleAge = max(newer.w, older.w);
                    float ageFade = exp(-sampleAge / max(1.0, _WakeAgeDuration));
                    float pathFade = exp(-travelledDistance / max(1.0, _WakeLength));
                    float weight = saturate(depthFade * ageFade * pathFade);
                    float transverseWave = sin(waveNumber * aft);
                    float packetEnvelope = 0.0;
                    float packetHeight = 0.0;
                    float packetCrest = 0.0;
                    float minimumDistance = min(newerDistance, olderDistance);
                    float maximumDistance = max(newerDistance, olderDistance);
                    float packetIndex = floor(maximumDistance / packetSpacing + 0.0001);
                    if (packetIndex > floor(minimumDistance / packetSpacing + 0.0001))
                    {
                        float packetDistance = packetIndex * packetSpacing;
                        float packetProgress = saturate((packetDistance - newerDistance)
                            / (olderDistance - newerDistance));
                        source = lerp(newer.xy, older.xy, packetProgress);
                        delta = worldXZ - source;
                        forward = dot(delta, direction);
                        aft = max(0.0, -forward);
                        lateral = abs(dot(delta, side));
                        sampleDepth = lerp(newer.z, older.z, packetProgress) * _DepthScale;
                        sampleAge = lerp(newer.w, older.w, packetProgress);
                        depthFade = exp(-sampleDepth / max(0.0001, _DepthFadeDistance));
                        ageFade = exp(-sampleAge / max(1.0, _WakeAgeDuration));
                        float packetTravel = travelledDistance + packetProgress * segmentLength;
                        pathFade = exp(-packetTravel / max(1.0, _WakeLength * 0.5));
                        float packetWeight = saturate(depthFade * ageFade * pathFade)
                            * smoothstep(0.0, packetSpacing * 0.25, packetTravel);
                        float packetAge = saturate(sampleAge / max(1.0, _WakeAgeDuration));
                        float lateralReach = packetSpacing * (0.72 + packetAge * 0.75);
                        float crestOffset = aft - packetSpacing * (0.35 + packetAge * 0.75)
                            - 0.42 * lateral * lateral / (packetSpacing * (1.0 + packetAge));
                        float crestWidth = max(_FieldTexelWidth * 2.0,
                            packetSpacing * (0.25 + packetAge * 0.15));
                        float transverseEnvelope = exp(-pow(lateral / lateralReach, 4.0));
                        packetEnvelope = exp(-pow(crestOffset / crestWidth, 2.0))
                            * transverseEnvelope * step(0.05, aft);
                        packetHeight = packetEnvelope * sin(waveNumber * crestOffset) * 1.50
                            * packetWeight;
                        packetCrest = packetEnvelope * 0.60 * packetWeight;
                    }

                    float segmentMask = saturate(max(packetCrest * 0.65, centerEnvelope * 0.65 * weight));
                    float segmentHeight = _WakeStrength
                        * (packetHeight + weight * centerEnvelope * transverseWave * 0.10);

                    height += segmentHeight;
                    interactionMask = max(interactionMask, segmentMask);
                    crest = max(crest, packetCrest);
                    accumulatedWeight += weight;
                    travelledDistance += segmentLength;
                    newer = older;
                    newerDistance = olderDistance;
                }

                float normalization = 1.0 / max(1.0, sqrt(accumulatedWeight));
                height = clamp(height * normalization, -_WakeStrength, _WakeStrength);
                crest = saturate(crest);
                return fixed4(height, interactionMask, crest, saturate(interactionMask));
            }
            ENDCG
        }
    }
    Fallback Off
}

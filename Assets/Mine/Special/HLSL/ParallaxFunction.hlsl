// Parallax Functions

#ifndef PARALLAX_FUNCTION_INCLUDED
#define PARALLAX_FUNCTION_INCLUDED

TEXTURE2D(_HeightMap);
SAMPLER(sampler_HeightMap);

float GetHeight(float2 heightUV)
{
	return 1.0 - SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, heightUV).r;
}

float2 GetParallaxOffsetSingle(float2 heightUV, float3 viewDirTS, float heightScale)
{
	float height = GetHeight(heightUV);
	float parallax = height * heightScale;
	return (viewDirTS.xy / max(viewDirTS.z, 0.1)) * parallax;
}

float2 GetParallaxOffsetSteep(float2 heightUV, float3 viewDirTS, float heightScale, int layerCount)
{
	float layerDepth = 1.0 / layerCount;
	float2 parallaxDir = (viewDirTS.xy / max(viewDirTS.z, 0.1)) * heightScale;
	float2 deltaUV = parallaxDir / layerCount;

	float2 currentUV = heightUV;
	float currentDepth = 0.0;
	float currentHeight = GetHeight(currentUV);

	float2 prevUV = currentUV;
	float prevDepth = currentDepth;
	float prevHeight = currentHeight;

	[unroll]
	for (int i = 0; i < layerCount; i++)
	{
		prevUV = currentUV;
		prevDepth = currentDepth;
		prevHeight = currentHeight;

		currentUV -= deltaUV;
		currentDepth += layerDepth;
		currentHeight = GetHeight(currentUV);

		if (currentDepth >= currentHeight)
			break;
	}

	float weight = (currentHeight - prevDepth) / max(currentDepth - prevDepth, 1e-5);
	float2 interpUV = lerp(prevUV, currentUV, saturate(weight));
	return interpUV - heightUV;
}

float2 GetParallaxOffsetRelief(float2 heightUV, float3 viewDirTS, float heightScale, int layerCount, int refineCount)
{
	float layerDepth = 1.0 / layerCount;
	float2 parallaxDir = (viewDirTS.xy / max(viewDirTS.z, 0.1)) * heightScale;
	float2 deltaUV = parallaxDir / layerCount;

	float2 lowUV = heightUV;
	float lowDepth = 0.0;
	float lowHeight = GetHeight(lowUV);

	float2 highUV = heightUV;
	float highDepth = 0.0;
	float highHeight = lowHeight;

	[unroll]
	for (int i = 0; i < layerCount; i++)
	{
		highUV -= deltaUV;
		highDepth += layerDepth;
		highHeight = GetHeight(highUV);

		if (highDepth >= highHeight)
		{
			break;
		}

		lowUV = highUV;
		lowDepth = highDepth;
		lowHeight = highHeight;
	}

	[unroll]
	for (int i = 0; i < refineCount; i++)
	{
		float2 midUV = lerp(lowUV, highUV, 0.5);
		float midDepth = lerp(lowDepth, highDepth, 0.5);
		float midHeight = GetHeight(midUV);

		if (midDepth >= midHeight)
		{
			highUV = midUV;
			highDepth = midDepth;
			highHeight = midHeight;
		}
		else
		{
			lowUV = midUV;
			lowDepth = midDepth;
			lowHeight = midHeight;
		}
	}

	float2 resultUV = highUV;
	return resultUV - heightUV;
}

float GetParallaxShadow(float2 parallaxUV, float3 lightDirTS, float heightScale, int layerCount)
{
	float layerDepth = 1.0 / layerCount;
	float2 parallaxDir = (lightDirTS.xy / max(lightDirTS.z, 0.1)) * heightScale;
	float2 deltaUV = parallaxDir / layerCount;

	float2 currentUV = parallaxUV;
	float currentHeight = GetHeight(currentUV);
	float currentDepth = currentHeight;

	[unroll]
	for (int i = 0; i < layerCount; i++)
	{
		currentUV -= deltaUV;
		currentDepth += layerDepth;
		currentHeight = GetHeight(currentUV);

		if (currentDepth <= currentHeight)
		{
			return 0.0;
		}
	}

	return 1.0;
}

float UVClipValue(float2 parallaxUV, float2 clipMin, float2 clipMax)
{
	bool outOfRange = (parallaxUV.x < clipMin.x) || (parallaxUV.x > clipMax.x) ||
					  (parallaxUV.y < clipMin.y) || (parallaxUV.y > clipMax.y);
	return outOfRange ? -1.0 : 1.0;
}

float CSClipValue(float2 parallaxUV, float3 normalWS, float3 viewDirWS, float3 positionOS,
	float horizonFalloffPower, float horizonClipStrength)
{
	float NdotV = abs(dot(normalWS, viewDirWS));
	float horizonFactor = pow(1.0 - NdotV, horizonFalloffPower);

	float3 viewDirOS = normalize(TransformWorldToObjectDir(viewDirWS));
	float3 projVector = positionOS - viewDirOS * dot(positionOS, viewDirOS);
	float edgeFactor = saturate(length(projVector));

	float heightThreshold = saturate(horizonFactor * edgeFactor * horizonClipStrength);
	float surfaceHeight = 1.0 - GetHeight(parallaxUV);
	return (surfaceHeight < heightThreshold) ? -1.0 : 1.0;
}

#endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Noise
{
  public static float[,] GenerateNoiseMap(int mapWidth, int mapHeight, int seed, float scale, float mix, int octaves, float persistance, float lacunarity, Vector2 offset) {
	  float[,] noiseMap = new float[mapWidth,mapHeight];

	 // float maxNoiseHeight = float.MinValue;
	 // float minNoiseHeight = float.MaxValue;

	  for (int y = 0; y < mapHeight; y++) {
		  for (int x = 0; x < mapWidth; x++) {
			  //every point self normalizes, calculated against maxHeight in getNoiseAtPoint()
		  noiseMap[x, y] = GetNoiseAtPoint(x, y, mapWidth, mapHeight, seed, scale, mix, octaves, persistance, lacunarity, offset);
			  //noiseMap[x, y] = finalValue;
		}
	 }

	  // removed because it was relative scaling which was dumb as fuck, now it compares points to against the overall maximum possible noise value
	  // normalizes lowest point in map to be 0, and the highest point to be 1
	  // forces noise into 0 - 1 range
	  // for (int y = 0; y < mapHeight; y++) {
		//  for (int x = 0; x < mapWidth; x++) {
			  // use inverse loop for normalization
		//	  noiseMap[x,y] = Mathf.InverseLerp(minNoiseHeight, maxNoiseHeight, noiseMap [x,y]);
	//  }
 // }
	  return noiseMap;
  }

  public static float GetNoiseAtPoint(float x, float y, int mapWidth, int mapHeight, int seed, float scale, float mix, int octaves, float persistance, float lacunarity, Vector2 offset) {
      System.Random prng = new System.Random(seed);
	  Vector2[] octaveOffsets = new Vector2[octaves];

	  float maxHeight = 0;
	  //rename? how does this work fuck
	  // calculate maximum perlin noise value
	  float amplitudeTracker = 1;
	  for (int i = 0; i < octaves; i++){
		  maxHeight += amplitudeTracker;
		  amplitudeTracker *= persistance;
	  }

	  for (int i = 0; i < octaves; i++) {
		  float offsetX = prng.Next(-100000, 100000) + offset.x;
		  float offsetY = prng.Next(-100000, 100000) + offset.y;
		  octaveOffsets [i] = new Vector2 (offsetX, offsetY);
	  }

	  if (scale < 0) {
		  scale = 0.0001f;
	  }

	  float halfWidth = mapWidth / 2f;
	  float halfHeight = mapHeight / 2f;


	   float amplitude = 1;
	   float frequency = 1;
	   float noiseHeight = 0;

	    for (int i = 0; i < octaves; i++) {
			  // Basic Perlin Noise w/ Octaves (e)
			  // Higher the frequency, further apart the sample points will be, greater rate of change in height value as result
			  float sampleX = (x - halfWidth) / scale * frequency + octaveOffsets[i].x;
			  float sampleY = (y - halfHeight) / scale * frequency + octaveOffsets[i].y;

			  // keep a strict 0 to 1 value range
			  float e = Mathf.PerlinNoise(sampleX, sampleY);

			  noiseHeight += e * amplitude;
			  amplitude *= persistance;
              frequency *= lacunarity;
		}

			// shrink the ceiling, ex set ceiling to decimal set
			// e.x 100 * 0.9f = 90% being the new ceiling
			// set due to perlin noise being a bitch to get to snow (1) value
			// maybe better to reimplement relative scaling rather than global so that lowest value found is mapped to 0 and highest value found is mapped to 1
			// still dont understand this though, not getting any help either soo... fuck it for now...
			float normalizedNoise = noiseHeight / (maxHeight * 0.9f);
			normalizedNoise = Mathf.Clamp01(normalizedNoise);

			// literally just a blob of noise
			// Euclidean Distance (d), radius of circle
			// declare center 0, and edges 1
			float nx = 2f * x / mapWidth - 1f;
			float ny = 2f * y / mapHeight - 1f;

			// Euclidean Distance Formula
			// use distance against pythagorean theorem to find out distance from center of island relative to point/mesh (distance squared formula)
			float d = Mathf.Sqrt(nx * nx + ny * ny);

			// Square Bump Formula
			// plot (1-x^2)(1-y^2) from -1 to 1
			// www.wolframalpha.com/input/?i=plot+(1-x^2)(1-y^2)+from+-1+to+
			// float d = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));

			// raising the distance to the 10th power creates a "flat" top
			// cuts off near the edges, forcing edges to be water
			// visual representation
			// plot 1 - (x^2 + y^2)^5 for x from -1 to 1, y from -1 to 1
			// www.wolframalpha.com/input?i=plot+1+-+(x^2+%2B+y^2)^5+for+x+from+-1+to+1%2C+y+from+-1+to+1
			// take it to 30th power to cut off less
			float falloffMap = Mathf.Pow(d, 30f); 
			// if less than 0, keep it at 0

			// our euclidean shape (blob), has a high point of 1
			// apply distance squared formula (1 - (x^2 + y^2)) with x & y represented with d
			// is this being applied twice? because we already subtract 1f - d^10
			float targetShape = 1f - d;

			// map mix slider, allowing us to have pure noise (0), or pure falloff (1)
			//float combinedValue = Mathf.Lerp(noiseHeight, targetShape, mix);
			float combinedValue = Mathf.Lerp(normalizedNoise, targetShape, mix);

			float finalMapValue = combinedValue;

			// return mixed value of perlin noise and falloff map
			// - (d * 0.2f), force terrain down extra 20% at edge of map to ensure proper cutoff, maybe re-add as editor variable
			// removed as we are no longer normalizing from -1 to 1, cleaning up our math to make it strict 0 to 1
			// d represets chebyshev distance (distance between two points, minimum number of moves king requires to move between them)
			// instead of multiplying by 1 - d^10, just subtract finalMapValue - d^10 to preserve noise heights while still sinking edges into ocean
			// before by taking finalMapValue * falloffMap, we lower the expected noise by multiplying it by our falloff (e.x 1 * 0.9 = 0.9, we can never hit snow as a result)
			return (finalMapValue - falloffMap);
	}
}
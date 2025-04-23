Shader "Instanced/GridTestParticleShader"
{
	Properties
	{
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0
        _Color("Color", Color) = (0.25, 0.5, 0.5, 1)
		_DensityRange ("Density Range", Range(0,500000)) = 1.0
	}

	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		Pass
		{

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma instancing_options procedural:setup

			#include "UnityCG.cginc"

			float _Glossiness;
			float _Metallic;
			float3 _Color;
			float _DensityRange;
			float _size;
			
			#define PI 3.141592654

			struct Particle
			{
				float pressure;
				float density;
				float3 force;
				float3 velocity;
				float3 position;
				
			};

			struct appdata
			{
				float2 texcoord : TEXCOORD0;
				float3 vertex : POSITION;
				float3 normal : NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 worldPos : TEXCOORD1;
				float3 worldNormal : TEXCOORD2;
				float velocity : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			StructuredBuffer<Particle> _particlesBuffer;

			void setup() {}

			v2f vert(appdata v)
			{
				v2f o;
				
				UNITY_SETUP_INSTANCE_ID(v);
			
				Particle p = _particlesBuffer[unity_InstanceID];
				
				// use this for spheres
				/*float3 pos = p.position;
				float scale = _size;
				float3 worldPos = pos + v.vertex * scale;

				o.pos = UnityObjectToClipPos(float4(worldPos, 1.0));
				o.worldPos = worldPos;

				float3 worldNormal = UnityObjectToWorldNormal(v.normal);
				o.worldNormal = normalize(worldNormal);

				o.velocity = length(p.velocity);
				
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				
				return o;*/
				
				// use this for circles
				// https://www.youtube.com/watch?v=kOkfC5fLfgE
				o.uv = v.texcoord;

				float3 worldCentre = p.position;
				float2 vertOffset = v.vertex.xy * _size * 2;
				
				float3 camUp = unity_CameraToWorld._m01_m11_m21;
				float3 camRight = unity_CameraToWorld._m00_m10_m20;

				float3 vertPosWorld = worldCentre + camRight * vertOffset.x + camUp * vertOffset.y;

				o.pos = mul(UNITY_MATRIX_VP, float4(vertPosWorld, 1));
				o.worldPos = vertPosWorld;

				float3 worldNormal = UnityObjectToWorldNormal(v.normal);
				o.worldNormal = normalize(worldNormal);
				o.velocity = length(p.velocity);

				return o;
			}

			/*float3 greyScaleA = float3(0.50, 0.50, 0.50);
			float3 greyScaleB = float3(0.50, 0.50, 0.50);
			float3 greyScaleC = float3(0.50, 0.50, 0.50);
			float3 greyScaleD = float3(0.50, 0.50, 0.50);*/
			
			// color helper function
			float3 palette(in float t, in float3 a, in float3 b, in float3 c, in float3 d)
			{
				return a + b * cos(2 * PI * (c * t + d));
			}

			fixed4 frag(v2f i) : SV_TARGET
			{

				// use for spheres
				//float alpha = 1.0;

				// use for circles
				float2 centreOffset = (i.uv - 0.5) * 2;
				float sqrDst = dot(centreOffset, centreOffset);
				if (sqrDst > 1) discard;

				float alpha = smoothstep(1, 0.7, sqrDst);
				
				float3 a  = float3(0.50, 0.50, 0.50);
				float3 b  = float3(0.50, 0.50, 0.50);
				float3 c  = float3(1.00, 1.00, 1.00);
				float3 d  = float3(0.00, 0.33, 0.67);

				float3 colNoLight = palette(i.velocity, a, b, c, d);
				
				float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
				float dotProduct = max(0, dot(i.worldNormal, lightDir));

				float3 colLight = colNoLight * (dotProduct + 0.2);
				
				return float4(colLight, alpha);
			}
			ENDCG
		}
	}	
}
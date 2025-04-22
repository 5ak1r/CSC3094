//https://github.com/AJTech2002/Smoothed-Particle-Hydrodynamics/blob/youtube-base/Assets/Shaders/GridParticle.shader

Shader "Instanced/GridTestParticleShader" {
	Properties{
		_MainTex("Albedo (RGB)", 2D) = "white" {}
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0
        _Color("Color", Color) = (0.25, 0.5, 0.5, 1)
		_DensityRange ("Density Range", Range(0,500000)) = 1.0
	}
		SubShader{
			Tags { "RenderType" = "Opaque" }
			LOD 200

			CGPROGRAM
			// Physically based Standard lighting model
			#pragma surface surf Standard addshadow fullforwardshadows
			#pragma multi_compile_instancing
			#pragma instancing_options procedural:setup

			sampler2D _MainTex;
			float _size;
            float3 _Color;
			float _DensityRange;

			struct Input {
				float2 uv_MainTex;
			};

			struct Particle
			{
                float pressure;
                float density;
                float3 force;
                float3 velocity;
				float3 position;
				
			};

		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
			StructuredBuffer<Particle> _particlesBuffer;
			StructuredBuffer<uint> _cellIndices;
		#endif

			void setup()
			{
			#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
				float3 pos = _particlesBuffer[unity_InstanceID].position;
				float size = _size;

				unity_ObjectToWorld._11_21_31_41 = float4(size, 0, 0, 0);
				unity_ObjectToWorld._12_22_32_42 = float4(0, size, 0, 0);
				unity_ObjectToWorld._13_23_33_43 = float4(0, 0, size, 0);
				unity_ObjectToWorld._14_24_34_44 = float4(pos.xyz, 1);
				unity_WorldToObject = unity_ObjectToWorld;
				unity_WorldToObject._14_24_34 *= -1;
				unity_WorldToObject._11_22_33 = 1.0f / unity_WorldToObject._11_22_33;
			#endif
			}

			half _Glossiness;
			half _Metallic;

			#define PI 3.141592654

			float3 palette(in float t, in float3 a, in float3 b, in float3 c, in float3 d)
			{
				return a + b * cos(2 * PI * (c * t + d));
			}

			void surf(Input IN, inout SurfaceOutputStandard o) {
				float4 col = float4(_Color, 1.0);
				#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
					float vel = abs(_particlesBuffer[unity_InstanceID].velocity);
					float3 a = float3(0.5, 0.5, 0.5);
					float3 b = float3(0.5, 0.5, 0.5);
					float3 c = float3(1.0, 1.0, 1.0);
					float3 d = float3(0.0, 0.33, 0.67);
					col = float4(palette(vel, a, b, c, d), 1.0);
				#endif
				o.Albedo = col.rgb;
			}
			ENDCG
		}
			FallBack "Diffuse"
}
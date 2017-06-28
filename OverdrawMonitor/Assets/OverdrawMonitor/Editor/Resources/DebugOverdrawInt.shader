// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Debug/OverdrawInt"
{
	Properties
	{
		[Header(Hardware settings)]
		[Enum(UnityEngine.Rendering.CullMode)] HARDWARE_CullMode ("Cull faces", Float) = 2
		[Enum(On, 1, Off, 0)] HARDWARE_ZWrite ("Depth write", Float) = 1
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" "LightMode" = "ForwardBase" "Queue" = "Geometry+50"}
		LOD 100
		
		Pass
		{
			Cull [HARDWARE_CullMode]
			ZWrite [HARDWARE_ZWrite]
			Blend One One
		
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma exclude_renderers d3d11_9x

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
			};
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}
			
			float4 frag (v2f i) : SV_Target
			{
				// 1 / 512 = 0.001953125; 1 / 1024 = 0.0009765625
				return 0.0009765625;
			}
			ENDCG
		}
	}
}

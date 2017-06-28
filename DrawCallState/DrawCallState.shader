Shader "DrawCallState/DrawCallState"
{
	Properties
	{
		[Header(Hardware settings)]
		[Enum(UnityEngine.Rendering.CullMode)] HARDWARE_CullMode ("Cull faces", Float) = 2
		[Enum(UnityEngine.Rendering.BlendMode)] HARDWARE_BlendSrc ("Blend Source", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] HARDWARE_BlendDst ("Blend Destination", Float) = 0
		
		[Enum(On, 1, Off, 0)] HARDWARE_ZWrite ("Depth write", Float) = 1
		[Enum(UnityEngine.Rendering.CompareFunction)] HARDWARE_ZTest("Depth test", Float) = 4
		
		[Header(Hardware stencil)]
		HARDWARE_StencilRef ("Stencil REF", Range(0, 255)) = 0
		HARDWARE_ReadMask ("Stencil Read Mask", Range(0, 255)) = 255
		HARDWARE_WriteMask ("Stencil Write Mask", Range(0, 255)) = 255
		
		[Enum(UnityEngine.Rendering.CompareFunction)] HARDWARE_StencilComp ("Stencil comparison", Float) = 0
		[Enum(UnityEngine.Rendering.StencilOp)] HARDWARE_StencilPass ("Stencil Pass", Float) = 0
		[Enum(UnityEngine.Rendering.StencilOp)] HARDWARE_StencilFail ("Stencil Fail", Float) = 0
		[Enum(UnityEngine.Rendering.StencilOp)] HARDWARE_StencilZFail ("Stencil Z Fail", Float) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" "LightMode" = "ForwardBase" "Queue" = "Geometry+50"}
		LOD 100

		Pass
		{
			Cull [HARDWARE_CullMode]
			ZWrite [HARDWARE_ZWrite]
			ZTest [HARDWARE_ZTest]
			Blend [HARDWARE_BlendSrc] [HARDWARE_BlendDst]
			
			Stencil
			{
				Ref [HARDWARE_StencilRef]
				Comp [HARDWARE_StencilComp]
				
				Pass [HARDWARE_StencilPass]
				Fail [HARDWARE_StencilFail]
				ZFail [HARDWARE_StencilZFail]
			}
		
			CGPROGRAM
			
			// Write your shader code here
			
			ENDCG
		}
	}
}

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Unlit/Overlay" 
{
	Properties {
		_MainTex ("Texture", any) = "" {}
		_Alpha ("Alpha", float) = 0.5
	} 

	SubShader {

		Tags { "ForceSupported" = "True" "RenderType"="Transparent" } 
		
		Lighting Off 
		Blend SrcAlpha OneMinusSrcAlpha 
		Cull Off 
		ZWrite Off 
		Fog { Mode Off } 
		ZTest Always 
		
		Pass {	
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float2 texcoord : TEXCOORD0;
			};

			sampler2D _MainTex;

			uniform float4 _MainTex_ST;

			float _Alpha;
			
			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 result = 3.0 * tex2D(_MainTex, i.texcoord);
				result.w = _Alpha;
				return result;
			}
			ENDCG 
		}
	} 
	
	Fallback off 
}

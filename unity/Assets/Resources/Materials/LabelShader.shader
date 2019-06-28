// Based on the UberReplacement Shader:
// https://bitbucket.org/Unity-Technologies/ml-imagesynthesis/src/2b2bce9c0fc3dc450ead31fb17b7e3fe94510603/Assets/ImageSynthesis/Shaders/UberReplacement.shader?at=master&fileviewer=file-view-default
Shader "Hidden/LabelShader" {
    Properties {
        _CategoryColor ("Catergory Color", Color) = (0, 1, 0, 1)
    }

    SubShader {
        CGINCLUDE
        // Upgrade NOTE: excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays
        #pragma exclude_renderers d3d11 gles
        #include "UnityCG.cginc"

        fixed4 _CategoryColor;
        int _OutputMode;
               
        float4 Output(float depth01, float3 normal)
        {
            if (_OutputMode == 0) // Segmentation map
            {
                return _CategoryColor;
            }
            else if (_OutputMode == 1) // Depth and normals
            {
                // convert to absolute depth
                float near = _ProjectionParams.y;
                float far = _ProjectionParams.z;
                float depth_abs = depth01 * (far - near) + near;
                // convert normal from [-1, 1] to [0, 1]
                float3 c = normal * 0.5 + 0.5;
                return float4(depth_abs, c);
            }
            // unsupported _OutputMode
            return float4(1, 1, 1, 1);
        }
        ENDCG

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct v2f {
                float4 pos : SV_POSITION;
                float4 depth : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert( appdata_base v ) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.depth.xyz = COMPUTE_VIEW_NORMAL;
                o.depth.w = COMPUTE_DEPTH_01;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target {
                return Output (i.depth.w, i.depth.xyz);
            }
            ENDCG
        }
    }

    Fallback Off
}
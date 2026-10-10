Shader "TabletopMayhem/RoomOcclusion"
{
    SubShader
    {
        Tags { "Queue"="Geometry-10" "RenderType"="Opaque" }
        Pass
        {
            ZWrite On
            ColorMask 0
            Cull Off
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct input {float4 vertex:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct output {float4 vertex:SV_POSITION;UNITY_VERTEX_OUTPUT_STEREO};
            output vert(input v){output o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(output,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);return o;}
            fixed4 frag(output i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);return 0;}
            ENDCG
        }
    }
}

Shader "TabletopMayhem/PetShadow"
{
    Properties { _Color ("Tint", Color) = (0.12,0.08,0.17,0.24) }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct input {float4 vertex:POSITION;fixed4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct output {float4 vertex:SV_POSITION;fixed4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            output vert(input v)
            {
                output o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(output,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;return o;
            }
            fixed4 frag(output i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);return i.color;}
            ENDCG
        }
    }
}

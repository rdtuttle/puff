Shader "TabletopMayhem/VertexColor"
{
    Properties { _Tint ("Pet tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            fixed4 _Tint;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float3 normal : TEXCOORD0; float3 world : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.color=v.color*_Tint;return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normal),l=normalize(float3(-.4,.9,.3)),v=normalize(_WorldSpaceCameraPos-i.world);
                float diffuse=.40+.60*max(0,dot(n,l));
                float rough=max(.25,i.color.a);
                float spec=pow(max(0,dot(n,normalize(l+v))),lerp(65,8,rough))*(1-rough)*.55;
                float rim=pow(1-saturate(dot(n,v)),3)*.10;
                return fixed4(i.color.rgb*(diffuse+rim)+spec,1);
            }
            ENDCG
        }
    }
}

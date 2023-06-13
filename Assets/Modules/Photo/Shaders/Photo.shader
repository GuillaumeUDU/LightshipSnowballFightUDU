Shader "VFX/Photo"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
         _MaskTexture ("Mask Texture", 2D) = "white" {}
         _BlurTexture ("Blur Texture", 2D) = "white" {}
    }
 
    CGINCLUDE
    #include "UnityCG.cginc"
    #include "UnityUI.cginc"
 

    sampler2D _MainTex;
    sampler2D _BlurTexture;
    sampler2D _MaskTexture;

    struct appdata_t
    {
        float4 vertex   : POSITION;
        float4 color    : COLOR;
        float2 texcoord : TEXCOORD0;
    };
 
    struct v2f
    {
        float4 vertex   : SV_POSITION;
        fixed4 color    : COLOR;
        half2 texcoord  : TEXCOORD0;
    };
 
    v2f vert(appdata_t IN)
    {
        v2f OUT;
        OUT.vertex = UnityObjectToClipPos(IN.vertex);
 
        OUT.texcoord = IN.texcoord;
     
        #ifdef UNITY_HALF_TEXEL_OFFSET
        OUT.vertex.xy += (_ScreenParams.zw-1.0)*float2(-1,1);
        #endif
     
        OUT.color = IN.color;
        return OUT;
    }
 

    fixed4 frag(v2f IN) : SV_Target
    {
        float4 maskColor = tex2D(_MaskTexture, IN.texcoord);
        float progress = max(IN.color.a , maskColor.a);

        float4 mainColor = tex2D(_MainTex, IN.texcoord);
        float4 blurColor = tex2D(_BlurTexture, IN.texcoord);
        float4 zoomColor = tex2D(_MainTex, IN.texcoord * (.95 + (progress/20.)) + ( ((1.-progress)/40.)));

        blurColor.a = step(.75,mainColor.a);

        float4 dimColor = mainColor * zoomColor;
        dimColor.r /= 4.;
        dimColor.g /= 2.;
        dimColor.a = 1.;

        float4 photoColor = lerp(dimColor, mainColor,progress);
        float4 color = lerp(blurColor, photoColor,progress);
        return color;
    }
    ENDCG
 
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
 
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
 
        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
        ENDCG
        }
    }
}
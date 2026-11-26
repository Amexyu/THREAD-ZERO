// 11-25 世界观「褪色之城」：舞台上的图先褪成黑白线稿（带一点纸色和漫画网点），按饱和度和「晕开的颜色圈」把原色加回来。
// 用法见 Scripts/Fade.cs：Graphics.DrawTexture 画 IMGUI 里的一张图，_TZRect / _TZUV 是这次画的逻辑矩形和贴图坐标，
// 片元里由贴图坐标反算出舞台逻辑坐标，再和颜色圈（同一坐标系）比距离。界面的剪裁（GUI.BeginGroup）照 IMGUI 自己的着色器做。
Shader "Hidden/ThreadZero/Fade"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Lighting Off Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            sampler2D _GUIClipTexture;
            uniform float4x4 unity_GUIClipTextureMatrix;
            float _TZUseClip;
            float4 _TZRect, _TZUV;        // 逻辑矩形 (x, y, w, h)；贴图坐标矩形 (x, y, w, h)
            float _TZSat, _TZTone;        // 饱和度 0 = 线稿灰 … 1 = 原色；网点强度
            float4 _TZBlooms[16];       // 颜色圈：xy 逻辑坐标，z 半径，w 强度
            float _TZBloomCount;
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 clipUV : TEXCOORD1; };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                float3 eyePos = UnityObjectToViewPos(v.vertex);
                o.clipUV = mul(unity_GUIClipTextureMatrix, float4(eyePos.xy, 0, 1.0)).xy;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                if (_TZUseClip > .5) c.a *= tex2D(_GUIClipTexture, i.clipUV).a;
                float2 f = float2((i.uv.x - _TZUV.x) / _TZUV.z, 1 - (i.uv.y - _TZUV.y) / _TZUV.w);
                float2 p = _TZRect.xy + f * _TZRect.zw;
                float s = _TZSat;
                for (int k = 0; k < 16; k++)
                {
                    if (k < _TZBloomCount)
                    {
                        float4 b = _TZBlooms[k];
                        float2 d = p - b.xy;
                        float r = length(d);
                        float a = atan2(d.y, d.x);
                        // 边缘不是正圆：几道不同频率的波浪叠起来，像颜料晕开
                        float edge = b.z * (1 + .10 * sin(a * 5 + b.x * .013) + .06 * sin(a * 9 - b.y * .021) + .04 * sin(a * 17 + b.x * .007));
                        s = max(s, saturate((edge - r) / max(b.z * .07, 1)) * b.w);
                    }
                }
                float g = dot(c.rgb, float3(.299, .587, .114));
                float2 cell = frac(p / 5.0) - .5;                              // 漫画网点：暗的地方网点大
                float dotR = saturate((1 - g) * .8) * .5;
                float tone = length(cell) < dotR ? .8 : 1;
                float gg = lerp(g, g * tone, _TZTone);
                float3 gray = gg * float3(.98, .96, .91);                       // 稍微偏纸色
                c.rgb = lerp(gray, c.rgb, saturate(s));
                return c;
            }
            ENDCG
        }
    }
}

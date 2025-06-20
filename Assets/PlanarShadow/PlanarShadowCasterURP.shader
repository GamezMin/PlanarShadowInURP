Shader "Custom/PlanarShadowCasterURP"
{
    Properties
    {
        _ShadowColor("Shadow Color", Color) = (0,0,0,0.5)
        _PlaneHeight("Plane Height", Float) = 0.1
        _LightDir("Light Direction", Vector) = (0.5,-1,0.5,0)
        _ShadowFalloff("ShadowFalloff",Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent"
        }

        Pass
        {
            Name "Planar Shadow"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            //用使用模板测试以保证alpha显示正确
            Stencil
            {
                Ref 0
                Comp Always
                Pass IncrWrap
                Fail Keep
                ZFail Keep
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            //深度稍微偏移防止阴影与地面穿插
            Offset -1 , -1



            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #pragma vertex shadow_vert
            #pragma fragment shadow_frag

            CBUFFER_START(UnityPerMaterial)
                float4 _ShadowColor;
                float _PlaneHeight;
                float4 _LightDir;
                float _ShadowFalloff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
            };

            //得到阴影的世界空间坐标
            //https://github.com/ozlael/PlannarShadowForUnity
            //https://ozlael.tistory.com/10
            float3 ShadowProjectWorldPos(float4 vertPos, float3 lightDir)
            {
                //得到顶点的世界空间坐标
                float3 worldPos = TransformObjectToWorld(vertPos);

                //阴影的世界空间坐标（低于地面的部分不做改变）
                //float3 shadowPos
                //shadowPos.y = min(worldPos.y, _PlaneHeight);
                //shadowPos.xz = worldPos.xz - lightDir.xz * max(0, worldPos.y - _PlaneHeight) / lightDir.y;


                float opposite = max(0, worldPos.y - _PlaneHeight);
                float cosTheta = -lightDir.y; // 等同于：dot(lightDir, float3(0,-1,0));
                float hypotenuse = opposite / cosTheta;
                //阴影的世界空间坐标
                float3 shadowPos = worldPos.xyz + (lightDir * hypotenuse);
                shadowPos.y = _PlaneHeight + 0.01; // 保证阴影的位置和屏幕坐标的y轴一致
                //把阴影的世界空间坐标转为屏幕空间坐标
                return shadowPos;
            }

            Varyings shadow_vert(Attributes IN)
            {
                Varyings OUT;

                //使用主光源方向
                Light light = GetMainLight();
                _LightDir.xyz = normalize(light.direction);

                //得到阴影的世界空间坐标
                float3 shadowWorldPos = ShadowProjectWorldPos(IN.positionOS, _LightDir.xyz);
                //得到阴影的屏幕空间坐标
                OUT.positionCS = TransformWorldToHClip(shadowWorldPos);

                //得到中心点世界坐标
                float3 center = float3(unity_ObjectToWorld[0].w, _PlaneHeight, unity_ObjectToWorld[2].w);
                //计算阴影衰减
                float falloff = 1 - saturate(distance(shadowWorldPos, center) * _ShadowFalloff);

                //阴影颜色
                OUT.color = _ShadowColor;
                OUT.color.a *= falloff;


                return OUT;
            }

            half4 shadow_frag(Varyings IN) : SV_Target
            {
                return IN.color;
            }
            ENDHLSL
        }
    }
}
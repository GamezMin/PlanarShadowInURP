using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

    /// <summary>
    /// 平面阴影
    /// </summary>
    public class PlanarShadowFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public LayerMask shadowCasterLayers = -1;
            public Color shadowColor = new Color(0, 0, 0, 0.5f);
            public Vector3 lightDirection = new Vector3(0.5f, -1f, 0.5f);
            public float planeHeight = 0.01f;
            public float shadowFalloff = 0.25f;
            public int resolution = 512;
            public RenderTextureFormat format = RenderTextureFormat.ARGB32;
            public FilterMode filterMode = FilterMode.Bilinear;
        }
        
        [SerializeField]
        public Settings settings = new Settings();
       
        //阴影RT标识符
        private static readonly RenderTargetHandle m_PlanarShadowTarget = new RenderTargetHandle 
            { id = Shader.PropertyToID("_PlanarShadowTexture") };
        
        private PlanarShadowPass m_ShadowPass;
        
        public override void Create()
        {
            m_ShadowPass = new PlanarShadowPass(settings, m_PlanarShadowTarget)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_ShadowPass);
        }

        /// <summary>
        /// 平面阴影绘制pass
        /// </summary>
        class PlanarShadowPass : ScriptableRenderPass
        {
            private static string k_SamplerName = "Planar Shadows";
            List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();
            
            private Settings settings;
            private RenderTargetHandle renderTargetHandle;
            
            private RenderTexture m_ShadowRT;
            private Material shadowMaterial;

            private FilteringSettings filteringSettings;
            private ProfilingSampler profilingSampler;
            RenderStateBlock m_RenderStateBlock;
            
            private bool isInitialized = false;
            
            public PlanarShadowPass(Settings settings, RenderTargetHandle targetHandle)
            {
                this.settings = settings;
                renderTargetHandle = targetHandle;
                
                profilingSampler = new ProfilingSampler(k_SamplerName);
                
                m_ShaderTagIdList.Add(new ShaderTagId("UniversalForward"));
                filteringSettings = new FilteringSettings(RenderQueueRange.all,//渲染队列范围 只有这个队列范围内的才会被筛选出来
                    settings.shadowCasterLayers); 
                
                m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            }
            

            private bool InitRenderResources()
            {
                if (shadowMaterial == null)
                {
                    Shader shadowShader = Shader.Find("Custom/PlanarShadowCasterURP");
                    shadowMaterial = shadowShader ? new Material(shadowShader) : null;
                    return shadowShader != null;
                }

                return true;
            }
            
            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                if (!isInitialized)
                {
                    if (InitRenderResources())
                    {
                        isInitialized = true;
                    }
                }
                
                //创建RT
                m_ShadowRT =  RenderTexture.GetTemporary(settings.resolution, settings.resolution, 
                    24, //24才会有模板缓存
                    settings.format);
                
                //把RT设置为这次渲染的渲染目标
                ConfigureTarget(m_ShadowRT);
                //清空RT
                ConfigureClear(ClearFlag.Color, new Color(1, 0, 0, 1f));
            }
            
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (shadowMaterial == null || renderingData.cameraData.isPreviewCamera)
                    return;
                
                CommandBuffer cmd = CommandBufferPool.Get("Planar Shadows");
                
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    // 传递材质参数
                    shadowMaterial.SetColor("_ShadowColor", settings.shadowColor);
                    shadowMaterial.SetFloat("_PlaneHeight", settings.planeHeight);
                    shadowMaterial.SetVector("_LightDir", settings.lightDirection.normalized);
                    shadowMaterial.SetFloat("_ShadowFalloff", settings.shadowFalloff);
                    
                    // 绘制阴影投射器
                    DrawingSettings drawingSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, SortingCriteria.CommonOpaque);
                    drawingSettings.overrideMaterial = shadowMaterial;
                    drawingSettings.overrideMaterialPassIndex = 0;
                    
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                    
                    // 执行绘制
                    context.DrawRenderers(
                        renderingData.cullResults, 
                        ref drawingSettings,    
                        ref filteringSettings,
                        ref m_RenderStateBlock
                    );
                    
                    // 设置全局纹理 if Need
                    //cmd.SetGlobalTexture(renderTargetHandle.id, m_ShadowRT);
                }
                
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
          
                // 调试用
                GameObject debug = GameObject.Find("Canvas/RawImage");
                if (debug)
                {
                    RawImage img = debug.GetComponent<RawImage>();
                    if (img)
                    {
                        img.texture = m_ShadowRT;
                    }
                }
            }
            
            public override void FrameCleanup(CommandBuffer cmd)
            {
                if (m_ShadowRT)
                {
                    RenderTexture.ReleaseTemporary(m_ShadowRT);
                    m_ShadowRT = null;
                }
            }
        }
    }
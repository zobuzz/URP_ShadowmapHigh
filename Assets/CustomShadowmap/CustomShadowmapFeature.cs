using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

public class CustomShadowmapFeature : ScriptableRendererFeature
{
    [System.Serializable, ReloadGroup]
    public class CustomLayerSettings
    {
        [Range(0, 0.01f)]
        [SerializeField]
        public float ShadowBias = 0.0005f;
        public Color ShadowColor = Color.grey;
        public LayerMask OpaqueLayerMask;
        public LayerMask TransparentLayerMask;
    }

    class CustomShadowmapRenderPass : ScriptableRenderPass
    {
        List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();
        string m_ProfilerTag;
        ProfilingSampler m_ProfilingSampler;
        FilteringSettings m_FilteringSettings;
        RenderStateBlock m_RenderStateBlock;
        Camera m_shadowCamera;

        RenderTargetHandle m_MainLightShadowmap;
        RenderTexture m_MainLightShadowmapTexture;

        public static int _CustomWorldToShadowID;
        public static int _CustomShadowParams;
        Matrix4x4 _MatrixWorldToShadow;
        public Vector4 _ShadowParams;

        public CustomShadowmapRenderPass(string profilerTag, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask)//, StencilState stencilState, int stencilReference)
        {
            m_ProfilerTag = profilerTag;
            m_ProfilingSampler = new ProfilingSampler(profilerTag);

            m_ShaderTagIdList.Add(new ShaderTagId("ZorroShadow"));

            renderPassEvent = evt;

            m_MainLightShadowmap.Init("_CustomShadowmapTexture");

            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

            //if (stencilState.enabled)
            //{
            //    m_RenderStateBlock.stencilReference = stencilReference;
            //    m_RenderStateBlock.mask = RenderStateMask.Stencil;
            //    m_RenderStateBlock.stencilState = stencilState;
            //}
            _CustomWorldToShadowID = Shader.PropertyToID("_ZorroShadowMatrix");
            _CustomShadowParams = Shader.PropertyToID("_ZorroShadowParams");

            m_MainLightShadowmap.Init("_CustomLightShadowmapTexture");
        }

        const int k_ShadowmapBufferBits = 32;
        const int s_shadowmap_size = 2048;

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in an performance manner.
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            //TODO by sj
            GameObject obj = GameObject.FindGameObjectWithTag("ShadowCamera");
            if (null == obj)
                return;
            m_shadowCamera = obj.GetComponent<Camera>();

            if (null == m_shadowCamera)
                return;


            var des = cameraTextureDescriptor;
            des.colorFormat = RenderTextureFormat.ARGB32;
            des.width = s_shadowmap_size;
            des.height = s_shadowmap_size;
            des.depthBufferBits = 32;
            des.useMipMap = false;
            des.autoGenerateMips = false;
            m_MainLightShadowmapTexture = RenderTexture.GetTemporary(des);
            m_MainLightShadowmapTexture.filterMode = FilterMode.Point;
            m_MainLightShadowmapTexture.wrapMode = TextureWrapMode.Clamp;

            ConfigureTarget(new RenderTargetIdentifier(m_MainLightShadowmapTexture));
            ConfigureClear(ClearFlag.All, Color.white);
        }

        //public static RenderTexture GetShadowmapRt(int width, int height, int bits)
        //{
        //    var shadowTexture = RenderTexture.GetTemporary(width, height, bits, RenderTextureFormat.ARGB32);
        //    shadowTexture.filterMode = m_ForceShadowPointSampling ? FilterMode.Point : FilterMode.Bilinear;
        //    shadowTexture.wrapMode = TextureWrapMode.Clamp;

        //    return shadowTexture;
        //}

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (null == m_shadowCamera)
                return;

            #if UNITY_EDITOR
            // When rendering the preview camera, we want the layer mask to be forced to Everything
            if (renderingData.cameraData.isPreviewCamera)
            {
                return;
            }
            #endif
            //if(renderingData.cameraData.isSceneViewCamera)
            //    return;

            _MatrixWorldToShadow = m_shadowCamera.projectionMatrix * m_shadowCamera.worldToCameraMatrix;
            Camera camera = renderingData.cameraData.camera;

            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                //// Global render pass data containing various settings.
                //// x,y,z are currently unused
                //// w is used for knowing whether the object is opaque(1) or alpha blended(0)
                //Vector4 drawObjectPassData = new Vector4(0.0f, 0.0f, 0.0f, (m_IsOpaque) ? 1.0f : 0.0f);
                //cmd.SetGlobalVector(s_DrawObjectPassDataPropID, drawObjectPassData);
                //context.ExecuteCommandBuffer(cmd);
                //cmd.Clear();

                cmd.SetGlobalMatrix(_CustomWorldToShadowID, _MatrixWorldToShadow);
                cmd.SetGlobalVector(_CustomShadowParams, _ShadowParams);
                //change view projection matrix using cmd buffer
                cmd.SetViewProjectionMatrices(m_shadowCamera.worldToCameraMatrix, m_shadowCamera.projectionMatrix);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortFlags);
                var filterSettings = m_FilteringSettings;

                #if UNITY_EDITOR
                // When rendering the preview camera, we want the layer mask to be forced to Everything
                if (renderingData.cameraData.isPreviewCamera)
                {
                    filterSettings.layerMask = -1;
                }
                #endif

                ////only clear depth buffer here
                //cmd.ClearRenderTarget(true, false, Color.clear);
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings, ref m_RenderStateBlock);

                cmd.SetGlobalTexture(m_MainLightShadowmap.id, m_MainLightShadowmapTexture);
                //cmd.SetGlobalMatrix(_CustomWorldToShadowID, _MatrixWorldToShadow);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// Cleanup any allocated resources that were created during the execution of this render pass.
        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");

            if (m_MainLightShadowmapTexture)
            {
                RenderTexture.ReleaseTemporary(m_MainLightShadowmapTexture);
                m_MainLightShadowmapTexture = null;
            }
        }

        void Clear()
        {
            m_MainLightShadowmapTexture = null;
        }
    }

    CustomShadowmapRenderPass m_ScriptablePassOpaque;

    public CustomLayerSettings settings = new CustomLayerSettings();

    public override void Create()
    {
#if UNITY_EDITOR
        ResourceReloader.TryReloadAllNullIn(settings, "Assets/");
#endif
        m_ScriptablePassOpaque = new CustomShadowmapRenderPass("Render Player Opaque", true, RenderPassEvent.BeforeRendering - 10, RenderQueueRange.opaque, settings.OpaqueLayerMask);

        m_ScriptablePassOpaque._ShadowParams = settings.ShadowColor;
        m_ScriptablePassOpaque._ShadowParams.w = settings.ShadowBias;
        //m_ScriptablePassTransparent = new LayerRenderPass("Render Player Transparent", false, RenderPassEvent.AfterRenderingTransparents + 11, RenderQueueRange.transparent, settings.TransparentLayerMask);
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_ScriptablePassOpaque);
    }
}

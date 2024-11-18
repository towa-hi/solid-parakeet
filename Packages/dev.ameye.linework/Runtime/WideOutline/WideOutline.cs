using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine.Rendering.RenderGraphModule;
#endif
using UnityEngine.Rendering.Universal;

namespace Linework.WideOutline
{
    [ExcludeFromPreset]
    //[DisallowMultipleRendererFeature("Wide Outline")]
#if UNITY_6000_0_OR_NEWER
    [SupportedOnRenderer(typeof(UniversalRendererData))]
#endif
    [Tooltip("Wide Outline renders an outline by generating a signed distance field (SDF) for each object and then sampling it. This creates consistent outlines that smoothly follows the shape of an object.")]
    [HelpURL("https://linework.ameye.dev/outlines/wide-outline")]
    public class WideOutline : ScriptableRendererFeature
    {
        private class WideOutlinePass : ScriptableRenderPass
        {
            private WideOutlineSettings settings;
            private Material mask, silhouetteBase, composite, clear;

            public WideOutlinePass()
            {
                profilingSampler = new ProfilingSampler(nameof(WideOutlinePass));
            }

            public bool Setup(ref WideOutlineSettings wideOutlineSettings, ref Material maskMaterial, ref Material silhouetteMaterial, ref Material compositeMaterial, ref Material clearMaterial, float renderScale)
            {
                settings = wideOutlineSettings;
                mask = maskMaterial;
                silhouetteBase = silhouetteMaterial;
                composite = compositeMaterial;
                clear = clearMaterial;
                renderPassEvent = (RenderPassEvent) wideOutlineSettings.InjectionPoint;

                foreach (var outline in settings.Outlines)
                {
                    if (outline.material == null)
                    {
                        outline.AssignMaterial(silhouetteBase);
                    }
                }

                foreach (var outline in settings.Outlines)
                {
                    if (!outline.IsActive())
                    {
                        continue;
                    }
                    
                    outline.material.SetColor(CommonShaderPropertyId.Color, outline.color);
                    if (outline.occlusion == WideOutlineOcclusion.AsMask) outline.material.SetColor(CommonShaderPropertyId.Color, Color.clear);

                    if (outline.alphaCutout) outline.material.EnableKeyword(ShaderFeature.AlphaCutout);
                    else outline.material.DisableKeyword(ShaderFeature.AlphaCutout);
                    outline.material.SetTexture(CommonShaderPropertyId.AlphaCutoutTexture, outline.alphaCutoutTexture);
                    outline.material.SetFloat(CommonShaderPropertyId.AlphaCutoutThreshold, outline.alphaCutoutThreshold);
                    
                    if (settings.customDepthBuffer)
                    {
                        outline.material.SetFloat(CommonShaderPropertyId.ZTest, (float) CompareFunction.LessEqual);
                    }
                    else
                    {
                        switch (outline.occlusion)
                        {
                            case WideOutlineOcclusion.Always:
                                outline.material.SetFloat(CommonShaderPropertyId.ZTest, (float) CompareFunction.Always);
                                break;
                            case WideOutlineOcclusion.WhenOccluded:
                                outline.material.SetFloat(CommonShaderPropertyId.ZTest, (float) CompareFunction.Greater);
                                break;
                            case WideOutlineOcclusion.WhenNotOccluded:
                                outline.material.SetFloat(CommonShaderPropertyId.ZTest, (float) CompareFunction.LessEqual);
                                break;
                            case WideOutlineOcclusion.AsMask:
                                outline.material.SetFloat(CommonShaderPropertyId.ZTest, (float) CompareFunction.Always);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                    
                    outline.material.SetFloat(CommonShaderPropertyId.ZWrite, settings.customDepthBuffer ? 1.0f : 0.0f);
                }

                // Set outline material properties.
                var (sourceBlend, destinationBlend) = RenderUtils.GetSrcDstBlend(settings.blendMode);
                composite.SetInt(CommonShaderPropertyId.BlendModeSource, sourceBlend);
                composite.SetInt(CommonShaderPropertyId.BlendModeDestination, destinationBlend);
                composite.SetColor(ShaderPropertyId.OutlineOccludedColor, settings.occludedColor);
                composite.SetFloat(ShaderPropertyId.OutlineWidth, settings.width);
                composite.SetFloat(ShaderPropertyId.MinOutlineWidth, settings.minWidth);
                composite.SetFloat(ShaderPropertyId.RenderScale, renderScale);
                if (settings.customDepthBuffer) composite.EnableKeyword(ShaderFeature.CustomDepth);
                else composite.DisableKeyword(ShaderFeature.CustomDepth);
    
                return settings.Outlines.Any(ShouldRenderOutline);
            }

            private static bool ShouldRenderOutline(Outline outline)
            {
                return outline.IsActive() && outline.occlusion != WideOutlineOcclusion.AsMask;
            }

            private static bool ShouldRenderStencilMask(Outline outline)
            {
                return outline.IsActive() && outline.occlusion == WideOutlineOcclusion.WhenOccluded;
            }
            
#if UNITY_6000_0_OR_NEWER
            private class PassData
            {
                internal readonly List<RendererListHandle> MaskRendererListHandles = new();
                internal readonly List<RendererListHandle> SilhouetteRendererListHandles = new();
            }
            
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();

                // Ensure that the render pass doesn't blit from the back buffer.
                if (resourceData.isActiveTargetBackBuffer) return;

                CreateRenderGraphTextures(renderGraph, cameraData, out var silhouetteHandle, out var silhouetteDepthHandle, out var pingHandle, out var pongHandle);
                if (!silhouetteHandle.IsValid() || !silhouetteDepthHandle.IsValid() || !pingHandle.IsValid() || !pongHandle.IsValid()) return;

                // 1. Mask.
                // -> Render a mask to the stencil buffer.
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ShaderPassName.Mask, out var passData))
                {
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);

                    InitMaskRendererLists(renderGraph, frameData, ref passData);
                    foreach (var rendererListHandle in passData.MaskRendererListHandles)
                    {
                        builder.UseRendererList(rendererListHandle);
                    }

                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        foreach (var handle in data.MaskRendererListHandles)
                        {
                            context.cmd.DrawRendererList(handle);
                        }
                    });
                }

                if (settings.DebugStage == DebugStage.Mask)
                {
                    RenderUtils.RenderDebug(renderGraph, resourceData.activeDepthTexture, resourceData);
                    return;
                }

                // 2. Silhouette.
                // -> Render a silhouette.
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ShaderPassName.Silhouette, out var passData))
                {
                    builder.SetRenderAttachment(silhouetteHandle, 0);
                    builder.SetRenderAttachmentDepth(settings.customDepthBuffer ? silhouetteDepthHandle : resourceData.activeDepthTexture);

                    builder.SetGlobalTextureAfterPass(silhouetteHandle, ShaderPropertyId.SilhouetteBuffer);
                    if (settings.customDepthBuffer) builder.SetGlobalTextureAfterPass(silhouetteDepthHandle, ShaderPropertyId.SilhouetteDepthBuffer);

                    InitSilhouetteRendererLists(renderGraph, frameData, ref passData);
                    foreach (var rendererListHandle in passData.SilhouetteRendererListHandles)
                    {
                        builder.UseRendererList(rendererListHandle);
                    }

                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        foreach (var handle in data.SilhouetteRendererListHandles)
                        {
                            context.cmd.DrawRendererList(handle);
                        }
                    });
                }

                if (settings.DebugStage == DebugStage.Silhouette)
                {
                    RenderUtils.RenderDebug(renderGraph, silhouetteHandle, resourceData);
                    return;
                }

                // 3. Flood.
                // -> Flood the silhouette.
                using (var builder = renderGraph.AddUnsafePass<PassData>(ShaderPassName.Flood, out _))
                {
                    builder.UseTexture(silhouetteHandle);
                    builder.UseTexture(pingHandle, AccessFlags.ReadWrite);
                    builder.UseTexture(pongHandle, AccessFlags.ReadWrite);

                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData _, UnsafeGraphContext context) =>
                    {
                        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                        Blitter.BlitCameraTexture(cmd, silhouetteHandle, pingHandle, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, composite, ShaderPass.FloodInit);

                        var width = settings.width * cameraData.renderScale;
                        var numberOfMips = Mathf.CeilToInt(Mathf.Log(width + 1.0f, 2.0f));
                        
                        for (var i = numberOfMips - 1; i >= 0; i--)
                        {
                            var stepWidth = Mathf.Pow(2, i) + 0.5f;

                            cmd.SetGlobalVector(ShaderPropertyId.AxisWidthId, new Vector2(stepWidth, 0f));
                            Blitter.BlitCameraTexture(cmd, pingHandle, pongHandle, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, composite, ShaderPass.FloodJump);
                            cmd.SetGlobalVector(ShaderPropertyId.AxisWidthId, new Vector2(0f, stepWidth));
                            Blitter.BlitCameraTexture(cmd, pongHandle, pingHandle, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, composite, ShaderPass.FloodJump);
                        }
                    });
                }

                if (settings.DebugStage == DebugStage.Flood)
                {
                    RenderUtils.RenderDebug(renderGraph, pingHandle, resourceData);
                    return;
                }

                // 4. Outline.
                // -> Render an outline.
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ShaderPassName.Outline, out _))
                {
                    builder.UseTexture(pingHandle);

                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    builder.SetRenderAttachmentDepth(settings.customDepthBuffer ? silhouetteDepthHandle : resourceData.activeDepthTexture);

                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData _, RasterGraphContext context) => { Blitter.BlitTexture(context.cmd, pingHandle, Vector2.one, composite, ShaderPass.Outline); });
                }

                // 5. Clear stencil.
                // -> Clear the stencil buffer.
                if (settings.ClearStencil)
                {
                    RenderUtils.ClearStencil(renderGraph, resourceData, clear);
                }
            }

            private void InitMaskRendererLists(RenderGraph renderGraph, ContextContainer frameData, ref PassData passData)
            {
                passData.MaskRendererListHandles.Clear();

                var renderingData = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var lightData = frameData.Get<UniversalLightData>();

                var sortingCriteria = cameraData.defaultOpaqueSortFlags;
                var renderQueueRange = RenderQueueRange.opaque;

                var i = 0;
                foreach (var outline in settings.Outlines)
                {
                    if (!ShouldRenderStencilMask(outline))
                    {
                        i++;
                        continue;
                    }

                    var drawingSettings = RenderingUtils.CreateDrawingSettings(RenderUtils.DefaultShaderTagIds, renderingData, cameraData, lightData, sortingCriteria);
                    drawingSettings.overrideMaterial = mask;

                    var filteringSettings = new FilteringSettings(renderQueueRange, -1, outline.RenderingLayer);
                    var renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

                    var blendState = BlendState.defaultValue;
                    blendState.blendState0 = new RenderTargetBlendState(0);
                    renderStateBlock.blendState = blendState;

                    // Set stencil state.
                    var stencilState = StencilState.defaultValue;
                    stencilState.enabled = true;
                    stencilState.SetCompareFunction(CompareFunction.Always);
                    stencilState.SetPassOperation(StencilOp.Replace);
                    stencilState.SetFailOperation(StencilOp.Keep);
                    stencilState.SetZFailOperation(StencilOp.Keep);
                    stencilState.writeMask = (byte) (1 << i);
                    renderStateBlock.mask |= RenderStateMask.Stencil;
                    renderStateBlock.stencilReference = 1 << i;
                    renderStateBlock.stencilState = stencilState;

                    var handle = new RendererListHandle();
                    RenderUtils.CreateRendererListWithRenderStateBlock(renderGraph, ref renderingData.cullResults, drawingSettings, filteringSettings, renderStateBlock,
                        ref handle);
                    passData.MaskRendererListHandles.Add(handle);
                }
            }

            private void InitSilhouetteRendererLists(RenderGraph renderGraph, ContextContainer frameData, ref PassData passData)
            {
                passData.SilhouetteRendererListHandles.Clear();

                var universalRenderingData = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var lightData = frameData.Get<UniversalLightData>();

                var sortingCriteria = cameraData.defaultOpaqueSortFlags;
                var renderQueueRange = RenderQueueRange.opaque;

                var i = 0;
                foreach (var outline in settings.Outlines)
                {
                    if (!outline.IsActive())
                    {
                        i++;
                        continue;
                    }

                    var drawingSettings = RenderingUtils.CreateDrawingSettings(RenderUtils.DefaultShaderTagIds, universalRenderingData, cameraData, lightData, sortingCriteria);
                    drawingSettings.overrideMaterial = outline.material;
                    drawingSettings.overrideMaterialPassIndex = ShaderPass.Silhouette;
                    drawingSettings.perObjectData = PerObjectData.None;
                    drawingSettings.enableInstancing = false;

                    var filteringSettings = new FilteringSettings(renderQueueRange, -1, outline.RenderingLayer);

                    var renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

                    var stencilState = StencilState.defaultValue;
                    stencilState.enabled = true;
                    stencilState.SetCompareFunction(outline.occlusion == WideOutlineOcclusion.WhenOccluded ? CompareFunction.NotEqual : CompareFunction.Always);
                    stencilState.SetPassOperation(StencilOp.Replace);
                    stencilState.SetFailOperation(StencilOp.Keep);
                    stencilState.SetZFailOperation(outline.closedLoop ? StencilOp.Keep : StencilOp.Replace);
                    stencilState.readMask = (byte) (1 << i);
                    stencilState.writeMask = (byte) (1 << i);
                    renderStateBlock.mask |= RenderStateMask.Stencil;
                    renderStateBlock.stencilReference = 1 << i;
                    renderStateBlock.stencilState = stencilState;

                    var handle = new RendererListHandle();
                    RenderUtils.CreateRendererListWithRenderStateBlock(renderGraph, ref universalRenderingData.cullResults, drawingSettings, filteringSettings, renderStateBlock,
                        ref handle);
                    passData.SilhouetteRendererListHandles.Add(handle);

                    i++;
                }
            }

            private static void CreateRenderGraphTextures(RenderGraph renderGraph, UniversalCameraData cameraData,
                out TextureHandle silhouetteHandle,
                out TextureHandle silhouetteDepthHandle,
                out TextureHandle pingHandle,
                out TextureHandle pongHandle)
            {
                // Silhouette buffer.
                var silhouetteDescriptor = cameraData.cameraTargetDescriptor;
                silhouetteDescriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                silhouetteDescriptor.depthBufferBits = (int) DepthBits.None;
                silhouetteDescriptor.msaaSamples = 1;
                silhouetteDescriptor.sRGB = false;
                silhouetteDescriptor.useMipMap = false;
                silhouetteDescriptor.autoGenerateMips = false;
                silhouetteHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, silhouetteDescriptor, Buffer.Silhouette, false);

                // Silhouette depth buffer.
                var silhouetteDepthDescriptor = cameraData.cameraTargetDescriptor;
                silhouetteDepthDescriptor.graphicsFormat = GraphicsFormat.None;
                silhouetteDepthDescriptor.depthBufferBits = (int) DepthBits.Depth32;
                silhouetteDepthDescriptor.msaaSamples = 1;
                silhouetteDepthHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, silhouetteDepthDescriptor, Buffer.SilhouetteDepth, false);

                // Ping pong buffers.
                var pingPongDescriptor = cameraData.cameraTargetDescriptor;
                pingPongDescriptor.graphicsFormat = SystemInfo.IsFormatSupported(GraphicsFormat.R16G16_SNorm, GraphicsFormatUsage.Render) 
                    ? GraphicsFormat.R16G16_SNorm 
                    : GraphicsFormat.R32G32_SFloat;
                pingPongDescriptor.depthBufferBits = (int) DepthBits.None;
                pingPongDescriptor.msaaSamples = 1;
                pingHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, pingPongDescriptor, Buffer.Ping, false);
                pongHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, pingPongDescriptor, Buffer.Pong, false);
            }
#endif
            private RTHandle cameraDepthRTHandle, silhouetteRTHandle, silhouetteDepthRTHandle, pingRTHandle, pongRTHandle;
            
            #pragma warning disable 618, 672
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                ConfigureTarget(silhouetteRTHandle, settings.customDepthBuffer ? silhouetteDepthRTHandle : renderingData.cameraData.renderer.cameraDepthTargetHandle);
                ConfigureClear(settings.customDepthBuffer ? ClearFlag.All : ClearFlag.Color, Color.clear);
            }

            public void CreateHandles(RenderingData renderingData)
            {
                const float renderTextureScale = 1.0f; 
                var width = (int)(renderingData.cameraData.cameraTargetDescriptor.width * renderTextureScale);
                var height = (int)(renderingData.cameraData.cameraTargetDescriptor.height * renderTextureScale);
                
                var descriptor = new RenderTextureDescriptor(width, height)
                {
                    dimension = TextureDimension.Tex2D,
                    msaaSamples = 1,
                    sRGB = false,
                    useMipMap = false,
                    autoGenerateMips = false,
                    graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm,
                    depthBufferBits = (int) DepthBits.None,
                    colorFormat = RenderTextureFormat.Default
                };
                RenderingUtils.ReAllocateIfNeeded(ref silhouetteRTHandle, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: Buffer.Silhouette);
                
                // Silhouette depth buffer.
                var silhouetteDepthDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                silhouetteDepthDescriptor.graphicsFormat = GraphicsFormat.None;
                silhouetteDepthDescriptor.depthBufferBits = (int) DepthBits.Depth32;
                silhouetteDepthDescriptor.msaaSamples = 1;
                RenderingUtils.ReAllocateIfNeeded(ref silhouetteDepthRTHandle, silhouetteDepthDescriptor, FilterMode.Point, TextureWrapMode.Clamp, name: Buffer.SilhouetteDepth);
                
                // Ping pong buffers.
                var pingPongDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                pingPongDescriptor.graphicsFormat = SystemInfo.IsFormatSupported(GraphicsFormat.R16G16_SNorm, FormatUsage.Render) 
                    ? GraphicsFormat.R16G16_SNorm 
                    : GraphicsFormat.R32G32_SFloat;
                pingPongDescriptor.depthBufferBits = (int) DepthBits.None;
                pingPongDescriptor.msaaSamples = 1;
                RenderingUtils.ReAllocateIfNeeded(ref pingRTHandle, pingPongDescriptor, FilterMode.Point, TextureWrapMode.Clamp, name: Buffer.Ping);
                RenderingUtils.ReAllocateIfNeeded(ref pongRTHandle, pingPongDescriptor, FilterMode.Point, TextureWrapMode.Clamp, name: Buffer.Pong);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // 1. Mask.
                // -> Render a mask to the stencil buffer.
                var maskCmd = CommandBufferPool.Get();

                using (new ProfilingScope(maskCmd, new ProfilingSampler(ShaderPassName.Mask)))
                {
                 //   CoreUtils.SetRenderTarget(maskCmd, renderingData.cameraData.renderer.cameraDepthTargetHandle);
                    
                    context.ExecuteCommandBuffer(maskCmd);
                    maskCmd.Clear();

                    var sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
                    var renderQueueRange = RenderQueueRange.opaque;

                    var maskIndex = 0;
                    foreach (var outline in settings.Outlines)
                    {
                        if (!ShouldRenderStencilMask(outline))
                        {
                            maskIndex++;
                            continue;
                        }

                        var drawingSettings = RenderingUtils.CreateDrawingSettings(RenderUtils.DefaultShaderTagIds, ref renderingData, sortingCriteria);
                        drawingSettings.overrideMaterial = mask;

                        var filteringSettings = new FilteringSettings(renderQueueRange, -1, outline.RenderingLayer);
                        var renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

                        var blendState = BlendState.defaultValue;
                        blendState.blendState0 = new RenderTargetBlendState(0);
                        renderStateBlock.blendState = blendState;

                        var stencilState = StencilState.defaultValue;
                        stencilState.enabled = true;
                        stencilState.SetCompareFunction(CompareFunction.Always);
                        stencilState.SetPassOperation(StencilOp.Replace);
                        stencilState.SetFailOperation(StencilOp.Keep);
                        stencilState.SetZFailOperation(StencilOp.Keep);
                        stencilState.writeMask = (byte) (1 << maskIndex);
                        renderStateBlock.mask |= RenderStateMask.Stencil;
                        renderStateBlock.stencilReference = 1 << maskIndex;
                        renderStateBlock.stencilState = stencilState;

                        context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);

                        maskIndex++;
                    }
                }
                
                context.ExecuteCommandBuffer(maskCmd);
                CommandBufferPool.Release(maskCmd);

                // 2. Silhouette.
                // -> Render a silhouette.
                var silhouetteCmd = CommandBufferPool.Get();

                using (new ProfilingScope(silhouetteCmd, new ProfilingSampler(ShaderPassName.Silhouette)))
                {
                    CoreUtils.SetRenderTarget(silhouetteCmd, silhouetteRTHandle, settings.customDepthBuffer ? silhouetteDepthRTHandle : renderingData.cameraData.renderer.cameraDepthTargetHandle);
                    
                    context.ExecuteCommandBuffer(silhouetteCmd);
                    silhouetteCmd.Clear();

                    var sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
                    var renderQueueRange = RenderQueueRange.opaque;

                    var silhouetteIndex = 0;
                    foreach (var outline in settings.Outlines)
                    {
                        if (!outline.IsActive())
                        {
                            silhouetteIndex++;
                            continue;
                        }

                        var drawingSettings = RenderingUtils.CreateDrawingSettings(RenderUtils.DefaultShaderTagIds, ref renderingData, sortingCriteria);
                        drawingSettings.overrideMaterial = outline.material;
                        drawingSettings.overrideMaterialPassIndex = ShaderPass.Silhouette;

                        var filteringSettings = new FilteringSettings(renderQueueRange, -1, outline.RenderingLayer);
                        var renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

                        var stencilState = StencilState.defaultValue;
                        stencilState.enabled = true;
                        stencilState.SetCompareFunction(outline.occlusion == WideOutlineOcclusion.WhenOccluded ? CompareFunction.NotEqual : CompareFunction.Always);
                        stencilState.SetPassOperation(StencilOp.Replace);
                        stencilState.SetFailOperation(StencilOp.Keep);
                        stencilState.SetZFailOperation(outline.closedLoop ? StencilOp.Keep : StencilOp.Replace);
                        stencilState.readMask = (byte) (1 << silhouetteIndex);
                        stencilState.writeMask = (byte) (1 << silhouetteIndex);
                        renderStateBlock.mask |= RenderStateMask.Stencil;
                        renderStateBlock.stencilReference = 1 << silhouetteIndex;
                        renderStateBlock.stencilState = stencilState;
                        
                        context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);

                        silhouetteIndex++;
                    }
                }
                
                if (settings.customDepthBuffer) silhouetteCmd.SetGlobalTexture(ShaderPropertyId.SilhouetteDepthBuffer, silhouetteDepthRTHandle.nameID);
                silhouetteCmd.SetGlobalTexture(ShaderPropertyId.SilhouetteBuffer, silhouetteRTHandle.nameID);
                context.ExecuteCommandBuffer(silhouetteCmd);
                CommandBufferPool.Release(silhouetteCmd);

                // 3. Flood.
                // -> Flood the silhouette.
                var floodCmd = CommandBufferPool.Get();

                using (new ProfilingScope(floodCmd, new ProfilingSampler(ShaderPassName.Flood)))
                {
                    context.ExecuteCommandBuffer(floodCmd);
                    floodCmd.Clear();

                    Blitter.BlitCameraTexture(floodCmd, silhouetteRTHandle, pingRTHandle, composite, ShaderPass.FloodInit);

                    var width = settings.width * renderingData.cameraData.renderScale;
                    var numberOfMips = Mathf.CeilToInt(Mathf.Log(width + 1.0f, 2.0f));

                    for (var passIndex = numberOfMips - 1; passIndex >= 0; passIndex--)
                    {
                        var stepWidth = Mathf.Pow(2, passIndex) + 0.5f;

                        floodCmd.SetGlobalVector(ShaderPropertyId.AxisWidthId, new Vector2(stepWidth, 0f));
                        Blitter.BlitCameraTexture(floodCmd, pingRTHandle, pongRTHandle, composite, ShaderPass.FloodJump);
                        floodCmd.SetGlobalVector(ShaderPropertyId.AxisWidthId, new Vector2(0f, stepWidth));
                        Blitter.BlitCameraTexture(floodCmd, pongRTHandle, pingRTHandle, composite, ShaderPass.FloodJump);
                    }
                }
                
                context.ExecuteCommandBuffer(floodCmd);
                CommandBufferPool.Release(floodCmd);

                // 4. Outline.
                // -> Render an outline.
                var outlineCmd = CommandBufferPool.Get();

                using (new ProfilingScope(outlineCmd, new ProfilingSampler(ShaderPassName.Outline)))
                {
                    context.ExecuteCommandBuffer(outlineCmd);
                    outlineCmd.Clear();
                    
                    CoreUtils.SetRenderTarget(outlineCmd, renderingData.cameraData.renderer.cameraColorTargetHandle, settings.customDepthBuffer ? silhouetteDepthRTHandle : cameraDepthRTHandle); // if using cameraColorRTHandle this does not render in scene view when rendering after post-processing with post-processing enabled
                    Blitter.BlitTexture(outlineCmd, pingRTHandle, Vector2.one, composite, ShaderPass.Outline);
                }

                context.ExecuteCommandBuffer(outlineCmd);
                CommandBufferPool.Release(outlineCmd);
            }
            #pragma warning restore 618, 672

            public void SetTarget(RTHandle depth)
            {
                cameraDepthRTHandle = depth;
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                if (cmd == null)
                {
                    throw new ArgumentNullException(nameof(cmd));
                }

                cameraDepthRTHandle = null;
            }

            public void Dispose()
            {
                settings = null; // de-reference settings to allow them to be freed from memory
                
                silhouetteRTHandle?.Release();
                silhouetteDepthRTHandle?.Release();
                pingRTHandle?.Release();
                pongRTHandle?.Release();
            }
        }

        [SerializeField] private WideOutlineSettings settings;
        [SerializeField] private ShaderResources shaders;
        private Material maskMaterial, silhouetteMaterial, outlineMaterial, clearMaterial;
        private WideOutlinePass wideOutlinePass;

        /// <summary>
        /// Called
        /// - When the Scriptable Renderer Feature loads the first time.
        /// - When you enable or disable the Scriptable Renderer Feature.
        /// - When you change a property in the Inspector window of the Renderer Feature.
        /// </summary>
        public override void Create()
        {
            if (settings == null) return;
            settings.OnSettingsChanged = null;
            settings.OnSettingsChanged += Create;

            shaders = new ShaderResources().Load();
            wideOutlinePass ??= new WideOutlinePass();
        }

        /// <summary>
        /// Called
        /// - Every frame, once for each camera.
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null) return;

            // Don't render for some views.
            if (renderingData.cameraData.cameraType == CameraType.Preview
                || renderingData.cameraData.cameraType == CameraType.Reflection
                || renderingData.cameraData.cameraType == CameraType.SceneView && !settings.ShowInSceneView
#if UNITY_6000_0_OR_NEWER
                || UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData))
#else
                )
#endif
                return;
            
            if (!CreateMaterials())
            {
                Debug.LogWarning("Not all required materials could be created. Wide Outline will not render.");
                return;
            }
            
            var render = wideOutlinePass.Setup(ref settings, ref maskMaterial, ref silhouetteMaterial, ref outlineMaterial, ref clearMaterial, renderingData.cameraData.renderScale);
            if (render) renderer.EnqueuePass(wideOutlinePass);
        }

        #pragma warning disable 618, 672
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            if (settings == null || renderingData.cameraData.cameraType == CameraType.SceneView && !settings.ShowInSceneView) return;

            wideOutlinePass.CreateHandles(renderingData);
            wideOutlinePass.ConfigureInput(ScriptableRenderPassInput.Color);
            wideOutlinePass.ConfigureInput(ScriptableRenderPassInput.Depth);
            wideOutlinePass.SetTarget(renderer.cameraDepthTargetHandle);
        }
        #pragma warning restore 618, 672

        /// <summary>
        /// Clean up resources allocated to the Scriptable Renderer Feature such as materials.
        /// </summary>
        override protected void Dispose(bool disposing)
        {
            wideOutlinePass?.Dispose();
            wideOutlinePass = null;
            DestroyMaterials();
        }

        private void OnDestroy()
        {
            settings = null; // de-reference settings to allow them to be freed from memory
            wideOutlinePass?.Dispose();
        }
        
        private void DestroyMaterials()
        {
            CoreUtils.Destroy(maskMaterial);
            CoreUtils.Destroy(silhouetteMaterial);
            CoreUtils.Destroy(outlineMaterial);
            CoreUtils.Destroy(clearMaterial);
        }

        private bool CreateMaterials()
        {
            if (maskMaterial == null)
            {
                maskMaterial = CoreUtils.CreateEngineMaterial(shaders.mask);
            }

            if (silhouetteMaterial == null)
            {
                silhouetteMaterial = CoreUtils.CreateEngineMaterial(shaders.silhouette);
            }

            if (outlineMaterial == null)
            {
                outlineMaterial = CoreUtils.CreateEngineMaterial(shaders.outline);
            }

            if (clearMaterial == null)
            {
                clearMaterial = CoreUtils.CreateEngineMaterial(shaders.clear);
            }

            return maskMaterial != null && silhouetteMaterial != null && outlineMaterial != null && clearMaterial != null;
        }
    }
}
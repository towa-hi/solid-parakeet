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

namespace Linework.SoftOutline
{
    [ExcludeFromPreset]
    [DisallowMultipleRendererFeature("Soft Outline")]
#if UNITY_6000_0_OR_NEWER
    [SupportedOnRenderer(typeof(UniversalRendererData))]
#endif
    [Tooltip("Soft Outline renders outlines by generating a silhouette of an object and applying a dilation/blur effect, resulting in smooth, soft-edged contours around objects.")]
    [HelpURL("https://linework.ameye.dev/outlines/soft-outline")]
    public class SoftOutline : ScriptableRendererFeature
    {
        private class SoftOutlinePass : ScriptableRenderPass
        {
            private SoftOutlineSettings settings;
            private Material mask, silhouetteBase, blur, composite, clear;
            private readonly ProfilingSampler maskSampler, silhouetteSampler, blurSampler, outlineSampler;
            
            public SoftOutlinePass()
            {
                profilingSampler = new ProfilingSampler(nameof(SoftOutlinePass));
                maskSampler = new ProfilingSampler(ShaderPassName.Mask);
                silhouetteSampler = new ProfilingSampler(ShaderPassName.Silhouette);
                blurSampler = new ProfilingSampler(ShaderPassName.Blur);
                outlineSampler = new ProfilingSampler(ShaderPassName.Outline);
            }
            
            public bool Setup(ref SoftOutlineSettings softOutlineSettings, ref Material maskMaterial, ref Material compositeMaterial, ref Material blurMaterial, ref Material silhouetteMaterial, ref Material clearMaterial)
            {
                settings = softOutlineSettings;
                mask = maskMaterial;
                silhouetteBase = silhouetteMaterial;
                blur = blurMaterial;
                composite = compositeMaterial;
                clear = clearMaterial;
                renderPassEvent = (RenderPassEvent) softOutlineSettings.InjectionPoint;

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
                    
                    outline.material.SetColor(CommonShaderPropertyId.Color, settings.type == OutlineType.Hard ? Color.white : outline.color);
                    if(outline.occlusion == SoftOutlineOcclusion.AsMask) outline.material.SetColor(CommonShaderPropertyId.Color, Color.clear);

                    if (outline.alphaCutout) outline.material.EnableKeyword(ShaderFeature.AlphaCutout);
                    else outline.material.DisableKeyword(ShaderFeature.AlphaCutout);
                    outline.material.SetTexture(CommonShaderPropertyId.AlphaCutoutTexture, outline.alphaCutoutTexture);
                    outline.material.SetFloat(CommonShaderPropertyId.AlphaCutoutThreshold, outline.alphaCutoutThreshold);
                    
                    switch (outline.occlusion)
                    {
                        case SoftOutlineOcclusion.Always:
                            outline.material.SetFloat(CommonShaderPropertyId.ZTest, (float) CompareFunction.Always);
                            break;
                        case SoftOutlineOcclusion.WhenOccluded:
                            outline.material.SetFloat(CommonShaderPropertyId.ZTest, (float) CompareFunction.Greater);
                            break;
                        case SoftOutlineOcclusion.WhenNotOccluded:
                            outline.material.SetFloat(CommonShaderPropertyId.ZTest, (float) CompareFunction.LessEqual);
                            break;
                        case SoftOutlineOcclusion.AsMask:
                            outline.material.SetFloat(CommonShaderPropertyId.ZTest, (float) CompareFunction.Always);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                // Set blur material properties.
                if (settings.scaleWithResolution) blur.EnableKeyword(ShaderFeature.ScaleWithResolution);
                else blur.DisableKeyword(ShaderFeature.ScaleWithResolution);
                switch (settings.referenceResolution)
                {
                    case Resolution._480:
                        blur.SetFloat(CommonShaderPropertyId.ReferenceResolution, 480.0f);
                        break;
                    case Resolution._720:
                        blur.SetFloat(CommonShaderPropertyId.ReferenceResolution, 720.0f);
                        break;
                    case Resolution._1080:
                        blur.SetFloat(CommonShaderPropertyId.ReferenceResolution, 1080.0f);
                        break;
                    case Resolution.Custom:
                        blur.SetFloat(CommonShaderPropertyId.ReferenceResolution, settings.customResolution);
                        break;
                }
                
                if (settings.dilationMethod is DilationMethod.Box or DilationMethod.Gaussian or DilationMethod.Dilate)
                {
                    blur.SetInt(ShaderPropertyId.KernelSize, settings.kernelSize);
                    blur.SetInt(ShaderPropertyId.Samples, settings.kernelSize * 2 + 1);
                }
                if (settings.dilationMethod is DilationMethod.Gaussian)
                {
                    blur.SetFloat(ShaderPropertyId.KernelSpread, settings.blurSpread);
                }

                blur.SetFloat(ShaderPropertyId.OutlineHardness, settings.hardness);

                // Set composite material properties.
                var (srcBlend, dstBlend) = RenderUtils.GetSrcDstBlend(settings.blendMode);
                composite.SetInt(CommonShaderPropertyId.BlendModeSource, srcBlend);
                composite.SetInt(CommonShaderPropertyId.BlendModeDestination, dstBlend);
                composite.SetColor(ShaderPropertyId.OutlineColor, settings.color);
                composite.SetFloat(ShaderPropertyId.OutlineHardness, settings.hardness);
                composite.SetFloat(ShaderPropertyId.OutlineIntensity, settings.type == OutlineType.Hard ? 1.0f : settings.intensity);

                if (settings.type == OutlineType.Hard) composite.EnableKeyword(ShaderFeature.HardOutline);
                else composite.DisableKeyword(ShaderFeature.HardOutline);

                return settings.Outlines.Any(ShouldRenderOutline);
            }
            
            private static bool ShouldRenderOutline(Outline outline)
            {
                return outline.IsActive() && outline.occlusion != SoftOutlineOcclusion.AsMask;
            }

            private static bool ShouldRenderStencilMask(Outline outline)
            {
                return outline.IsActive() && outline.occlusion == SoftOutlineOcclusion.WhenOccluded;
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

                CreateRenderGraphTextures(renderGraph, cameraData, out var silhouetteHandle, out var blurHandle);
                if (!silhouetteHandle.IsValid() || !blurHandle.IsValid()) return;

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
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);
                    builder.SetGlobalTextureAfterPass(silhouetteHandle, ShaderPropertyId.SilhouetteBuffer);
                    
                    InitSilhouetteRendererLists(renderGraph, frameData, ref passData);
                    foreach (var handle in passData.SilhouetteRendererListHandles)
                    {
                        builder.UseRendererList(handle);
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
                
                // 3. Blur.
                // -> Blur the silhouette.
                using (var builder = renderGraph.AddUnsafePass<PassData>(ShaderPassName.Blur, out _))
                {
                    builder.UseTexture(silhouetteHandle);
                    builder.UseTexture(blurHandle, AccessFlags.Write);
                    
                    builder.AllowPassCulling(false);
                
                    builder.SetRenderFunc((PassData _, UnsafeGraphContext context) =>
                    {
                        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                
                        switch (settings.dilationMethod)
                        {
                            case DilationMethod.Box or DilationMethod.Gaussian or DilationMethod.Dilate:
                                Blitter.BlitCameraTexture(cmd, silhouetteHandle, blurHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, blur,
                                    ShaderPass.VerticalBlur);
                                Blitter.BlitCameraTexture(cmd, blurHandle, silhouetteHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, blur,
                                    ShaderPass.HorizontalBlur);
                                break;
                            case DilationMethod.Kawase:
                                for (var i = 1; i < settings.blurPasses; i++)
                                {
                                    blur.SetFloat(ShaderPropertyId.Offset, 0.5f + i);
                                    Blitter.BlitCameraTexture(cmd, silhouetteHandle, blurHandle, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, blur,
                                        ShaderPass.Blur);
                                    (silhouetteHandle, blurHandle) = (blurHandle, silhouetteHandle);
                                }
                                break;
                        }
                    });
                }
                
                if (settings.DebugStage == DebugStage.Blur)
                {
                    RenderUtils.RenderDebug(renderGraph, silhouetteHandle, resourceData);
                    return;
                }
                
                // 4. Outline.
                // -> Render an outline.
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ShaderPassName.Outline, out _))
                {
                    var source = settings.dilationMethod switch
                    {
                        DilationMethod.Box or DilationMethod.Gaussian => silhouetteHandle,
                        DilationMethod.Kawase => blurHandle,
                        _ => silhouetteHandle
                    };
                    builder.UseTexture(source);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);
                
                    builder.AllowPassCulling(false);
                
                    builder.SetRenderFunc((PassData _, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, source, Vector2.one, composite, ShaderPass.Outline);
                    });
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
                    drawingSettings.overrideShaderPassIndex = ShaderPass.Mask;

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

                var renderingData = frameData.Get<UniversalRenderingData>();
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

                    var drawingSettings = RenderingUtils.CreateDrawingSettings(RenderUtils.DefaultShaderTagIds, renderingData, cameraData, lightData, sortingCriteria);
                    drawingSettings.overrideMaterial = outline.material;
                    drawingSettings.overrideMaterialPassIndex = ShaderPass.Silhouette;
                    drawingSettings.perObjectData = PerObjectData.None;
                    drawingSettings.enableInstancing = false;

                    var filteringSettings = new FilteringSettings(renderQueueRange, -1, outline.RenderingLayer);
                    
                    var renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                    
                    var stencilState = StencilState.defaultValue;
                    stencilState.enabled = true;
                    stencilState.SetCompareFunction(outline.occlusion == SoftOutlineOcclusion.WhenOccluded ? CompareFunction.NotEqual : CompareFunction.Always);
                    stencilState.SetPassOperation(StencilOp.Replace);
                    stencilState.SetFailOperation(StencilOp.Keep);
                    stencilState.SetZFailOperation(outline.closedLoop ? StencilOp.Keep : StencilOp.Replace);
                    stencilState.readMask = (byte) (1 << i);
                    stencilState.writeMask = (byte) (1 << i);
                    renderStateBlock.mask |= RenderStateMask.Stencil;
                    renderStateBlock.stencilReference = 1 << i;
                    renderStateBlock.stencilState = stencilState;

                    var handle = new RendererListHandle();
                    RenderUtils.CreateRendererListWithRenderStateBlock(renderGraph, ref renderingData.cullResults, drawingSettings, filteringSettings, renderStateBlock,
                        ref handle);
                    passData.SilhouetteRendererListHandles.Add(handle);
                    
                    i++;
                }
            }
            
            private void CreateRenderGraphTextures(RenderGraph renderGraph, UniversalCameraData cameraData, out TextureHandle silhouetteHandle, out TextureHandle blurHandle)
            {
                const float renderTextureScale = 1.0f; 
                var width = (int)(cameraData.cameraTargetDescriptor.width * renderTextureScale);
                var height = (int)(cameraData.cameraTargetDescriptor.height * renderTextureScale);
                
                var descriptor = new RenderTextureDescriptor(width, height)
                {
                    dimension = TextureDimension.Tex2D,
                    msaaSamples = 1,
                    sRGB = false,
                    useMipMap = false,
                    autoGenerateMips = false,
                    graphicsFormat = settings.dilationMethod == DilationMethod.Dilate ? GraphicsFormat.R8G8B8A8_UNorm :
                        settings.type == OutlineType.Hard ? GraphicsFormat.R8_UNorm : GraphicsFormat.R8G8B8A8_UNorm,
                    depthBufferBits = (int) DepthBits.None,
                    colorFormat = RenderTextureFormat.Default
                };

                silhouetteHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, Buffer.Silhouette, false);
                blurHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, Buffer.Blur, false);
            }
#endif
            private RTHandle cameraDepthRTHandle, silhouetteRTHandle, blurRTHandle;
            private RTHandle[] handles;
            
            #pragma warning disable 618, 672
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                if(handles is not {Length: 2})
                {
                    handles = new RTHandle[2];
                }
                handles[0] = silhouetteRTHandle;
                handles[1] = blurRTHandle;
                
                ConfigureTarget(handles, cameraDepthRTHandle);
                ConfigureClear(ClearFlag.Color, Color.clear);
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
                    graphicsFormat = settings.dilationMethod == DilationMethod.Dilate
                        ? GraphicsFormat.R8G8B8A8_UNorm
                        : settings.type == OutlineType.Hard
                            ? GraphicsFormat.R8_UNorm
                            : GraphicsFormat.R8G8B8A8_UNorm,
                    depthBufferBits = (int) DepthBits.None,
                    colorFormat = RenderTextureFormat.Default
                };
                
                RenderingUtils.ReAllocateIfNeeded(ref silhouetteRTHandle, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: Buffer.Silhouette);
                RenderingUtils.ReAllocateIfNeeded(ref blurRTHandle, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: Buffer.Blur);
            }
            
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // 1. Mask.
                // -> Render a mask to the stencil buffer.
                var maskCmd = CommandBufferPool.Get();

                using (new ProfilingScope(maskCmd, maskSampler))
                {
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
                        drawingSettings.overrideShaderPassIndex = ShaderPass.Mask;

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
                
                using (new ProfilingScope(silhouetteCmd, silhouetteSampler))
                { 
                    CoreUtils.SetRenderTarget(silhouetteCmd, silhouetteRTHandle, renderingData.cameraData.renderer.cameraDepthTargetHandle);
                    
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
                        drawingSettings.overrideShaderPassIndex = ShaderPass.Silhouette;
                        
                        var filteringSettings = new FilteringSettings(renderQueueRange, -1, outline.RenderingLayer);
                        var renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                        
                        var stencilState = StencilState.defaultValue;
                        stencilState.enabled = true;
                        stencilState.SetCompareFunction(outline.occlusion == SoftOutlineOcclusion.WhenOccluded ? CompareFunction.NotEqual : CompareFunction.Always);
                        stencilState.SetPassOperation(StencilOp.Replace);
                        stencilState.SetFailOperation(StencilOp.Keep);
                        stencilState.SetZFailOperation(outline.closedLoop ? StencilOp.Keep : StencilOp.Replace);
                        stencilState.writeMask = (byte) (1 << silhouetteIndex);
                        renderStateBlock.mask |= RenderStateMask.Stencil;
                        renderStateBlock.stencilReference = 1 << silhouetteIndex;
                        renderStateBlock.stencilState = stencilState;
                        
                        context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);

                        silhouetteIndex++;
                    }
                }
                
                silhouetteCmd.SetGlobalTexture(ShaderPropertyId.SilhouetteBuffer, silhouetteRTHandle.nameID);
                context.ExecuteCommandBuffer(silhouetteCmd);
                CommandBufferPool.Release(silhouetteCmd);
                
                // 3. Blur.
                // -> Blur the silhouette.
                var blurCmd = CommandBufferPool.Get();
                
                using (new ProfilingScope(blurCmd, blurSampler))
                {
                    context.ExecuteCommandBuffer(blurCmd);
                    blurCmd.Clear();
                
                    switch (settings.dilationMethod)
                    {
                        case DilationMethod.Box or DilationMethod.Gaussian or DilationMethod.Dilate:
                            Blitter.BlitCameraTexture(blurCmd, silhouetteRTHandle, blurRTHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, blur,
                                ShaderPass.VerticalBlur);
                            Blitter.BlitCameraTexture(blurCmd, blurRTHandle, silhouetteRTHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, blur,
                                ShaderPass.HorizontalBlur);
                            break;
                        case DilationMethod.Kawase:
                            for (var i = 1; i < settings.blurPasses; i++)
                            {
                                blur.SetFloat(ShaderPropertyId.Offset, 0.5f + i);
                                Blitter.BlitCameraTexture(blurCmd, silhouetteRTHandle, blurRTHandle, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, blur,
                                    ShaderPass.Blur);
                                (silhouetteRTHandle, blurRTHandle) = (blurRTHandle, silhouetteRTHandle);
                            }
                            break;
                    }
                }
                
                context.ExecuteCommandBuffer(blurCmd);
                CommandBufferPool.Release(blurCmd);
                
                // 4. Outline.
                // -> Render an outline.
                var outlineCmd = CommandBufferPool.Get();
                
                using (new ProfilingScope(outlineCmd, outlineSampler))
                {
                    context.ExecuteCommandBuffer(outlineCmd);
                    outlineCmd.Clear();
                    
                    var source = settings.dilationMethod switch
                    {
                        DilationMethod.Box or DilationMethod.Gaussian => silhouetteRTHandle,
                        DilationMethod.Kawase => blurRTHandle,
                        _ => silhouetteRTHandle
                    };
                
                    CoreUtils.SetRenderTarget(outlineCmd, renderingData.cameraData.renderer.cameraColorTargetHandle, cameraDepthRTHandle); // if using cameraColorRTHandle this does not render in scene view when rendering after post processing with post processing enabled
                    Blitter.BlitTexture(outlineCmd, source, Vector2.one, composite, ShaderPass.Outline);
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
                blurRTHandle?.Release();
            }
        }

        [SerializeField] private SoftOutlineSettings settings;
        [SerializeField] private ShaderResources shaders;
        private Material maskMaterial, silhouetteMaterial, blurMaterial, outlineMaterial, clearMaterial;
        private SoftOutlinePass softOutlinePass;

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
            softOutlinePass ??= new SoftOutlinePass();
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
                Debug.LogWarning("Not all required materials could be created. Soft Outline will not render.");
                return;
            }

            var render = softOutlinePass.Setup(ref settings, ref maskMaterial, ref outlineMaterial, ref blurMaterial, ref silhouetteMaterial, ref clearMaterial);
            if (render) renderer.EnqueuePass(softOutlinePass);
        }
        
        #pragma warning disable 618, 672
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            if (settings == null || renderingData.cameraData.cameraType == CameraType.SceneView && !settings.ShowInSceneView) return;

            softOutlinePass.CreateHandles(renderingData);
            softOutlinePass.SetTarget(renderer.cameraDepthTargetHandle);
        }
        #pragma warning restore 618, 672

        /// <summary>
        /// Clean up resources allocated to the Scriptable Renderer Feature such as materials.
        /// </summary>
        override protected void Dispose(bool disposing)
        {
            softOutlinePass?.Dispose();
            softOutlinePass = null;
            DestroyMaterials();
        }
        
        private void OnDestroy()
        {
            settings = null; // de-reference settings to allow them to be freed from memory
            softOutlinePass?.Dispose();
        }

        private void DestroyMaterials()
        {
            CoreUtils.Destroy(maskMaterial);
            CoreUtils.Destroy(silhouetteMaterial);
            CoreUtils.Destroy(blurMaterial);
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

            if (blurMaterial != null) CoreUtils.Destroy(blurMaterial);
            blurMaterial = settings.dilationMethod switch
            {
                DilationMethod.Box => CoreUtils.CreateEngineMaterial(shaders.boxBlur),
                DilationMethod.Gaussian => CoreUtils.CreateEngineMaterial(shaders.gaussianBlur),
                DilationMethod.Kawase => CoreUtils.CreateEngineMaterial(shaders.kawaseBlur),
                DilationMethod.Dilate => CoreUtils.CreateEngineMaterial(shaders.dilate),
                _ => blurMaterial
            };

            if (outlineMaterial == null)
            {
                outlineMaterial = CoreUtils.CreateEngineMaterial(shaders.outline);
            }
            
            if (clearMaterial == null)
            {
                clearMaterial = CoreUtils.CreateEngineMaterial(shaders.clear);
            }

            return maskMaterial != null && silhouetteMaterial != null && blurMaterial != null && outlineMaterial != null && clearMaterial != null;
        }
    }
}
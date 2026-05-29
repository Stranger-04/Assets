using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

using UnityEngine.Experimental.Rendering;

public class PreparationRenderer : ScriptableRenderer
{
    DepthOnlyPass DepthPrepass;
    DepthNormalOnlyPass DepthNormalPrepass;
    DrawObjectsPass ColorPrepass;

    RTHandle DepthTexture;
    RTHandle NormalTexture;
    RTHandle ColorTexture;

    private PreparationRendererData m_Data;

    public PreparationRenderer(PreparationRendererData data) : base(data)
    {
        m_Data = data;

        var queueRange = data.transparentMode
            ? RenderQueueRange.all
            : RenderQueueRange.opaque;

        var passEvent = data.transparentMode
            ? RenderPassEvent.AfterRenderingTransparents
            : RenderPassEvent.BeforeRenderingPrePasses;

        DepthPrepass = new DepthOnlyPass(
            passEvent,
            queueRange,
            data.opaqueLayerMask
        );

        DepthNormalPrepass = new DepthNormalOnlyPass(
            passEvent,
            queueRange,
            data.opaqueLayerMask
        );

        if (!data.transparentMode)
        {
            ColorPrepass = new DrawObjectsPass(
                "Render Opaques",
                true,
                RenderPassEvent.BeforeRenderingOpaques,
                RenderQueueRange.opaque,
                data.opaqueLayerMask,
                StencilState.defaultValue,
                0
            );
        }
        else
        {
            ColorPrepass = new DrawObjectsPass(
                "Render Transparents",
                false,
                RenderPassEvent.BeforeRenderingTransparents,
                RenderQueueRange.transparent,
                data.transparentLayerMask,
                StencilState.defaultValue,
                0
            );
        }
        
    }

    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var data = m_Data;
        var desc = renderingData.cameraData.cameraTargetDescriptor;

        if (data.copyDepth)
        {
            desc.graphicsFormat = GraphicsFormat.None;
            desc.depthStencilFormat = GraphicsFormat.D32_SFloat_S8_UInt;
            desc.depthBufferBits = 32;
            desc.msaaSamples = 1;

            RenderingUtils.ReAllocateIfNeeded(
                ref DepthTexture,
                desc,
                FilterMode.Point,
                TextureWrapMode.Clamp,
                name: "_CameraDepthTexture"
            );
            Shader.SetGlobalTexture("_CameraDepthTexture", DepthTexture);
            DepthPrepass.Setup(desc, DepthTexture);
            EnqueuePass(DepthPrepass);
        }

        if (data.copyNormal)
        {
            desc.graphicsFormat = DepthNormalOnlyPass.GetGraphicsFormat();
            desc.depthStencilFormat = GraphicsFormat.None;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;

            RenderingUtils.ReAllocateIfNeeded(
                ref NormalTexture,
                desc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_CameraNormalTexture"
            );

            DepthNormalPrepass.Setup(DepthTexture, NormalTexture);
            EnqueuePass(DepthNormalPrepass);
        }

        if (data.copyColor)
        {
            desc.graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB;
            desc.depthStencilFormat = GraphicsFormat.None;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;

            RenderingUtils.ReAllocateIfNeeded(
                ref ColorTexture,
                desc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_CameraColorTexture"
            );
            ConfigureCameraTarget(ColorTexture, DepthTexture);

            Shader.SetGlobalTexture("_CameraColorTexture", ColorTexture);
            EnqueuePass(ColorPrepass);
        }
        

        // for (int i = 0; i < rendererFeatures.Count; i++)
        // {
        //     if (rendererFeatures[i].isActive)
        //     {
        //         rendererFeatures[i].AddRenderPasses(this, ref renderingData);
        //     }
        // }
    }
}

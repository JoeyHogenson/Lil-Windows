using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PixelationRenderFeature : ScriptableRendererFeature
{
    class PixelPass : ScriptableRenderPass
    {
        private Material material;
        private RenderTargetIdentifier source;
        private RenderTargetHandle tempTex;

        public PixelPass(Material mat)
        {
            material = mat;
            tempTex.Init("_TempPixelTex");
        }

        public void Setup(RenderTargetIdentifier src)
        {
            source = src;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("PixelationPass");

            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            cmd.GetTemporaryRT(tempTex.id, desc);

            // Blit from source → temp using pixelation material
            Blit(cmd, source, tempTex.Identifier(), material, 0);
            // Blit back to source
            Blit(cmd, tempTex.Identifier(), source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    public Material pixelMaterial;
    PixelPass pixelPass;

    public override void Create()
    {
        pixelPass = new PixelPass(pixelMaterial);
        pixelPass.renderPassEvent = RenderPassEvent.AfterRendering; // Runs after everything else
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        pixelPass.Setup(renderer.cameraColorTarget);
        renderer.EnqueuePass(pixelPass);
    }
}

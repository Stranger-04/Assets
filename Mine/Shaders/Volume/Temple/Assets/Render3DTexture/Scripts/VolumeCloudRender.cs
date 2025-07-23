using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[ImageEffectAllowedInSceneView]
public class VolumeCloudRender : ImageBase
{

	public Transform Cube;
    public Transform PointLight;
    public float PointLightRange=0.1f;
	[Range(0,10.0f)]
	public float size = 1 ;
    [Range(0, 10)]
    public float _LightMul=1;

    [Range(-10, 10)]
    public float _TransmittanceFactor = 1;

    public Texture3D VoloumeTex;

    public bool ShowRange = false;
    [Range(0,1.0f)]
    public float detail = 0.1f;

    public Texture2D RandNoiseTex;
    private void OnEnable()
    {
        
    }
    private void OnDrawGizmos()
    {
        if (!ShowRange)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Cube.position, Cube.localScale);
    }
    protected override void OnRenderImage(RenderTexture s,RenderTexture d)
	{
		if(mMaterial!=null)
		{
			//
			mMaterial.SetVector("_BoxSize",Cube.localScale);
			mMaterial.SetVector("_BoxPos",Cube.position);
			mMaterial.SetFloat("_UVWsize",size);
            mMaterial.SetFloat("_LightMul", _LightMul);
            mMaterial.SetFloat("_TransmittanceFactor", _TransmittanceFactor);
            mMaterial.SetFloat("_detail", detail);
            mMaterial.SetVector("_pointLight", new Vector4(PointLight.position.x, PointLight.position.y, PointLight.position.z, PointLightRange));
            
            if (VoloumeTex != null)
                mMaterial.SetTexture("_NoiseTex", VoloumeTex);

            if (RandNoiseTex != null)
                mMaterial.SetTexture("_RandNoiseTex", RandNoiseTex);
                
			Graphics.Blit(s,d,mMaterial);
		}
	}
}
